using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EDOC.Mobile.Models;
using EDOC.Mobile.Services;

namespace EDOC.Mobile.ViewModels;

public partial class HospitalsViewModel : ObservableObject
{
    private readonly EdocApiClient api;
    private readonly LanguageService language;

    public HospitalsViewModel(EdocApiClient api, LanguageService language)
    {
        this.api = api;
        this.language = language;
        language.LanguageChanged += (_, _) => RefreshLabels();
    }

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private DirectoryProvince? selectedProvince;
    [ObservableProperty] private DirectoryFilterOption? selectedNssfStatus;
    [ObservableProperty] private DirectoryFilterOption? selectedFacilityType;
    [ObservableProperty] private DirectoryFilterOption? selectedOwnership;
    private bool filtersInitialized;
    private bool reloadPending;
    private bool rebuildingOptions;
    public ObservableCollection<HospitalCard> Hospitals { get; } = [];
    public ObservableCollection<HospitalCard> FilteredHospitals { get; } = [];
    public ObservableCollection<DirectoryProvince> Provinces { get; } = [];
    public ObservableCollection<DirectoryFilterOption> NssfStatuses { get; } = [];
    public ObservableCollection<DirectoryFilterOption> FacilityTypes { get; } = [];
    public ObservableCollection<DirectoryFilterOption> OwnershipTypes { get; } = [];
    public string Title => language["hospital_directory"];
    public string Intro => language["hospital_intro"];
    public string SearchHint => language["hospital_search_hint"];
    public string EmptyText => language["no_hospitals"];
    public string ContactText => language["hospital_contact"];
    public string ServicesText => language["hospital_services"];
    public string ProvinceFilterLabel => language["directory_province"];
    public string NssfFilterLabel => language["directory_nssf_status"];
    public string TypeFilterLabel => language["directory_facility_type"];
    public string OwnershipFilterLabel => language["directory_ownership"];
    public string VerificationPendingText => language["verification_pending"];
    public string DirectoryAllText => language["directory_all"];

    private void RefreshLabels()
    {
        OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(Intro)); OnPropertyChanged(nameof(SearchHint));
        OnPropertyChanged(nameof(EmptyText)); OnPropertyChanged(nameof(ContactText)); OnPropertyChanged(nameof(ServicesText));
        OnPropertyChanged(nameof(ProvinceFilterLabel)); OnPropertyChanged(nameof(NssfFilterLabel)); OnPropertyChanged(nameof(TypeFilterLabel));
        OnPropertyChanged(nameof(OwnershipFilterLabel));
        OnPropertyChanged(nameof(VerificationPendingText)); OnPropertyChanged(nameof(DirectoryAllText));
        RebuildOptions();
        RefreshFiltered();
    }

    partial void OnSearchTextChanged(string value) => RefreshFiltered();
    partial void OnSelectedProvinceChanged(DirectoryProvince? value) => RequestReloadForFilterChange();
    partial void OnSelectedNssfStatusChanged(DirectoryFilterOption? value) => RequestReloadForFilterChange();
    partial void OnSelectedFacilityTypeChanged(DirectoryFilterOption? value) => RequestReloadForFilterChange();
    partial void OnSelectedOwnershipChanged(DirectoryFilterOption? value) => RequestReloadForFilterChange();

    private void RequestReloadForFilterChange()
    {
        if (filtersInitialized && !rebuildingOptions) _ = LoadHospitalsAsync(SearchText);
    }

    private void RefreshFiltered()
    {
        var term = SearchText.Trim();
        var values = string.IsNullOrEmpty(term) ? Hospitals.ToList() : Hospitals.Where(x =>
            x.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)
            || (x.NameKm?.Contains(term, StringComparison.CurrentCultureIgnoreCase) ?? false)
            || (x.Address?.Contains(term, StringComparison.CurrentCultureIgnoreCase) ?? false)
            || (x.Province?.Contains(term, StringComparison.CurrentCultureIgnoreCase) ?? false)
            || (x.ProvinceKm?.Contains(term, StringComparison.CurrentCultureIgnoreCase) ?? false)).ToList();
        FilteredHospitals.Clear();
        foreach (var hospital in values) FilteredHospitals.Add(hospital with { NssfStatusLabel = NssfStatusLabel(hospital.NssfStatus) });
        if (!IsBusy && !string.IsNullOrWhiteSpace(StatusMessage) && StatusMessage != language["offline"])
            StatusMessage = values.Count == 0 ? EmptyText : string.Empty;
    }

    private string NssfStatusLabel(string? status) => status switch
    {
        "NSSF" => language["nssf_verified"],
        "NON_NSSF" => language["nssf_not_participating"],
        "INACTIVE" => language["facility_inactive"],
        _ => language["verification_pending"]
    };

    [RelayCommand]
    private async Task OpenDetailsAsync(HospitalCard hospital) =>
        await Shell.Current.GoToAsync(nameof(HospitalDetailsPage), new Dictionary<string, object> { ["Hospital"] = hospital });

    [RelayCommand]
    private async Task LoadAsync()
        => await LoadHospitalsAsync(string.Empty);

    [RelayCommand]
    private async Task SearchAsync()
        => await LoadHospitalsAsync(SearchText);

    private async Task LoadHospitalsAsync(string? search)
    {
        if (IsBusy) { reloadPending = true; return; }
        IsBusy = true;
        StatusMessage = language["loading"];
        try
        {
            if (Provinces.Count == 0)
            {
                var provinceRows = await api.GetDirectoryProvincesAsync();
                Provinces.Clear();
                Provinces.Add(new DirectoryProvince(string.Empty, DirectoryAllText, "គ្រប់ខេត្ត"));
                foreach (var province in provinceRows) Provinces.Add(province);
                SelectedProvince ??= Provinces.FirstOrDefault();
            }
            RebuildOptions();
            filtersInitialized = true;
            var values = await api.GetHospitalsAsync(search, SelectedProvince?.Code, SelectedNssfStatus?.Value,
                SelectedFacilityType?.Value, SelectedOwnership?.Value);
            Hospitals.Clear();
            foreach (var hospital in values) Hospitals.Add(hospital);
            RefreshFiltered();
            StatusMessage = FilteredHospitals.Count == 0 ? EmptyText : string.Empty;
        }
        catch (HttpRequestException)
        {
            Hospitals.Clear(); FilteredHospitals.Clear();
            StatusMessage = language["offline"];
        }
        catch (TaskCanceledException)
        {
            Hospitals.Clear(); FilteredHospitals.Clear();
            StatusMessage = language["offline"];
        }
        catch (JsonException)
        {
            Hospitals.Clear(); FilteredHospitals.Clear();
            StatusMessage = language["offline"];
        }
        finally { IsBusy = false; }
        if (reloadPending)
        {
            reloadPending = false;
            await LoadHospitalsAsync(SearchText);
        }
    }

    private void RebuildOptions()
    {
        var selectedStatus = SelectedNssfStatus?.Value;
        var selectedType = SelectedFacilityType?.Value;
        var selectedOwnership = SelectedOwnership?.Value;
        rebuildingOptions = true;
        try
        {
        NssfStatuses.Clear();
        NssfStatuses.Add(new DirectoryFilterOption(string.Empty, DirectoryAllText));
        NssfStatuses.Add(new DirectoryFilterOption("NSSF", language["nssf_verified"]));
        NssfStatuses.Add(new DirectoryFilterOption("NON_NSSF", language["nssf_not_participating"]));
        NssfStatuses.Add(new DirectoryFilterOption("PENDING_VERIFICATION", language["verification_pending"]));
        NssfStatuses.Add(new DirectoryFilterOption("INACTIVE", language["facility_inactive"]));
        FacilityTypes.Clear();
        FacilityTypes.Add(new DirectoryFilterOption(string.Empty, DirectoryAllText));
        FacilityTypes.Add(new DirectoryFilterOption("NationalHospital", language["facility_national_hospital"]));
        FacilityTypes.Add(new DirectoryFilterOption("ProvincialHospital", language["facility_provincial_hospital"]));
        FacilityTypes.Add(new DirectoryFilterOption("ReferralHospital", language["facility_referral_hospital"]));
        FacilityTypes.Add(new DirectoryFilterOption("SpecialistHospital", language["facility_specialist_hospital"]));
        FacilityTypes.Add(new DirectoryFilterOption("MedicalCenter", language["facility_medical_center"]));
        FacilityTypes.Add(new DirectoryFilterOption("Clinic", language["facility_clinic"]));
        OwnershipTypes.Clear();
        OwnershipTypes.Add(new DirectoryFilterOption(string.Empty, DirectoryAllText));
        OwnershipTypes.Add(new DirectoryFilterOption("Public", language["ownership_public"]));
        OwnershipTypes.Add(new DirectoryFilterOption("Private", language["ownership_private"]));
        OwnershipTypes.Add(new DirectoryFilterOption("Unknown", language["unknown"]));
        if (selectedStatus is not null) SelectedNssfStatus = NssfStatuses.FirstOrDefault(x => x.Value == selectedStatus);
        else SelectedNssfStatus = NssfStatuses.FirstOrDefault();
        if (selectedType is not null) SelectedFacilityType = FacilityTypes.FirstOrDefault(x => x.Value == selectedType);
        else SelectedFacilityType = FacilityTypes.FirstOrDefault();
        if (selectedOwnership is not null) SelectedOwnership = OwnershipTypes.FirstOrDefault(x => x.Value == selectedOwnership);
        else SelectedOwnership = OwnershipTypes.FirstOrDefault();
        }
        finally { rebuildingOptions = false; }
    }
}
