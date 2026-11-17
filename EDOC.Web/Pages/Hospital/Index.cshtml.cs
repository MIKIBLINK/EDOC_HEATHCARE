using EDOC.Web.Models;
using EDOC.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EDOC.Web.Pages.Hospital;

public sealed class IndexModel(ApiPortalClient api) : PageModel
{
    public IReadOnlyList<PortalAppointment> Items { get; private set; } = [];
    public int Total { get; private set; }
    public string? Message { get; private set; }
    public bool IsError { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostStatusAsync(Guid id, string status, CancellationToken cancellationToken)
    {
        var statusCode = status switch { "Confirmed" => 1, "Completed" => 2, "Cancelled" => 3, _ => -1 };
        if (statusCode < 0) return BadRequest();
        using var response = await api.PutAsync(HttpContext, $"api/appointments/{id}/status", new { status = statusCode }, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            Message = response.StatusCode == System.Net.HttpStatusCode.Conflict
                ? "That status change is no longer available. The list below has been refreshed."
                : "The appointment could not be updated. Please try again.";
            IsError = true;
            await LoadAsync(cancellationToken);
            return Page();
        }
        TempData["StatusMessage"] = "Appointment updated.";
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Message ??= TempData["StatusMessage"] as string;
        try
        {
            var result = await api.GetAsync<DirectoryPageResult<PortalAppointment>>(HttpContext,
                "api/appointments?page=1&pageSize=50", cancellationToken);
            if (result is null) { Message = "Assigned appointments could not be loaded."; IsError = true; }
            else { Items = result.Items; Total = result.Total; }
        }
        catch (HttpRequestException) { Message = "The E-DOC service is unavailable. Please try again shortly."; IsError = true; }
    }

    public string StatusClass(string value) => value switch
    {
        "Confirmed" => "status-green",
        "Completed" => "status-blue",
        "Cancelled" => "status-red",
        _ => "status-amber"
    };
}
