using System.Security.Claims;
using EDOC.Api.Data;
using EDOC.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDOC.Api.Controllers;

[ApiController]
public sealed class DoctorSchedulesController(HealthcareDbContext database) : ControllerBase
{
    [Authorize(Roles = "Doctor")]
    [HttpGet("api/doctors/me/schedules")]
    public async Task<IActionResult> Mine([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken cancellationToken)
    {
        var start = from ?? DateTimeOffset.UtcNow;
        var end = to ?? start.AddDays(30);
        if (end <= start || end > start.AddDays(90)) return BadRequest(new { message = "Choose a date range of up to 90 days." });
        var idText = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(idText, out var userId)) return Unauthorized();
        var doctorId = await database.Doctors.AsNoTracking().Where(x => x.UserId == userId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
        if (!doctorId.HasValue) return NotFound();
        var schedules = await database.DoctorSchedules.AsNoTracking()
            .Where(x => x.DoctorId == doctorId.Value && x.StartsAt >= start && x.StartsAt < end)
            .OrderBy(x => x.StartsAt)
            .Select(x => new DoctorScheduleSummary(x.Id, x.StartsAt, x.EndsAt, x.IsAvailable))
            .ToListAsync(cancellationToken);
        return Ok(schedules);
    }

    [HttpGet("api/doctors/{doctorId:guid}/schedules")]
    public async Task<IActionResult> Available(Guid doctorId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken cancellationToken)
    {
        var start = from ?? DateTimeOffset.UtcNow;
        var end = to ?? start.AddDays(30);
        if (end <= start || end > start.AddDays(90)) return BadRequest(new { message = "Choose a date range of up to 90 days." });
        var slots = await database.DoctorSchedules.AsNoTracking().Where(x => x.DoctorId == doctorId && x.Doctor!.IsApproved && x.Doctor.User!.IsActive
                && x.IsAvailable && x.StartsAt >= start && x.StartsAt < end && x.StartsAt > DateTimeOffset.UtcNow)
            .OrderBy(x => x.StartsAt).Select(x => new { x.Id, x.StartsAt, x.EndsAt }).ToListAsync(cancellationToken);
        return Ok(slots);
    }

    [HttpPost("api/doctors/me/schedules"), Authorize(Roles = "Doctor")]
    public async Task<IActionResult> Create(CreateScheduleRequest request, CancellationToken cancellationToken)
    {
        if (request.EndsAt <= request.StartsAt || request.EndsAt - request.StartsAt > TimeSpan.FromHours(8) || request.StartsAt <= DateTimeOffset.UtcNow)
            return BadRequest(new { message = "Schedule times must be in the future and last no more than eight hours." });
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
        var doctor = await database.Doctors.SingleOrDefaultAsync(x => x.UserId == userId && x.IsApproved && x.User!.IsActive, cancellationToken);
        if (doctor is null) return Forbid();

        await using var transaction = await database.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var overlaps = await database.DoctorSchedules.AnyAsync(x => x.DoctorId == doctor.Id && x.StartsAt < request.EndsAt && request.StartsAt < x.EndsAt, cancellationToken);
        if (overlaps) return Conflict(new { message = "The schedule overlaps an existing time." });
        var schedule = new DoctorSchedule { DoctorId = doctor.Id, StartsAt = request.StartsAt, EndsAt = request.EndsAt };
        database.DoctorSchedules.Add(schedule);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CreatedAtAction(nameof(Available), new { doctorId = doctor.Id }, new { schedule.Id, schedule.StartsAt, schedule.EndsAt });
    }
}

public sealed record CreateScheduleRequest(DateTimeOffset StartsAt, DateTimeOffset EndsAt);
public sealed record DoctorScheduleSummary(Guid Id, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsAvailable);
