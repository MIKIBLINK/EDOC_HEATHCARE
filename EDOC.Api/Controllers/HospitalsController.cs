using EDOC.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDOC.Api.Controllers;

[ApiController]
[Route("api/hospitals")]
public sealed class HospitalsController(HealthcareDbContext database) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] string? provinceCode,
        [FromQuery] string? nssfStatus, [FromQuery] string? facilityType, [FromQuery] string? ownership,
        [FromQuery] string? operationalStatus, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.Hospitals.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(provinceCode)) query = query.Where(x => x.ProvinceCode == provinceCode.Trim());
        if (!string.IsNullOrWhiteSpace(nssfStatus))
        {
            var status = nssfStatus.Trim().ToUpperInvariant();
            if (status is not ("NSSF" or "NON_NSSF" or "PENDING_VERIFICATION" or "INACTIVE"))
                return BadRequest(new { error = "Unsupported NSSF status." });
            query = status == "INACTIVE" ? query.Where(x => x.OperationalStatus == "INACTIVE")
                : query.Where(x => x.IsActive && x.NssfStatus == status);
        }
        if (!string.IsNullOrWhiteSpace(facilityType)) query = query.Where(x => x.FacilityType == facilityType.Trim());
        if (!string.IsNullOrWhiteSpace(ownership))
        {
            var owner = ownership.Trim();
            if (owner is not ("Public" or "Private" or "Unknown")) return BadRequest(new { error = "Unsupported ownership value." });
            query = query.Where(x => x.Ownership == owner);
        }
        if (!string.IsNullOrWhiteSpace(operationalStatus))
        {
            var state = operationalStatus.Trim().ToUpperInvariant();
            if (state is not ("ACTIVE" or "INACTIVE" or "PENDING_VERIFICATION")) return BadRequest(new { error = "Unsupported operational status." });
            query = query.Where(x => x.OperationalStatus == state);
        }
        if (string.IsNullOrWhiteSpace(nssfStatus) && !string.Equals(operationalStatus, "INACTIVE", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Name.Contains(term) || (x.NameKm != null && x.NameKm.Contains(term))
                || (x.Address != null && x.Address.Contains(term)) || (x.Province != null && (x.Province.Name.Contains(term) || x.Province.NameKm.Contains(term))));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.Id, x.Name, x.NameKm, x.Address, x.Phone, x.Email, x.OpeningHours, x.Website,
                x.FacilityType, x.Ownership, x.NssfStatus, x.OperationalStatus, x.ProvinceCode,
                Province = x.Province == null ? null : x.Province.Name,
                ProvinceKm = x.Province == null ? null : x.Province.NameKm,
                District = x.District == null ? null : x.District.Name,
                Commune = x.Commune == null ? null : x.Commune.Name,
                x.Latitude, x.Longitude, x.SourceUrl, x.SourcePublishedOn, x.VerifiedOn,
                x.IsActive,
                Services = x.Services.Select(s => s.Name),
                Specialties = x.Specialties.Select(s => s.Specialty!.Name)
            }).ToListAsync(cancellationToken);
        return Ok(new { items, page, pageSize, total });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var hospital = await database.Hospitals.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id, x.Name, x.NameKm, x.Address, x.Phone, x.Email, x.OpeningHours, x.Website,
                x.FacilityType, x.Ownership, x.NssfStatus, x.OperationalStatus, x.ProvinceCode,
                Province = x.Province == null ? null : x.Province.Name,
                ProvinceKm = x.Province == null ? null : x.Province.NameKm,
                District = x.District == null ? null : x.District.Name,
                Commune = x.Commune == null ? null : x.Commune.Name,
                x.Latitude, x.Longitude, x.SourceUrl, x.SourcePublishedOn, x.VerifiedOn, x.IsActive,
                Services = x.Services.Select(s => new { s.Id, s.Name, s.Description }),
                Specialties = x.Specialties.Select(s => new { s.Specialty!.Id, s.Specialty.Name, s.Specialty.NameKm })
            })
            .SingleOrDefaultAsync(cancellationToken);
        return hospital is null ? NotFound() : Ok(hospital);
    }

    [HttpGet("provinces")]
    public async Task<IActionResult> Provinces(CancellationToken cancellationToken) =>
        Ok(await database.Provinces.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new { x.Code, x.Name, x.NameKm }).ToListAsync(cancellationToken));

    [HttpGet("specialties")]
    public async Task<IActionResult> Specialties(CancellationToken cancellationToken) =>
        Ok(await database.Specialties.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.NameKm }).ToListAsync(cancellationToken));
}
