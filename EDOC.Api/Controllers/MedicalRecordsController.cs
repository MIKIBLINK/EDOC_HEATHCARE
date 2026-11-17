using System.Security.Claims;
using EDOC.Api.Data;
using EDOC.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EDOC.Api.Services;

namespace EDOC.Api.Controllers;

[ApiController, Authorize]
[Route("api/medical-records")]
public sealed class MedicalRecordsController(HealthcareDbContext database, IPrivateFileStore fileStore) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? patientId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        if (User.IsInRole("Doctor") && !await database.Doctors.AnyAsync(x => x.UserId == UserId && x.IsApproved && x.User!.IsActive, cancellationToken)) return Forbid();
        var query = database.MedicalRecords.AsNoTracking().AsQueryable();
        // Administrators can manage accounts and aggregate reporting but do not receive clinical content by default.
        if (User.IsInRole("Administrator")) return Forbid();
        else if (User.IsInRole("HospitalStaff")) query = query.Where(x => x.PatientId == patientId && x.Appointment != null && x.Appointment.Doctor!.HospitalId.HasValue && database.HospitalStaffAssignments.Any(a => a.UserId == UserId && a.HospitalId == x.Appointment.Doctor.HospitalId));
        else if (User.IsInRole("Patient") && User.IsInRole("Doctor")) query = query.Where(x => x.Patient!.UserId == UserId || x.Doctor!.UserId == UserId);
        else if (User.IsInRole("Patient")) query = query.Where(x => x.Patient!.UserId == UserId);
        else if (User.IsInRole("Doctor")) query = query.Where(x => x.Doctor!.UserId == UserId);
        else return Forbid();
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.RecordedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.PatientId, x.DoctorId, x.RecordType, x.Summary, x.RecordedAt })
            .ToListAsync(cancellationToken);
        return Ok(new { items, page, pageSize, total });
    }

    [HttpPost, Authorize(Roles = "Doctor")]
    public async Task<IActionResult> Create(CreateMedicalRecordRequest request, CancellationToken cancellationToken)
    {
        var doctor = await database.Doctors.SingleOrDefaultAsync(x => x.UserId == UserId && x.IsApproved && x.User!.IsActive, cancellationToken);
        if (doctor is null) return Forbid();
        var authorizedAppointment = request.AppointmentId.HasValue && await database.Appointments.AnyAsync(x =>
            x.Id == request.AppointmentId.Value && x.PatientId == request.PatientId && x.DoctorId == doctor.Id && x.Status != AppointmentStatus.Cancelled, cancellationToken);
        if (!authorizedAppointment) return Forbid();
        if (!await database.Patients.AnyAsync(x => x.Id == request.PatientId, cancellationToken)) return NotFound();
        var record = new MedicalRecord
        {
            PatientId = request.PatientId,
            DoctorId = doctor.Id,
            AppointmentId = request.AppointmentId,
            RecordType = request.RecordType.Trim(),
            Summary = request.Summary.Trim()
        };
        database.MedicalRecords.Add(record);
        await database.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(List), new { patientId = record.PatientId }, new { record.Id, record.PatientId, record.RecordType, record.RecordedAt });
    }

    [HttpPost("{id:guid}/documents"), RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Upload(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > 9_500_000) return BadRequest(new { message = "Choose a document smaller than 9.5 MB." });
        var record = await database.MedicalRecords.Include(x => x.Patient).Include(x => x.Doctor)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (record is null) return NotFound();
        var allowed = record.Patient?.UserId == UserId || record.Doctor?.UserId == UserId;
        if (!allowed) return Forbid();
        if (User.IsInRole("Doctor") && !await database.Doctors.AnyAsync(x => x.UserId == UserId && x.IsApproved && x.User!.IsActive, cancellationToken)) return Forbid();
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var expected = extension switch { ".pdf" => "application/pdf", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", _ => string.Empty };
        if (expected.Length == 0) return BadRequest(new { message = "Only PDF, PNG, and JPEG documents are accepted." });
        await using var input = file.OpenReadStream();
        var header = new byte[8];
        var read = await input.ReadAsync(header, cancellationToken);
        if (!HasExpectedSignature(extension, header.AsSpan(0, read))) return BadRequest(new { message = "The document content does not match its file type." });
        input.Position = 0;
        var key = await fileStore.SaveAsync(input, extension, cancellationToken);
        record.DocumentStorageKey = key;
        await database.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Download), new { id }, new { id = record.Id });
    }

    [HttpGet("{id:guid}/documents")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var record = await database.MedicalRecords.Include(x => x.Patient).Include(x => x.Doctor)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (record is null || record.DocumentStorageKey is null) return NotFound();
        var allowed = record.Patient?.UserId == UserId || record.Doctor?.UserId == UserId;
        if (!allowed) return Forbid();
        if (User.IsInRole("Doctor") && !await database.Doctors.AnyAsync(x => x.UserId == UserId && x.IsApproved && x.User!.IsActive, cancellationToken)) return Forbid();
        var stream = await fileStore.OpenReadAsync(record.DocumentStorageKey, cancellationToken);
        if (stream is null) return NotFound();
        var contentType = Path.GetExtension(record.DocumentStorageKey).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf", ".png" => "image/png", _ => "image/jpeg"
        };
        return File(stream, contentType, $"medical-record-{record.Id:N}{Path.GetExtension(record.DocumentStorageKey)}", enableRangeProcessing: false);
    }

    private static bool HasExpectedSignature(string extension, ReadOnlySpan<byte> bytes) => extension switch
    {
        ".pdf" => bytes.StartsWith("%PDF-"u8),
        ".png" => bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        ".jpg" or ".jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        _ => false
    };
}

public sealed record CreateMedicalRecordRequest(
    [param: System.ComponentModel.DataAnnotations.Required] Guid PatientId,
    Guid? AppointmentId,
    [param: System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(80)] string RecordType,
    [param: System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(4000)] string Summary);
