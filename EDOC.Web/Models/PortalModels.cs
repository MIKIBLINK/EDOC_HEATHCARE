using System.Text;
using System.Text.Json;

namespace EDOC.Web.Models;

public sealed record PortalTokenResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken, DateTimeOffset RefreshTokenExpiresAt, string FullName, string[] Roles)
{
    public string? Subject { get; init; }

    public static PortalTokenResponse? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        var result = JsonSerializer.Deserialize<PortalTokenResponse>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (result is null) return null;
        var segments = result.AccessToken.Split('.');
        if (segments.Length != 3) return result;
        try
        {
            var payload = segments[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            return result with { Subject = document.RootElement.TryGetProperty("sub", out var sub) ? sub.GetString() : null };
        }
        catch (FormatException) { return result; }
        catch (JsonException) { return result; }
    }
}

public sealed record AdminDashboardSummary(int Patients, int Doctors, int Hospitals, int Appointments,
    int PendingAppointments, int CompletedAppointments, int UpcomingAppointments)
{
    public int VerifiedDoctors { get; init; }
    public int PendingDoctors { get; init; }
    public int NssfHospitals { get; init; }
    public int NonNssfHospitals { get; init; }
    public int PendingNssfHospitals { get; init; }
    public int Staff { get; init; }
    public int TodayAppointments { get; init; }
    public int MonthlyRegistrations { get; init; }
}

public sealed record DirectoryPageResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public sealed record PortalHospital(Guid Id, string Name, string? NameKm, string? Address, string? Phone,
    string? Email, string? Website, string? OpeningHours, string FacilityType, string Ownership,
    string NssfStatus, string OperationalStatus, string? ProvinceCode, string? Province, string? ProvinceKm,
    string? DistrictCode, string? CommuneCode, double? Latitude, double? Longitude, string? SourceUrl,
    DateOnly? SourcePublishedOn, DateOnly? VerifiedOn, bool IsActive, IReadOnlyList<string> Services,
    IReadOnlyList<Guid> SpecialtyIds);

public sealed record PortalAppointment(Guid Id, Guid PatientId, Guid DoctorId, string DoctorName,
    string PatientName, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Status, bool IsTelehealth, string? Reason);

public sealed record AdminPortalUser(Guid Id, string FullName, string? Email, string? PhoneNumber, bool IsActive, string[] Roles)
{
    public bool CanAssignHospital => !Roles.Contains("Administrator", StringComparer.OrdinalIgnoreCase)
        && !Roles.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase)
        && !Roles.Contains("Doctor", StringComparer.OrdinalIgnoreCase);
}
public sealed record AdminPortalDoctor(Guid Id, Guid UserId, string FullName, string? Email, string Specialty,
    string? Hospital, string? Qualifications, int YearsExperience, decimal? ConsultationFee, bool IsApproved, bool IsActive);
public sealed record PortalHospitalOption(Guid Id, string Name, string? Province);
public sealed record AdminPortalSpecialty(Guid Id, string Name, string? NameKm);
public sealed record AdminPatientSummary(Guid PatientId, Guid UserId, string FullName, string? PhoneNumber,
    string? Email, bool IsActive, DateTimeOffset RegisteredAt);
public sealed record AdminStaffSummary(Guid UserId, string FullName, string? Email, string? PhoneNumber,
    bool IsActive, Guid HospitalId, string HospitalName, DateTimeOffset AssignedAt);
public sealed record PortalProvince(string Code, string Name, string? NameKm);
public sealed record PortalDistrict(string Code, string Name, string ProvinceCode);
public sealed record PortalCommune(string Code, string Name, string DistrictCode);
public sealed record DirectoryLocationOptions(IReadOnlyList<PortalProvince> Provinces,
    IReadOnlyList<PortalDistrict> Districts, IReadOnlyList<PortalCommune> Communes);
public sealed record PortalDirectoryAudit(Guid Id, string Action, string EntityType, Guid EntityId,
    string? Summary, string? ActorName, DateTimeOffset CreatedAt);

public sealed class SaveFacilityRequest
{
    public string Name { get; init; } = string.Empty;
    public string? NameKm { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Website { get; init; }
    public string? OpeningHours { get; init; }
    public string FacilityType { get; init; } = string.Empty;
    public string Ownership { get; init; } = string.Empty;
    public string NssfStatus { get; init; } = string.Empty;
    public string OperationalStatus { get; init; } = string.Empty;
    public string ProvinceCode { get; init; } = string.Empty;
    public string? DistrictCode { get; init; }
    public string? CommuneCode { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? SourceUrl { get; init; }
    public DateOnly? SourcePublishedOn { get; init; }
    public DateOnly? VerifiedOn { get; init; }
    public bool IsActive { get; init; }
    public Guid[]? SpecialtyIds { get; init; }
    public string[]? Services { get; init; }
}
