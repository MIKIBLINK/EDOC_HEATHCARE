using EDOC.Web.Models;
using EDOC.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EDOC.Web.Pages.HospitalAdmin;

public sealed class IndexModel(ApiPortalClient api) : PageModel
{
    [FromQuery] public Guid? HospitalId { get; set; }
    public IReadOnlyList<HospitalAdminHospitalOption> Hospitals { get; private set; } = [];
    public HospitalAdminHospitalOption? SelectedHospital { get; private set; }
    public HospitalAdminDashboard? Summary { get; private set; }
    public IReadOnlyList<HospitalAdminAppointment> Appointments { get; private set; } = [];
    public string? Error { get; private set; }
    public string? Message { get; private set; }
    public bool IsError { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            Hospitals = await api.GetAsync<List<HospitalAdminHospitalOption>>(HttpContext,
                "api/hospital-admin/hospitals", cancellationToken) ?? [];
            if (Hospitals.Count == 0) { Error = "No hospital is assigned to this administrator. Contact a platform administrator."; return; }
            SelectedHospital = Hospitals.FirstOrDefault(x => x.Id == HospitalId) ?? Hospitals[0];
            HospitalId = SelectedHospital.Id;
            Message = TempData["HospitalAdminMessage"] as string;
            IsError = string.Equals(TempData["HospitalAdminError"] as string, "true", StringComparison.Ordinal);
            Summary = await api.GetAsync<HospitalAdminDashboard>(HttpContext,
                $"api/hospital-admin/hospitals/{HospitalId}/dashboard", cancellationToken);
            var result = await api.GetAsync<DirectoryPageResult<HospitalAdminAppointment>>(HttpContext,
                $"api/hospital-admin/hospitals/{HospitalId}/appointments?page=1&pageSize=30", cancellationToken);
            Appointments = result?.Items ?? [];
        }
        catch (HttpRequestException) { Error = "The E-DOC service is unavailable. Hospital data could not be loaded."; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { Error = "The E-DOC service timed out. Please refresh the page."; }
    }

    public async Task<IActionResult> OnPostStatusAsync(Guid hospitalId, Guid appointmentId, string status,
        CancellationToken cancellationToken)
    {
        var value = status switch { "Confirmed" => 1, "Completed" => 2, "Cancelled" => 3, _ => -1 };
        if (value < 0) return BadRequest();
        try
        {
            using var response = await api.PutAsync(HttpContext,
                $"api/hospital-admin/hospitals/{hospitalId}/appointments/{appointmentId}/status", new { status = value }, cancellationToken);
            TempData["HospitalAdminMessage"] = response.IsSuccessStatusCode
                ? "Appointment status updated."
                : response.StatusCode == System.Net.HttpStatusCode.Conflict
                    ? "That status transition is not available anymore."
                    : "The appointment could not be updated.";
            TempData["HospitalAdminError"] = response.IsSuccessStatusCode ? "false" : "true";
        }
        catch (HttpRequestException) { TempData["HospitalAdminMessage"] = "The E-DOC service is unavailable. No change was made."; TempData["HospitalAdminError"] = "true"; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { TempData["HospitalAdminMessage"] = "The E-DOC service timed out. Check appointment status before retrying."; TempData["HospitalAdminError"] = "true"; }
        return RedirectToPage(new { hospitalId });
    }
}
