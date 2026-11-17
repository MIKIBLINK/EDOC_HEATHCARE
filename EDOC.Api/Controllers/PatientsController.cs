using System.Security.Claims;
using EDOC.Api.Data;
using EDOC.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDOC.Api.Controllers;

[ApiController, Authorize]
[Route("api/patients")]
public sealed class PatientsController(HealthcareDbContext database) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet("me")]
    public async Task<ActionResult<PatientSummary>> GetMine(CancellationToken cancellationToken)
    {
        var patient = await database.Patients.AsNoTracking().Where(x => x.UserId == UserId)
            .Select(x => new PatientSummary(x.Id, x.User!.FullName, x.User.Email, x.User.PhoneNumber, x.DateOfBirth, x.Gender, x.Address, x.EmergencyContactName, x.EmergencyContactPhone))
            .SingleOrDefaultAsync(cancellationToken);
        return patient is null ? NotFound() : Ok(patient);
    }

    [HttpGet("{id:guid}"), Authorize(Roles = "Doctor,HospitalStaff,Administrator")]
    public async Task<ActionResult<PatientSummary>> GetAuthorized(Guid id, CancellationToken cancellationToken)
    {
        // A doctor must have an appointment with this patient. Administrative roles are explicitly trusted.
        var currentUserId = UserId;
        var isAdmin = User.IsInRole("Administrator");
        var doctorId = User.IsInRole("Doctor")
            ? await database.Doctors.Where(d => d.UserId == currentUserId && d.IsApproved && d.User!.IsActive)
                .Select(d => (Guid?)d.Id).SingleOrDefaultAsync(cancellationToken)
            : null;
        var assignedDoctor = doctorId.HasValue && await database.Appointments.AnyAsync(x => x.PatientId == id && x.DoctorId == doctorId.Value, cancellationToken);
        var assignedStaff = User.IsInRole("HospitalStaff") && await database.Appointments.AnyAsync(x => x.PatientId == id && x.Doctor!.HospitalId.HasValue && database.HospitalStaffAssignments.Any(a => a.UserId == currentUserId && a.HospitalId == x.Doctor.HospitalId), cancellationToken);
        var assigned = isAdmin || assignedDoctor || assignedStaff;
        if (!assigned) return Forbid();
        var patient = await database.Patients.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new PatientSummary(x.Id, x.User!.FullName, x.User.Email, x.User.PhoneNumber, x.DateOfBirth, x.Gender, x.Address, x.EmergencyContactName, x.EmergencyContactPhone))
            .SingleOrDefaultAsync(cancellationToken);
        return patient is null ? NotFound() : Ok(patient);
    }

    [HttpPut("me"), Authorize(Roles = "Patient")]
    public async Task<IActionResult> UpdateMine(UpdatePatientRequest request, CancellationToken cancellationToken)
    {
        var patient = await database.Patients.Include(x => x.User).SingleOrDefaultAsync(x => x.UserId == UserId, cancellationToken);
        if (patient?.User is null) return NotFound();
        patient.User.FullName = request.FullName.Trim();
        patient.User.PhoneNumber = request.PhoneNumber.Trim();
        patient.DateOfBirth = request.DateOfBirth;
        patient.Gender = request.Gender;
        patient.Address = request.Address;
        patient.EmergencyContactName = request.EmergencyContactName;
        patient.EmergencyContactPhone = request.EmergencyContactPhone;
        await database.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

public sealed record UpdatePatientRequest(
    [param: System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(160, MinimumLength = 2)] string FullName,
    [param: System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.Phone] string PhoneNumber,
    DateOnly? DateOfBirth,
    [param: System.ComponentModel.DataAnnotations.StringLength(40)] string? Gender,
    [param: System.ComponentModel.DataAnnotations.StringLength(500)] string? Address,
    [param: System.ComponentModel.DataAnnotations.StringLength(160)] string? EmergencyContactName,
    [param: System.ComponentModel.DataAnnotations.Phone] string? EmergencyContactPhone);
