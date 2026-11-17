using System.Text.Json;
using EDOC.Web.Models;
using EDOC.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EDOC.Web.Pages.Admin;

public sealed class PatientsModel(ApiPortalClient api) : PageModel
{
    [FromQuery] public string? Search { get; set; }
    [FromQuery] public bool? Active { get; set; }
    [FromQuery] public int PageNumber { get; set; } = 1;
    public IReadOnlyList<AdminPatientSummary> Patients { get; private set; } = [];
    public int Total { get; private set; }
    public int PageCount => Math.Max(1, (int)Math.Ceiling(Total / 30d));
    public string? Message { get; private set; }
    public bool IsError { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        PageNumber = Math.Max(1, PageNumber);
        try
        {
            var route = $"api/admin/patients?page={PageNumber}&pageSize=30&search={Uri.EscapeDataString(Search?.Trim() ?? string.Empty)}";
            if (Active.HasValue) route += $"&active={Active.Value.ToString().ToLowerInvariant()}";
            var result = await api.GetAsync<DirectoryPageResult<AdminPatientSummary>>(HttpContext, route, cancellationToken);
            Patients = result?.Items ?? [];
            Total = result?.Total ?? 0;
            Message = TempData["PatientMessage"] as string;
            IsError = string.Equals(TempData["PatientError"] as string, "true", StringComparison.Ordinal);
        }
        catch (HttpRequestException) { Message = "The E-DOC service is unavailable. Patient accounts could not be loaded."; IsError = true; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { Message = "The E-DOC service took too long to respond. Please refresh."; IsError = true; }
    }

    public async Task<IActionResult> OnPostActivationAsync(Guid userId, bool active, string? search, bool? accountFilter,
        int pageNumber, CancellationToken cancellationToken)
    {
        var currentUser = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.Equals(userId.ToString(), currentUser, StringComparison.OrdinalIgnoreCase))
        {
            TempData["PatientMessage"] = "You cannot suspend the account currently signed in.";
            TempData["PatientError"] = "true";
            return RedirectToPage(new { search, active = accountFilter, pageNumber });
        }
        try
        {
            using var response = await api.PutAsync(HttpContext, $"api/admin/users/{userId}/activation", new { active }, cancellationToken);
            TempData["PatientMessage"] = response.IsSuccessStatusCode
                ? active ? "Patient account reactivated." : "Patient account suspended."
                : await ReadErrorAsync(response, cancellationToken);
            TempData["PatientError"] = response.IsSuccessStatusCode ? "false" : "true";
        }
        catch (HttpRequestException) { TempData["PatientMessage"] = "The E-DOC service is unavailable. No account change was made."; TempData["PatientError"] = "true"; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { TempData["PatientMessage"] = "The E-DOC service timed out. Check account status before retrying."; TempData["PatientError"] = "true"; }
        return RedirectToPage(new { search, active = accountFilter, pageNumber });
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(body);
            foreach (var key in new[] { "message", "detail", "title" })
                if (document.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(value.GetString())) return value.GetString()!;
        }
        catch (JsonException) { }
        return "The account change was rejected. Refresh and try again.";
    }
}
