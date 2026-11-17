using EDOC.Api.Data;
using EDOC.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EDOC.Api.Models;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace EDOC.Api.Controllers;

[ApiController, Authorize(Roles = "Administrator,SuperAdmin")]
[Route("api/admin")]
public sealed class AdminController(HealthcareDbContext database, UserManager<UserAccount> users) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var dayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var result = new
        {
            Patients = await database.Patients.CountAsync(cancellationToken),
            Doctors = await database.Doctors.CountAsync(cancellationToken),
            VerifiedDoctors = await database.Doctors.CountAsync(x => x.IsApproved && x.User!.IsActive, cancellationToken),
            PendingDoctors = await database.Doctors.CountAsync(x => !x.IsApproved && x.User!.IsActive, cancellationToken),
            Hospitals = await database.Hospitals.CountAsync(cancellationToken),
            NssfHospitals = await database.Hospitals.CountAsync(x => x.NssfStatus == "NSSF" && x.IsActive, cancellationToken),
            NonNssfHospitals = await database.Hospitals.CountAsync(x => x.NssfStatus == "NON_NSSF" && x.IsActive, cancellationToken),
            PendingNssfHospitals = await database.Hospitals.CountAsync(x => x.NssfStatus == "PENDING_VERIFICATION" && x.IsActive, cancellationToken),
            Staff = await database.HospitalStaffAssignments.Select(x => x.UserId).Distinct().CountAsync(cancellationToken),
            Appointments = await database.Appointments.CountAsync(cancellationToken),
            PendingAppointments = await database.Appointments.CountAsync(x => x.Status == Models.AppointmentStatus.Pending, cancellationToken),
            TodayAppointments = await database.Appointments.CountAsync(x => x.Schedule!.StartsAt >= dayStart && x.Schedule.StartsAt < dayStart.AddDays(1), cancellationToken),
            CompletedAppointments = await database.Appointments.CountAsync(x => x.Status == Models.AppointmentStatus.Completed, cancellationToken),
            UpcomingAppointments = await database.Appointments.CountAsync(x => x.Schedule!.StartsAt >= now && x.Status == Models.AppointmentStatus.Confirmed, cancellationToken),
            MonthlyRegistrations = await users.Users.CountAsync(x => x.CreatedAt >= monthStart, cancellationToken)
        };
        return Ok(result);
    }

    /// <summary>Returns a paged patient directory without exposing clinical records.</summary>
    [HttpGet("patients")]
    public async Task<IActionResult> Patients([FromQuery] string? search, [FromQuery] bool? active,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.Patients.AsNoTracking().Include(x => x.User).AsQueryable();
        if (active.HasValue) query = query.Where(x => x.User!.IsActive == active.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var isPatientId = Guid.TryParse(term, out var patientId);
            query = query.Where(x => x.User!.FullName.Contains(term)
                || (x.User.PhoneNumber != null && x.User.PhoneNumber.Contains(term))
                || (isPatientId && x.Id == patientId));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.User!.FullName).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AdminPatientSummary(x.Id, x.UserId, x.User!.FullName, x.User.PhoneNumber,
                x.User.Email, x.User.IsActive, x.CreatedAt))
            .ToListAsync(cancellationToken);
        return Ok(new { items, page, pageSize, total });
    }

    /// <summary>Returns staff account and hospital assignment information, without permission to read patient records.</summary>
    [HttpGet("staff")]
    public async Task<IActionResult> Staff([FromQuery] string? search, [FromQuery] Guid? hospitalId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.HospitalStaffAssignments.AsNoTracking().Include(x => x.User).Include(x => x.Hospital).AsQueryable();
        if (hospitalId.HasValue) query = query.Where(x => x.HospitalId == hospitalId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.User!.FullName.Contains(term)
                || (x.User.Email != null && x.User.Email.Contains(term))
                || (x.User.PhoneNumber != null && x.User.PhoneNumber.Contains(term))
                || x.Hospital!.Name.Contains(term));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Hospital!.Name).ThenBy(x => x.User!.FullName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AdminStaffSummary(x.UserId, x.User!.FullName, x.User.Email, x.User.PhoneNumber,
                x.User.IsActive, x.HospitalId, x.Hospital!.Name, x.CreatedAt))
            .ToListAsync(cancellationToken);
        return Ok(new { items, page, pageSize, total });
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = users.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.FullName.Contains(search) || (x.Email != null && x.Email.Contains(search)));
        var total = await query.CountAsync(cancellationToken);
        var listed = await query.OrderBy(x => x.FullName).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.FullName, x.Email, x.PhoneNumber, x.IsActive }).ToListAsync(cancellationToken);
        var items = new List<object>(listed.Count);
        foreach (var item in listed) items.Add(new { item.Id, item.FullName, item.Email, item.PhoneNumber, item.IsActive, Roles = await users.GetRolesAsync(new UserAccount { Id = item.Id }) });
        return Ok(new { items, page, pageSize, total });
    }

    [HttpGet("doctors")]
    public async Task<IActionResult> Doctors([FromQuery] string? search, [FromQuery] bool? approved,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.Doctors.AsNoTracking().AsQueryable();
        if (approved.HasValue) query = query.Where(x => x.IsApproved == approved.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.User!.FullName.Contains(term)
                || (x.User.Email != null && x.User.Email.Contains(term))
                || x.Specialty!.Name.Contains(term));
        }
        if (approved == false) query = query.Where(x => x.User!.IsActive);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.User!.FullName).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AdminDoctorSummary(x.Id, x.UserId, x.User!.FullName, x.User.Email,
                x.Specialty!.Name, x.Hospital == null ? null : x.Hospital.Name, x.Qualifications,
                x.YearsExperience, x.ConsultationFee, x.IsApproved, x.User.IsActive))
            .ToListAsync(cancellationToken);
        return Ok(new { items, page, pageSize, total });
    }

    [HttpPost("doctors")]
    public async Task<IActionResult> CreateDoctor(CreateDoctorRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is null) return NotFound(new { message = "User not found." });
        if (await database.Doctors.AnyAsync(x => x.UserId == user.Id, cancellationToken)) return Conflict(new { message = "This user already has a doctor profile." });
        if (!await database.Specialties.AnyAsync(x => x.Id == request.SpecialtyId, cancellationToken)) return BadRequest(new { message = "Choose an existing specialty." });
        if (request.HospitalId.HasValue && !await database.Hospitals.AnyAsync(x => x.Id == request.HospitalId, cancellationToken)) return BadRequest(new { message = "Choose an existing hospital." });
        var role = await users.AddToRoleAsync(user, "Doctor");
        if (!role.Succeeded) return BadRequest(new { errors = role.Errors.Select(x => new { x.Code, x.Description }) });
        var doctor = new Doctor { UserId = user.Id, SpecialtyId = request.SpecialtyId, HospitalId = request.HospitalId, Qualifications = request.Qualifications, YearsExperience = request.YearsExperience, Biography = request.Biography, ConsultationFee = request.ConsultationFee, IsApproved = false };
        database.Doctors.Add(doctor);
        database.AuditLogs.Add(new AuditLog { ActorUserId = CurrentUserId(), Action = "admin.doctor.create", EntityType = "Doctor", EntityId = doctor.Id, Summary = $"Doctor profile created for user {user.Id}; hospital {request.HospitalId?.ToString() ?? "unassigned"}." });
        await database.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(CreateDoctor), new { id = doctor.Id }, new { doctor.Id, doctor.UserId, doctor.SpecialtyId, doctor.HospitalId });
    }

    [HttpPut("doctors/{doctorId:guid}/approval")]
    public async Task<IActionResult> SetDoctorApproval(Guid doctorId, [FromBody] DoctorApprovalRequest request, CancellationToken cancellationToken)
    {
        var doctor = await database.Doctors.Include(x => x.User).SingleOrDefaultAsync(x => x.Id == doctorId, cancellationToken);
        if (doctor is null) return NotFound();
        doctor.IsApproved = request.Approved;
        if (!request.Approved && doctor.User is not null)
        {
            doctor.User.IsActive = false;
            await database.RefreshTokens.Where(x => x.UserId == doctor.UserId && x.RevokedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow), cancellationToken);
        }
        database.AuditLogs.Add(new AuditLog { ActorUserId = CurrentUserId(), Action = request.Approved ? "admin.doctor.approve" : "admin.doctor.reject", EntityType = "Doctor", EntityId = doctor.Id, Summary = $"Doctor verification {(request.Approved ? "approved" : "rejected")}." });
        await database.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("users/{userId:guid}/activation")]
    public async Task<IActionResult> SetUserActivation(Guid userId, [FromBody] UserActivationRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return NotFound();
        user.IsActive = request.Active;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded) return BadRequest(new { errors = result.Errors.Select(x => new { x.Code, x.Description }) });
        if (!request.Active)
            await database.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow), cancellationToken);
        database.AuditLogs.Add(new AuditLog { ActorUserId = CurrentUserId(), Action = request.Active ? "admin.user.activate" : "admin.user.suspend", EntityType = "UserAccount", EntityId = userId, Summary = $"Account access {(request.Active ? "activated" : "suspended")}." });
        await database.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("hospitals/{hospitalId:guid}/staff/{userId:guid}")]
    public async Task<IActionResult> AssignHospitalStaff(Guid hospitalId, Guid userId, CancellationToken cancellationToken)
    {
        if (!await database.Hospitals.AnyAsync(x => x.Id == hospitalId, cancellationToken)) return NotFound(new { message = "Hospital not found." });
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return NotFound(new { message = "User not found." });
        if (await database.HospitalStaffAssignments.AnyAsync(x => x.HospitalId == hospitalId && x.UserId == userId, cancellationToken)) return Conflict();
        if (!await users.IsInRoleAsync(user, "HospitalStaff"))
        {
            var role = await users.AddToRoleAsync(user, "HospitalStaff");
            if (!role.Succeeded) return BadRequest(new { errors = role.Errors.Select(x => new { x.Code, x.Description }) });
        }
        database.HospitalStaffAssignments.Add(new HospitalStaffAssignment { HospitalId = hospitalId, UserId = userId });
        database.AuditLogs.Add(new AuditLog { ActorUserId = CurrentUserId(), Action = "admin.hospital_staff.assign", EntityType = "Hospital", EntityId = hospitalId, Summary = $"Hospital staff assigned: {userId}." });
        await database.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("hospitals/{hospitalId:guid}/admins/{userId:guid}")]
    public async Task<IActionResult> AssignHospitalAdmin(Guid hospitalId, Guid userId, CancellationToken cancellationToken)
    {
        if (!await database.Hospitals.AnyAsync(x => x.Id == hospitalId, cancellationToken)) return NotFound(new { message = "Hospital not found." });
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return NotFound(new { message = "User not found." });
        if (await users.IsInRoleAsync(user, "SuperAdmin") || await users.IsInRoleAsync(user, "Administrator"))
            return BadRequest(new { message = "Global administrators cannot be assigned as hospital administrators." });
        var assignment = await database.HospitalStaffAssignments.SingleOrDefaultAsync(x => x.HospitalId == hospitalId && x.UserId == userId, cancellationToken);
        if (assignment is not null) return Conflict(new { message = "This hospital administrator is already assigned." });
        if (assignment is null) database.HospitalStaffAssignments.Add(new HospitalStaffAssignment { HospitalId = hospitalId, UserId = userId });
        if (!await users.IsInRoleAsync(user, "HospitalAdmin"))
        {
            var role = await users.AddToRoleAsync(user, "HospitalAdmin");
            if (!role.Succeeded) return BadRequest(new { errors = role.Errors.Select(x => new { x.Code, x.Description }) });
        }
        database.AuditLogs.Add(new AuditLog { ActorUserId = CurrentUserId(), Action = "admin.hospital_admin.assign", EntityType = "Hospital", EntityId = hospitalId, Summary = $"Hospital administrator assigned: {user.Id}." });
        await database.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private Guid? CurrentUserId() => Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
}

public sealed record CreateDoctorRequest(
    [param: Required] Guid UserId,
    [param: Required] Guid SpecialtyId,
    Guid? HospitalId,
    [param: StringLength(300)] string? Qualifications,
    [param: Range(0, 80)] int YearsExperience,
    [param: StringLength(4000)] string? Biography,
    [param: Range(0, 100000)] decimal? ConsultationFee);

public sealed record DoctorApprovalRequest(bool Approved);
public sealed record UserActivationRequest(bool Active);
public sealed record AdminPatientSummary(Guid PatientId, Guid UserId, string FullName, string? PhoneNumber,
    string? Email, bool IsActive, DateTimeOffset RegisteredAt);
public sealed record AdminStaffSummary(Guid UserId, string FullName, string? Email, string? PhoneNumber,
    bool IsActive, Guid HospitalId, string HospitalName, DateTimeOffset AssignedAt);
