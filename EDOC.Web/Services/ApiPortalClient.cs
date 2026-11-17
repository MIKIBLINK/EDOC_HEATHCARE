using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using EDOC.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace EDOC.Web.Services;

public sealed class ApiPortalClient(IHttpClientFactory clients)
{
    private const string RefreshedTicketKey = "EDOC.Portal.RefreshedTokens";
    public async Task<PortalTokenResponse?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        using var response = await clients.CreateClient("EDOC.Api").PostAsJsonAsync("api/auth/login",
            new { identifier = email, password }, cancellationToken);
        if ((int)response.StatusCode >= 500)
            throw new HttpRequestException("The E-DOC sign-in service returned a server error.", null, response.StatusCode);
        if (!response.IsSuccessStatusCode) return null;
        return PortalTokenResponse.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task<T?> GetAsync<T>(HttpContext context, string path, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(context, HttpMethod.Get, path, cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode) return default;
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    public async Task<PortalDownload?> GetDownloadAsync(HttpContext context, string path,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(context, HttpMethod.Get, path, cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        return new PortalDownload(content, response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream", fileName);
    }

    public Task<HttpResponseMessage> PutAsync<T>(HttpContext context, string path, T body,
        CancellationToken cancellationToken = default) => SendAsync(context, HttpMethod.Put, path, body, cancellationToken);

    public Task<HttpResponseMessage> PostAsync<T>(HttpContext context, string path, T body,
        CancellationToken cancellationToken = default) => SendAsync(context, HttpMethod.Post, path, body, cancellationToken);

    public async Task SignOutAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        var refreshed = context.Items[RefreshedTicketKey] as PortalTokenResponse;
        if (refreshed is null) _ = await GetCurrentAccessTokenAsync(context, cancellationToken);
        var ticket = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var refreshToken = refreshed?.RefreshToken ?? ticket.Properties?.GetTokenValue("refresh_token");
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout")
            {
                Content = JsonContent.Create(new { refreshToken })
            };
            var token = refreshed?.AccessToken ?? ticket.Properties?.GetTokenValue("access_token");
            if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            try { using var _ = await clients.CreateClient("EDOC.Api").SendAsync(request, cancellationToken); }
            catch (HttpRequestException) { }
        }
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public async Task RevokeAsync(PortalTokenResponse tokens, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout")
        {
            Content = JsonContent.Create(new { refreshToken = tokens.RefreshToken })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        try { using var _ = await clients.CreateClient("EDOC.Api").SendAsync(request, cancellationToken); }
        catch (HttpRequestException) { }
    }

    public static ClaimsPrincipal CreatePrincipal(PortalTokenResponse tokens)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, tokens.Subject ?? string.Empty),
            new(ClaimTypes.Name, tokens.FullName)
        };
        claims.AddRange(tokens.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public static AuthenticationProperties CreateTicket(PortalTokenResponse tokens)
    {
        var properties = new AuthenticationProperties
        {
            IsPersistent = true,
            AllowRefresh = true,
            ExpiresUtc = tokens.RefreshTokenExpiresAt
        };
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = tokens.AccessToken },
            new AuthenticationToken { Name = "refresh_token", Value = tokens.RefreshToken },
            new AuthenticationToken { Name = "access_expires_at", Value = tokens.AccessTokenExpiresAt.ToString("O") }
        ]);
        return properties;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpContext context, HttpMethod method, string path,
        object? body = null, CancellationToken cancellationToken = default)
    {
        var token = await GetCurrentAccessTokenAsync(context, cancellationToken);
        if (string.IsNullOrWhiteSpace(token)) return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        var response = await SendCoreAsync(method, path, body, token, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;
        response.Dispose();
        token = await RefreshAccessTokenAsync(context, cancellationToken);
        return string.IsNullOrWhiteSpace(token)
            ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
            : await SendCoreAsync(method, path, body, token, cancellationToken);
    }

    private async Task<string?> GetCurrentAccessTokenAsync(HttpContext context, CancellationToken cancellationToken)
    {
        if (context.Items[RefreshedTicketKey] is PortalTokenResponse refreshedTokens)
            return refreshedTokens.AccessToken;
        var ticket = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var properties = ticket.Properties;
        var accessToken = properties?.GetTokenValue("access_token");
        var expiryText = properties?.GetTokenValue("access_expires_at");
        if (!string.IsNullOrWhiteSpace(accessToken)
            && DateTimeOffset.TryParse(expiryText, out var expiresAt)
            && expiresAt > DateTimeOffset.UtcNow.AddMinutes(1)) return accessToken;
        return await RefreshAccessTokenAsync(context, cancellationToken);
    }

    private async Task<string?> RefreshAccessTokenAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var ticket = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var refreshToken = ticket.Properties?.GetTokenValue("refresh_token");
        if (string.IsNullOrWhiteSpace(refreshToken)) return null;
        PortalTokenResponse? replacement;
        try
        {
            using var response = await clients.CreateClient("EDOC.Api").PostAsJsonAsync("api/auth/refresh",
                new { refreshToken }, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return null;
            }
            replacement = PortalTokenResponse.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (HttpRequestException) { return null; }
        if (replacement is null) return null;
        context.Items[RefreshedTicketKey] = replacement;
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, CreatePrincipal(replacement), CreateTicket(replacement));
        return replacement.AccessToken;
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string path, object? body, string token,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await clients.CreateClient("EDOC.Api").SendAsync(request, cancellationToken);
    }
}

public sealed record PortalDownload(byte[] Content, string ContentType, string? FileName);
