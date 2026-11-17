using EDOC.Web.Models;
using EDOC.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EDOC.Web.Pages.Admin;

public sealed class SpecialtiesModel(ApiPortalClient api) : PageModel
{
    public IReadOnlyList<AdminPortalSpecialty> Items { get; private set; } = [];
    public string? Message { get; private set; }
    public bool IsError { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Items = await api.GetAsync<List<AdminPortalSpecialty>>(HttpContext, "api/admin/directory/specialties", cancellationToken) ?? [];
        Message = TempData["SpecialtyMessage"] as string;
        IsError = string.Equals(TempData["SpecialtyError"] as string, "true", StringComparison.Ordinal);
    }

    public Task<IActionResult> OnPostCreateAsync(string name, string? nameKm, CancellationToken cancellationToken) =>
        SaveAsync(null, name, nameKm, cancellationToken);

    public Task<IActionResult> OnPostUpdateAsync(Guid id, string name, string? nameKm, CancellationToken cancellationToken) =>
        SaveAsync(id, name, nameKm, cancellationToken);

    private async Task<IActionResult> SaveAsync(Guid? id, string name, string? nameKm, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120 || (nameKm?.Length ?? 0) > 160)
        {
            TempData["SpecialtyMessage"] = "Enter a specialty name of 1–120 characters and an optional Khmer name up to 160 characters.";
            TempData["SpecialtyError"] = "true";
            return RedirectToPage();
        }
        var request = new { name = name.Trim(), nameKm = string.IsNullOrWhiteSpace(nameKm) ? null : nameKm.Trim() };
        using var response = id.HasValue
            ? await api.PutAsync(HttpContext, $"api/admin/directory/specialties/{id.Value}", request, cancellationToken)
            : await api.PostAsync(HttpContext, "api/admin/directory/specialties", request, cancellationToken);
        TempData["SpecialtyMessage"] = response.IsSuccessStatusCode ? "Specialty saved."
            : response.StatusCode == System.Net.HttpStatusCode.Conflict ? "A specialty with this English name already exists."
            : "The specialty could not be saved. Please try again.";
        TempData["SpecialtyError"] = response.IsSuccessStatusCode ? "false" : "true";
        return RedirectToPage();
    }
}
