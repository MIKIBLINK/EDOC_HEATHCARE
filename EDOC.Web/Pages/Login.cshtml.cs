using System.ComponentModel.DataAnnotations;
using EDOC.Web.Models;
using EDOC.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EDOC.Web.Pages;

public sealed class LoginModel(ApiPortalClient api) : PageModel
{
    [BindProperty] public LoginInput Input { get; set; } = new();
    public string? Message { get; private set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true) return Redirect(User.IsInRole("Administrator") || User.IsInRole("SuperAdmin") ? "/admin" : User.IsInRole("HospitalAdmin") ? "/hospital-admin" : "/hospital");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        PortalTokenResponse? tokens;
        try { tokens = await api.LoginAsync(Input.LoginIdentifier.Trim(), Input.Password, cancellationToken); }
        catch (HttpRequestException)
        {
            Message = "The E-DOC sign-in service or database is unavailable. Please try again after the service is restored.";
            return Page();
        }
        if (tokens is null)
        {
            Message = "The email or password is incorrect, or the account is unavailable.";
            return Page();
        }

        var isWebRole = tokens.Roles.Any(role => role is "Administrator" or "SuperAdmin" or "HospitalAdmin" or "HospitalStaff");
        if (!isWebRole)
        {
            await api.RevokeAsync(tokens, cancellationToken);
            Message = "This account uses the E-DOC mobile app. Please sign in there to continue.";
            return Page();
        }

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            ApiPortalClient.CreatePrincipal(tokens), ApiPortalClient.CreateTicket(tokens));
        return Redirect(tokens.Roles.Contains("Administrator", StringComparer.OrdinalIgnoreCase) || tokens.Roles.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase)
            ? "/admin" : tokens.Roles.Contains("HospitalAdmin", StringComparer.OrdinalIgnoreCase) ? "/hospital-admin" : "/hospital");
    }

    public sealed class LoginInput
    {
        [Required, StringLength(256)] public string LoginIdentifier { get; set; } = string.Empty;
        [Required] public string Password { get; set; } = string.Empty;
    }
}
