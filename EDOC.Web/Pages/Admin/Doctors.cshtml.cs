using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using EDOC.Web.Models;
using EDOC.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EDOC.Web.Pages.Admin;

public sealed class DoctorsModel(ApiPortalClient api) : PageModel
{
    [BindProperty] public DoctorInput Input { get; set; } = new();
    [FromQuery] public string? Search { get; set; }
    public IReadOnlyList<AdminPortalUser> AvailableUsers { get; private set; } = [];
    public IReadOnlyList<AdminPortalSpecialty> Specialties { get; private set; } = [];
    public IReadOnlyList<PortalHospitalOption> Hospitals { get; private set; } = [];
    public string? Message { get; private set; }
    public bool IsError { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadOptionsAsync(cancellationToken);

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await LoadOptionsAsync(cancellationToken);
            if (IsError) return Page();
            if (!ModelState.IsValid || Input.UserId == Guid.Empty || Input.SpecialtyId == Guid.Empty || Input.HospitalId == Guid.Empty)
                return Page();

            var request = new
            {
                userId = Input.UserId,
                specialtyId = Input.SpecialtyId,
                hospitalId = Input.HospitalId,
                qualifications = Clean(Input.Qualifications),
                yearsExperience = Input.YearsExperience,
                biography = Clean(Input.Biography),
                consultationFee = Input.ConsultationFee
            };
            using var response = await api.PostAsync(HttpContext, "api/admin/doctors", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Message = await ReadErrorAsync(response, cancellationToken);
                IsError = true;
                return Page();
            }

            TempData["DoctorMessage"] = "Doctor profile added and linked to the selected hospital.";
            return RedirectToPage();
        }
        catch (HttpRequestException)
        {
            Message = "The E-DOC service is unavailable. No doctor profile was created.";
            IsError = true;
            return Page();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Message = "The E-DOC service took too long to respond. No doctor profile was created.";
            IsError = true;
            return Page();
        }
    }

    private async Task LoadOptionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var users = await api.GetAsync<DirectoryPageResult<AdminPortalUser>>(HttpContext,
                $"api/admin/users?page=1&pageSize=100&search={Uri.EscapeDataString(Search?.Trim() ?? string.Empty)}", cancellationToken);
            var doctors = await api.GetAsync<DirectoryPageResult<AdminPortalDoctor>>(HttpContext,
                "api/admin/doctors?page=1&pageSize=100", cancellationToken);
            Specialties = await api.GetAsync<List<AdminPortalSpecialty>>(HttpContext,
                "api/admin/directory/specialties", cancellationToken) ?? [];
            var hospitals = await api.GetAsync<DirectoryPageResult<PortalHospital>>(HttpContext,
                "api/admin/directory/hospitals?page=1&pageSize=100", cancellationToken);
            Hospitals = hospitals?.Items.Where(x => x.IsActive)
                .Select(x => new PortalHospitalOption(x.Id, x.Name, x.Province)).ToArray() ?? [];

            var existingDoctorUsers = doctors?.Items.Select(x => x.UserId).ToHashSet() ?? [];
            AvailableUsers = users?.Items
                .Where(x => x.IsActive && !existingDoctorUsers.Contains(x.Id))
                .Where(x => x.Roles.Any(role => role is "Patient" or "Caregiver" or "Doctor"))
                .ToArray() ?? [];
            Message = TempData["DoctorMessage"] as string;
        }
        catch (HttpRequestException)
        {
            Message = "The E-DOC service is unavailable. Doctor options could not be loaded.";
            IsError = true;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Message = "The E-DOC service took too long to respond. Doctor options could not be loaded.";
            IsError = true;
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            foreach (var key in new[] { "message", "detail", "title" })
                if (root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(value.GetString())) return value.GetString()!;
        }
        catch (JsonException) { }
        return response.StatusCode == System.Net.HttpStatusCode.Conflict
            ? "This account already has a doctor profile."
            : "The doctor profile could not be created. Check the account and try again.";
    }

    public sealed class DoctorInput
    {
        [Required] public Guid UserId { get; set; }
        [Required] public Guid SpecialtyId { get; set; }
        [Required] public Guid HospitalId { get; set; }
        [StringLength(300)] public string? Qualifications { get; set; }
        [Range(0, 80)] public int YearsExperience { get; set; }
        [StringLength(4000)] public string? Biography { get; set; }
        [Range(0, 100000)] public decimal? ConsultationFee { get; set; }
    }
}
