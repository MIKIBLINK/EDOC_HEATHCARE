using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EDOC.Mobile.Models;
using EDOC.Mobile.Services;

namespace EDOC.Mobile.ViewModels;

public partial class HospitalDetailsViewModel : ObservableObject, IQueryAttributable
{
    private readonly LanguageService language;

    public HospitalDetailsViewModel(LanguageService language)
    {
        this.language = language;
        language.LanguageChanged += (_, _) => RefreshLabels();
    }

    [ObservableProperty] private HospitalCard? hospital;
    public string ContactText => language["hospital_contact"];
    public string ServicesText => language["hospital_services"];
    public string HoursText => language["opening_hours"];
    public string VerificationText => language["facility_verification"];
    public string OperationStatusText => language["facility_operational_status"];
    public string SourceText => language["facility_source"];
    public string MapText => language["facility_map"];
    public bool HasVerifiedCoordinates => Hospital?.Latitude is not null && Hospital.Longitude is not null && Hospital.VerifiedOn is not null;
    public string VerificationStatusText => Hospital?.NssfStatus == "NSSF" ? language["nssf_verified"]
        : Hospital?.NssfStatus == "NON_NSSF" ? language["nssf_not_participating"]
        : Hospital?.NssfStatus == "INACTIVE" ? language["facility_inactive"] : language["verification_pending"];
    public string OperationalStatusLabel => Hospital?.OperationalStatus == "ACTIVE" ? language["facility_active"]
        : Hospital?.OperationalStatus == "INACTIVE" ? language["facility_inactive"] : language["verification_pending"];
    public string SourceDateText => Hospital?.SourcePublishedOn is { } date ? date.ToString("yyyy-MM-dd") : language["unknown"];

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Hospital", out var value) && value is HospitalCard details)
            Hospital = details;
    }

    private void RefreshLabels()
    {
        OnPropertyChanged(nameof(ContactText)); OnPropertyChanged(nameof(ServicesText)); OnPropertyChanged(nameof(HoursText));
        OnPropertyChanged(nameof(VerificationText)); OnPropertyChanged(nameof(SourceText)); OnPropertyChanged(nameof(MapText));
        OnPropertyChanged(nameof(OperationStatusText)); OnPropertyChanged(nameof(VerificationStatusText)); OnPropertyChanged(nameof(OperationalStatusLabel)); OnPropertyChanged(nameof(SourceDateText));
    }

    partial void OnHospitalChanged(HospitalCard? value)
    {
        OnPropertyChanged(nameof(HasVerifiedCoordinates));
        OnPropertyChanged(nameof(VerificationStatusText));
        OnPropertyChanged(nameof(OperationalStatusLabel));
        OnPropertyChanged(nameof(SourceDateText));
    }

    [RelayCommand]
    private async Task OpenMapAsync()
    {
        if (!HasVerifiedCoordinates || Hospital is null) return;
        var uri = new Uri($"https://www.google.com/maps/search/?api=1&query={Hospital.Latitude!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)},{Hospital.Longitude!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        await Launcher.Default.OpenAsync(uri);
    }
}
