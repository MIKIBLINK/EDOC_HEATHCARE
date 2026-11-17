using Microsoft.AspNetCore.Identity;

namespace EDOC.Api.Models;

public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}

public sealed class UserAccount : IdentityUser<Guid>, IAuditableEntity
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool RequireEmailVerification { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class UserRole : IdentityRole<Guid> { }

public sealed class Patient : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public UserAccount? User { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Address { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Patient-controlled, revocable access for one invited caregiver account.</summary>
public sealed class CaregiverLink : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }
    public string InviteEmail { get; set; } = string.Empty;
    public string NormalizedInviteEmail { get; set; } = string.Empty;
    public Guid? CaregiverUserId { get; set; }
    public UserAccount? CaregiverUser { get; set; }
    public string InvitationTokenHash { get; set; } = string.Empty;
    public DateTimeOffset InvitationExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? ConsentGrantedAt { get; set; }
    public bool CanViewAppointments { get; set; }
    public bool CanViewMedicalRecords { get; set; }
    public bool CanViewMedicationReminders { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Specialty : IAuditableEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameKm { get; set; }
    public string? Description { get; set; }
    public string? SourceKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Hospital : IAuditableEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? OpeningHours { get; set; }
    public string? NameKm { get; set; }
    public string FacilityType { get; set; } = "Unknown";
    public string Ownership { get; set; } = "Unknown";
    public string NssfStatus { get; set; } = "PENDING_VERIFICATION";
    public string OperationalStatus { get; set; } = "PENDING_VERIFICATION";
    public string? ProvinceCode { get; set; }
    public Province? Province { get; set; }
    public string? DistrictCode { get; set; }
    public District? District { get; set; }
    public string? CommuneCode { get; set; }
    public Commune? Commune { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Website { get; set; }
    public string? SourceRecordKey { get; set; }
    public string? DataSourceKey { get; set; }
    public DataSource? DataSource { get; set; }
    public string? SourceUrl { get; set; }
    public DateOnly? SourcePublishedOn { get; set; }
    public DateOnly? VerifiedOn { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<HospitalService> Services { get; set; } = new List<HospitalService>();
    public ICollection<HospitalSpecialty> Specialties { get; set; } = new List<HospitalSpecialty>();
}

public sealed class Province : IAuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameKm { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<District> Districts { get; set; } = new List<District>();
}

public sealed class District : IAuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string ProvinceCode { get; set; } = string.Empty;
    public Province? Province { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameKm { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<Commune> Communes { get; set; } = new List<Commune>();
}

public sealed class Commune : IAuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string DistrictCode { get; set; } = string.Empty;
    public District? District { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameKm { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class HospitalSpecialty : IAuditableEntity
{
    public Guid HospitalId { get; set; }
    public Hospital? Hospital { get; set; }
    public Guid SpecialtyId { get; set; }
    public Specialty? Specialty { get; set; }
    public string? SourceUrl { get; set; }
    public string? DataSourceKey { get; set; }
    public DataSource? DataSource { get; set; }
    public DateOnly? VerifiedOn { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DataSource : IAuditableEntity
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateOnly? PublishedOn { get; set; }
    public DateOnly RetrievedOn { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class NssfVerificationRecord : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid HospitalId { get; set; }
    public Hospital? Hospital { get; set; }
    public string Status { get; set; } = "PENDING_VERIFICATION";
    public string SourceUrl { get; set; } = string.Empty;
    public string? DataSourceKey { get; set; }
    public DataSource? DataSource { get; set; }
    public DateOnly CheckedOn { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Doctor : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public UserAccount? User { get; set; }
    public Guid SpecialtyId { get; set; }
    public Specialty? Specialty { get; set; }
    public Guid? HospitalId { get; set; }
    public Hospital? Hospital { get; set; }
    public string? Qualifications { get; set; }
    public int YearsExperience { get; set; }
    public string? Biography { get; set; }
    public decimal? ConsultationFee { get; set; }
    public bool IsApproved { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum AccountSecurityTokenPurpose { EmailVerification, PasswordReset }

public sealed class AccountSecurityToken : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public UserAccount? User { get; set; }
    public AccountSecurityTokenPurpose Purpose { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AccountSecurityLog : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public UserAccount? User { get; set; }
    public string EventType { get; set; } = string.Empty;
    public Guid? ResourceId { get; set; }
    public bool Succeeded { get; set; }
    public string? ClientAddress { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AuditLog : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid? ActorUserId { get; set; }
    public UserAccount? Actor { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string? Summary { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class HospitalService : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid HospitalId { get; set; }
    public Hospital? Hospital { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class HospitalStaffAssignment : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public UserAccount? User { get; set; }
    public Guid HospitalId { get; set; }
    public Hospital? Hospital { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum AppointmentStatus { Pending, Confirmed, Completed, Cancelled }

public sealed class DoctorSchedule : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Appointment : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }
    public Guid DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    public Guid ScheduleId { get; set; }
    public DoctorSchedule? Schedule { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public string? Reason { get; set; }
    public bool IsTelehealth { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class MedicalRecord : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }
    public Guid? DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    public Guid? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }
    public string RecordType { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? DocumentStorageKey { get; set; }
    public ICollection<Diagnosis> Diagnoses { get; set; } = new List<Diagnosis>();
    public ICollection<LabResult> LabResults { get; set; } = new List<LabResult>();
    public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Prescription : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid MedicalRecordId { get; set; }
    public MedicalRecord? MedicalRecord { get; set; }
    // Nullable to support structured multi-item prescriptions while preserving legacy rows.
    public string? Medication { get; set; }
    public string? Instructions { get; set; }
    public DateOnly? StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Diagnosis : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid MedicalRecordId { get; set; }
    public MedicalRecord? MedicalRecord { get; set; }
    public string? Code { get; set; }
    public string? CodeSystem { get; set; }
    public string Display { get; set; } = string.Empty;
    public string ClinicalStatus { get; set; } = "Active";
    public DateOnly? OnsetDate { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class LabResult : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid MedicalRecordId { get; set; }
    public MedicalRecord? MedicalRecord { get; set; }
    public string TestName { get; set; } = string.Empty;
    public string? ResultValue { get; set; }
    public string? Unit { get; set; }
    public string? ReferenceRange { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset CollectedAt { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PrescriptionItem : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid PrescriptionId { get; set; }
    public Prescription? Prescription { get; set; }
    public string MedicationName { get; set; } = string.Empty;
    public string? Strength { get; set; }
    public string Dose { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public int? DurationDays { get; set; }
    public int? Quantity { get; set; }
    public string? Instructions { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class HealthMeasurement : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }
    public string MetricType { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal? SecondaryValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTimeOffset MeasuredAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class MedicationReminder : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }
    public string MedicationName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public int? RepeatIntervalDays { get; set; }
    public string Status { get; set; } = "Scheduled";
    public string? LastDoseStatus { get; set; }
    public DateTimeOffset? ActionedAt { get; set; }
    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[] RowVersion { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Notification : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public UserAccount? User { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class EmergencyContact : IAuditableEntity
{
    public Guid Id { get; set; }
    public string RegionCode { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ConsultationSession : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? ProviderSessionId { get; set; }
    public string Status { get; set; } = "Scheduled";
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RefreshToken : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public UserAccount? User { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[] RowVersion { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
