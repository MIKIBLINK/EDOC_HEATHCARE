using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using EDOC.Mobile.Models;
using EDOC.Mobile.Services;

namespace EDOC.Mobile.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly AuthSessionService auth;
    private readonly EdocApiClient api;
    private readonly LanguageService language;

    public ProfileViewModel(AuthSessionService auth, EdocApiClient api, LanguageService language)
    {
        this.auth = auth;
        this.api = api;
        this.language = language;
        language.LanguageChanged += (_, _) => RefreshLabels();
        isDarkMode = Preferences.Default.Get("dark_mode", false);
        notificationsEnabled = Preferences.Default.Get("notifications_enabled", true);
    }

    [ObservableProperty] private string message = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string fullName = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string phoneNumber = string.Empty;
    [ObservableProperty] private string dateOfBirth = string.Empty;
    [ObservableProperty] private string gender = string.Empty;
    [ObservableProperty] private string address = string.Empty;
    [ObservableProperty] private string emergencyContactName = string.Empty;
    [ObservableProperty] private string emergencyContactPhone = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsSignedOut))] private bool isSignedIn;
    [ObservableProperty] private ObservableCollection<CaregiverConnectionCard> caregiverLinks = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsPatientProfile))] private bool isPatient;
    [ObservableProperty] private string caregiverEmail = string.Empty;
    [ObservableProperty] private bool shareAppointments;
    [ObservableProperty] private bool shareMedicalRecords;
    [ObservableProperty] private bool shareMedicationReminders;

    [ObservableProperty] private bool isDarkMode;
    [ObservableProperty] private bool notificationsEnabled;

    public string Title => language["my_profile"];
    public string SignInText => language["sign_in"];
    public string SignOutText => language["sign_out"];
    public string SaveText => language["save"];
    public string FullNameText => language["full_name"];
    public string EmailText => language["email"];
    public string PhoneText => language["phone"];
    public string DateOfBirthText => language["date_of_birth"];
    public string GenderText => language["gender"];
    public string AddressText => language["address"];
    public string EmergencyContactText => language["emergency_contact"];
    public string DateFormatHint => language["date_format"];
    public bool IsSignedOut => !IsSignedIn;
    public bool IsPatientProfile => IsSignedIn && IsPatient;
    public string CaregiverAccessTitle => language["caregiver_access_section"];
    public string CaregiverInviteHint => language["caregiver_invite_hint"];
    public string ShareAppointmentsText => language["share_appointments"];
    public string ShareMedicalRecordsText => language["share_medical_records"];
    public string ShareMedicationRemindersText => language["share_medication_reminders"];
    public string InviteCaregiverText => language["invite_caregiver"];
    public string ToggleAppointmentAccessText => language["toggle_appointment_access"];
    public string ToggleRecordAccessText => language["toggle_record_access"];
    public string ToggleMedicationReminderAccessText => language["toggle_medication_reminder_access"];
    public string RevokeAccessText => language["revoke_access"];
    public string SettingsTitle => language["settings"];
    public string AppearanceTitle => language["appearance"];
    public string DarkModeLabel => language["dark_mode"];
    public string NotificationsLabel => language["notifications"];
    public string PrivacyTitle => language["privacy"];
    public string PrivacyMessage => language["privacy_message"];
    public string LanguageTitle => language["language"];
    public string CurrentLanguageText => language.Language == "km" ? language["khmer"] : language["english"];
    public string LanguageButtonText => language.Language == "km" ? language["english"] : language["khmer"];
    public string ProfileIntro => language["profile_intro"];

    private void RefreshLabels()
    {
        OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(SignInText)); OnPropertyChanged(nameof(SignOutText));
        OnPropertyChanged(nameof(CaregiverAccessTitle)); OnPropertyChanged(nameof(CaregiverInviteHint)); OnPropertyChanged(nameof(ShareAppointmentsText));
        OnPropertyChanged(nameof(ShareMedicalRecordsText)); OnPropertyChanged(nameof(ShareMedicationRemindersText)); OnPropertyChanged(nameof(InviteCaregiverText)); OnPropertyChanged(nameof(ToggleAppointmentAccessText));
        OnPropertyChanged(nameof(ToggleRecordAccessText)); OnPropertyChanged(nameof(RevokeAccessText));
        OnPropertyChanged(nameof(ToggleMedicationReminderAccessText));
        OnPropertyChanged(nameof(SaveText)); OnPropertyChanged(nameof(FullNameText)); OnPropertyChanged(nameof(EmailText));
        OnPropertyChanged(nameof(PhoneText)); OnPropertyChanged(nameof(DateOfBirthText)); OnPropertyChanged(nameof(GenderText));
        OnPropertyChanged(nameof(AddressText)); OnPropertyChanged(nameof(EmergencyContactText)); OnPropertyChanged(nameof(DateFormatHint));
        OnPropertyChanged(nameof(SettingsTitle)); OnPropertyChanged(nameof(AppearanceTitle)); OnPropertyChanged(nameof(DarkModeLabel));
        OnPropertyChanged(nameof(NotificationsLabel)); OnPropertyChanged(nameof(PrivacyTitle)); OnPropertyChanged(nameof(PrivacyMessage));
        OnPropertyChanged(nameof(LanguageTitle)); OnPropertyChanged(nameof(CurrentLanguageText)); OnPropertyChanged(nameof(LanguageButtonText)); OnPropertyChanged(nameof(ProfileIntro));
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var token = await auth.GetAccessTokenAsync();
        IsSignedIn = !string.IsNullOrWhiteSpace(token);
        if (!IsSignedIn) { Message = language["appointments_hint"]; return; }
        var roles = await auth.GetRolesAsync();
        IsPatient = roles.Contains("Patient", StringComparer.OrdinalIgnoreCase);
        if (!IsPatient)
        {
            FullName = await auth.GetDisplayNameAsync() ?? string.Empty;
            Message = roles.Contains("Caregiver", StringComparer.OrdinalIgnoreCase) ? language["caregiver_intro"] : language["role_profile_limited"];
            return;
        }
        try
        {
            var profile = await api.GetMyProfileAsync(token!);
            FullName = profile.FullName;
            Email = profile.Email ?? string.Empty;
            PhoneNumber = profile.PhoneNumber ?? string.Empty;
            DateOfBirth = profile.DateOfBirth?.ToString("yyyy-MM-dd") ?? string.Empty;
            Gender = profile.Gender ?? string.Empty;
            Address = profile.Address ?? string.Empty;
            EmergencyContactName = profile.EmergencyContactName ?? string.Empty;
            EmergencyContactPhone = profile.EmergencyContactPhone ?? string.Empty;
            CaregiverLinks = new ObservableCollection<CaregiverConnectionCard>(await api.GetCaregiverLinksAsync(token!));
            Message = string.Empty;
        }
        catch (HttpRequestException) { Message = language["offline"]; }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        if (!IsPatient) { Message = language["role_profile_limited"]; return; }
        if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(PhoneNumber))
        {
            Message = language["profile_required"];
            return;
        }
        if (!string.IsNullOrWhiteSpace(DateOfBirth) && !DateOnly.TryParseExact(DateOfBirth, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
        {
            Message = language["date_format"];
            return;
        }
        var token = await auth.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) { IsSignedIn = false; Message = language["appointments_hint"]; return; }
        IsBusy = true;
        try
        {
            DateOnly? birthDate = string.IsNullOrWhiteSpace(DateOfBirth) ? null : DateOnly.ParseExact(DateOfBirth, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            await api.SaveMyProfileAsync(token, new PatientProfileUpdate(FullName.Trim(), PhoneNumber.Trim(), birthDate,
                NullIfBlank(Gender), NullIfBlank(Address), NullIfBlank(EmergencyContactName), NullIfBlank(EmergencyContactPhone)));
            Message = language["profile_saved"];
        }
        catch (HttpRequestException) { Message = language["profile_save_error"]; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await auth.SignOutAsync();
        IsSignedIn = false;
        IsPatient = false;
        CaregiverLinks.Clear();
        FullName = Email = PhoneNumber = DateOfBirth = Gender = Address = EmergencyContactName = EmergencyContactPhone = string.Empty;
        Message = language["signed_out"];
    }

    [RelayCommand]
    private async Task SignInAsync() => await Shell.Current.GoToAsync(nameof(LoginPage));

    [RelayCommand]
    private async Task InviteCaregiverAsync()
    {
        var token = await auth.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) { Message = language["appointments_hint"]; return; }
        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(CaregiverEmail)
            || (!ShareAppointments && !ShareMedicalRecords && !ShareMedicationReminders))
        {
            Message = language["caregiver_invitation_error"];
            return;
        }
        IsBusy = true;
        try
        {
            await api.InviteCaregiverAsync(token, CaregiverEmail.Trim(), ShareAppointments, ShareMedicalRecords, ShareMedicationReminders);
            CaregiverEmail = string.Empty;
            Message = language["caregiver_invitation_sent"];
            CaregiverLinks = new ObservableCollection<CaregiverConnectionCard>(await api.GetCaregiverLinksAsync(token));
        }
        catch (HttpRequestException) { Message = language["caregiver_invitation_error"]; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ToggleCaregiverAppointmentsAsync(CaregiverConnectionCard link) => await UpdateCaregiverAsync(link, !link.CanViewAppointments, link.CanViewMedicalRecords, link.CanViewMedicationReminders);

    [RelayCommand]
    private async Task ToggleCaregiverRecordsAsync(CaregiverConnectionCard link) => await UpdateCaregiverAsync(link, link.CanViewAppointments, !link.CanViewMedicalRecords, link.CanViewMedicationReminders);

    [RelayCommand]
    private async Task ToggleCaregiverRemindersAsync(CaregiverConnectionCard link) => await UpdateCaregiverAsync(link, link.CanViewAppointments, link.CanViewMedicalRecords, !link.CanViewMedicationReminders);

    private async Task UpdateCaregiverAsync(CaregiverConnectionCard link, bool appointments, bool records, bool medicationReminders)
    {
        if (link.AcceptedAt is null || link.RevokedAt is not null) return;
        var token = await auth.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return;
        try
        {
            await api.UpdateCaregiverConsentAsync(token, link.Id, appointments, records, medicationReminders);
            CaregiverLinks = new ObservableCollection<CaregiverConnectionCard>(await api.GetCaregiverLinksAsync(token));
            Message = language["consent_updated"];
        }
        catch (HttpRequestException) { Message = language["caregiver_invitation_error"]; }
    }

    [RelayCommand]
    private async Task RevokeCaregiverAsync(CaregiverConnectionCard link)
    {
        var confirm = await Shell.Current.DisplayAlertAsync(language["revoke_access"], language["revoke_caregiver_confirm"], language["revoke_access"], language["cancel"]);
        if (!confirm) return;
        var token = await auth.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return;
        try
        {
            await api.RevokeCaregiverAsync(token, link.Id);
            CaregiverLinks = new ObservableCollection<CaregiverConnectionCard>(await api.GetCaregiverLinksAsync(token));
            Message = language["consent_revoked"];
        }
        catch (HttpRequestException) { Message = language["caregiver_invitation_error"]; }
    }

    [RelayCommand]
    private void ToggleLanguage() => language.Toggle();

    [RelayCommand]
    private async Task ShowPrivacyAsync() => await Shell.Current.DisplayAlertAsync(PrivacyTitle, PrivacyMessage, language["retry"]);

    partial void OnIsDarkModeChanged(bool value)
    {
        Preferences.Default.Set("dark_mode", value);
        if (Application.Current is { } app) app.UserAppTheme = value ? AppTheme.Dark : AppTheme.Light;
    }

    partial void OnNotificationsEnabledChanged(bool value) => Preferences.Default.Set("notifications_enabled", value);

    private static string? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
