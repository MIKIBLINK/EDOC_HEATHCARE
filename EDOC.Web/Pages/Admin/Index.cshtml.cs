using EDOC.Web.Models;
using EDOC.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EDOC.Web.Pages.Admin;

public sealed class IndexModel(ApiPortalClient api) : PageModel
{
    [FromQuery] public string? Search { get; set; }
    public AdminDashboardSummary? Summary { get; private set; }
    public IReadOnlyList<AdminPortalDoctor> PendingDoctors { get; private set; } = [];
    public IReadOnlyList<AdminPortalUser> Users { get; private set; } = [];
    public IReadOnlyList<PortalHospitalOption> Hospitals { get; private set; } = [];
    public string? Error { get; private set; }
    public string? Message { get; private set; }
    public bool IsError { get; private set; }
    public string? CurrentUserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    public int PendingWidth => Width(Summary?.PendingAppointments ?? 0);
    public int UpcomingWidth => Width(Summary?.UpcomingAppointments ?? 0);
    public int CompletedWidth => Width(Summary?.CompletedAppointments ?? 0);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            Summary = await api.GetAsync<AdminDashboardSummary>(HttpContext, "api/admin/dashboard", cancellationToken);
            if (Summary is null) Error = "The administrator dashboard could not be loaded.";
            var doctors = await api.GetAsync<DirectoryPageResult<AdminPortalDoctor>>(HttpContext,
                "api/admin/doctors?approved=false&page=1&pageSize=30", cancellationToken);
            PendingDoctors = doctors?.Items ?? [];
            var users = await api.GetAsync<DirectoryPageResult<AdminPortalUser>>(HttpContext,
                $"api/admin/users?page=1&pageSize=30&search={Uri.EscapeDataString(Search?.Trim() ?? string.Empty)}", cancellationToken);
            Users = users?.Items ?? [];
            var hospitals = await api.GetAsync<DirectoryPageResult<PortalHospital>>(HttpContext,
                "api/admin/directory/hospitals?page=1&pageSize=100", cancellationToken);
            Hospitals = hospitals?.Items.Select(x => new PortalHospitalOption(x.Id, x.Name, x.Province)).ToArray() ?? [];
            Message = TempData["AdminMessage"] as string;
            IsError = string.Equals(TempData["AdminError"] as string, "true", StringComparison.Ordinal);
        }
        catch (HttpRequestException) { Error = "The E-DOC service is unavailable. Please refresh in a moment."; }
    }

    public async Task<IActionResult> OnPostDoctorApprovalAsync(Guid doctorId, bool approved, CancellationToken cancellationToken)
    {
        using var response = await api.PutAsync(HttpContext, $"api/admin/doctors/{doctorId}/approval", new { approved }, cancellationToken);
        return FinishAction(response, approved ? "Doctor profile approved." : "Doctor profile declined.");
    }

    public async Task<IActionResult> OnPostUserActivationAsync(Guid userId, bool active, CancellationToken cancellationToken)
    {
        if (string.Equals(userId.ToString(), CurrentUserId, StringComparison.OrdinalIgnoreCase))
        {
            TempData["AdminMessage"] = "You cannot suspend the account currently signed in.";
            TempData["AdminError"] = "true";
            return RedirectToPage(new { search = Search });
        }
        using var response = await api.PutAsync(HttpContext, $"api/admin/users/{userId}/activation", new { active }, cancellationToken);
        return FinishAction(response, active ? "Account reactivated." : "Account suspended.");
    }

    public async Task<IActionResult> OnPostAssignHospitalStaffAsync(Guid userId, Guid hospitalId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || hospitalId == Guid.Empty) return BadRequest();
        try
        {
            using var response = await api.PostAsync(HttpContext, $"api/admin/hospitals/{hospitalId}/staff/{userId}", new { }, cancellationToken);
            return FinishAction(response, "Hospital staff access assigned.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TempData["AdminMessage"] = "The E-DOC service took too long to respond. Please try again.";
            TempData["AdminError"] = "true";
            return RedirectToPage(new { search = Search });
        }
    }

    public async Task<IActionResult> OnPostAssignHospitalAdminAsync(Guid userId, Guid hospitalId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || hospitalId == Guid.Empty) return BadRequest();
        try
        {
            using var response = await api.PostAsync(HttpContext, $"api/admin/hospitals/{hospitalId}/admins/{userId}", new { }, cancellationToken);
            return FinishAction(response, "Hospital administrator assigned with facility-scoped access.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TempData["AdminMessage"] = "The E-DOC service timed out. Check the assignment before retrying.";
            TempData["AdminError"] = "true";
            return RedirectToPage(new { search = Search });
        }
    }

    private IActionResult FinishAction(HttpResponseMessage response, string success)
    {
        TempData["AdminMessage"] = response.IsSuccessStatusCode ? success
            : response.StatusCode == System.Net.HttpStatusCode.Conflict ? "This assignment already exists."
            : "The requested change was not accepted. Check the account and try again.";
        TempData["AdminError"] = response.IsSuccessStatusCode ? "false" : "true";
        return RedirectToPage(new { search = Search });
    }

    private int Width(int value)
    {
        var total = (Summary?.PendingAppointments ?? 0) + (Summary?.UpcomingAppointments ?? 0) + (Summary?.CompletedAppointments ?? 0);
        return total == 0 ? 3 : Math.Clamp((int)Math.Round(100d * value / total), 3, 100);
    }
}
