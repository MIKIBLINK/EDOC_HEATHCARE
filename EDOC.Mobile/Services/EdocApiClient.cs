using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EDOC.Mobile.Models;

namespace EDOC.Mobile.Services;

public sealed class EdocApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<DoctorCard>> GetDoctorsAsync(string? search = null, Guid? specialtyId = null,
        Guid? hospitalId = null, CancellationToken cancellationToken = default)
    {
        var query = new List<string> { "page=1", "pageSize=100" };
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        if (specialtyId.HasValue) query.Add($"specialtyId={specialtyId.Value}");
        if (hospitalId.HasValue) query.Add($"hospitalId={hospitalId.Value}");
        using var response = await httpClient.GetAsync($"api/doctors?{string.Join("&", query)}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<DoctorCard>>(JsonOptions, cancellationToken);
        return page?.Items ?? [];
    }

    public async Task<IReadOnlyList<DoctorSpecialtyOption>> GetDoctorSpecialtiesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/doctors/specialties", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<DoctorSpecialtyOption>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<HospitalCard>> GetHospitalsAsync(string? search = null, string? provinceCode = null,
        string? nssfStatus = null, string? facilityType = null, string? ownership = null, CancellationToken cancellationToken = default)
    {
        var filters = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) filters.Add($"search={Uri.EscapeDataString(search.Trim())}");
        if (!string.IsNullOrWhiteSpace(provinceCode)) filters.Add($"provinceCode={Uri.EscapeDataString(provinceCode)}");
        if (!string.IsNullOrWhiteSpace(nssfStatus)) filters.Add($"nssfStatus={Uri.EscapeDataString(nssfStatus)}");
        if (!string.IsNullOrWhiteSpace(facilityType)) filters.Add($"facilityType={Uri.EscapeDataString(facilityType)}");
        if (!string.IsNullOrWhiteSpace(ownership)) filters.Add($"ownership={Uri.EscapeDataString(ownership)}");

        const int pageSize = 100;
        var hospitals = new List<HospitalCard>();
        var pageNumber = 1;
        var total = int.MaxValue;
        while (hospitals.Count < total)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var query = string.Join("&", new[] { $"page={pageNumber}", $"pageSize={pageSize}" }.Concat(filters));
            using var response = await httpClient.GetAsync($"api/hospitals?{query}", cancellationToken);
            response.EnsureSuccessStatusCode();
            var page = await response.Content.ReadFromJsonAsync<PagedResponse<HospitalCard>>(JsonOptions, cancellationToken);
            if (page is null || page.Items.Count == 0) break;
            hospitals.AddRange(page.Items);
            total = page.Total;
            pageNumber++;
        }
        return hospitals;
    }

    public async Task<IReadOnlyList<DirectoryProvince>> GetDirectoryProvincesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/hospitals/provinces", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<DirectoryProvince>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<AppointmentCard>> GetAppointmentsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/appointments?page=1&pageSize=20");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<AppointmentCard>>(JsonOptions, cancellationToken);
        return page?.Items ?? [];
    }

    public async Task<IReadOnlyList<MedicalRecordCard>> GetMedicalRecordsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/medical-records?page=1&pageSize=30");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<MedicalRecordCard>>(JsonOptions, cancellationToken);
        return page?.Items ?? [];
    }

    public async Task<IReadOnlyList<ClinicalRecordCard>> GetClinicalRecordsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/clinical-records/me?page=1&pageSize=30");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ClinicalRecordCard>>(JsonOptions, cancellationToken);
        return page?.Items ?? [];
    }

    public async Task<IReadOnlyList<HealthMeasurementCard>> GetHealthMeasurementsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/health/measurements/me?page=1&pageSize=50");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<HealthMeasurementCard>>(JsonOptions, cancellationToken);
        return page?.Items ?? [];
    }

    public async Task<IReadOnlyList<MedicationReminderCard>> GetMedicationRemindersAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/health/reminders/me?page=1&pageSize=50");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<MedicationReminderCard>>(JsonOptions, cancellationToken);
        return page?.Items ?? [];
    }

    public async Task AddHealthMeasurementAsync(string accessToken, string metricType, decimal value, decimal? secondaryValue,
        string unit, DateTimeOffset measuredAt, string? notes, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/health/measurements")
        {
            Content = JsonContent.Create(new { metricType, value, secondaryValue, unit, measuredAt, notes }, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task AddMedicationReminderAsync(string accessToken, string medicationName, string dosage,
        string? instructions, DateTimeOffset scheduledAt, int? repeatIntervalDays, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/health/reminders")
        {
            Content = JsonContent.Create(new { medicationName, dosage, instructions, scheduledAt, repeatIntervalDays }, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateMedicationReminderAsync(string accessToken, Guid reminderId, string status, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"api/health/reminders/{reminderId}/status")
        {
            Content = JsonContent.Create(new { status }, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<DoctorScheduleSlot>> GetAvailableSlotsAsync(Guid doctorId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/doctors/{doctorId}/schedules?from={Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"))}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<DoctorScheduleSlot>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task BookAppointmentAsync(string accessToken, Guid doctorId, Guid scheduleId, string? reason, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/appointments")
        {
            Content = JsonContent.Create(new { doctorId, scheduleId, reason, isTelehealth = false }, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task CancelAppointmentAsync(string accessToken, Guid appointmentId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/appointments/{appointmentId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAppointmentStatusAsync(string accessToken, Guid appointmentId, int status, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"api/appointments/{appointmentId}/status")
        {
            Content = JsonContent.Create(new { status }, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task CreateClinicalRecordAsync(string accessToken, Guid appointmentId, int recordType, string first, string second,
        string third, string fourth, string notes, CancellationToken cancellationToken = default)
    {
        var (path, payload) = recordType switch
        {
            1 => ($"api/clinical-records/appointments/{appointmentId}/lab-results", (object)new { testName = first, resultValue = second, unit = third, status = fourth, notes }),
            2 => ($"api/clinical-records/appointments/{appointmentId}/prescriptions", (object)new { items = new[] { new { medicationName = first, dose = second, route = fourth, frequency = third, instructions = notes } } }),
            _ => ($"api/clinical-records/appointments/{appointmentId}/diagnoses", (object)new { display = first, clinicalStatus = string.IsNullOrWhiteSpace(second) ? "Active" : second, code = third, notes })
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(payload, options: JsonOptions) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<DoctorWorkspaceProfile> GetDoctorWorkspaceAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/doctors/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DoctorWorkspaceProfile>(JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("The doctor profile response was empty.");
    }

    public async Task<IReadOnlyList<DoctorScheduleCard>> GetDoctorSchedulesAsync(string accessToken, DateTimeOffset from,
        DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var path = $"api/doctors/me/schedules?from={Uri.EscapeDataString(from.ToUniversalTime().ToString("O"))}&to={Uri.EscapeDataString(to.ToUniversalTime().ToString("O"))}";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<DoctorScheduleCard>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task CreateDoctorScheduleAsync(string accessToken, DateTimeOffset startsAt, DateTimeOffset endsAt,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/doctors/me/schedules")
        {
            Content = JsonContent.Create(new { startsAt, endsAt }, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<AdminDashboardMetrics> GetAdminDashboardAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/admin/dashboard");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AdminDashboardMetrics>(JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("The administrator dashboard response was empty.");
    }

    public async Task<PagedResponse<AdminUserCard>> GetAdminUsersAsync(string accessToken, string? search = null,
        CancellationToken cancellationToken = default)
    {
        var path = "api/admin/users?page=1&pageSize=50" + (string.IsNullOrWhiteSpace(search) ? string.Empty : $"&search={Uri.EscapeDataString(search.Trim())}");
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResponse<AdminUserCard>>(JsonOptions, cancellationToken)
            ?? new PagedResponse<AdminUserCard>([], 1, 50, 0);
    }

    public async Task<PagedResponse<AdminDoctorCard>> GetAdminDoctorsAsync(string accessToken, string? search = null,
        bool? approved = null, CancellationToken cancellationToken = default)
    {
        var path = "api/admin/doctors?page=1&pageSize=50";
        if (!string.IsNullOrWhiteSpace(search)) path += $"&search={Uri.EscapeDataString(search.Trim())}";
        if (approved.HasValue) path += $"&approved={approved.Value.ToString().ToLowerInvariant()}";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResponse<AdminDoctorCard>>(JsonOptions, cancellationToken)
            ?? new PagedResponse<AdminDoctorCard>([], 1, 50, 0);
    }

    public async Task SetDoctorApprovalAsync(string accessToken, Guid doctorId, bool approved, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"api/admin/doctors/{doctorId}/approval")
        {
            Content = JsonContent.Create(new { approved }, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetUserActivationAsync(string accessToken, Guid userId, bool active, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"api/admin/users/{userId}/activation")
        {
            Content = JsonContent.Create(new { active }, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<PagedResponse<AdminHospitalDirectoryCard>> GetAdminHospitalsAsync(string accessToken,
        string? search = null, CancellationToken cancellationToken = default)
    {
        var path = "api/admin/directory/hospitals?page=1&pageSize=100";
        if (!string.IsNullOrWhiteSpace(search)) path += $"&search={Uri.EscapeDataString(search.Trim())}";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResponse<AdminHospitalDirectoryCard>>(JsonOptions, cancellationToken)
            ?? new PagedResponse<AdminHospitalDirectoryCard>([], 1, 100, 0);
    }

    public async Task SaveAdminHospitalAsync(string accessToken, Guid? hospitalId, AdminDirectoryHospitalRequest hospital,
        CancellationToken cancellationToken = default)
    {
        var method = hospitalId.HasValue ? HttpMethod.Put : HttpMethod.Post;
        var path = hospitalId.HasValue ? $"api/admin/directory/hospitals/{hospitalId.Value}" : "api/admin/directory/hospitals";
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(hospital, options: JsonOptions) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<AdminSpecialtyDirectoryCard>> GetAdminSpecialtiesAsync(string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/admin/directory/specialties");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<AdminSpecialtyDirectoryCard>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task<PagedResponse<AdminDirectoryAuditCard>> GetAdminDirectoryAuditAsync(string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/admin/directory/audit-logs?page=1&pageSize=50");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResponse<AdminDirectoryAuditCard>>(JsonOptions, cancellationToken)
            ?? new PagedResponse<AdminDirectoryAuditCard>([], 1, 50, 0);
    }

    public async Task SaveAdminSpecialtyAsync(string accessToken, Guid? specialtyId, AdminDirectorySpecialtyRequest specialty,
        CancellationToken cancellationToken = default)
    {
        var method = specialtyId.HasValue ? HttpMethod.Put : HttpMethod.Post;
        var path = specialtyId.HasValue ? $"api/admin/directory/specialties/{specialtyId.Value}" : "api/admin/directory/specialties";
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(specialty, options: JsonOptions) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<PatientProfile> GetMyProfileAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/patients/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PatientProfile>(JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("The patient profile response was empty.");
    }

    public async Task SaveMyProfileAsync(string accessToken, PatientProfileUpdate profile, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "api/patients/me")
        {
            Content = JsonContent.Create(profile, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try { using var response = await httpClient.GetAsync("health", cancellationToken); return response.IsSuccessStatusCode; }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return false; }
    }

    public async Task<IReadOnlyList<CaregiverPatientCard>> GetCaregiverPatientsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/caregiver-links/mine");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<CaregiverPatientCard>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task<PagedResponse<SharedMedicalRecordCard>> GetCaregiverRecordsAsync(string accessToken, Guid linkId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/caregiver-links/{linkId}/records?page=1&pageSize=50");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResponse<SharedMedicalRecordCard>>(JsonOptions, cancellationToken)
            ?? new PagedResponse<SharedMedicalRecordCard>([], 1, 50, 0);
    }

    public async Task<PagedResponse<ClinicalRecordCard>> GetCaregiverClinicalRecordsAsync(string accessToken, Guid linkId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/clinical-records/caregiver-links/{linkId}?page=1&pageSize=30");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResponse<ClinicalRecordCard>>(JsonOptions, cancellationToken)
            ?? new PagedResponse<ClinicalRecordCard>([], 1, 30, 0);
    }

    public async Task<PagedResponse<MedicationReminderCard>> GetCaregiverMedicationRemindersAsync(string accessToken, Guid linkId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/health/caregiver-links/{linkId}/reminders?page=1&pageSize=30");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResponse<MedicationReminderCard>>(JsonOptions, cancellationToken)
            ?? new PagedResponse<MedicationReminderCard>([], 1, 30, 0);
    }

    public async Task<PagedResponse<SharedAppointmentCard>> GetCaregiverAppointmentsAsync(string accessToken, Guid linkId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/caregiver-links/{linkId}/appointments?page=1&pageSize=50");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResponse<SharedAppointmentCard>>(JsonOptions, cancellationToken)
            ?? new PagedResponse<SharedAppointmentCard>([], 1, 50, 0);
    }

    public async Task<IReadOnlyList<CaregiverConnectionCard>> GetCaregiverLinksAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/caregiver-links");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<CaregiverConnectionCard>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task AcceptCaregiverInvitationAsync(string accessToken, Guid invitationId, string invitationToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/caregiver-links/{invitationId}/accept")
        {
            Content = JsonContent.Create(new { token = invitationToken }, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task InviteCaregiverAsync(string accessToken, string email, bool canViewAppointments, bool canViewMedicalRecords,
        bool canViewMedicationReminders = false, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/caregiver-links")
        {
            Content = JsonContent.Create(new { email, canViewAppointments, canViewMedicalRecords, canViewMedicationReminders }, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateCaregiverConsentAsync(string accessToken, Guid linkId, bool canViewAppointments, bool canViewMedicalRecords,
        bool canViewMedicationReminders = false, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"api/caregiver-links/{linkId}/consent")
        {
            Content = JsonContent.Create(new { canViewAppointments, canViewMedicalRecords, canViewMedicationReminders }, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RevokeCaregiverAsync(string accessToken, Guid linkId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/caregiver-links/{linkId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public sealed record PatientProfileUpdate(string FullName, string PhoneNumber, DateOnly? DateOfBirth, string? Gender, string? Address, string? EmergencyContactName, string? EmergencyContactPhone);
