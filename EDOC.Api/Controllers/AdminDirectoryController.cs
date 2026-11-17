using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using EDOC.Api.Data;
using EDOC.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDOC.Api.Controllers;

[ApiController]
[Authorize(Roles = "Administrator,SuperAdmin")]
[Route("api/admin/directory")]
public sealed class AdminDirectoryController(HealthcareDbContext database, IConfiguration configuration,
    IHostEnvironment environment) : ControllerBase
{
    private bool DemoMode => environment.IsDevelopment() && configuration.GetValue<bool>("Directory:DemoMode");
    [HttpGet("hospitals")]
    public async Task<IActionResult> Hospitals([FromQuery] string? search, [FromQuery] string? nssfStatus, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.Hospitals.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Name.Contains(term) || (x.NameKm != null && x.NameKm.Contains(term))
                || (x.Province != null && (x.Province.Name.Contains(term) || x.Province.NameKm.Contains(term))));
        }
        if (!string.IsNullOrWhiteSpace(nssfStatus))
        {
            var status = nssfStatus.Trim().ToUpperInvariant();
            if (status is not ("NSSF" or "NON_NSSF" or "PENDING_VERIFICATION" or "INACTIVE"))
                return BadRequest(new { message = "Unsupported NSSF status." });
            query = status == "INACTIVE" ? query.Where(x => x.OperationalStatus == "INACTIVE")
                : query.Where(x => x.NssfStatus == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new HospitalDirectoryRow(x.Id, x.Name, x.NameKm, x.Address, x.Phone, x.Email,
                x.Website, x.OpeningHours, x.FacilityType, x.Ownership, x.NssfStatus, x.OperationalStatus, x.ProvinceCode,
                x.Province == null ? null : x.Province.Name, x.Province == null ? null : x.Province.NameKm,
                x.DistrictCode, x.CommuneCode, x.Latitude, x.Longitude, x.SourceUrl, x.SourcePublishedOn,
                x.VerifiedOn, x.IsActive))
            .ToListAsync(cancellationToken);
        var items = await AddHospitalRelationsAsync(rows, cancellationToken);
        return Ok(new { items, page, pageSize, total });
    }

    [HttpGet("hospitals/{hospitalId:guid}")]
    public async Task<IActionResult> Hospital(Guid hospitalId, CancellationToken cancellationToken)
    {
        var row = await database.Hospitals.AsNoTracking()
            .Where(x => x.Id == hospitalId)
            .Select(x => new HospitalDirectoryRow(x.Id, x.Name, x.NameKm, x.Address, x.Phone, x.Email,
                x.Website, x.OpeningHours, x.FacilityType, x.Ownership, x.NssfStatus, x.OperationalStatus, x.ProvinceCode,
                x.Province == null ? null : x.Province.Name, x.Province == null ? null : x.Province.NameKm,
                x.DistrictCode, x.CommuneCode, x.Latitude, x.Longitude, x.SourceUrl, x.SourcePublishedOn,
                x.VerifiedOn, x.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null) return NotFound();
        var cards = await AddHospitalRelationsAsync([row], cancellationToken);
        return Ok(cards[0]);
    }

    [HttpPost("hospitals")]
    public async Task<IActionResult> CreateHospital([FromBody] SaveHospitalDirectoryRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateHospitalAsync(request, null, cancellationToken);
        if (validation is not null) return validation;
        var hospital = new Hospital { Name = request.Name.Trim() };
        database.Hospitals.Add(hospital);
        await ApplyHospitalAsync(hospital, request, cancellationToken);
        database.AuditLogs.Add(CreateAudit("directory.hospital.create", "Hospital", hospital.Id,
            $"Ownership={hospital.Ownership}; NSSF={hospital.NssfStatus}; operational={hospital.OperationalStatus}."));
        await database.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Hospitals), new { id = hospital.Id }, new { hospital.Id });
    }

    [HttpPut("hospitals/{hospitalId:guid}")]
    public async Task<IActionResult> UpdateHospital(Guid hospitalId, [FromBody] SaveHospitalDirectoryRequest request,
        CancellationToken cancellationToken)
    {
        var hospital = await database.Hospitals.Include(x => x.Services)
            .Include(x => x.Specialties).SingleOrDefaultAsync(x => x.Id == hospitalId, cancellationToken);
        if (hospital is null) return NotFound();
        var validation = await ValidateHospitalAsync(request, hospital, cancellationToken);
        if (validation is not null) return validation;
        await ApplyHospitalAsync(hospital, request, cancellationToken);
        database.AuditLogs.Add(CreateAudit("directory.hospital.update", "Hospital", hospital.Id,
            $"Ownership={hospital.Ownership}; NSSF={hospital.NssfStatus}; operational={hospital.OperationalStatus}."));
        await database.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("specialties")]
    public async Task<IActionResult> Specialties(CancellationToken cancellationToken) =>
        Ok(await database.Specialties.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new AdminSpecialtyDirectoryCard(x.Id, x.Name, x.NameKm)).ToListAsync(cancellationToken));

    [HttpGet("locations")]
    public async Task<IActionResult> Locations(CancellationToken cancellationToken)
    {
        var provinces = await database.Provinces.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new { x.Code, x.Name, x.NameKm }).ToListAsync(cancellationToken);
        var districts = await database.Districts.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new { x.Code, x.Name, x.ProvinceCode }).ToListAsync(cancellationToken);
        var communes = await database.Communes.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new { x.Code, x.Name, x.DistrictCode }).ToListAsync(cancellationToken);
        return Ok(new { provinces, districts, communes });
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> AuditLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.AuditLogs.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AdminDirectoryAuditCard(x.Id, x.Action, x.EntityType, x.EntityId, x.Summary,
                x.Actor == null ? null : x.Actor.FullName, x.CreatedAt))
            .ToListAsync(cancellationToken);
        return Ok(new { items, page, pageSize, total });
    }

    [HttpPost("specialties")]
    public Task<IActionResult> CreateSpecialty([FromBody] SaveSpecialtyRequest request,
        CancellationToken cancellationToken) => SaveSpecialtyAsync(null, request, cancellationToken);

    [HttpPut("specialties/{specialtyId:guid}")]
    public Task<IActionResult> UpdateSpecialty(Guid specialtyId, [FromBody] SaveSpecialtyRequest request,
        CancellationToken cancellationToken) => SaveSpecialtyAsync(specialtyId, request, cancellationToken);

    private async Task<IActionResult> SaveSpecialtyAsync(Guid? specialtyId, SaveSpecialtyRequest request,
        CancellationToken cancellationToken)
    {
        var duplicate = await database.Specialties.AnyAsync(x => x.Name == request.Name.Trim()
            && (!specialtyId.HasValue || x.Id != specialtyId.Value), cancellationToken);
        if (duplicate) return Conflict(new { message = "A specialty with this name already exists." });
        Specialty specialty;
        if (specialtyId.HasValue)
        {
            var existing = await database.Specialties.SingleOrDefaultAsync(x => x.Id == specialtyId.Value, cancellationToken);
            if (existing is null) return NotFound();
            specialty = existing;
        }
        else
        {
            specialty = new Specialty();
            database.Specialties.Add(specialty);
        }
        specialty.Name = request.Name.Trim();
        specialty.NameKm = string.IsNullOrWhiteSpace(request.NameKm) ? null : request.NameKm.Trim();
        database.AuditLogs.Add(CreateAudit(specialtyId.HasValue ? "directory.specialty.update" : "directory.specialty.create",
            "Specialty", specialty.Id, "Specialty name and localized name updated."));
        await database.SaveChangesAsync(cancellationToken);
        return specialtyId.HasValue ? NoContent() : CreatedAtAction(nameof(Specialties), new { id = specialty.Id }, new { specialty.Id });
    }

    private async Task<IReadOnlyList<AdminHospitalDirectoryCard>> AddHospitalRelationsAsync(
        IReadOnlyList<HospitalDirectoryRow> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return [];
        var ids = rows.Select(row => row.Id).ToArray();
        var services = await database.HospitalServices.AsNoTracking().Where(service => ids.Contains(service.HospitalId))
            .Select(service => new { service.HospitalId, service.Name }).ToListAsync(cancellationToken);
        var specialties = await database.HospitalSpecialties.AsNoTracking().Where(link => ids.Contains(link.HospitalId))
            .Select(link => new { link.HospitalId, link.SpecialtyId }).ToListAsync(cancellationToken);

        return rows.Select(row => new AdminHospitalDirectoryCard(row.Id, row.Name, row.NameKm, row.Address, row.Phone, row.Email,
            row.Website, row.OpeningHours, row.FacilityType, row.Ownership, row.NssfStatus, row.OperationalStatus,
            row.ProvinceCode, row.Province, row.ProvinceKm, row.DistrictCode, row.CommuneCode, row.Latitude, row.Longitude,
            row.SourceUrl, row.SourcePublishedOn, row.VerifiedOn, row.IsActive,
            services.Where(service => service.HospitalId == row.Id).Select(service => service.Name).ToArray(),
            specialties.Where(link => link.HospitalId == row.Id).Select(link => link.SpecialtyId).ToArray())).ToArray();
    }

    private async Task<IActionResult?> ValidateHospitalAsync(SaveHospitalDirectoryRequest request, Hospital? existing,
        CancellationToken cancellationToken)
    {
        if (request.Ownership is not ("Public" or "Private" or "Unknown"))
            return BadRequest(new { message = "Ownership must be Public, Private, or Unknown." });
        if (request.FacilityType is not ("NationalHospital" or "ProvincialHospital" or "ReferralHospital" or "SpecialistHospital" or "Clinic" or "MedicalCenter" or "Unknown"))
            return BadRequest(new { message = "Facility type is not supported." });
        if (request.NssfStatus is not ("NSSF" or "NON_NSSF" or "PENDING_VERIFICATION"))
            return BadRequest(new { message = "NSSF status must be explicitly verified or pending verification." });
        if (request.OperationalStatus is not ("ACTIVE" or "INACTIVE" or "PENDING_VERIFICATION"))
            return BadRequest(new { message = "Operational status is not supported." });
        if ((request.Latitude is null) != (request.Longitude is null))
            return BadRequest(new { message = "Latitude and longitude must be provided together." });
        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
            return BadRequest(new { message = "Coordinates are outside valid geographic ranges." });
        if (!string.IsNullOrWhiteSpace(request.SourceUrl) && !IsHttpUrl(request.SourceUrl))
            return BadRequest(new { message = "The source URL must be an absolute HTTP or HTTPS address." });
        if (!string.IsNullOrWhiteSpace(request.Website) && !IsHttpUrl(request.Website))
            return BadRequest(new { message = "The website must be an absolute HTTP or HTTPS address." });
        if (request.VerifiedOn > DateOnly.FromDateTime(DateTime.UtcNow)
            || request.SourcePublishedOn > DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequest(new { message = "Verification and publication dates cannot be in the future." });
        if (request.Services is { Length: > 30 }
            || request.Services?.Any(service => string.IsNullOrWhiteSpace(service) || service.Trim().Length > 120) == true)
            return BadRequest(new { message = "Enter up to 30 service names, each no longer than 120 characters." });
        if (request.Services is not null && request.Services.Select(service => service.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Services.Length)
            return BadRequest(new { message = "Duplicate service names are not allowed." });
        var requiresEvidence = request.NssfStatus is "NSSF" or "NON_NSSF" || request.OperationalStatus == "ACTIVE"
            || request.Latitude is not null || request.SpecialtyIds is { Length: > 0 } || request.Services is { Length: > 0 }
            || (existing is null && (request.Ownership != "Unknown" || request.FacilityType != "Unknown"))
            || (existing is not null && (request.Ownership != existing.Ownership || request.FacilityType != existing.FacilityType));
        if (!DemoMode && requiresEvidence
            && (string.IsNullOrWhiteSpace(request.SourceUrl) || request.VerifiedOn is null || request.VerifiedOn > DateOnly.FromDateTime(DateTime.UtcNow)))
            return BadRequest(new { message = "Verified facility details, services, coordinates, and specialties require a source URL and a non-future verification date." });

        if (!await database.Provinces.AnyAsync(x => x.Code == request.ProvinceCode, cancellationToken))
            return BadRequest(new { message = "Choose an existing province." });
        if (request.DistrictCode is not null && !await database.Districts.AnyAsync(x => x.Code == request.DistrictCode
            && x.ProvinceCode == request.ProvinceCode, cancellationToken))
            return BadRequest(new { message = "The district does not belong to the selected province." });
        if (request.CommuneCode is not null && (request.DistrictCode is null
            || !await database.Communes.AnyAsync(x => x.Code == request.CommuneCode && x.DistrictCode == request.DistrictCode, cancellationToken)))
            return BadRequest(new { message = "The commune does not belong to the selected district." });
        if (request.SpecialtyIds is { Length: > 0 })
        {
            if (request.SpecialtyIds.Distinct().Count() != request.SpecialtyIds.Length)
                return BadRequest(new { message = "Duplicate specialty IDs are not allowed." });
            var count = await database.Specialties.CountAsync(x => request.SpecialtyIds.Contains(x.Id), cancellationToken);
            if (count != request.SpecialtyIds.Length) return BadRequest(new { message = "Choose existing specialties." });
        }
        return null;
    }

    private async Task ApplyHospitalAsync(Hospital hospital, SaveHospitalDirectoryRequest request,
        CancellationToken cancellationToken)
    {
        hospital.Name = request.Name.Trim();
        hospital.NameKm = Clean(request.NameKm);
        hospital.Address = Clean(request.Address);
        hospital.Phone = Clean(request.Phone);
        hospital.Email = Clean(request.Email);
        hospital.Website = Clean(request.Website);
        hospital.OpeningHours = Clean(request.OpeningHours);
        hospital.FacilityType = request.FacilityType;
        hospital.Ownership = request.Ownership;
        hospital.NssfStatus = request.NssfStatus;
        hospital.OperationalStatus = request.OperationalStatus;
        hospital.ProvinceCode = request.ProvinceCode;
        hospital.DistrictCode = request.DistrictCode;
        hospital.CommuneCode = request.CommuneCode;
        hospital.Latitude = request.Latitude;
        hospital.Longitude = request.Longitude;
        hospital.IsActive = request.IsActive;
        hospital.SourceUrl = Clean(request.SourceUrl);
        hospital.SourcePublishedOn = request.SourcePublishedOn;
        hospital.VerifiedOn = request.VerifiedOn;

        var evidence = string.IsNullOrWhiteSpace(request.SourceUrl)
            ? null : await EnsureDataSourceAsync(hospital.Id, "hospital", request.SourceUrl, request.VerifiedOn, cancellationToken);
        hospital.DataSourceKey = evidence?.Key;

        if (request.SpecialtyIds is not null)
        {
            var selectedIds = request.SpecialtyIds.ToHashSet();
            var currentLinks = await database.HospitalSpecialties.Where(x => x.HospitalId == hospital.Id)
                .ToListAsync(cancellationToken);
            database.HospitalSpecialties.RemoveRange(currentLinks.Where(link => !selectedIds.Contains(link.SpecialtyId)));
            foreach (var link in currentLinks.Where(link => selectedIds.Contains(link.SpecialtyId)))
            {
                link.SourceUrl = Clean(request.SourceUrl);
                link.DataSourceKey = evidence?.Key;
                link.VerifiedOn = request.VerifiedOn;
            }
            var currentIds = currentLinks.Select(link => link.SpecialtyId).ToHashSet();
            foreach (var specialtyId in selectedIds.Except(currentIds))
            {
                database.HospitalSpecialties.Add(new HospitalSpecialty
                {
                    Hospital = hospital,
                    SpecialtyId = specialtyId,
                    SourceUrl = Clean(request.SourceUrl),
                    DataSourceKey = evidence?.Key,
                    VerifiedOn = request.VerifiedOn
                });
            }
        }

        if (request.Services is not null)
        {
            var serviceNames = request.Services.Select(service => service.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var existingServices = await database.HospitalServices.Where(service => service.HospitalId == hospital.Id)
                .ToListAsync(cancellationToken);
            database.HospitalServices.RemoveRange(existingServices.Where(service =>
                !serviceNames.Contains(service.Name, StringComparer.OrdinalIgnoreCase)));
            var existingNames = existingServices.Select(service => service.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var serviceName in serviceNames.Where(name => !existingNames.Contains(name)))
                database.HospitalServices.Add(new HospitalService { Hospital = hospital, Name = serviceName });
        }

        if (request.NssfStatus is "NSSF" or "NON_NSSF" or "PENDING_VERIFICATION")
        {
            var checkedOn = request.VerifiedOn ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var verification = await database.NssfVerificationRecords
                .Where(x => x.HospitalId == hospital.Id && x.CheckedOn == checkedOn)
                .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
            if (verification is null)
            {
                verification = new NssfVerificationRecord { Hospital = hospital, CheckedOn = checkedOn };
                database.NssfVerificationRecords.Add(verification);
            }
            verification.Status = request.NssfStatus;
            if (request.NssfStatus == "PENDING_VERIFICATION")
            {
                var nssfSource = await EnsureNssfDirectorySourceAsync(cancellationToken);
                verification.SourceUrl = nssfSource.Url;
                verification.DataSourceKey = nssfSource.Key;
            }
            else
            {
                verification.SourceUrl = Clean(request.SourceUrl)
                    ?? (DemoMode ? "DEMO: not independently verified" : request.SourceUrl!);
                verification.DataSourceKey = evidence?.Key;
            }
            verification.Notes = DemoMode && evidence is null
                ? "Demo entry; not independently verified against an external source."
                : "Updated through administrator directory management.";
        }
    }

    private async Task<DataSource> EnsureDataSourceAsync(Guid ownerId, string kind, string url, DateOnly? verifiedOn,
        CancellationToken cancellationToken)
    {
        var sourceDate = verifiedOn ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url.Trim())))[..12].ToLowerInvariant();
        var key = $"admin-{kind}:{ownerId:N}:{sourceDate:yyyyMMdd}:{hash}";
        var source = await database.DataSources.SingleOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (source is null)
        {
            source = new DataSource { Key = key };
            database.DataSources.Add(source);
        }
        source.Name = kind == "hospital" ? "Administrator supplied facility verification source" : "Administrator supplied directory source";
        source.Url = url.Trim();
        source.PublishedOn = verifiedOn;
        source.RetrievedOn = DateOnly.FromDateTime(DateTime.UtcNow);
        return source;
    }

    private async Task<DataSource> EnsureNssfDirectorySourceAsync(CancellationToken cancellationToken)
    {
        const string key = "nssf-public-site";
        var source = await database.DataSources.SingleOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (source is null)
        {
            source = new DataSource { Key = key };
            database.DataSources.Add(source);
        }
        source.Name = "National Social Security Fund public website (facility-level status not confirmed)";
        source.Url = "https://www.nssf.gov.kh/";
        source.RetrievedOn = DateOnly.FromDateTime(DateTime.UtcNow);
        return source;
    }

    private AuditLog CreateAudit(string action, string entityType, Guid entityId, string summary)
    {
        var actorText = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return new AuditLog
        {
            ActorUserId = Guid.TryParse(actorText, out var actorId) ? actorId : null,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Summary = summary
        };
    }

    private static bool IsHttpUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https";

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record HospitalDirectoryRow(Guid Id, string Name, string? NameKm, string? Address, string? Phone,
        string? Email, string? Website, string? OpeningHours, string FacilityType, string Ownership, string NssfStatus,
        string OperationalStatus, string? ProvinceCode, string? Province, string? ProvinceKm, string? DistrictCode,
        string? CommuneCode, double? Latitude, double? Longitude, string? SourceUrl, DateOnly? SourcePublishedOn,
        DateOnly? VerifiedOn, bool IsActive);
}

public sealed class SaveHospitalDirectoryRequest
{
    [Required, StringLength(200)] public string Name { get; init; } = string.Empty;
    [StringLength(240)] public string? NameKm { get; init; }
    [StringLength(500)] public string? Address { get; init; }
    [Phone, StringLength(32)] public string? Phone { get; init; }
    [EmailAddress, StringLength(256)] public string? Email { get; init; }
    [StringLength(1000)] public string? Website { get; init; }
    [StringLength(240)] public string? OpeningHours { get; init; }
    [Required, StringLength(40)] public string FacilityType { get; init; } = string.Empty;
    [Required, StringLength(20)] public string Ownership { get; init; } = string.Empty;
    [Required, StringLength(32)] public string NssfStatus { get; init; } = string.Empty;
    [Required, StringLength(32)] public string OperationalStatus { get; init; } = string.Empty;
    [Required, StringLength(2)] public string ProvinceCode { get; init; } = string.Empty;
    [StringLength(4)] public string? DistrictCode { get; init; }
    [StringLength(6)] public string? CommuneCode { get; init; }
    [Range(-90, 90)] public double? Latitude { get; init; }
    [Range(-180, 180)] public double? Longitude { get; init; }
    [StringLength(1000)] public string? SourceUrl { get; init; }
    public string[]? Services { get; init; }
    public DateOnly? SourcePublishedOn { get; init; }
    public DateOnly? VerifiedOn { get; init; }
    public bool IsActive { get; init; }
    public Guid[]? SpecialtyIds { get; init; }
}

public sealed class SaveSpecialtyRequest
{
    [Required, StringLength(120)] public string Name { get; init; } = string.Empty;
    [StringLength(160)] public string? NameKm { get; init; }
}

public sealed record AdminHospitalDirectoryCard(Guid Id, string Name, string? NameKm, string? Address, string? Phone,
    string? Email, string? Website, string? OpeningHours, string FacilityType, string Ownership, string NssfStatus, string OperationalStatus,
    string? ProvinceCode, string? Province, string? ProvinceKm, string? DistrictCode, string? CommuneCode, double? Latitude, double? Longitude,
    string? SourceUrl, DateOnly? SourcePublishedOn, DateOnly? VerifiedOn, bool IsActive,
    IReadOnlyList<string> Services, IReadOnlyList<Guid> SpecialtyIds);

public sealed record AdminSpecialtyDirectoryCard(Guid Id, string Name, string? NameKm);
public sealed record AdminDirectoryAuditCard(Guid Id, string Action, string EntityType, Guid EntityId,
    string? Summary, string? ActorName, DateTimeOffset CreatedAt);
