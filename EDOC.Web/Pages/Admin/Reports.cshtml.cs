using EDOC.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EDOC.Web.Pages.Admin;

public sealed class ReportsModel(ApiPortalClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetDownloadAsync(string report, CancellationToken cancellationToken)
    {
        var route = report.ToLowerInvariant() switch
        {
            "hospitals" => "api/admin/reports/hospitals.csv",
            "doctors" => "api/admin/reports/doctors.csv",
            "patients" => "api/admin/reports/patients.csv",
            "appointments" => $"api/admin/reports/appointments.csv?from={From?.ToString("yyyy-MM-dd") ?? string.Empty}&to={To?.ToString("yyyy-MM-dd") ?? string.Empty}",
            _ => null
        };
        if (route is null) return BadRequest();
        try
        {
            var download = await api.GetDownloadAsync(HttpContext, route, cancellationToken);
            if (download is null) { Error = "The report could not be generated. Check the selected dates and try again."; return Page(); }
            return File(download.Content, download.ContentType, download.FileName ?? $"edoc-{report}.csv");
        }
        catch (HttpRequestException) { Error = "The E-DOC service is unavailable. No report was generated."; return Page(); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { Error = "The report request timed out. Narrow the date range and try again."; return Page(); }
    }
}
