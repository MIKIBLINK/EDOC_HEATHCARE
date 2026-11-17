using System.Security.Claims;
using EDOC.Api.Data;
using EDOC.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDOC.Api.Controllers;

/// <summary>Facility-scoped workspace. Every query is anchored to a hospital assignment.</summary>
[ApiController, Authorize(Roles = "HospitalAdmin")]
[Route("api/hospital-admin")]
public sealed class HospitalAdminController(HealthcareDbContext database) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet("hospitals")]
    public async Task<IActionResult> Hospitals(CancellationToken cancellationToken)
    {
        var items = await AssignedHospitals().OrderBy(x => x.Name)
            .Select(x => new HospitalAdminHospital(x.Id, x.Name, x.NameKm, x.Address, x.Phone, x.Email, x.FacilityType,
                x.NssfStatus, x.Province == null ? null : x.Province.Name, x.IsActive))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("hospitals/{hospitalId:guid}/dashboard")]
    public async Task<IActionResult> Dashboard(Guid hospitalId, CancellationToken cancellationToken)
    {
        if (!await IsAssignedAsync(hospitalId, cancellationToken)) return Forbid();
        var now = DateTimeOffset.UtcNow;
        var dayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var appointments = database.Appointments.Where(x => x.Doctor!.HospitalId == hospitalId);
        var result = new
        {
            HospitalId = hospitalId,
            Doctors = await database.Doctors.CountAsync(x => x.HospitalId == hospitalId && x.User!.IsActive, cancellationToken),
            VerifiedDoctors = await database.Doctors.CountAsync(x => x.HospitalId == hospitalId && x.IsApproved && x.User!.IsActive, cancellationToken),
            Staff = await database.HospitalStaffAssignments.CountAsync(x => x.HospitalId == hospitalId, cancellationToken),
            TodayAppointments = await appointments.CountAsync(x => x.Schedule!.StartsAt >= dayStart && x.Schedule.StartsAt < dayStart.AddDays(1), cancellationToken),
            PendingAppointments = await appointments.CountAsync(x => x.Status == AppointmentStatus.Pending, cancellationToken),
            UpcomingAppointments = await appointments.CountAsync(x => x.Status == AppointmentStatus.Confirmed && x.Schedule!.StartsAt >= now, cancellationToken)
        };
        return Ok(result);
    }

    [HttpGet("hospitals/{hospitalId:guid}/appointments")]
    public async Task<IActionResult> Appointments(Guid hospitalId, [FromQuery] string? status,
        [FromQuery] DateOnly? date, [FromQuery] int page = 1, [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAssignedAsync(hospitalId, cancellationToken)) return Forbid();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.Appointments.AsNoTracking().Where(x => x.Doctor!.HospitalId == hospitalId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AppointmentStatus>(status, true, out var parsed)) query = query.Where(x => x.Status == parsed);
        else if (!string.IsNullOrWhiteSpace(status)) return BadRequest(new { message = "Unsupported appointment status." });
        if (date.HasValue)
        {
            var start = new DateTimeOffset(date.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(x => x.Schedule!.StartsAt >= start && x.Schedule.StartsAt < start.AddDays(1));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Schedule!.StartsAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new HospitalAdminAppointment(x.Id, x.PatientId, x.Patient!.User!.FullName,
                x.DoctorId, x.Doctor!.User!.FullName, x.Doctor.Specialty!.Name,
                x.Schedule!.StartsAt, x.Schedule.EndsAt, x.Status.ToString()))
            .ToListAsync(cancellationToken);
        return Ok(new { items, page, pageSize, total });
    }

    [HttpGet("hospitals/{hospitalId:guid}/doctors")]
    public async Task<IActionResult> Doctors(Guid hospitalId, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken cancellationToken = default)
    {
        if (!await IsAssignedAsync(hospitalId, cancellationToken)) return Forbid();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.Doctors.AsNoTracking().Where(x => x.HospitalId == hospitalId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.User!.FullName.Contains(term) || x.Specialty!.Name.Contains(term));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.User!.FullName).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new HospitalAdminDoctor(x.Id, x.UserId, x.User!.FullName, x.User.PhoneNumber,
                x.Specialty!.Name, x.Qualifications, x.YearsExperience, x.ConsultationFee, x.IsApproved, x.User.IsActive))
            .ToListAsync(cancellationToken);
        return Ok(new { items, page, pageSize, total });
    }

    [HttpGet("hospitals/{hospitalId:guid}/doctors/{doctorId:guid}/schedules")]
    public async Task<IActionResult> Schedules(Guid hospitalId, Guid doctorId, [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to, CancellationToken cancellationToken)
    {
        if (!await IsAssignedAsync(hospitalId, cancellationToken)) return Forbid();
        if (!await database.Doctors.AnyAsync(x => x.Id == doctorId && x.HospitalId == hospitalId, cancellationToken)) return NotFound();
        var start = from ?? DateTimeOffset.UtcNow;
        var end = to ?? start.AddDays(30);
        if (end <= start || end > start.AddDays(90)) return BadRequest(new { message = "Choose a date range of up to 90 days." });
        var items = await database.DoctorSchedules.AsNoTracking().Where(x => x.DoctorId == doctorId && x.StartsAt >= start && x.StartsAt < end)
            .OrderBy(x => x.StartsAt).Select(x => new HospitalAdminSchedule(x.Id, x.StartsAt, x.EndsAt, x.IsAvailable))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpPost("hospitals/{hospitalId:guid}/doctors/{doctorId:guid}/schedules")]
    public async Task<IActionResult> CreateSchedule(Guid hospitalId, Guid doctorId,
        [FromBody] HospitalAdminCreateScheduleRequest request, CancellationToken cancellationToken)
    {
        if (!await IsAssignedAsync(hospitalId, cancellationToken)) return Forbid();
        if (request.EndsAt <= request.StartsAt || request.EndsAt - request.StartsAt > TimeSpan.FromHours(8)
            || request.StartsAt <= DateTimeOffset.UtcNow)
            return BadRequest(new { message = "Schedule times must be in the future and last no more than eight hours." });
        var doctor = await database.Doctors.SingleOrDefaultAsync(x => x.Id == doctorId && x.HospitalId == hospitalId
            && x.IsApproved && x.User!.IsActive, cancellationToken);
        if (doctor is null) return NotFound(new { message = "An active, verified hospital doctor was not found." });
        await using var transaction = await database.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var overlaps = await database.DoctorSchedules.AnyAsync(x => x.DoctorId == doctorId
            && x.StartsAt < request.EndsAt && request.StartsAt < x.EndsAt, cancellationToken);
        if (overlaps) return Conflict(new { message = "The schedule overlaps an existing time." });
        var schedule = new DoctorSchedule { DoctorId = doctorId, StartsAt = request.StartsAt, EndsAt = request.EndsAt };
        database.DoctorSchedules.Add(schedule);
        database.AuditLogs.Add(new AuditLog { ActorUserId = UserId, Action = "hospital_admin.schedule.create", EntityType = "DoctorSchedule", EntityId = schedule.Id, Summary = $"Schedule created for doctor {doctorId} at hospital {hospitalId}." });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CreatedAtAction(nameof(Schedules), new { hospitalId, doctorId }, new { schedule.Id, schedule.StartsAt, schedule.EndsAt, schedule.IsAvailable });
    }

    [HttpPut("hospitals/{hospitalId:guid}/doctors/{doctorId:guid}/schedules/{scheduleId:guid}/availability")]
    public async Task<IActionResult> SetScheduleAvailability(Guid hospitalId, Guid doctorId, Guid scheduleId,
        [FromBody] HospitalAdminScheduleAvailabilityRequest request, CancellationToken cancellationToken)
    {
        if (!await IsAssignedAsync(hospitalId, cancellationToken)) return Forbid();
        var schedule = await database.DoctorSchedules.SingleOrDefaultAsync(x => x.Id == scheduleId && x.DoctorId == doctorId
            && x.Doctor!.HospitalId == hospitalId, cancellationToken);
        if (schedule is null) return NotFound();
        if (request.Available && await database.Appointments.AnyAsync(x => x.ScheduleId == scheduleId
            && x.Status != AppointmentStatus.Cancelled, cancellationToken))
            return Conflict(new { message = "This schedule has an active appointment and cannot be reopened." });
        schedule.IsAvailable = request.Available;
        database.AuditLogs.Add(new AuditLog { ActorUserId = UserId, Action = "hospital_admin.schedule.availability", EntityType = "DoctorSchedule", EntityId = scheduleId, Summary = $"Schedule for doctor {doctorId} set available={request.Available}." });
        await database.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("hospitals/{hospitalId:guid}/appointments/{appointmentId:guid}/status")]
    public async Task<IActionResult> UpdateAppointmentStatus(Guid hospitalId, Guid appointmentId,
        [FromBody] HospitalAdminAppointmentStatusRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Status)) return BadRequest(new { message = "Unsupported appointment status." });
        var appointment = await database.Appointments.Include(x => x.Schedule)
            .SingleOrDefaultAsync(x => x.Id == appointmentId && x.Doctor!.HospitalId == hospitalId, cancellationToken);
        if (appointment is null) return NotFound();
        if (!await IsAssignedAsync(hospitalId, cancellationToken)) return Forbid();
        var validTransition = (appointment.Status, request.Status) switch
        {
            (AppointmentStatus.Pending, AppointmentStatus.Confirmed or AppointmentStatus.Cancelled) => true,
            (AppointmentStatus.Confirmed, AppointmentStatus.Completed or AppointmentStatus.Cancelled) => true,
            _ => false
        };
        if (!validTransition) return Conflict(new { message = "This appointment status transition is not allowed." });
        appointment.Status = request.Status;
        if (request.Status == AppointmentStatus.Cancelled && appointment.Schedule is not null)
            appointment.Schedule.IsAvailable = true;
        database.AuditLogs.Add(new AuditLog
        {
            ActorUserId = UserId,
            Action = "hospital_admin.appointment.status",
            EntityType = "Appointment",
            EntityId = appointment.Id,
            Summary = $"Hospital {hospitalId}; status changed to {request.Status}."
        });
        await database.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private IQueryable<Hospital> AssignedHospitals() => database.Hospitals.AsNoTracking()
        .Where(hospital => database.HospitalStaffAssignments.Any(assignment => assignment.UserId == UserId && assignment.HospitalId == hospital.Id));

    private Task<bool> IsAssignedAsync(Guid hospitalId, CancellationToken cancellationToken) =>
        database.HospitalStaffAssignments.AnyAsync(x => x.UserId == UserId && x.HospitalId == hospitalId, cancellationToken);
}

public sealed record HospitalAdminHospital(Guid Id, string Name, string? NameKm, string? Address, string? Phone,
    string? Email, string FacilityType, string NssfStatus, string? Province, bool IsActive);
public sealed record HospitalAdminAppointment(Guid Id, Guid PatientId, string PatientName, Guid DoctorId,
    string DoctorName, string Specialty, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Status);
public sealed record HospitalAdminDoctor(Guid Id, Guid UserId, string FullName, string? PhoneNumber, string Specialty,
    string? Qualifications, int YearsExperience, decimal? ConsultationFee, bool IsApproved, bool IsActive);
public sealed record HospitalAdminAppointmentStatusRequest(AppointmentStatus Status);
public sealed record HospitalAdminSchedule(Guid Id, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsAvailable);
public sealed record HospitalAdminCreateScheduleRequest(DateTimeOffset StartsAt, DateTimeOffset EndsAt);
public sealed record HospitalAdminScheduleAvailabilityRequest(bool Available);
