using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EDOC.Api.Data;
using EDOC.Api.Models;
using EDOC.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace EDOC.Api.Controllers;

[ApiController, Authorize]
[Route("api/caregiver-links")]
public sealed class CaregiverLinksController(
    HealthcareDbContext database,
    UserManager<UserAccount> users,
    IAccountEmailSender emailSender) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    // A patient grants narrowly scoped consent before an invite is sent. The recipient must
    // create or use a Caregiver account with that same verified email to accept it.
    [HttpPost, Authorize(Roles = "Patient")]
    public async Task<IActionResult> Invite(InviteCaregiverRequest request, CancellationToken cancellationToken)
    {
        if (!request.CanViewAppointments && !request.CanViewMedicalRecords && !request.CanViewMedicationReminders)
            return BadRequest(new { message = "Grant at least one access permission before inviting a caregiver." });
        if (!emailSender.IsConfigured)
            return Problem("Account email delivery is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);

        var patient = await database.Patients.AsNoTracking().Include(x => x.User).SingleOrDefaultAsync(x => x.UserId == UserId, cancellationToken);
        if (patient is null) return NotFound();
        var email = request.Email.Trim();
        var normalizedEmail = users.NormalizeEmail(email);
        if (string.Equals(normalizedEmail, users.NormalizeEmail(patient.User?.Email ?? string.Empty), StringComparison.Ordinal))
            return BadRequest(new { message = "You cannot invite your own account as a caregiver." });

        var now = DateTimeOffset.UtcNow;
        var existing = await database.CaregiverLinks
            .Where(x => x.PatientId == patient.Id && x.NormalizedInviteEmail == normalizedEmail && x.RevokedAt == null)
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is { AcceptedAt: not null })
            return Conflict(new { message = "This caregiver is already connected. Update or revoke the existing consent." });
        if (existing is { InvitationExpiresAt: var expiry } && expiry > now)
            return Conflict(new { message = "An invitation for this email is already pending." });
        if (existing is not null)
        {
            existing.RevokedAt = now;
            await database.SaveChangesAsync(cancellationToken);
        }

        var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var link = new CaregiverLink
        {
            PatientId = patient.Id,
            InviteEmail = email,
            NormalizedInviteEmail = normalizedEmail,
            InvitationTokenHash = Hash(rawToken),
            InvitationExpiresAt = now.AddDays(7),
            CanViewAppointments = request.CanViewAppointments,
            CanViewMedicalRecords = request.CanViewMedicalRecords,
            CanViewMedicationReminders = request.CanViewMedicationReminders
        };
        database.CaregiverLinks.Add(link);
        await database.SaveChangesAsync(cancellationToken);
        try
        {
            await emailSender.SendAsync(email, "E-DOC caregiver invitation",
                $"A patient invited you to connect as a caregiver. Sign in or register for an E-DOC Caregiver account using this email, then accept invitation {link.Id} with token: {rawToken}. The invitation expires in seven days.", cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            database.CaregiverLinks.Remove(link);
            await database.SaveChangesAsync(cancellationToken);
            return Problem("The caregiver invitation could not be delivered. No invitation was created.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        return Accepted(new { link.Id, link.InvitationExpiresAt, message = "Invitation sent. Access starts only after the invited caregiver accepts." });
    }

    [HttpGet, Authorize(Roles = "Patient")]
    public async Task<IActionResult> ListMine(CancellationToken cancellationToken)
    {
        var links = await database.CaregiverLinks.AsNoTracking()
            .Where(x => x.Patient!.UserId == UserId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new CaregiverLinkView(x.Id, x.InviteEmail, x.CaregiverUser == null ? null : x.CaregiverUser.FullName,
                x.InvitationExpiresAt, x.AcceptedAt, x.RevokedAt, x.ConsentGrantedAt,
                x.CanViewAppointments, x.CanViewMedicalRecords, x.CanViewMedicationReminders))
            .ToListAsync(cancellationToken);
        return Ok(links);
    }

    [HttpPost("{id:guid}/accept"), Authorize(Roles = "Caregiver")]
    public async Task<IActionResult> Accept(Guid id, AcceptCaregiverInvitationRequest request, CancellationToken cancellationToken)
    {
        var link = await database.CaregiverLinks.Include(x => x.Patient)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (link is null) return NotFound();
        var account = await users.FindByIdAsync(UserId.ToString());
        if (account?.Email is null || !string.Equals(users.NormalizeEmail(account.Email), link.NormalizedInviteEmail, StringComparison.Ordinal))
            return Forbid();
        if (link.RevokedAt is not null || link.AcceptedAt is not null || link.InvitationExpiresAt <= DateTimeOffset.UtcNow)
            return Conflict(new { message = "This invitation is expired, revoked, or already accepted." });
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(link.InvitationTokenHash), Encoding.ASCII.GetBytes(Hash(request.Token))))
            return BadRequest(new { message = "The invitation token is invalid." });

        link.CaregiverUserId = account.Id;
        link.AcceptedAt = DateTimeOffset.UtcNow;
        link.ConsentGrantedAt = link.AcceptedAt;
        await database.SaveChangesAsync(cancellationToken);
        database.AccountSecurityLogs.Add(new AccountSecurityLog { UserId = link.Patient!.UserId, ResourceId = link.PatientId, EventType = "caregiver_consent_accepted", Succeeded = true });
        await database.SaveChangesAsync(cancellationToken);
        return Ok(new { link.Id, link.PatientId, link.CanViewAppointments, link.CanViewMedicalRecords, link.CanViewMedicationReminders });
    }

    [HttpPut("{id:guid}/consent"), Authorize(Roles = "Patient")]
    public async Task<IActionResult> UpdateConsent(Guid id, UpdateCaregiverConsentRequest request, CancellationToken cancellationToken)
    {
        var link = await database.CaregiverLinks.Include(x => x.Patient).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (link is null || link.Patient?.UserId != UserId) return NotFound();
        if (link.AcceptedAt is null || link.RevokedAt is not null) return Conflict(new { message = "Only an active caregiver connection can be updated." });
        link.CanViewAppointments = request.CanViewAppointments;
        link.CanViewMedicalRecords = request.CanViewMedicalRecords;
        link.CanViewMedicationReminders = request.CanViewMedicationReminders;
        link.ConsentGrantedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        database.AccountSecurityLogs.Add(new AccountSecurityLog { UserId = UserId, ResourceId = link.PatientId, EventType = "caregiver_consent_updated", Succeeded = true });
        await database.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}"), Authorize(Roles = "Patient")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        var link = await database.CaregiverLinks.Include(x => x.Patient).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (link is null || link.Patient?.UserId != UserId) return NotFound();
        if (link.RevokedAt is null)
        {
            link.RevokedAt = DateTimeOffset.UtcNow;
            link.CanViewAppointments = false;
            link.CanViewMedicalRecords = false;
            link.CanViewMedicationReminders = false;
            await database.SaveChangesAsync(cancellationToken);
            database.AccountSecurityLogs.Add(new AccountSecurityLog { UserId = UserId, ResourceId = link.PatientId, EventType = "caregiver_consent_revoked", Succeeded = true });
            await database.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }

    [HttpGet("mine"), Authorize(Roles = "Caregiver")]
    public async Task<IActionResult> ListPatients(CancellationToken cancellationToken)
    {
        var links = await database.CaregiverLinks.AsNoTracking()
            .Where(x => x.CaregiverUserId == UserId && x.AcceptedAt != null && x.RevokedAt == null)
            .Select(x => new CaregiverPatientView(x.Id, x.PatientId, x.Patient!.User!.FullName,
                x.CanViewAppointments, x.CanViewMedicalRecords, x.CanViewMedicationReminders))
            .ToListAsync(cancellationToken);
        return Ok(links);
    }

    [HttpGet("{id:guid}/records"), Authorize(Roles = "Caregiver")]
    public async Task<IActionResult> SharedRecords(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var link = await database.CaregiverLinks.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == id && x.CaregiverUserId == UserId && x.AcceptedAt != null && x.RevokedAt == null && x.CanViewMedicalRecords, cancellationToken);
        if (link is null) return NotFound();
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.MedicalRecords.AsNoTracking().Where(x => x.PatientId == link.PatientId);
        var total = await query.CountAsync(cancellationToken);
        var records = await query.OrderByDescending(x => x.RecordedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.RecordType, x.Summary, x.RecordedAt })
            .ToListAsync(cancellationToken);
        await AuditReadAsync("caregiver_records_read", link.PatientId, cancellationToken);
        return Ok(new { items = records, page, pageSize, total });
    }

    [HttpGet("{id:guid}/appointments"), Authorize(Roles = "Caregiver")]
    public async Task<IActionResult> SharedAppointments(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var link = await database.CaregiverLinks.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == id && x.CaregiverUserId == UserId && x.AcceptedAt != null && x.RevokedAt == null && x.CanViewAppointments, cancellationToken);
        if (link is null) return NotFound();
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.Appointments.AsNoTracking().Where(x => x.PatientId == link.PatientId);
        var total = await query.CountAsync(cancellationToken);
        var appointments = await query.OrderByDescending(x => x.Schedule!.StartsAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, DoctorName = x.Doctor!.User!.FullName, x.Schedule!.StartsAt, x.Schedule.EndsAt, Status = x.Status.ToString() })
            .ToListAsync(cancellationToken);
        await AuditReadAsync("caregiver_appointments_read", link.PatientId, cancellationToken);
        return Ok(new { items = appointments, page, pageSize, total });
    }

    private async Task AuditReadAsync(string eventType, Guid patientId, CancellationToken cancellationToken)
    {
        database.AccountSecurityLogs.Add(new AccountSecurityLog { UserId = UserId, ResourceId = patientId, EventType = eventType, Succeeded = true });
        await database.SaveChangesAsync(cancellationToken);
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public sealed record InviteCaregiverRequest(
    [property: System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.EmailAddress, System.ComponentModel.DataAnnotations.StringLength(256)] string Email,
    bool CanViewAppointments,
    bool CanViewMedicalRecords,
    bool CanViewMedicationReminders = false);
public sealed record AcceptCaregiverInvitationRequest([property: System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(128, MinimumLength = 20)] string Token);
public sealed record UpdateCaregiverConsentRequest(bool CanViewAppointments, bool CanViewMedicalRecords, bool CanViewMedicationReminders = false);
public sealed record CaregiverLinkView(Guid Id, string InviteEmail, string? CaregiverName, DateTimeOffset ExpiresAt, DateTimeOffset? AcceptedAt,
    DateTimeOffset? RevokedAt, DateTimeOffset? ConsentGrantedAt, bool CanViewAppointments, bool CanViewMedicalRecords, bool CanViewMedicationReminders);
public sealed record CaregiverPatientView(Guid LinkId, Guid PatientId, string PatientName, bool CanViewAppointments, bool CanViewMedicalRecords, bool CanViewMedicationReminders);
