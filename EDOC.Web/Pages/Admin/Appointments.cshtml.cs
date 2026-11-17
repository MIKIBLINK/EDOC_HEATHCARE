using EDOC.Web.Models;
using EDOC.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EDOC.Web.Pages.Admin;

public sealed class AppointmentsModel(ApiPortalClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? HospitalId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? DoctorId { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? Date { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public IReadOnlyList<PortalAppointment> Appointments { get; private set; } = [];
    public IReadOnlyList<PortalHospitalOption> Hospitals { get; private set; } = [];
    public IReadOnlyList<AdminPortalDoctor> Doctors { get; private set; } = [];
    public int Total { get; private set; }
    public int PageCount => Math.Max(1, (int)Math.Ceiling(Total / 30d));
    public string? Error { get; private set; }
    public string? Message { get; private set; }
    public bool IsError { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        PageNumber = Math.Max(1, PageNumber);
        try
        {
            var hospitals = await api.GetAsync<DirectoryPageResult<PortalHospital>>(HttpContext,
                "api/admin/directory/hospitals?page=1&pageSize=100", cancellationToken);
            Hospitals = hospitals?.Items.Select(x => new PortalHospitalOption(x.Id, x.Name, x.Province)).ToArray() ?? [];
            var doctors = await api.GetAsync<DirectoryPageResult<AdminPortalDoctor>>(HttpContext,
                "api/admin/doctors?page=1&pageSize=100", cancellationToken);
            Doctors = doctors?.Items ?? [];
            var parameters = new List<string> { $"page={PageNumber}", "pageSize=30" };
            Add(parameters, "search", Search);
            Add(parameters, "status", Status);
            if (HospitalId.HasValue) parameters.Add($"hospitalId={HospitalId}");
            if (DoctorId.HasValue) parameters.Add($"doctorId={DoctorId}");
            if (Date.HasValue) parameters.Add($"date={Date:yyyy-MM-dd}");
            var result = await api.GetAsync<DirectoryPageResult<PortalAppointment>>(HttpContext,
                $"api/appointments?{string.Join('&', parameters)}", cancellationToken);
            Appointments = result?.Items ?? [];
            Total = result?.Total ?? 0;
            Message = TempData["AppointmentMessage"] as string;
            IsError = string.Equals(TempData["AppointmentError"] as string, "true", StringComparison.Ordinal);
        }
        catch (HttpRequestException) { Error = "The E-DOC service is unavailable. Appointments could not be loaded."; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { Error = "The E-DOC service timed out. Please refresh."; }
    }

    public async Task<IActionResult> OnPostStatusAsync(Guid id, string status, string? search, string? currentStatus,
        Guid? hospitalId, Guid? doctorId, DateOnly? date, int pageNumber, CancellationToken cancellationToken)
    {
        var value = status switch { "Confirmed" => 1, "Completed" => 2, "Cancelled" => 3, _ => -1 };
        if (value < 0) return BadRequest();
        try
        {
            using var response = await api.PutAsync(HttpContext, $"api/appointments/{id}/status", new { status = value }, cancellationToken);
            TempData["AppointmentMessage"] = response.IsSuccessStatusCode ? "Appointment status updated."
                : response.StatusCode == System.Net.HttpStatusCode.Conflict ? "That status transition is no longer available."
                : "The appointment could not be updated.";
            TempData["AppointmentError"] = response.IsSuccessStatusCode ? "false" : "true";
        }
        catch (HttpRequestException) { TempData["AppointmentMessage"] = "The E-DOC service is unavailable. No change was made."; TempData["AppointmentError"] = "true"; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { TempData["AppointmentMessage"] = "The service timed out. Check the appointment status before retrying."; TempData["AppointmentError"] = "true"; }
        return RedirectToPage(new { search, status = currentStatus, hospitalId, doctorId, date, pageNumber });
    }

    private static void Add(ICollection<string> values, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) values.Add($"{key}={Uri.EscapeDataString(value.Trim())}");
    }
}
