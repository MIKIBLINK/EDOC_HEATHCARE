namespace EDOC.Mobile.Models;

public sealed record DoctorCard(Guid Id, string FullName, string? Qualifications, int YearsExperience, string Specialty, string? Hospital, decimal? ConsultationFee);
public sealed record AppointmentCard(Guid Id, Guid PatientId, Guid DoctorId, string DoctorName, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Status, bool IsTelehealth,
    string? PatientName = null, string? Reason = null)
{
    public bool CanCancel => Status is "Pending" or "Confirmed";
    public bool CanConfirm => Status == "Pending";
    public bool CanComplete => Status == "Confirmed";
    public bool CanAddClinicalRecord => Status is "Confirmed" or "Completed";
}
public sealed record DoctorScheduleSlot(Guid Id, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
public sealed record DoctorSpecialtyOption(Guid Id, string Name, string? NameKm = null);
public sealed record DoctorScheduleCard(Guid Id, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsAvailable)
{
    public bool IsReserved => !IsAvailable;
}
public sealed record DoctorWorkspaceProfile(Guid Id, string FullName, string? Qualifications, int YearsExperience, string Specialty,
    string? Hospital, decimal? ConsultationFee, bool IsApproved);
public sealed record AdminDashboardMetrics(int Patients, int Doctors, int Hospitals, int Appointments, int PendingAppointments,
    int CompletedAppointments, int UpcomingAppointments);
public sealed record AdminUserCard(Guid Id, string FullName, string? Email, string? PhoneNumber, bool IsActive, string[] Roles)
{
    public string RoleSummary => string.Join(", ", Roles);
    public bool CanActivate => !IsActive;
    public bool CanDeactivate => IsActive;
}
public sealed record AdminDoctorCard(Guid Id, Guid UserId, string FullName, string? Email, string Specialty, string? Hospital,
    string? Qualifications, int YearsExperience, decimal? ConsultationFee, bool IsApproved, bool IsActive);
public sealed record PatientProfile(Guid Id, string FullName, string? Email, string? PhoneNumber, DateOnly? DateOfBirth, string? Gender, string? Address, string? EmergencyContactName, string? EmergencyContactPhone);
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
public sealed record MedicalRecordCard(Guid Id, Guid PatientId, Guid? DoctorId, string RecordType, string Summary, DateTimeOffset RecordedAt);
public sealed record ClinicalRecordCard(Guid Id, string RecordType, string Summary, DateTimeOffset RecordedAt, bool HasDocument,
    IReadOnlyList<DiagnosisCard> Diagnoses, IReadOnlyList<LabResultCard> LabResults, IReadOnlyList<PrescriptionCard> Prescriptions);
public sealed record DiagnosisCard(Guid Id, string? Code, string? CodeSystem, string Display, string ClinicalStatus, DateOnly? OnsetDate, string? Notes);
public sealed record LabResultCard(Guid Id, string TestName, string? ResultValue, string? Unit, string? ReferenceRange, string Status, DateTimeOffset CollectedAt, string? Notes);
public sealed record PrescriptionCard(Guid Id, string? LegacyMedication, string? LegacyInstructions, DateOnly? StartsOn, DateOnly? EndsOn, IReadOnlyList<PrescriptionItemCard> Items);
public sealed record PrescriptionItemCard(Guid Id, string MedicationName, string? Strength, string Dose, string Route, string Frequency, int? DurationDays, int? Quantity, string? Instructions);
public sealed record HealthMeasurementCard(Guid Id, string MetricType, decimal Value, decimal? SecondaryValue, string Unit, DateTimeOffset MeasuredAt, string? Notes,
    string? LocalizedMetricType = null)
{
    public string MetricLabel => LocalizedMetricType ?? MetricType;
    public string DisplayValue => MetricType == "BloodPressure" && SecondaryValue.HasValue
        ? $"{Value:0.#}/{SecondaryValue:0.#} {Unit}" : $"{Value:0.##} {Unit}";
}
public sealed record MedicationReminderCard(Guid Id, string MedicationName, string Dosage, string? Instructions,
    DateTimeOffset ScheduledAt, int? RepeatIntervalDays, string Status, string? LastDoseStatus, DateTimeOffset? ActionedAt,
    string? LocalizedStatus = null, string? LocalizedSchedule = null, string? LocalizedLastDoseStatus = null)
{
    public bool IsScheduled => Status == "Scheduled";
    public bool CanActionDose => IsScheduled && ScheduledAt <= DateTimeOffset.Now.AddMinutes(5);
    public bool CanCancel => IsScheduled;
    public string StatusLabel => LocalizedStatus ?? Status;
    public string ScheduleLabel => LocalizedSchedule ?? (RepeatIntervalDays.HasValue ? $"Every {RepeatIntervalDays} days" : "One time");
    public string LastDoseStatusLabel => LocalizedLastDoseStatus ?? LastDoseStatus ?? string.Empty;
}
public sealed record HospitalCard(Guid Id, string Name, string? Address, string? Phone, string? Email, string? OpeningHours, IReadOnlyList<string> Services,
    string? NameKm = null, string FacilityType = "Unknown", string Ownership = "Unknown", string NssfStatus = "PENDING_VERIFICATION",
    string OperationalStatus = "PENDING_VERIFICATION", string? ProvinceCode = null,
    string? Province = null, string? ProvinceKm = null, string? District = null, string? Commune = null, double? Latitude = null,
    double? Longitude = null, string? Website = null, string? SourceUrl = null, DateOnly? SourcePublishedOn = null,
    DateOnly? VerifiedOn = null, bool IsActive = true)
{
    public string NssfStatusLabel { get; init; } = "Pending verification";
}
public sealed record DirectoryProvince(string Code, string Name, string NameKm);
public sealed record DirectoryFilterOption(string Value, string Label);
public sealed record AdminHospitalDirectoryCard(Guid Id, string Name, string? NameKm, string? Address, string? Phone,
    string? Email, string? Website, string? OpeningHours, string FacilityType, string Ownership, string NssfStatus, string OperationalStatus,
    string? ProvinceCode, string? Province, string? ProvinceKm, string? DistrictCode, string? CommuneCode, double? Latitude, double? Longitude,
    string? SourceUrl, DateOnly? SourcePublishedOn, DateOnly? VerifiedOn, bool IsActive,
    IReadOnlyList<string> Services, IReadOnlyList<Guid> SpecialtyIds)
{
    public string NssfStatusLabel { get; init; } = "Pending verification";
}
public sealed record AdminSpecialtyDirectoryCard(Guid Id, string Name, string? NameKm);
public sealed record AdminDirectoryAuditCard(Guid Id, string Action, string EntityType, Guid EntityId,
    string? Summary, string? ActorName, DateTimeOffset CreatedAt);
public sealed record AdminDirectoryHospitalRequest(string Name, string? NameKm, string? Address, string? Phone,
    string? Email, string? Website, string? OpeningHours, string FacilityType, string Ownership, string NssfStatus,
    string OperationalStatus, string ProvinceCode, string? DistrictCode, string? CommuneCode, double? Latitude,
    double? Longitude, string? SourceUrl, DateOnly? SourcePublishedOn, DateOnly? VerifiedOn, bool IsActive,
    Guid[]? SpecialtyIds);
public sealed record AdminDirectorySpecialtyRequest(string Name, string? NameKm);
public sealed record CaregiverPatientCard(Guid LinkId, Guid PatientId, string PatientName, bool CanViewAppointments, bool CanViewMedicalRecords, bool CanViewMedicationReminders);
public sealed record SharedMedicalRecordCard(Guid Id, string RecordType, string Summary, DateTimeOffset RecordedAt);
public sealed record SharedAppointmentCard(Guid Id, string DoctorName, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Status);
public sealed record CaregiverConnectionCard(Guid Id, string InviteEmail, string? CaregiverName, DateTimeOffset ExpiresAt, DateTimeOffset? AcceptedAt,
    DateTimeOffset? RevokedAt, DateTimeOffset? ConsentGrantedAt, bool CanViewAppointments, bool CanViewMedicalRecords, bool CanViewMedicationReminders);
