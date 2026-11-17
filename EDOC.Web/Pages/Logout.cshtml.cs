using EDOC.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EDOC.Web.Pages;

[Authorize]
public sealed class LogoutModel(ApiPortalClient api) : PageModel
{
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await api.SignOutAsync(HttpContext, cancellationToken);
        return RedirectToPage("/Login");
    }
}
