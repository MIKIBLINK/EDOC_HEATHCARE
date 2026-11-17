using EDOC.Api.Data;
using EDOC.Api.DTOs;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDOC.Api.Controllers;

[ApiController]
[Route("api/doctors")]
public sealed class DoctorsController(HealthcareDbContext database) : ControllerBase
{
    [Authorize(Roles = "Doctor")]
    [HttpGet("me")]
    public async Task<ActionResult<DoctorWorkspaceProfile>> Mine(CancellationToken cancellationToken)
    {
        var idText = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(idText, out var userId)) return Unauthorized();
        var doctor = await database.Doctors.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => new DoctorWorkspaceProfile(x.Id, x.User!.FullName, x.Qualifications, x.YearsExperience,
                x.Specialty!.Name, x.Hospital == null ? null : x.Hospital.Name, x.ConsultationFee, x.IsApproved))
            .SingleOrDefaultAsync(cancellationToken);
        return doctor is null ? NotFound() : Ok(doctor);
    }

    [HttpGet]
    public async Task<ActionResult<object>> List([FromQuery] string? search, [FromQuery] Guid? specialtyId,
        [FromQuery] Guid? hospitalId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.Doctors.AsNoTracking().Where(x => x.User!.IsActive && x.IsApproved);
        if (specialtyId.HasValue) query = query.Where(x => x.SpecialtyId == specialtyId.Value);
        if (hospitalId.HasValue) query = query.Where(x => x.HospitalId == hospitalId.Value);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.User!.FullName.Contains(search) || x.Specialty!.Name.Contains(search));
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.User!.FullName).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new DoctorSummary(x.Id, x.User!.FullName, x.Qualifications, x.YearsExperience, x.Specialty!.Name, x.Hospital!.Name, x.ConsultationFee))
            .ToListAsync(cancellationToken);
        return Ok(new { items, page, pageSize, total });
    }

    [HttpGet("specialties")]
    public async Task<IActionResult> Specialties(CancellationToken cancellationToken) =>
        Ok(await database.Specialties.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.NameKm }).ToListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DoctorSummary>> Get(Guid id, CancellationToken cancellationToken)
    {
        var doctor = await database.Doctors.AsNoTracking().Where(x => x.Id == id && x.User!.IsActive && x.IsApproved)
            .Select(x => new DoctorSummary(x.Id, x.User!.FullName, x.Qualifications, x.YearsExperience, x.Specialty!.Name, x.Hospital!.Name, x.ConsultationFee))
            .SingleOrDefaultAsync(cancellationToken);
        return doctor is null ? NotFound() : Ok(doctor);
    }
}
