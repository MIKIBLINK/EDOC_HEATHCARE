namespace EDOC.Web.Models;

public sealed record HospitalAdminHospitalOption(Guid Id, string Name, string? NameKm, string? Address,
    string? Phone, string? Email, string FacilityType, string NssfStatus, string? Province, bool IsActive);
public sealed record HospitalAdminDashboard(Guid HospitalId, int Doctors, int VerifiedDoctors, int Staff,
    int TodayAppointments, int PendingAppointments, int UpcomingAppointments);
public sealed record HospitalAdminAppointment(Guid Id, Guid PatientId, string PatientName, Guid DoctorId,
    string DoctorName, string Specialty, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Status, string? Reason);
