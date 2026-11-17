using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using EDOC.Web.Models;
using EDOC.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EDOC.Web.Pages.Admin;

public sealed class EditFacilityModel(ApiPortalClient api, IConfiguration configuration, IWebHostEnvironment environment) : PageModel
{
    public bool DemoMode => environment.IsDevelopment() && configuration.GetValue<bool>("Directory:DemoMode");
    [BindProperty] public FacilityInput Input { get; set; } = NewFacility();
    public PortalHospital? Facility { get; private set; }
    public DirectoryLocationOptions Locations { get; private set; } = new([], [], []);
    public IReadOnlyList<AdminPortalSpecialty> Specialties { get; private set; } = [];
    public string? Message { get; private set; }
    public bool IsError { get; private set; }
    public bool IsEditing => Input.Id != Guid.Empty;

    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken cancellationToken)
    {
        try
        {
            if (!await LoadOptionsAsync(cancellationToken)) return Page();
            if (id is null) return Page();

            Facility = await FindAsync(id.Value, cancellationToken);
            if (Facility is null) return NotFound();
            Input = FromFacility(Facility);
            return Page();
        }
        catch (HttpRequestException)
        {
            Message = "The E-DOC service is unavailable. Please try again shortly.";
            IsError = true;
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await LoadOptionsAsync(cancellationToken)) return Page();
            if (IsEditing)
            {
                Facility = await FindAsync(Input.Id, cancellationToken);
                if (Facility is null) return NotFound();
            }
            if (!ModelState.IsValid) return Page();

            var request = new SaveFacilityRequest
            {
                Name = Input.Name.Trim(),
                NameKm = Clean(Input.NameKm),
                Address = Clean(Input.Address),
                Phone = Clean(Input.Phone),
                Email = Clean(Input.Email),
                Website = Clean(Input.Website),
                OpeningHours = Clean(Input.OpeningHours),
                FacilityType = Input.FacilityType,
                Ownership = Input.Ownership,
                NssfStatus = Input.NssfStatus,
                OperationalStatus = Input.OperationalStatus,
                ProvinceCode = Input.ProvinceCode,
                DistrictCode = Clean(Input.DistrictCode),
                CommuneCode = Clean(Input.CommuneCode),
                Latitude = Input.Latitude,
                Longitude = Input.Longitude,
                SourceUrl = Clean(Input.SourceUrl),
                SourcePublishedOn = Input.SourcePublishedOn,
                VerifiedOn = Input.VerifiedOn,
                IsActive = Input.IsActive,
                SpecialtyIds = (Input.SpecialtyIds ?? []).Distinct().ToArray(),
                Services = ParseServices(Input.ServicesText)
            };

            using var response = IsEditing
                ? await api.PutAsync(HttpContext, $"api/admin/directory/hospitals/{Input.Id}", request, cancellationToken)
                : await api.PostAsync(HttpContext, "api/admin/directory/hospitals", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Message = await ReadErrorAsync(response, cancellationToken);
                IsError = true;
                return Page();
            }

            TempData["DirectoryMessage"] = IsEditing ? "Facility details updated." : "Facility added to the directory.";
            return RedirectToPage("/Admin/Directory");
        }
        catch (HttpRequestException)
        {
            Message = "The E-DOC service is unavailable. Your changes were not saved.";
            IsError = true;
            return Page();
        }
    }

    private async Task<bool> LoadOptionsAsync(CancellationToken cancellationToken)
    {
        var locations = await api.GetAsync<DirectoryLocationOptions>(HttpContext,
            "api/admin/directory/locations", cancellationToken);
        var specialties = await api.GetAsync<List<AdminPortalSpecialty>>(HttpContext,
            "api/admin/directory/specialties", cancellationToken);
        if (locations is null || specialties is null)
        {
            Message = "Facility options could not be loaded. Check your administrator session and try again.";
            IsError = true;
            return false;
        }
        Locations = locations;
        Specialties = specialties;
        return true;
    }

    private Task<PortalHospital?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        api.GetAsync<PortalHospital>(HttpContext, $"api/admin/directory/hospitals/{id}", cancellationToken);

    private static FacilityInput NewFacility() => new()
    {
        FacilityType = "Unknown",
        Ownership = "Unknown",
        NssfStatus = "PENDING_VERIFICATION",
        OperationalStatus = "PENDING_VERIFICATION",
        IsActive = false
    };

    private static FacilityInput FromFacility(PortalHospital facility) => new()
    {
        Id = facility.Id,
        Name = facility.Name,
        NameKm = facility.NameKm,
        Address = facility.Address,
        Phone = facility.Phone,
        Email = facility.Email,
        Website = facility.Website,
        OpeningHours = facility.OpeningHours,
        FacilityType = facility.FacilityType,
        Ownership = facility.Ownership,
        NssfStatus = facility.NssfStatus is "NSSF" or "NON_NSSF" or "PENDING_VERIFICATION"
            ? facility.NssfStatus : "PENDING_VERIFICATION",
        OperationalStatus = facility.OperationalStatus,
        ProvinceCode = facility.ProvinceCode ?? string.Empty,
        DistrictCode = facility.DistrictCode,
        CommuneCode = facility.CommuneCode,
        Latitude = facility.Latitude,
        Longitude = facility.Longitude,
        SourceUrl = facility.SourceUrl,
        SourcePublishedOn = facility.SourcePublishedOn,
        VerifiedOn = facility.VerifiedOn,
        IsActive = facility.IsActive,
        SpecialtyIds = facility.SpecialtyIds.ToArray(),
        ServicesText = string.Join(Environment.NewLine, facility.Services)
    };

    private static string[] ParseServices(string? value) => (value ?? string.Empty)
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(service => service.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

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
        return response.StatusCode == System.Net.HttpStatusCode.Unauthorized
            ? "Your session expired. Sign in again to continue."
            : "The facility could not be saved. Check the highlighted details and try again.";
    }

    public sealed class FacilityInput
    {
        public Guid Id { get; set; }
        [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
        [StringLength(240)] public string? NameKm { get; set; }
        [StringLength(500)] public string? Address { get; set; }
        [Phone, StringLength(32)] public string? Phone { get; set; }
        [EmailAddress, StringLength(256)] public string? Email { get; set; }
        [Url, StringLength(1000)] public string? Website { get; set; }
        [StringLength(240)] public string? OpeningHours { get; set; }
        [Required] public string FacilityType { get; set; } = "Unknown";
        [Required] public string Ownership { get; set; } = "Unknown";
        [Required] public string NssfStatus { get; set; } = "PENDING_VERIFICATION";
        [Required] public string OperationalStatus { get; set; } = "PENDING_VERIFICATION";
        [Required] public string ProvinceCode { get; set; } = string.Empty;
        public string? DistrictCode { get; set; }
        public string? CommuneCode { get; set; }
        [Range(-90, 90)] public double? Latitude { get; set; }
        [Range(-180, 180)] public double? Longitude { get; set; }
        [Url, StringLength(1000)] public string? SourceUrl { get; set; }
        public DateOnly? SourcePublishedOn { get; set; }
        public DateOnly? VerifiedOn { get; set; }
        public bool IsActive { get; set; }
        public Guid[]? SpecialtyIds { get; set; } = [];
        [StringLength(4000)] public string? ServicesText { get; set; }
    }
}
