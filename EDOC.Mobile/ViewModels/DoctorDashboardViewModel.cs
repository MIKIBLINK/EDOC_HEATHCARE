using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EDOC.Mobile.Models;
using EDOC.Mobile.Services;

namespace EDOC.Mobile.ViewModels;

public partial class DoctorDashboardViewModel : ObservableObject
{
    private readonly AuthSessionService _auth;
    private readonly EdocApiClient _api;
    private readonly LanguageService _language;

    public DoctorDashboardViewModel(AuthSessionService auth, EdocApiClient api, LanguageService language)
    {
        _auth = auth;
        _api = api;
        _language = language;
        _language.LanguageChanged += (_, _) => RefreshLabels();
        RefreshLabels();
    }

    [ObservableProperty] private DoctorWorkspaceProfile? profile;
    [ObservableProperty] private string message = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isSavingSchedule;
    [ObservableProperty] private DateTime scheduleDate = DateTime.Today.AddDays(1);
    [ObservableProperty] private TimeSpan scheduleStartTime = TimeSpan.FromHours(9);
    [ObservableProperty] private TimeSpan scheduleEndTime = TimeSpan.FromHours(10);

    public ObservableCollection<AppointmentCard> Appointments { get; } = [];
    public ObservableCollection<DoctorScheduleCard> UpcomingSchedules { get; } = [];
    public string Title => _language["doctor_dashboard"];
    public string Intro => _language["doctor_dashboard_intro"];
    public string AppointmentsTitle => _language["my_appointments"];
    public string AvailabilityTitle => _language["manage_availability"];
    public string ScheduleDateText => _language["schedule_date"];
    public string StartTimeText => _language["start_time"];
    public string EndTimeText => _language["end_time"];
    public string PublishScheduleText => _language["publish_schedule"];
    public string UpcomingAvailabilityText => _language["upcoming_availability"];
    public string PatientText => _language["patient"];
    public string VisitReasonText => _language["appointment_reason"];
    public string ConfirmText => _language["confirm_appointment"];
    public string DeclineText => _language["reject_appointment"];
    public string CompleteText => _language["complete_appointment"];
    public string AddClinicalRecordText => _language["clinical_add_record"];
    public string EmptyAppointmentsText => _language["no_appointments"];
    public string AvailableText => _language["available"];
    public string ReservedText => _language["reserved"];
    public string PendingVerificationText => _language["doctor_pending_verification"];
    public bool IsPendingVerification => Profile is { IsApproved: false };
    public string RefreshText => _language["refresh"];
    public DateTime MinimumScheduleDate => DateTime.Today;

    private void RefreshLabels()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Intro));
        OnPropertyChanged(nameof(AppointmentsTitle));
        OnPropertyChanged(nameof(AvailabilityTitle));
        OnPropertyChanged(nameof(ScheduleDateText));
        OnPropertyChanged(nameof(StartTimeText));
        OnPropertyChanged(nameof(EndTimeText));
        OnPropertyChanged(nameof(PublishScheduleText));
        OnPropertyChanged(nameof(UpcomingAvailabilityText));
        OnPropertyChanged(nameof(PatientText));
        OnPropertyChanged(nameof(VisitReasonText));
        OnPropertyChanged(nameof(ConfirmText));
        OnPropertyChanged(nameof(DeclineText));
        OnPropertyChanged(nameof(CompleteText));
        OnPropertyChanged(nameof(AddClinicalRecordText));
        OnPropertyChanged(nameof(EmptyAppointmentsText));
        OnPropertyChanged(nameof(AvailableText));
        OnPropertyChanged(nameof(ReservedText));
        OnPropertyChanged(nameof(PendingVerificationText));
        OnPropertyChanged(nameof(RefreshText));
    }

    partial void OnProfileChanged(DoctorWorkspaceProfile? value) => OnPropertyChanged(nameof(IsPendingVerification));

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Message = string.Empty;
        try
        {
            var token = await _auth.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                Message = _language["appointments_hint"];
                return;
            }

            Profile = await _api.GetDoctorWorkspaceAsync(token);
            if (!Profile.IsApproved)
            {
                Appointments.Clear();
                UpcomingSchedules.Clear();
                return;
            }
            var appointments = await _api.GetAppointmentsAsync(token);
            Appointments.Clear();
            foreach (var appointment in appointments) Appointments.Add(appointment);

            var start = DateTimeOffset.UtcNow;
            var schedules = await _api.GetDoctorSchedulesAsync(token, start, start.AddDays(30));
            UpcomingSchedules.Clear();
            foreach (var schedule in schedules.Take(6)) UpcomingSchedules.Add(schedule);
        }
        catch (HttpRequestException)
        {
            Message = _language["api_error"];
        }
        catch (InvalidDataException)
        {
            Message = _language["api_error"];
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PublishScheduleAsync()
    {
        if (IsSavingSchedule) return;
        var localStart = ScheduleDate.Date + ScheduleStartTime;
        var localEnd = ScheduleDate.Date + ScheduleEndTime;
        if (localStart <= DateTime.Now || localEnd <= localStart)
        {
            Message = _language["schedule_invalid"];
            return;
        }

        var token = await _auth.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            Message = _language["appointments_hint"];
            return;
        }

        IsSavingSchedule = true;
        Message = string.Empty;
        try
        {
            var startOffset = new DateTimeOffset(DateTime.SpecifyKind(localStart, DateTimeKind.Unspecified), TimeZoneInfo.Local.GetUtcOffset(localStart));
            var endOffset = new DateTimeOffset(DateTime.SpecifyKind(localEnd, DateTimeKind.Unspecified), TimeZoneInfo.Local.GetUtcOffset(localEnd));
            await _api.CreateDoctorScheduleAsync(token, startOffset.ToUniversalTime(), endOffset.ToUniversalTime());
            Message = _language["schedule_created"];
            await LoadAsync();
        }
        catch (HttpRequestException)
        {
            Message = _language["api_error"];
        }
        finally
        {
            IsSavingSchedule = false;
        }
    }

    [RelayCommand]
    private Task ConfirmAppointmentAsync(AppointmentCard appointment) => ChangeAppointmentStatusAsync(appointment, 1);

    [RelayCommand]
    private Task DeclineAppointmentAsync(AppointmentCard appointment) => ChangeAppointmentStatusAsync(appointment, 3);

    [RelayCommand]
    private Task CompleteAppointmentAsync(AppointmentCard appointment) => ChangeAppointmentStatusAsync(appointment, 2);

    [RelayCommand]
    private Task AddClinicalRecordAsync(AppointmentCard appointment) => Shell.Current.GoToAsync(nameof(ClinicalEntryPage),
        new Dictionary<string, object> { ["appointmentId"] = appointment.Id });

    private async Task ChangeAppointmentStatusAsync(AppointmentCard appointment, int status)
    {
        var token = await _auth.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            Message = _language["appointments_hint"];
            return;
        }

        try
        {
            await _api.UpdateAppointmentStatusAsync(token, appointment.Id, status);
            Message = _language["admin_action_success"];
            await LoadAsync();
        }
        catch (HttpRequestException)
        {
            Message = _language["api_error"];
        }
    }
}
