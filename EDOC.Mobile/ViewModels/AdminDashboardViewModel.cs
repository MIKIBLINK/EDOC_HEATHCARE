using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EDOC.Mobile.Models;
using EDOC.Mobile.Services;

namespace EDOC.Mobile.ViewModels;

public partial class AdminDashboardViewModel : ObservableObject
{
    private readonly AuthSessionService _auth;
    private readonly EdocApiClient _api;
    private readonly LanguageService _language;

    public AdminDashboardViewModel(AuthSessionService auth, EdocApiClient api, LanguageService language)
    {
        _auth = auth;
        _api = api;
        _language = language;
        _language.LanguageChanged += (_, _) => RefreshLabels();
        RefreshLabels();
    }

    [ObservableProperty] private AdminDashboardMetrics? metrics;
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string message = string.Empty;
    [ObservableProperty] private bool isBusy;

    public ObservableCollection<AdminDoctorCard> PendingDoctors { get; } = [];
    public ObservableCollection<AdminUserCard> Users { get; } = [];
    public string Title => _language["admin_dashboard"];
    public string Intro => _language["admin_dashboard_intro"];
    public string OverviewTitle => _language["organization_overview"];
    public string PatientsText => _language["total_patients"];
    public string DoctorsText => _language["total_doctors"];
    public string HospitalsText => _language["total_hospitals"];
    public string AppointmentsText => _language["total_appointments"];
    public string PendingAppointmentsText => _language["pending_appointments"];
    public string CompletedAppointmentsText => _language["completed_appointments"];
    public string UpcomingAppointmentsText => _language["upcoming_appointments"];
    public string DoctorReviewTitle => _language["pending_doctor_review"];
    public string UserDirectoryTitle => _language["user_directory"];
    public string SearchHint => _language["search_accounts"];
    public string ApproveText => _language["approve_doctor"];
    public string DeclineText => _language["reject_doctor"];
    public string ActivateText => _language["activate_user"];
    public string DeactivateText => _language["deactivate_user"];
    public string NoPendingDoctorsText => _language["no_pending_doctors"];
    public string ActiveText => _language["account_active"];
    public string SuspendedText => _language["account_suspended"];
    public string RefreshText => _language["refresh"];
    public string NoUsersText => _language["no_users"];

    private void RefreshLabels()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Intro));
        OnPropertyChanged(nameof(OverviewTitle));
        OnPropertyChanged(nameof(PatientsText));
        OnPropertyChanged(nameof(DoctorsText));
        OnPropertyChanged(nameof(HospitalsText));
        OnPropertyChanged(nameof(AppointmentsText));
        OnPropertyChanged(nameof(PendingAppointmentsText));
        OnPropertyChanged(nameof(CompletedAppointmentsText));
        OnPropertyChanged(nameof(UpcomingAppointmentsText));
        OnPropertyChanged(nameof(DoctorReviewTitle));
        OnPropertyChanged(nameof(UserDirectoryTitle));
        OnPropertyChanged(nameof(SearchHint));
        OnPropertyChanged(nameof(ApproveText));
        OnPropertyChanged(nameof(DeclineText));
        OnPropertyChanged(nameof(ActivateText));
        OnPropertyChanged(nameof(DeactivateText));
        OnPropertyChanged(nameof(NoPendingDoctorsText));
        OnPropertyChanged(nameof(ActiveText));
        OnPropertyChanged(nameof(SuspendedText));
        OnPropertyChanged(nameof(RefreshText));
        OnPropertyChanged(nameof(NoUsersText));
    }

    [RelayCommand]
    private async Task LoadAsync() => await LoadCoreAsync(SearchText);

    [RelayCommand]
    private async Task SearchAsync() => await LoadCoreAsync(SearchText);

    private async Task LoadCoreAsync(string? search)
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

            Metrics = await _api.GetAdminDashboardAsync(token);
            var doctors = await _api.GetAdminDoctorsAsync(token, search, approved: false);
            PendingDoctors.Clear();
            foreach (var doctor in doctors.Items) PendingDoctors.Add(doctor);

            var users = await _api.GetAdminUsersAsync(token, search);
            Users.Clear();
            foreach (var user in users.Items) Users.Add(user);
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
    private Task ApproveDoctorAsync(AdminDoctorCard doctor) => SetDoctorApprovalAsync(doctor, true);

    [RelayCommand]
    private Task DeclineDoctorAsync(AdminDoctorCard doctor) => SetDoctorApprovalAsync(doctor, false);

    private async Task SetDoctorApprovalAsync(AdminDoctorCard doctor, bool approved)
    {
        var token = await _auth.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return;
        try
        {
            await _api.SetDoctorApprovalAsync(token, doctor.Id, approved);
            Message = _language["admin_action_success"];
            await LoadCoreAsync(SearchText);
        }
        catch (HttpRequestException)
        {
            Message = _language["api_error"];
        }
    }

    [RelayCommand]
    private Task ActivateUserAsync(AdminUserCard user) => SetUserActivationAsync(user, true);

    [RelayCommand]
    private Task DeactivateUserAsync(AdminUserCard user) => SetUserActivationAsync(user, false);

    private async Task SetUserActivationAsync(AdminUserCard user, bool active)
    {
        var token = await _auth.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return;
        try
        {
            await _api.SetUserActivationAsync(token, user.Id, active);
            var index = Users.IndexOf(user);
            if (index >= 0) Users[index] = user with { IsActive = active };
            Message = _language["admin_action_success"];
        }
        catch (HttpRequestException)
        {
            Message = _language["api_error"];
        }
    }
}
