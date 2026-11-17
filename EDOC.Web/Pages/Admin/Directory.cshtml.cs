using EDOC.Web.Models;
using EDOC.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EDOC.Web.Pages.Admin;

public sealed class DirectoryModel(ApiPortalClient api, IConfiguration configuration, IWebHostEnvironment environment) : PageModel
{
    public bool DemoMode => environment.IsDevelopment() && configuration.GetValue<bool>("Directory:DemoMode");
    [FromQuery] public string? Search { get; set; }
    [FromQuery] public string? NssfStatus { get; set; }
    [FromQuery(Name = "page")] public int CurrentPage { get; set; } = 1;
    public IReadOnlyList<PortalHospital> Items { get; private set; } = [];
    public IReadOnlyList<PortalDirectoryAudit> Audit { get; private set; } = [];
    public int Total { get; private set; }
    public string? Message { get; private set; }
    public bool IsError { get; private set; }
    public int PageCount => Math.Max(1, (int)Math.Ceiling(Total / 24d));
    public string? Error { get; private set; }
    public string[] NssfOptions => ["NSSF", "NON_NSSF", "PENDING_VERIFICATION", "INACTIVE"];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        CurrentPage = Math.Max(1, CurrentPage);
        var query = $"api/admin/directory/hospitals?page={CurrentPage}&pageSize=24";
        if (!string.IsNullOrWhiteSpace(Search)) query += $"&search={Uri.EscapeDataString(Search.Trim())}";
        if (!string.IsNullOrWhiteSpace(NssfStatus)) query += $"&nssfStatus={Uri.EscapeDataString(NssfStatus.Trim())}";
        try
        {
            var result = await api.GetAsync<DirectoryPageResult<PortalHospital>>(HttpContext, query, cancellationToken);
            if (result is null) Error = "The facility directory could not be loaded.";
            else { Items = result.Items; Total = result.Total; }
            Audit = await api.GetAsync<DirectoryPageResult<PortalDirectoryAudit>>(HttpContext,
                "api/admin/directory/audit-logs?page=1&pageSize=10", cancellationToken) is { } audit ? audit.Items : [];
            Message = TempData["DirectoryMessage"] as string;
            IsError = string.Equals(TempData["DirectoryError"] as string, "true", StringComparison.Ordinal);
        }
        catch (HttpRequestException) { Error = "The E-DOC service is unavailable. Please try again shortly."; }
    }

    public string StatusLabel(string? value, string? sourceUrl = null, DateOnly? verifiedOn = null)
    {
        if (DemoMode && (string.IsNullOrWhiteSpace(sourceUrl) || verifiedOn is null))
            return value switch
            {
                "NSSF" => "NSSF · demo, unverified",
                "NON_NSSF" => "Non-NSSF · demo, unverified",
                "ACTIVE" => "Operating · demo, unverified",
                _ => "Pending verification"
            };
        return value switch
        {
            "NSSF" => "NSSF partner · verified",
            "NON_NSSF" => "Not NSSF · verified",
            "INACTIVE" => "Inactive",
            "ACTIVE" => "Operating · verified",
            _ => "Pending verification"
        };
    }

    public string StatusClass(string? value) => value switch
    {
        "NSSF" or "ACTIVE" => "status-green",
        "NON_NSSF" => "status-blue",
        "INACTIVE" => "status-red",
        _ => "status-amber"
    };

    public string OperationalStatusLabel(PortalHospital hospital) => DemoMode
        ? "Pending verification"
        : StatusLabel(hospital.OperationalStatus, hospital.SourceUrl, hospital.VerifiedOn);

    public string ReviewDateLabel(PortalHospital hospital) => hospital.VerifiedOn is { } date
        ? $"Last checked: {date:dd MMM yyyy}"
        : "Last checked: Pending";

    public string TypeLabel(string value) => value switch
    {
        "NationalHospital" => "National hospital",
        "ProvincialHospital" => "Provincial hospital",
        "ReferralHospital" => "Referral hospital",
        "SpecialistHospital" => "Specialist hospital",
        "MedicalCenter" => "Medical center",
        "Clinic" => "Clinic",
        _ => "Facility type pending"
    };

    public string? MapUrl(PortalHospital hospital)
    {
        if (hospital.Latitude is not { } latitude || hospital.Longitude is not { } longitude
            || hospital.VerifiedOn is null || string.IsNullOrWhiteSpace(hospital.SourceUrl)) return null;
        var coordinates = $"{latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        return $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(coordinates)}";
    }
}
