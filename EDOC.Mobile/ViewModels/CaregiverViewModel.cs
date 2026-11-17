using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EDOC.Mobile.Models;
using EDOC.Mobile.Services;

namespace EDOC.Mobile.ViewModels;

public partial class CaregiverViewModel : ObservableObject
{
    private readonly AuthSessionService auth;
    private readonly EdocApiClient api;
    private readonly LanguageService language;

    public CaregiverViewModel(AuthSessionService auth, EdocApiClient api, LanguageService language)
    {
        this.auth = auth;
        this.api = api;
        this.language = language;
        language.LanguageChanged += (_, _) => RefreshLabels();
    }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasNoPatients))] private ObservableCollection<CaregiverPatientCard> patients = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasNoRecords))] private ObservableCollection<ClinicalRecordCard> records = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasNoAppointments))] private ObservableCollection<SharedAppointmentCard> appointments = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasNoReminders))] private ObservableCollection<MedicationReminderCard> reminders = [];
    [ObservableProperty] private string message = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasSelectedPatient))] private string selectedPatientName = string.Empty;
    [ObservableProperty] private string invitationId = string.Empty;
    [ObservableProperty] private string invitationToken = string.Empty;
    [ObservableProperty] private bool isBusy;

    public string Title => language["caregiver_dashboard"];
    public string RefreshText => language["refresh"];
    public string AcceptInvitationTitle => language["accept_invitation"];
    public string InvitationIdText => language["invitation_id"];
    public string InvitationTokenText => language["invitation_token"];
    public string AcceptInvitationText => language["accept_caregiver_invite"];
    public string Intro => language["caregiver_intro"];
    public string PatientsTitle => language["authorized_patients"];
    public string RecordsTitle => language["shared_records"];
    public string AppointmentsTitle => language["shared_appointments"];
    public string RemindersTitle => language["medication_reminders"];
    public string RecordsButton => language["view_records"];
    public string AppointmentsButton => language["view_appointments"];
    public string RemindersButton => language["medication_reminders"];
    public string NoPatients => language["caregiver_no_patients"];
    public string NoRecords => language["no_shared_records"];
    public string NoAppointments => language["no_shared_appointments"];
    public string NoReminders => language["no_shared_reminders"];
    public bool HasNoPatients => Patients.Count == 0;
    public bool HasNoRecords => Records.Count == 0;
    public bool HasNoAppointments => Appointments.Count == 0;
    public bool HasNoReminders => Reminders.Count == 0;
    public bool HasSelectedPatient => !string.IsNullOrEmpty(SelectedPatientName);

    private void RefreshLabels()
    {
        foreach (var property in new[] { nameof(Title), nameof(RefreshText), nameof(AcceptInvitationTitle), nameof(InvitationIdText),
            nameof(InvitationTokenText), nameof(AcceptInvitationText), nameof(Intro), nameof(PatientsTitle), nameof(RecordsTitle),
            nameof(AppointmentsTitle), nameof(RemindersTitle), nameof(RecordsButton), nameof(AppointmentsButton), nameof(RemindersButton),
            nameof(NoPatients), nameof(NoRecords), nameof(NoAppointments), nameof(NoReminders) }) OnPropertyChanged(property);
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Message = string.Empty;
        try
        {
            var token = await auth.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token)) { Message = language["appointments_hint"]; Patients.Clear(); return; }
            Patients = new ObservableCollection<CaregiverPatientCard>(await api.GetCaregiverPatientsAsync(token));
        }
        catch (HttpRequestException) { Message = language["offline"]; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ShowRecordsAsync(CaregiverPatientCard patient)
    {
        if (!patient.CanViewMedicalRecords) { Message = language["caregiver_access_denied"]; return; }
        await LoadSharedDataAsync(patient, async token => await api.GetCaregiverClinicalRecordsAsync(token, patient.LinkId),
            result => Records = new ObservableCollection<ClinicalRecordCard>(result.Items));
    }

    [RelayCommand]
    private async Task ShowAppointmentsAsync(CaregiverPatientCard patient)
    {
        if (!patient.CanViewAppointments) { Message = language["caregiver_access_denied"]; return; }
        await LoadSharedDataAsync(patient, async token => await api.GetCaregiverAppointmentsAsync(token, patient.LinkId),
            result => Appointments = new ObservableCollection<SharedAppointmentCard>(result.Items));
    }

    [RelayCommand]
    private async Task ShowRemindersAsync(CaregiverPatientCard patient)
    {
        if (!patient.CanViewMedicationReminders) { Message = language["caregiver_access_denied"]; return; }
        await LoadSharedDataAsync(patient, async token => await api.GetCaregiverMedicationRemindersAsync(token, patient.LinkId),
            result => Reminders = new ObservableCollection<MedicationReminderCard>(result.Items.Select(x => x with
            {
                LocalizedStatus = TranslateReminderStatus(x.Status),
                LocalizedSchedule = language[x.RepeatIntervalDays switch { null => "health_repeat_once", 1 => "health_repeat_daily", 7 => "health_repeat_weekly", _ => "health_repeat_custom" }],
                LocalizedLastDoseStatus = x.LastDoseStatus is null ? null : TranslateReminderStatus(x.LastDoseStatus)
            })));
    }

    private string TranslateReminderStatus(string status) => language[status switch
    {
        "Scheduled" => "health_status_scheduled", "Taken" => "health_status_taken",
        "Missed" => "health_status_missed", "Cancelled" => "health_status_cancelled", _ => "health_status_scheduled"
    }];

    [RelayCommand]
    private async Task AcceptInvitationAsync()
    {
        if (!Guid.TryParse(InvitationId, out var id) || string.IsNullOrWhiteSpace(InvitationToken))
        {
            Message = language["invitation_invalid"];
            return;
        }
        var token = await auth.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) { Message = language["appointments_hint"]; return; }
        IsBusy = true;
        try
        {
            await api.AcceptCaregiverInvitationAsync(token, id, InvitationToken.Trim());
            InvitationId = InvitationToken = string.Empty;
            Message = language["invitation_accepted"];
            Patients = new ObservableCollection<CaregiverPatientCard>(await api.GetCaregiverPatientsAsync(token));
        }
        catch (HttpRequestException) { Message = language["invitation_invalid"]; }
        finally { IsBusy = false; }
    }

    private async Task LoadSharedDataAsync<T>(CaregiverPatientCard patient, Func<string, Task<PagedResponse<T>>> load, Action<PagedResponse<T>> update)
    {
        if (IsBusy) return;
        IsBusy = true;
        Message = string.Empty;
        SelectedPatientName = patient.PatientName;
        try
        {
            var token = await auth.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token)) { Message = language["appointments_hint"]; return; }
            update(await load(token));
        }
        catch (HttpRequestException) { Message = language["caregiver_access_denied"]; }
        finally { IsBusy = false; }
    }
}
