using EDOC.Web.Models;
using EDOC.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EDOC.Web.Pages.Admin;

public sealed class StaffModel(ApiPortalClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? HospitalId { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public IReadOnlyList<AdminStaffSummary> Staff { get; private set; } = [];
    public IReadOnlyList<PortalHospitalOption> Hospitals { get; private set; } = [];
    public int Total { get; private set; }
    public int PageCount => Math.Max(1, (int)Math.Ceiling(Total / 30d));
    public string? Message { get; private set; }
    public bool IsError { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        PageNumber = Math.Max(1, PageNumber);
        try
        {
            var hospitalResult = await api.GetAsync<DirectoryPageResult<PortalHospital>>(HttpContext,
                "api/admin/directory/hospitals?page=1&pageSize=100", cancellationToken);
            Hospitals = hospitalResult?.Items.Select(x => new PortalHospitalOption(x.Id, x.Name, x.Province)).ToArray() ?? [];
            var route = $"api/admin/staff?page={PageNumber}&pageSize=30&search={Uri.EscapeDataString(Search?.Trim() ?? string.Empty)}";
            if (HospitalId.HasValue) route += $"&hospitalId={HospitalId}";
            var result = await api.GetAsync<DirectoryPageResult<AdminStaffSummary>>(HttpContext, route, cancellationToken);
            Staff = result?.Items ?? [];
            Total = result?.Total ?? 0;
            Message = TempData["StaffMessage"] as string;
            IsError = string.Equals(TempData["StaffError"] as string, "true", StringComparison.Ordinal);
        }
        catch (HttpRequestException) { Message = "The E-DOC service is unavailable. Staff assignments could not be loaded."; IsError = true; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { Message = "The E-DOC service timed out. Please refresh."; IsError = true; }
    }

    public async Task<IActionResult> OnPostActivationAsync(Guid userId, bool active, string? search, Guid? hospitalId,
        int pageNumber, CancellationToken cancellationToken)
    {
        var currentUser = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.Equals(userId.ToString(), currentUser, StringComparison.OrdinalIgnoreCase))
        {
            TempData["StaffMessage"] = "You cannot suspend the account currently signed in.";
            TempData["StaffError"] = "true";
            return RedirectToPage(new { search, hospitalId, pageNumber });
        }
        try
        {
            using var response = await api.PutAsync(HttpContext, $"api/admin/users/{userId}/activation", new { active }, cancellationToken);
            TempData["StaffMessage"] = response.IsSuccessStatusCode ? active ? "Staff account reactivated." : "Staff account suspended."
                : "The staff account could not be updated.";
            TempData["StaffError"] = response.IsSuccessStatusCode ? "false" : "true";
        }
        catch (HttpRequestException) { TempData["StaffMessage"] = "The E-DOC service is unavailable. No account change was made."; TempData["StaffError"] = "true"; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { TempData["StaffMessage"] = "The service timed out. Check the account before retrying."; TempData["StaffError"] = "true"; }
        return RedirectToPage(new { search, hospitalId, pageNumber });
    }
}
