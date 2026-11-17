using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using EDOC.Api.Data;
using EDOC.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDOC.Api.Controllers;

[ApiController, Authorize]
[Route("api/health")]
public sealed class HealthController(HealthcareDbContext database) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet("measurements/me"), Authorize(Roles = "Patient")]
    public async Task<IActionResult> MyMeasurements([FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var patient = await database.Patients.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == UserId, cancellationToken);
        return patient is null ? NotFound() : await MeasurementsForPatientAsync(patient.Id, "patient_health_measurements_read", page, pageSize, cancellationToken);
    }

    [HttpGet("patients/{patientId:guid}/measurements"), Authorize(Roles = "Doctor")]
    public async Task<IActionResult> PatientMeasurements(Guid patientId, [FromQuery] int page = 1, [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        var doctor = await database.Doctors.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == UserId && x.IsApproved && x.User!.IsActive, cancellationToken);
        if (doctor is null || !await database.Appointments.AnyAsync(x => x.PatientId == patientId && x.DoctorId == doctor.Id
                && (x.Status == AppointmentStatus.Confirmed || x.Status == AppointmentStatus.Completed), cancellationToken)) return Forbid();
        return await MeasurementsForPatientAsync(patientId, "doctor_health_measurements_read", page, pageSize, cancellationToken);
    }

    [HttpPost("measurements"), Authorize(Roles = "Patient")]
    public async Task<IActionResult> AddMeasurement([FromBody] AddHealthMeasurementRequest request, CancellationToken cancellationToken)
    {
        if (!IsValidMeasurement(request, out var error)) return BadRequest(new { message = error });
        var patient = await database.Patients.SingleOrDefaultAsync(x => x.UserId == UserId, cancellationToken);
        if (patient is null) return NotFound();
        var measurement = new HealthMeasurement
        {
            PatientId = patient.Id, MetricType = request.MetricType, Value = request.Value,
            SecondaryValue = request.SecondaryValue, Unit = request.Unit.Trim(),
            MeasuredAt = request.MeasuredAt, Notes = Clean(request.Notes)
        };
        database.HealthMeasurements.Add(measurement);
        AddAudit(patient.Id, "health_measurement_created");
        await database.SaveChangesAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new { measurement.Id, measurement.MetricType, measurement.Value,
            measurement.SecondaryValue, measurement.Unit, measurement.MeasuredAt });
    }

    [HttpGet("reminders/me"), Authorize(Roles = "Patient")]
    public async Task<IActionResult> MyReminders([FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var patient = await database.Patients.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == UserId, cancellationToken);
        return patient is null ? NotFound() : await RemindersForPatientAsync(patient.Id, page, pageSize, cancellationToken);
    }

    [HttpGet("caregiver-links/{linkId:guid}/reminders"), Authorize(Roles = "Caregiver")]
    public async Task<IActionResult> CaregiverReminders(Guid linkId, [FromQuery] int page = 1, [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        var link = await database.CaregiverLinks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == linkId
            && x.CaregiverUserId == UserId && x.AcceptedAt != null && x.RevokedAt == null && x.CanViewMedicationReminders, cancellationToken);
        if (link is null) return NotFound();
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.MedicationReminders.AsNoTracking().Where(x => x.PatientId == link.PatientId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.ScheduledAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new MedicationReminderView(x.Id, x.MedicationName, x.Dosage, x.Instructions, x.ScheduledAt,
                x.RepeatIntervalDays, x.Status, x.LastDoseStatus, x.ActionedAt)).ToListAsync(cancellationToken);
        AddAudit(link.PatientId, "caregiver_medication_reminders_read");
        await database.SaveChangesAsync(cancellationToken);
        return Ok(new { items, page, pageSize, total });
    }

    [HttpPost("reminders"), Authorize(Roles = "Patient")]
    public async Task<IActionResult> AddReminder([FromBody] AddMedicationReminderRequest request, CancellationToken cancellationToken)
    {
        if (request.ScheduledAt < DateTimeOffset.UtcNow.AddMinutes(-5)) return BadRequest(new { message = "Reminder time must be in the future." });
        var patient = await database.Patients.SingleOrDefaultAsync(x => x.UserId == UserId, cancellationToken);
        if (patient is null) return NotFound();
        var reminder = new MedicationReminder
        {
            PatientId = patient.Id, MedicationName = request.MedicationName.Trim(), Dosage = request.Dosage.Trim(),
            Instructions = Clean(request.Instructions), ScheduledAt = request.ScheduledAt, RepeatIntervalDays = request.RepeatIntervalDays
        };
        database.MedicationReminders.Add(reminder);
        AddAudit(patient.Id, "medication_reminder_created");
        await database.SaveChangesAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new MedicationReminderView(reminder.Id, reminder.MedicationName,
            reminder.Dosage, reminder.Instructions, reminder.ScheduledAt, reminder.RepeatIntervalDays,
            reminder.Status, reminder.LastDoseStatus, reminder.ActionedAt));
    }

    [HttpPut("reminders/{id:guid}/status"), Authorize(Roles = "Patient")]
    public async Task<IActionResult> UpdateReminderStatus(Guid id, [FromBody] UpdateReminderStatusRequest request, CancellationToken cancellationToken)
    {
        if (request.Status is not ("Taken" or "Missed" or "Cancelled")) return BadRequest(new { message = "Choose Taken, Missed, or Cancelled." });
        var patient = await database.Patients.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == UserId, cancellationToken);
        if (patient is null) return NotFound();
        var reminder = await database.MedicationReminders.SingleOrDefaultAsync(x => x.Id == id && x.PatientId == patient.Id, cancellationToken);
        if (reminder is null) return NotFound();
        if (reminder.Status != "Scheduled") return Conflict(new { message = "This reminder has already been actioned." });
        if ((request.Status is "Taken" or "Missed") && reminder.ScheduledAt > DateTimeOffset.UtcNow.AddMinutes(5))
            return BadRequest(new { message = "A future reminder cannot be marked as taken or missed." });
        var now = DateTimeOffset.UtcNow;
        reminder.ActionedAt = now;
        if (request.Status is "Taken" or "Missed") reminder.LastDoseStatus = request.Status;
        if ((request.Status is "Taken" or "Missed") && reminder.RepeatIntervalDays is > 0)
        {
            do { reminder.ScheduledAt = reminder.ScheduledAt.AddDays(reminder.RepeatIntervalDays.Value); }
            while (reminder.ScheduledAt <= now);
            reminder.Status = "Scheduled";
        }
        else reminder.Status = request.Status;
        AddAudit(patient.Id, $"medication_reminder_{request.Status.ToLowerInvariant()}");
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "This reminder was updated by another request. Refresh and try again." }); }
        return Ok(new MedicationReminderView(reminder.Id, reminder.MedicationName, reminder.Dosage,
            reminder.Instructions, reminder.ScheduledAt, reminder.RepeatIntervalDays, reminder.Status,
            reminder.LastDoseStatus, reminder.ActionedAt));
    }

    private async Task<IActionResult> MeasurementsForPatientAsync(Guid patientId, string auditEvent, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        if (!await database.Patients.AnyAsync(x => x.Id == patientId, cancellationToken)) return NotFound();
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.HealthMeasurements.AsNoTracking().Where(x => x.PatientId == patientId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.MeasuredAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new HealthMeasurementView(x.Id, x.MetricType, x.Value, x.SecondaryValue, x.Unit, x.MeasuredAt, x.Notes)).ToListAsync(cancellationToken);
        AddAudit(patientId, auditEvent);
        await database.SaveChangesAsync(cancellationToken);
        return Ok(new { items, page, pageSize, total });
    }

    private async Task<IActionResult> RemindersForPatientAsync(Guid patientId, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.MedicationReminders.AsNoTracking().Where(x => x.PatientId == patientId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.ScheduledAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new MedicationReminderView(x.Id, x.MedicationName, x.Dosage, x.Instructions, x.ScheduledAt,
                x.RepeatIntervalDays, x.Status, x.LastDoseStatus, x.ActionedAt)).ToListAsync(cancellationToken);
        AddAudit(patientId, "patient_medication_reminders_read");
        await database.SaveChangesAsync(cancellationToken);
        return Ok(new { items, page, pageSize, total });
    }

    private void AddAudit(Guid patientId, string eventType) => database.AccountSecurityLogs.Add(new AccountSecurityLog
    {
        UserId = UserId, ResourceId = patientId, EventType = eventType, Succeeded = true
    });

    private static bool IsValidMeasurement(AddHealthMeasurementRequest request, out string error)
    {
        error = "Metric, value, and unit must match a supported measurement.";
        if (request.MeasuredAt > DateTimeOffset.UtcNow.AddMinutes(5) || request.Value <= 0 || string.IsNullOrWhiteSpace(request.Unit)) return false;
        return request.MetricType switch
        {
            "BloodPressure" => request.Value <= 400 && request.SecondaryValue is > 0 and <= 300
                && request.SecondaryValue.HasValue && request.Value > request.SecondaryValue.Value && request.Unit == "mmHg",
            "BloodGlucose" => request.SecondaryValue is null && (request.Unit == "mg/dL" && request.Value <= 3000
                || request.Unit == "mmol/L" && request.Value <= 166.5m),
            "Weight" => request.SecondaryValue is null && (request.Unit == "kg" && request.Value <= 1000
                || request.Unit == "lb" && request.Value <= 2200),
            "Height" => request.SecondaryValue is null && (request.Unit == "cm" && request.Value is >= 20 and <= 300
                || request.Unit == "in" && request.Value is >= 8 and <= 120),
            _ => false
        };
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class AddHealthMeasurementRequest
{
    [Required, StringLength(24)] public string MetricType { get; init; } = string.Empty;
    [Range(typeof(decimal), "0.01", "99999")] public decimal Value { get; init; }
    [Range(typeof(decimal), "0.01", "99999")] public decimal? SecondaryValue { get; init; }
    [Required, StringLength(24)] public string Unit { get; init; } = string.Empty;
    public DateTimeOffset MeasuredAt { get; init; } = DateTimeOffset.UtcNow;
    [StringLength(1000)] public string? Notes { get; init; }
}

public sealed class AddMedicationReminderRequest
{
    [Required, StringLength(200, MinimumLength = 2)] public string MedicationName { get; init; } = string.Empty;
    [Required, StringLength(120, MinimumLength = 1)] public string Dosage { get; init; } = string.Empty;
    [StringLength(1000)] public string? Instructions { get; init; }
    public DateTimeOffset ScheduledAt { get; init; }
    [Range(1, 365)] public int? RepeatIntervalDays { get; init; }
}

public sealed class UpdateReminderStatusRequest
{
    [Required, StringLength(16)] public string Status { get; init; } = string.Empty;
}

public sealed record HealthMeasurementView(Guid Id, string MetricType, decimal Value, decimal? SecondaryValue,
    string Unit, DateTimeOffset MeasuredAt, string? Notes);
public sealed record MedicationReminderView(Guid Id, string MedicationName, string Dosage, string? Instructions,
    DateTimeOffset ScheduledAt, int? RepeatIntervalDays, string Status, string? LastDoseStatus, DateTimeOffset? ActionedAt);
