using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EDOC.Mobile.Services;

namespace EDOC.Mobile.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly LanguageService language;
    private readonly EdocApiClient api;
    private readonly DoctorsViewModel doctors;

    public MainPageViewModel(LanguageService language, EdocApiClient api, DoctorsViewModel doctors)
    {
        this.language = language;
        this.api = api;
        this.doctors = doctors;
        language.LanguageChanged += (_, _) => RefreshTexts();
        RefreshTexts();
    }

    [ObservableProperty] private string welcomeText = string.Empty;
    [ObservableProperty] private string searchQuery = string.Empty;
    [ObservableProperty] private string homeText = string.Empty;
    [ObservableProperty] private string doctorsText = string.Empty;
    [ObservableProperty] private string appointmentsText = string.Empty;
    [ObservableProperty] private string profileText = string.Empty;
    [ObservableProperty] private string servicesText = string.Empty;
    [ObservableProperty] private string emergencyText = string.Empty;
    [ObservableProperty] private string emergencyNoteText = string.Empty;
    [ObservableProperty] private string languageButtonText = string.Empty;
    [ObservableProperty] private string availabilityText = string.Empty;
    [ObservableProperty] private string appointmentsHint = string.Empty;
    [ObservableProperty] private string hospitalsText = string.Empty;
    [ObservableProperty] private string recordsText = string.Empty;
    [ObservableProperty] private string telehealthText = string.Empty;
    [ObservableProperty] private string signInText = string.Empty;
    [ObservableProperty] private bool apiAvailable;

    public string BookText => language["book_appointment"];
    public string RecordsActionText => language["medical_records"];
    public string AssistantActionText => language["assistant"];
    public string ServicesSubtitle => language["services_subtitle"];
    public string WelcomeLabel => language["welcome_label"];
    public string HospitalsActionText => language["hospital_directory"];
    public string HealthActionText => language["health_title"];

    private void RefreshTexts()
    {
        WelcomeText = language["welcome"];
        HomeText = language["home"];
        DoctorsText = language["find_doctor"];
        AppointmentsText = language["upcoming"];
        ProfileText = language["profile"];
        ServicesText = language["services"];
        EmergencyText = language["emergency"];
        EmergencyNoteText = language["emergency_note"];
        LanguageButtonText = language["switch_language"];
        AvailabilityText = ApiAvailable ? language["today"] : language["offline"];
        OnPropertyChanged(nameof(BookText));
        OnPropertyChanged(nameof(RecordsActionText));
        OnPropertyChanged(nameof(AssistantActionText));
        OnPropertyChanged(nameof(ServicesSubtitle));
        OnPropertyChanged(nameof(WelcomeLabel));
        OnPropertyChanged(nameof(HospitalsActionText));
        OnPropertyChanged(nameof(HealthActionText));
    }

    [RelayCommand]
    private void ToggleLanguage()
    {
        language.Toggle();
        RefreshTexts();
    }

    [RelayCommand]
    private async Task CheckConnectionAsync()
    {
        ApiAvailable = await api.IsAvailableAsync();
        RefreshTexts();
    }

    [RelayCommand]
    private async Task ExploreDoctorsAsync() => await Shell.Current.GoToAsync("//doctors");

    [RelayCommand]
    private async Task SearchDoctorsAsync()
    {
        doctors.SearchText = SearchQuery;
        await doctors.SearchCommand.ExecuteAsync(null);
        await Shell.Current.GoToAsync("//doctors");
    }

    [RelayCommand]
    private async Task OpenAppointmentsAsync() => await Shell.Current.GoToAsync("//appointments");

    [RelayCommand]
    private async Task OpenAssistantAsync() => await Shell.Current.GoToAsync("//assistant");

    [RelayCommand]
    private async Task OpenRecordsAsync() => await Shell.Current.GoToAsync("records");

    [RelayCommand]
    private async Task OpenHospitalsAsync() => await Shell.Current.GoToAsync(nameof(HospitalsPage));

    [RelayCommand]
    private async Task OpenHealthAsync() => await Shell.Current.GoToAsync(nameof(HealthPage));

    [RelayCommand]
    private async Task OpenProfileAsync() => await Shell.Current.GoToAsync("//profile");

    [RelayCommand]
    private async Task SignInAsync() => await Shell.Current.GoToAsync(nameof(LoginPage));
}
