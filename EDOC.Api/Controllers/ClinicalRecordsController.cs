using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using EDOC.Api.Data;
using EDOC.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDOC.Api.Controllers;

[ApiController, Authorize]
[Route("api/clinical-records")]
public sealed class ClinicalRecordsController(HealthcareDbContext database) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet("me"), Authorize(Roles = "Patient")]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        var patient = await database.Patients.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == UserId, cancellationToken);
        return patient is null ? NotFound() : await ReadPatientRecordsAsync(patient.Id, "patient_clinical_records_read", page, pageSize, cancellationToken);
    }

    [HttpGet("patients/{patientId:guid}"), Authorize(Roles = "Doctor,HospitalStaff")]
    public async Task<IActionResult> PatientRecords(Guid patientId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30, CancellationToken cancellationToken = default)
    {
        if (User.IsInRole("Doctor"))
        {
            var doctor = await GetVerifiedDoctorAsync(cancellationToken);
            if (doctor is null || !await database.Appointments.AnyAsync(x => x.PatientId == patientId
                    && x.DoctorId == doctor.Id && (x.Status == AppointmentStatus.Confirmed || x.Status == AppointmentStatus.Completed), cancellationToken)) return Forbid();
        }
        else if (User.IsInRole("HospitalStaff"))
        {
            var assigned = await database.Appointments.AnyAsync(x => x.PatientId == patientId
                && (x.Status == AppointmentStatus.Confirmed || x.Status == AppointmentStatus.Completed) && x.Doctor!.HospitalId.HasValue
                && database.HospitalStaffAssignments.Any(a => a.UserId == UserId && a.HospitalId == x.Doctor.HospitalId), cancellationToken);
            if (!assigned) return Forbid();
        }
        else return Forbid();
        return await ReadPatientRecordsAsync(patientId, "clinical_staff_records_read", page, pageSize, cancellationToken);
    }

    [HttpGet("caregiver-links/{linkId:guid}"), Authorize(Roles = "Caregiver")]
    public async Task<IActionResult> CaregiverRecords(Guid linkId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var link = await database.CaregiverLinks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == linkId
            && x.CaregiverUserId == UserId && x.AcceptedAt != null && x.RevokedAt == null && x.CanViewMedicalRecords, cancellationToken);
        return link is null ? NotFound() : await ReadPatientRecordsAsync(link.PatientId,
            "caregiver_clinical_records_read", page, pageSize, cancellationToken);
    }

    [HttpPost("appointments/{appointmentId:guid}/diagnoses"), Authorize(Roles = "Doctor")]
    public async Task<IActionResult> AddDiagnosis(Guid appointmentId, [FromBody] AddDiagnosisRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OnsetDate > DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequest(new { message = "Diagnosis onset date cannot be in the future." });
        if (request.ClinicalStatus is not ("Active" or "Resolved" or "Historical"))
            return BadRequest(new { message = "Choose Active, Resolved, or Historical status." });
        var context = await GetAppointmentForDoctorAsync(appointmentId, cancellationToken);
        if (context is null) return Forbid();
        var (doctor, appointment) = context.Value;
        var record = new MedicalRecord
        {
            PatientId = appointment.PatientId,
            DoctorId = doctor.Id,
            AppointmentId = appointment.Id,
            RecordType = "Diagnosis",
            Summary = request.Display.Trim()
        };
        record.Diagnoses.Add(new Diagnosis
        {
            Code = Clean(request.Code),
            CodeSystem = Clean(request.CodeSystem),
            Display = request.Display.Trim(),
            ClinicalStatus = request.ClinicalStatus,
            OnsetDate = request.OnsetDate,
            Notes = Clean(request.Notes)
        });
        database.MedicalRecords.Add(record);
        AddClinicalAudit(appointment.PatientId, "clinical_diagnosis_created");
        await database.SaveChangesAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new { record.Id, record.RecordType, record.RecordedAt });
    }

    [HttpPost("appointments/{appointmentId:guid}/lab-results"), Authorize(Roles = "Doctor")]
    public async Task<IActionResult> AddLabResult(Guid appointmentId, [FromBody] AddLabResultRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Status is not ("Pending" or "Normal" or "Abnormal" or "Cancelled"))
            return BadRequest(new { message = "Lab result status is not supported." });
        if ((request.Status is "Normal" or "Abnormal") && string.IsNullOrWhiteSpace(request.ResultValue))
            return BadRequest(new { message = "A completed lab result requires a result value." });
        if (request.CollectedAt > DateTimeOffset.UtcNow.AddMinutes(5))
            return BadRequest(new { message = "Collection time cannot be in the future." });
        var context = await GetAppointmentForDoctorAsync(appointmentId, cancellationToken);
        if (context is null) return Forbid();
        var (doctor, appointment) = context.Value;
        var resultSummary = string.IsNullOrWhiteSpace(request.ResultValue)
            ? $"{request.TestName.Trim()} — {request.Status}"
            : $"{request.TestName.Trim()}: {request.ResultValue.Trim()} {request.Unit?.Trim()}".Trim();
        var record = new MedicalRecord
        {
            PatientId = appointment.PatientId,
            DoctorId = doctor.Id,
            AppointmentId = appointment.Id,
            RecordType = "Laboratory result",
            Summary = resultSummary
        };
        record.LabResults.Add(new LabResult
        {
            TestName = request.TestName.Trim(),
            ResultValue = Clean(request.ResultValue),
            Unit = Clean(request.Unit),
            ReferenceRange = Clean(request.ReferenceRange),
            Status = request.Status,
            CollectedAt = request.CollectedAt,
            Notes = Clean(request.Notes)
        });
        database.MedicalRecords.Add(record);
        AddClinicalAudit(appointment.PatientId, "clinical_lab_result_created");
        await database.SaveChangesAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new { record.Id, record.RecordType, record.RecordedAt });
    }

    [HttpPost("appointments/{appointmentId:guid}/prescriptions"), Authorize(Roles = "Doctor")]
    public async Task<IActionResult> AddPrescription(Guid appointmentId, [FromBody] AddPrescriptionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count is < 1 or > 20)
            return BadRequest(new { message = "A prescription must contain between one and twenty medication items." });
        if (request.StartsOn.HasValue && request.EndsOn.HasValue && request.EndsOn < request.StartsOn)
            return BadRequest(new { message = "Prescription end date must be on or after its start date." });
        var context = await GetAppointmentForDoctorAsync(appointmentId, cancellationToken);
        if (context is null) return Forbid();
        var (doctor, appointment) = context.Value;
        var record = new MedicalRecord
        {
            PatientId = appointment.PatientId,
            DoctorId = doctor.Id,
            AppointmentId = appointment.Id,
            RecordType = "Prescription",
            Summary = $"Prescription with {request.Items.Count} medication item(s)."
        };
        var prescription = new Prescription { StartsOn = request.StartsOn, EndsOn = request.EndsOn };
        foreach (var item in request.Items)
        {
            prescription.Items.Add(new PrescriptionItem
            {
                MedicationName = item.MedicationName.Trim(),
                Strength = Clean(item.Strength),
                Dose = item.Dose.Trim(),
                Route = item.Route.Trim(),
                Frequency = item.Frequency.Trim(),
                DurationDays = item.DurationDays,
                Quantity = item.Quantity,
                Instructions = Clean(item.Instructions)
            });
        }
        record.Prescriptions.Add(prescription);
        database.MedicalRecords.Add(record);
        AddClinicalAudit(appointment.PatientId, "clinical_prescription_created");
        await database.SaveChangesAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new { record.Id, PrescriptionId = prescription.Id, record.RecordType, record.RecordedAt });
    }

    private async Task<IActionResult> ReadPatientRecordsAsync(Guid patientId, string auditEvent, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        if (!await database.Patients.AnyAsync(x => x.Id == patientId, cancellationToken)) return NotFound();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.MedicalRecords.AsNoTracking().Where(x => x.PatientId == patientId);
        var total = await query.CountAsync(cancellationToken);
        var records = await query.OrderByDescending(x => x.RecordedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Include(x => x.Diagnoses).Include(x => x.LabResults)
            .Include(x => x.Prescriptions).ThenInclude(x => x.Items).AsSplitQuery().ToListAsync(cancellationToken);
        database.AccountSecurityLogs.Add(new AccountSecurityLog { UserId = UserId, ResourceId = patientId, EventType = auditEvent, Succeeded = true });
        await database.SaveChangesAsync(cancellationToken);
        var items = records.Select(record => new ClinicalRecordView(record.Id, record.RecordType, record.Summary,
            record.RecordedAt, record.DocumentStorageKey is not null,
            record.Diagnoses.Select(x => new DiagnosisView(x.Id, x.Code, x.CodeSystem, x.Display, x.ClinicalStatus, x.OnsetDate, x.Notes)).ToArray(),
            record.LabResults.Select(x => new LabResultView(x.Id, x.TestName, x.ResultValue, x.Unit, x.ReferenceRange, x.Status, x.CollectedAt, x.Notes)).ToArray(),
            record.Prescriptions.Select(x => new PrescriptionView(x.Id, x.Medication, x.Instructions, x.StartsOn, x.EndsOn,
                x.Items.OrderBy(i => i.CreatedAt).Select(i => new PrescriptionItemView(i.Id, i.MedicationName, i.Strength,
                    i.Dose, i.Route, i.Frequency, i.DurationDays, i.Quantity, i.Instructions)).ToArray())).ToArray()))
            .ToArray();
        return Ok(new { items, page, pageSize, total });
    }

    private async Task<(Doctor Doctor, Appointment Appointment)?> GetAppointmentForDoctorAsync(Guid appointmentId,
        CancellationToken cancellationToken)
    {
        var doctor = await GetVerifiedDoctorAsync(cancellationToken);
        if (doctor is null) return null;
        var appointment = await database.Appointments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == appointmentId
            && x.DoctorId == doctor.Id && (x.Status == AppointmentStatus.Confirmed || x.Status == AppointmentStatus.Completed), cancellationToken);
        return appointment is null ? null : (doctor, appointment);
    }

    private Task<Doctor?> GetVerifiedDoctorAsync(CancellationToken cancellationToken) => database.Doctors.AsNoTracking()
        .SingleOrDefaultAsync(x => x.UserId == UserId && x.IsApproved && x.User!.IsActive, cancellationToken);

    private void AddClinicalAudit(Guid patientId, string eventType) => database.AccountSecurityLogs.Add(new AccountSecurityLog
    {
        UserId = UserId,
        ResourceId = patientId,
        EventType = eventType,
        Succeeded = true
    });

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class AddDiagnosisRequest
{
    [Required, StringLength(240, MinimumLength = 2)] public string Display { get; init; } = string.Empty;
    [StringLength(32)] public string? Code { get; init; }
    [StringLength(80)] public string? CodeSystem { get; init; }
    [Required, StringLength(20)] public string ClinicalStatus { get; init; } = "Active";
    public DateOnly? OnsetDate { get; init; }
    [StringLength(2000)] public string? Notes { get; init; }
}

public sealed class AddLabResultRequest
{
    [Required, StringLength(200, MinimumLength = 2)] public string TestName { get; init; } = string.Empty;
    [StringLength(300)] public string? ResultValue { get; init; }
    [StringLength(40)] public string? Unit { get; init; }
    [StringLength(100)] public string? ReferenceRange { get; init; }
    [Required, StringLength(20)] public string Status { get; init; } = "Pending";
    public DateTimeOffset CollectedAt { get; init; } = DateTimeOffset.UtcNow;
    [StringLength(1000)] public string? Notes { get; init; }
}

public sealed class AddPrescriptionRequest
{
    public DateOnly? StartsOn { get; init; }
    public DateOnly? EndsOn { get; init; }
    [Required, MinLength(1), MaxLength(20)] public List<PrescriptionItemRequest> Items { get; init; } = [];
}

public sealed class PrescriptionItemRequest
{
    [Required, StringLength(200, MinimumLength = 2)] public string MedicationName { get; init; } = string.Empty;
    [StringLength(80)] public string? Strength { get; init; }
    [Required, StringLength(120)] public string Dose { get; init; } = string.Empty;
    [Required, StringLength(60)] public string Route { get; init; } = string.Empty;
    [Required, StringLength(120)] public string Frequency { get; init; } = string.Empty;
    [Range(1, 3650)] public int? DurationDays { get; init; }
    [Range(1, 10000)] public int? Quantity { get; init; }
    [StringLength(1000)] public string? Instructions { get; init; }
}

public sealed record ClinicalRecordView(Guid Id, string RecordType, string Summary, DateTimeOffset RecordedAt,
    bool HasDocument, IReadOnlyList<DiagnosisView> Diagnoses, IReadOnlyList<LabResultView> LabResults,
    IReadOnlyList<PrescriptionView> Prescriptions);
public sealed record DiagnosisView(Guid Id, string? Code, string? CodeSystem, string Display, string ClinicalStatus,
    DateOnly? OnsetDate, string? Notes);
public sealed record LabResultView(Guid Id, string TestName, string? ResultValue, string? Unit, string? ReferenceRange,
    string Status, DateTimeOffset CollectedAt, string? Notes);
public sealed record PrescriptionView(Guid Id, string? LegacyMedication, string? LegacyInstructions,
    DateOnly? StartsOn, DateOnly? EndsOn, IReadOnlyList<PrescriptionItemView> Items);
public sealed record PrescriptionItemView(Guid Id, string MedicationName, string? Strength, string Dose, string Route,
    string Frequency, int? DurationDays, int? Quantity, string? Instructions);
