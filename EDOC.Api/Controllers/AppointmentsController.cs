using System.Security.Claims;
using EDOC.Api.Data;
using EDOC.Api.DTOs;
using EDOC.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDOC.Api.Controllers;

[ApiController, Authorize]
[Route("api/appointments")]
public sealed class AppointmentsController(HealthcareDbContext database) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null, [FromQuery] string? search = null, [FromQuery] Guid? hospitalId = null, [FromQuery] Guid? doctorId = null,
        [FromQuery] DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        if (User.IsInRole("Doctor") && !await database.Doctors.AnyAsync(x => x.UserId == UserId && x.IsApproved && x.User!.IsActive, cancellationToken)) return Forbid();
        var query = database.Appointments.AsNoTracking().AsQueryable();
        if (User.IsInRole("Administrator")) { }
        else if (User.IsInRole("Patient") && User.IsInRole("Doctor")) query = query.Where(x => x.Patient!.UserId == UserId || x.Doctor!.UserId == UserId);
        else if (User.IsInRole("HospitalStaff"))
            query = query.Where(x => x.Doctor!.HospitalId.HasValue && database.HospitalStaffAssignments.Any(a => a.UserId == UserId && a.HospitalId == x.Doctor.HospitalId));
        else if (User.IsInRole("Patient")) query = query.Where(x => x.Patient!.UserId == UserId);
        else if (User.IsInRole("Doctor")) query = query.Where(x => x.Doctor!.UserId == UserId);
        else return Forbid();
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<AppointmentStatus>(status, true, out var parsedStatus)) return BadRequest(new { message = "Unsupported appointment status." });
            query = query.Where(x => x.Status == parsedStatus);
        }
        if (hospitalId.HasValue) query = query.Where(x => x.Doctor!.HospitalId == hospitalId.Value);
        if (doctorId.HasValue) query = query.Where(x => x.DoctorId == doctorId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var isAppointmentId = Guid.TryParse(term, out var appointmentId);
            query = query.Where(x => x.Patient!.User!.FullName.Contains(term)
                || x.Doctor!.User!.FullName.Contains(term)
                || (isAppointmentId && x.Id == appointmentId));
        }
        if (date.HasValue)
        {
            var startsAt = new DateTimeOffset(date.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(x => x.Schedule!.StartsAt >= startsAt && x.Schedule.StartsAt < startsAt.AddDays(1));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.Schedule!.StartsAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AppointmentSummary(x.Id, x.PatientId, x.DoctorId, x.Doctor!.User!.FullName,
                x.Patient!.User!.FullName, x.Schedule!.StartsAt, x.Schedule.EndsAt, x.Status.ToString(), x.IsTelehealth,
                User.IsInRole("Administrator") || User.IsInRole("SuperAdmin") ? null : x.Reason)).ToListAsync(cancellationToken);
        return Ok(new { items, page, pageSize, total });
    }

    [HttpPost, Authorize(Roles = "Patient")]
    public async Task<IActionResult> Book(AppointmentRequest request, CancellationToken cancellationToken)
    {
        var patient = await database.Patients.SingleOrDefaultAsync(x => x.UserId == UserId, cancellationToken);
        var schedule = await database.DoctorSchedules.SingleOrDefaultAsync(x => x.Id == request.ScheduleId && x.DoctorId == request.DoctorId
            && x.Doctor!.IsApproved && x.Doctor.User!.IsActive && x.IsAvailable, cancellationToken);
        if (patient is null || schedule is null || schedule.StartsAt <= DateTimeOffset.UtcNow || schedule.EndsAt <= schedule.StartsAt) return BadRequest(new { message = "The selected time is not available." });
        var booking = new Appointment { PatientId = patient.Id, DoctorId = request.DoctorId, ScheduleId = schedule.Id, Reason = request.Reason?.Trim(), IsTelehealth = request.IsTelehealth };
        schedule.IsAvailable = false;
        database.Appointments.Add(booking);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return Conflict(new { message = "This appointment time was just booked. Choose another time." }); }
        return CreatedAtAction(nameof(List), new { id = booking.Id }, new { booking.Id, status = booking.Status.ToString() });
    }

    [HttpPut("{id:guid}"), Authorize(Roles = "Patient")]
    public async Task<IActionResult> Reschedule(Guid id, AppointmentRequest request, CancellationToken cancellationToken)
    {
        var appointment = await database.Appointments.Include(x => x.Patient).Include(x => x.Schedule)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (appointment is null) return NotFound();
        if (appointment.Patient?.UserId != UserId) return Forbid();
        if (appointment.Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled) return Conflict(new { message = "This appointment cannot be rescheduled." });
        var replacement = await database.DoctorSchedules.SingleOrDefaultAsync(x => x.Id == request.ScheduleId && x.DoctorId == appointment.DoctorId && x.IsAvailable && x.StartsAt > DateTimeOffset.UtcNow, cancellationToken);
        if (replacement is null || request.DoctorId != appointment.DoctorId) return BadRequest(new { message = "Choose an available time for the same doctor." });
        if (appointment.Schedule is not null) appointment.Schedule.IsAvailable = true;
        replacement.IsAvailable = false;
        appointment.ScheduleId = replacement.Id;
        appointment.Schedule = replacement;
        appointment.Reason = request.Reason?.Trim();
        appointment.IsTelehealth = request.IsTelehealth;
        appointment.Status = AppointmentStatus.Pending;
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return Conflict(new { message = "This appointment time was just booked. Choose another time." }); }
        return NoContent();
    }

    [HttpDelete("{id:guid}"), Authorize(Roles = "Patient,Doctor,HospitalStaff,Administrator")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var appointment = await database.Appointments.Include(x => x.Patient).Include(x => x.Schedule).Include(x => x.Doctor).ThenInclude(x => x!.User)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (appointment is null) return NotFound();
        var isPatient = appointment.Patient?.UserId == UserId;
        var isAssignedDoctor = appointment.Doctor?.UserId == UserId && User.IsInRole("Doctor");
        var isAssignedStaff = User.IsInRole("HospitalStaff") && appointment.Doctor?.HospitalId is Guid hospitalId && await database.HospitalStaffAssignments.AnyAsync(x => x.UserId == UserId && x.HospitalId == hospitalId, cancellationToken);
        if (!isPatient && !isAssignedDoctor && !isAssignedStaff && !User.IsInRole("Administrator")) return Forbid();
        if (appointment.Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled) return Conflict(new { message = "This appointment can no longer be cancelled." });
        appointment.Status = AppointmentStatus.Cancelled;
        if (appointment.Schedule is not null) appointment.Schedule.IsAvailable = true;
        await database.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/status"), Authorize(Roles = "Doctor,HospitalStaff,Administrator")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateAppointmentStatusRequest request, CancellationToken cancellationToken)
    {
        var appointment = await database.Appointments.Include(x => x.Doctor).ThenInclude(x => x!.User)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (appointment is null) return NotFound();
        var assignedDoctor = User.IsInRole("Doctor") && appointment.Doctor?.UserId == UserId;
        if (assignedDoctor && appointment.Doctor?.IsApproved != true) return Forbid();
        var assignedStaff = User.IsInRole("HospitalStaff") && appointment.Doctor?.HospitalId is Guid hospitalId && await database.HospitalStaffAssignments.AnyAsync(x => x.UserId == UserId && x.HospitalId == hospitalId, cancellationToken);
        if (!assignedDoctor && !assignedStaff && !User.IsInRole("Administrator")) return Forbid();
        var validTransition = (appointment.Status, request.Status) switch
        {
            (AppointmentStatus.Pending, AppointmentStatus.Confirmed or AppointmentStatus.Cancelled) => true,
            (AppointmentStatus.Confirmed, AppointmentStatus.Completed or AppointmentStatus.Cancelled) => true,
            _ => false
        };
        if (!validTransition) return Conflict(new { message = "This appointment status transition is not allowed." });
        appointment.Status = request.Status;
        if (request.Status == AppointmentStatus.Cancelled)
        {
            var schedule = await database.DoctorSchedules.SingleOrDefaultAsync(x => x.Id == appointment.ScheduleId, cancellationToken);
            if (schedule is not null) schedule.IsAvailable = true;
        }
        await database.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

public sealed record UpdateAppointmentStatusRequest(AppointmentStatus Status);
