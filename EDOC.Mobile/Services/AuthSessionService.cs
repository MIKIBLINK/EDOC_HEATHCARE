using System.Net.Http.Json;
using System.Text.Json;
using EDOC.Mobile.Models;

namespace EDOC.Mobile.Services;

public sealed class AuthSessionService(HttpClient httpClient)
{
    public event EventHandler? SessionChanged;
    public async Task<bool> LoginAsync(string identifier, string password, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/auth/login", new { identifier, password }, cancellationToken);
        if (!response.IsSuccessStatusCode) return false;
        var tokens = await response.Content.ReadFromJsonAsync<TokenSession>(cancellationToken: cancellationToken);
        if (tokens is null) return false;
        await SaveAsync(tokens);
        SessionChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public async Task<RegistrationResult> RegisterAsync(string fullName, string email, string phoneNumber, string password, string confirmPassword, string accountType = "Patient", CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/auth/register", new { fullName, email, phoneNumber, password, confirmPassword, accountType }, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        return new RegistrationResult(response.IsSuccessStatusCode, response.StatusCode, ExtractErrorMessage(responseBody));
    }

    private static string? ExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            foreach (var name in new[] { "message", "detail", "title" })
                if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(value.GetString()))
                    return value.GetString();

            if (root.TryGetProperty("errors", out var errors))
            {
                var messages = new List<string>();
                if (errors.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in errors.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.Array)
                            messages.AddRange(property.Value.EnumerateArray()
                                .Where(item => item.ValueKind == JsonValueKind.String)
                                .Select(item => item.GetString()).OfType<string>()
                                .Where(message => !string.IsNullOrWhiteSpace(message)));
                    }
                }
                else if (errors.ValueKind == JsonValueKind.Array)
                    messages.AddRange(errors.EnumerateArray().Select(item =>
                        item.TryGetProperty("description", out var description) && description.ValueKind == JsonValueKind.String
                            ? description.GetString() : null).OfType<string>()
                        .Where(message => !string.IsNullOrWhiteSpace(message)));

                if (messages.Count > 0) return string.Join(" ", messages.Distinct());
            }
        }
        catch (JsonException) { }
        return null;
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync()
    {
        var roles = await SecureStorage.Default.GetAsync("edoc.roles");
        return string.IsNullOrWhiteSpace(roles) ? [] : roles.Split('|', StringSplitOptions.RemoveEmptyEntries);
    }

    public Task<string?> GetDisplayNameAsync() => SecureStorage.Default.GetAsync("edoc.display_name");

    public async Task<bool> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/auth/forgot-password", new { email }, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> VerifyEmailAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/auth/verify-email", new { userId, token }, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var current = await SecureStorage.Default.GetAsync("edoc.access_token");
        var expiryText = await SecureStorage.Default.GetAsync("edoc.access_expires_at");
        if (!string.IsNullOrWhiteSpace(current) && DateTimeOffset.TryParse(expiryText, out var expiry) && expiry > DateTimeOffset.UtcNow.AddMinutes(1)) return current;
        var refresh = await SecureStorage.Default.GetAsync("edoc.refresh_token");
        if (string.IsNullOrWhiteSpace(refresh)) return null;
        try
        {
            using var response = await httpClient.PostAsJsonAsync("api/auth/refresh", new { refreshToken = refresh }, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                SecureStorage.Default.Remove("edoc.access_token");
                SecureStorage.Default.Remove("edoc.access_expires_at");
                SecureStorage.Default.Remove("edoc.refresh_token");
                return null;
            }
            var tokens = await response.Content.ReadFromJsonAsync<TokenSession>(cancellationToken: cancellationToken);
            if (tokens is null) return null;
            await SaveAsync(tokens);
            return tokens.AccessToken;
        }
        catch (HttpRequestException) { return current; }
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var access = await SecureStorage.Default.GetAsync("edoc.access_token");
        var refresh = await SecureStorage.Default.GetAsync("edoc.refresh_token");
        if (!string.IsNullOrWhiteSpace(access) && !string.IsNullOrWhiteSpace(refresh))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout")
            {
                Content = JsonContent.Create(new { refreshToken = refresh })
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", access);
            try { using var _ = await httpClient.SendAsync(request, cancellationToken); }
            catch (HttpRequestException) { /* Local session is still cleared if the server is unavailable. */ }
        }
        SecureStorage.Default.Remove("edoc.access_token");
        SecureStorage.Default.Remove("edoc.access_expires_at");
        SecureStorage.Default.Remove("edoc.refresh_token");
        SecureStorage.Default.Remove("edoc.roles");
        SecureStorage.Default.Remove("edoc.display_name");
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    private static async Task SaveAsync(TokenSession tokens)
    {
        await SecureStorage.Default.SetAsync("edoc.access_token", tokens.AccessToken);
        await SecureStorage.Default.SetAsync("edoc.access_expires_at", tokens.AccessTokenExpiresAt.ToString("O"));
        await SecureStorage.Default.SetAsync("edoc.refresh_token", tokens.RefreshToken);
        await SecureStorage.Default.SetAsync("edoc.roles", string.Join('|', tokens.Roles));
        await SecureStorage.Default.SetAsync("edoc.display_name", tokens.FullName);
    }
}

public sealed record RegistrationResult(bool Succeeded, System.Net.HttpStatusCode StatusCode, string? ErrorMessage = null);

public sealed record TokenSession(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt, string FullName, string[] Roles);
