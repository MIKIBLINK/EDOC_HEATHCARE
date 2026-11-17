using System.ComponentModel.DataAnnotations;

namespace EDOC.Api.DTOs;

public sealed record RegisterRequest(
    [param: Required, StringLength(160, MinimumLength = 2)] string FullName,
    [param: Required, EmailAddress] string Email,
    [param: Required, MinLength(10)] string Password,
    [param: Required, RegularExpression("^(?:\\+[1-9][0-9]{7,14}|0[0-9]{8,9})$")] string PhoneNumber,
    [param: Required] string ConfirmPassword,
    [param: RegularExpression("^(Patient|Doctor|Caregiver)$")] string AccountType = "Patient",
    Guid? SpecialtyId = null,
    string? Qualifications = null);

public sealed record LoginRequest
{
    public string? Identifier { get; init; }
    // Kept for clients that used the original email-only login contract.
    public string? Email { get; init; }
    [Required] public string Password { get; init; } = string.Empty;
    public string LoginIdentifier => string.IsNullOrWhiteSpace(Identifier) ? Email ?? string.Empty : Identifier;
}
public sealed record RefreshRequest([param: Required] string RefreshToken);
public sealed record VerifyEmailRequest([param: Required] Guid UserId, [param: Required] string Token);
public sealed record ForgotPasswordRequest([param: Required, EmailAddress] string Email);
public sealed record ResetPasswordRequest([param: Required, EmailAddress] string Email, [param: Required] string Token,
    [param: Required, MinLength(10)] string Password, [param: Required] string ConfirmPassword);
public sealed record TokenResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt, string FullName, string[] Roles);
public sealed record PatientSummary(Guid Id, string FullName, string? Email, string? PhoneNumber, DateOnly? DateOfBirth, string? Gender, string? Address, string? EmergencyContactName, string? EmergencyContactPhone);
public sealed record DoctorSummary(Guid Id, string FullName, string? Qualifications, int YearsExperience, string Specialty, string? Hospital, decimal? ConsultationFee);
public sealed record AdminDoctorSummary(Guid Id, Guid UserId, string FullName, string? Email, string Specialty, string? Hospital,
    string? Qualifications, int YearsExperience, decimal? ConsultationFee, bool IsApproved, bool IsActive);
public sealed record DoctorWorkspaceProfile(Guid Id, string FullName, string? Qualifications, int YearsExperience, string Specialty,
    string? Hospital, decimal? ConsultationFee, bool IsApproved);
public sealed record AppointmentRequest([param: Required] Guid DoctorId, [param: Required] Guid ScheduleId, [param: StringLength(1000)] string? Reason, bool IsTelehealth = false);
public sealed record AppointmentSummary(Guid Id, Guid PatientId, Guid DoctorId, string DoctorName, string PatientName,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Status, bool IsTelehealth, string? Reason);
