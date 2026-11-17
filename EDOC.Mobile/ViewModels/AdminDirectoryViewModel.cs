using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EDOC.Mobile.Models;
using EDOC.Mobile.Services;

namespace EDOC.Mobile.ViewModels;

public partial class AdminDirectoryViewModel : ObservableObject
{
    private readonly AuthSessionService _auth;
    private readonly EdocApiClient _api;
    private readonly LanguageService _language;

    public AdminDirectoryViewModel(AuthSessionService auth, EdocApiClient api, LanguageService language)
    {
        _auth = auth;
        _api = api;
        _language = language;
        _language.LanguageChanged += (_, _) => RefreshLabels();
        RebuildOptions();
        for (var index = 0; index < Hospitals.Count; index++)
            Hospitals[index] = WithLocalizedNssfStatus(Hospitals[index]);
    }

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private AdminHospitalDirectoryCard? _selectedHospital;
    [ObservableProperty] private AdminSpecialtyDirectoryCard? _selectedSpecialty;
    [ObservableProperty] private DirectoryProvince? _selectedProvince;
    [ObservableProperty] private DirectoryFilterOption? _selectedFacilityType;
    [ObservableProperty] private DirectoryFilterOption? _selectedOwnership;
    [ObservableProperty] private DirectoryFilterOption? _selectedNssfStatus;
    [ObservableProperty] private DirectoryFilterOption? _selectedOperationalStatus;
    [ObservableProperty] private AdminSpecialtyDirectoryCard? _selectedHospitalSpecialty;
    [ObservableProperty] private string _hospitalName = string.Empty;
    [ObservableProperty] private string _hospitalNameKm = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _website = string.Empty;
    [ObservableProperty] private string _openingHours = string.Empty;
    [ObservableProperty] private string _sourceUrl = string.Empty;
    [ObservableProperty] private string _specialtyName = string.Empty;
    [ObservableProperty] private string _specialtyNameKm = string.Empty;
    [ObservableProperty] private DateTime _verifiedDate = DateTime.Today;
    [ObservableProperty] private bool _isEvidenceVerified;
    [ObservableProperty] private bool _isDirectoryEntryActive;

    public ObservableCollection<AdminHospitalDirectoryCard> Hospitals { get; } = [];
    public ObservableCollection<AdminSpecialtyDirectoryCard> Specialties { get; } = [];
    public ObservableCollection<AdminDirectoryAuditCard> AuditEntries { get; } = [];
    public ObservableCollection<DirectoryProvince> Provinces { get; } = [];
    public ObservableCollection<DirectoryFilterOption> FacilityTypes { get; } = [];
    public ObservableCollection<DirectoryFilterOption> OwnershipTypes { get; } = [];
    public ObservableCollection<DirectoryFilterOption> NssfStatuses { get; } = [];
    public ObservableCollection<DirectoryFilterOption> OperationalStatuses { get; } = [];

    public string Title => _language["admin_directory_title"];
    public string Intro => _language["admin_directory_intro"];
    public string HospitalSection => _language["admin_hospitals"];
    public string SpecialtySection => _language["admin_specialties"];
    public string AuditSection => _language["admin_directory_audit"];
    public string HospitalNameLabel => _language["admin_hospital_name"];
    public string KhmerNameLabel => _language["admin_hospital_name_km"];
    public string AddressLabel => _language["admin_hospital_address"];
    public string PhoneLabel => _language["admin_hospital_phone"];
    public string EmailLabel => _language["admin_hospital_email"];
    public string WebsiteLabel => _language["admin_hospital_website"];
    public string HoursLabel => _language["opening_hours"];
    public string SourceLabel => _language["admin_evidence_url"];
    public string VerifiedLabel => _language["admin_evidence_verified"];
    public string VerifiedDateLabel => _language["admin_verification_date"];
    public string ProvinceLabel => _language["directory_province"];
    public string TypeLabel => _language["directory_facility_type"];
    public string OwnershipLabel => _language["directory_ownership"];
    public string NssfLabel => _language["directory_nssf_status"];
    public string OperationLabel => _language["facility_operational_status"];
    public string SpecialtyLabel => _language["admin_specialty_attach"];
    public string SaveHospitalText => _language["admin_save_hospital"];
    public string NewHospitalText => _language["admin_new_hospital"];
    public string SaveSpecialtyText => _language["admin_save_specialty"];
    public string NewSpecialtyText => _language["admin_new_specialty"];
    public string SpecialtyNameLabel => _language["admin_specialty_name"];
    public string SpecialtyNameKmLabel => _language["admin_specialty_name_km"];
    public string ActiveDirectoryLabel => _language["admin_directory_enabled"];
    public string SearchHint => _language["hospital_search_hint"];
    public string PendingStatus => _language["verification_pending"];
    public string LoadingText => _language["loading"];
    public string NoHospitalsText => _language["no_hospitals"];
    public string NoSpecialtiesText => _language["admin_no_specialties"];
    public string NoAuditText => _language["admin_no_directory_audit"];

    public bool IsEditingHospital => SelectedHospital is not null;
    public bool IsEditingSpecialty => SelectedSpecialty is not null;

    private void RefreshLabels()
    {
        foreach (var property in new[] { nameof(Title), nameof(Intro), nameof(HospitalSection), nameof(SpecialtySection), nameof(AuditSection),
            nameof(HospitalNameLabel), nameof(KhmerNameLabel), nameof(AddressLabel), nameof(PhoneLabel), nameof(EmailLabel),
            nameof(WebsiteLabel), nameof(SourceLabel), nameof(VerifiedLabel), nameof(VerifiedDateLabel), nameof(ProvinceLabel),
            nameof(HoursLabel),
            nameof(TypeLabel), nameof(OwnershipLabel), nameof(NssfLabel), nameof(OperationLabel), nameof(SpecialtyLabel),
            nameof(SaveHospitalText), nameof(NewHospitalText), nameof(SaveSpecialtyText), nameof(NewSpecialtyText),
            nameof(SpecialtyNameLabel), nameof(SpecialtyNameKmLabel),
            nameof(ActiveDirectoryLabel), nameof(SearchHint), nameof(PendingStatus), nameof(LoadingText), nameof(NoHospitalsText),
            nameof(NoSpecialtiesText), nameof(NoAuditText) }) OnPropertyChanged(property);
        RebuildOptions();
    }

    partial void OnSelectedHospitalChanged(AdminHospitalDirectoryCard? value)
    {
        OnPropertyChanged(nameof(IsEditingHospital));
        if (value is null) return;
        HospitalName = value.Name;
        HospitalNameKm = value.NameKm ?? string.Empty;
        Address = value.Address ?? string.Empty;
        Phone = value.Phone ?? string.Empty;
        Email = value.Email ?? string.Empty;
        Website = value.Website ?? string.Empty;
        OpeningHours = value.OpeningHours ?? string.Empty;
        SourceUrl = value.SourceUrl ?? string.Empty;
        SelectedProvince = Provinces.FirstOrDefault(x => x.Code == value.ProvinceCode);
        SelectedFacilityType = FacilityTypes.FirstOrDefault(x => x.Value == value.FacilityType);
        SelectedOwnership = OwnershipTypes.FirstOrDefault(x => x.Value == value.Ownership);
        SelectedNssfStatus = NssfStatuses.FirstOrDefault(x => x.Value == value.NssfStatus);
        SelectedOperationalStatus = OperationalStatuses.FirstOrDefault(x => x.Value == value.OperationalStatus);
        IsDirectoryEntryActive = value.IsActive;
        IsEvidenceVerified = value.VerifiedOn is not null && value.SourceUrl is not null;
        VerifiedDate = value.VerifiedOn?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
        SelectedHospitalSpecialty = null;
    }

    partial void OnSelectedSpecialtyChanged(AdminSpecialtyDirectoryCard? value)
    {
        OnPropertyChanged(nameof(IsEditingSpecialty));
        if (value is null) return;
        SpecialtyName = value.Name;
        SpecialtyNameKm = value.NameKm ?? string.Empty;
    }

    partial void OnSelectedFacilityTypeChanged(DirectoryFilterOption? value) { }
    partial void OnSelectedOwnershipChanged(DirectoryFilterOption? value) { }
    partial void OnSelectedNssfStatusChanged(DirectoryFilterOption? value) { }
    partial void OnSelectedOperationalStatusChanged(DirectoryFilterOption? value) { }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Message = string.Empty;
        try
        {
            var token = await _auth.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token)) { Message = _language["appointments_hint"]; return; }
            if (Provinces.Count == 0)
            {
                foreach (var province in await _api.GetDirectoryProvincesAsync()) Provinces.Add(province);
            }
            await RefreshHospitalsAsync(token);
            await RefreshSpecialtiesAsync(token);
            await RefreshAuditAsync(token);
        }
        catch (HttpRequestException) { Message = _language["api_error"]; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var token = await _auth.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token)) { Message = _language["appointments_hint"]; return; }
            await RefreshHospitalsAsync(token);
        }
        catch (HttpRequestException) { Message = _language["api_error"]; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveHospitalAsync()
    {
        if (string.IsNullOrWhiteSpace(HospitalName) || SelectedProvince is null)
        { Message = _language["admin_required_hospital_fields"]; return; }
        var nssf = SelectedNssfStatus?.Value ?? "PENDING_VERIFICATION";
        var operational = SelectedOperationalStatus?.Value ?? "PENDING_VERIFICATION";
        var ownership = SelectedOwnership?.Value ?? "Unknown";
        var facilityType = SelectedFacilityType?.Value ?? "Unknown";
        var ownershipOrTypeChanged = SelectedHospital is null
            ? ownership != "Unknown" || facilityType != "Unknown"
            : ownership != SelectedHospital.Ownership || facilityType != SelectedHospital.FacilityType;
        var needsEvidence = nssf is "NSSF" or "NON_NSSF" || operational == "ACTIVE"
            || SelectedHospitalSpecialty is not null || ownershipOrTypeChanged;
        var verifiedOn = IsEvidenceVerified && needsEvidence ? DateOnly.FromDateTime(VerifiedDate) : SelectedHospital?.VerifiedOn;
        if (needsEvidence && (!IsEvidenceVerified || string.IsNullOrWhiteSpace(SourceUrl)))
        { Message = _language["admin_evidence_required"]; return; }

        var specialtyIds = SelectedHospital?.SpecialtyIds.ToList() ?? [];
        if (SelectedHospitalSpecialty is not null && !specialtyIds.Contains(SelectedHospitalSpecialty.Id))
            specialtyIds.Add(SelectedHospitalSpecialty.Id);
        var request = new AdminDirectoryHospitalRequest(HospitalName.Trim(), Clean(HospitalNameKm), Clean(Address),
            Clean(Phone), Clean(Email), Clean(Website), Clean(OpeningHours),
            facilityType, ownership, nssf, operational,
            SelectedProvince.Code, SelectedHospital?.DistrictCode, SelectedHospital?.CommuneCode,
            SelectedHospital?.Latitude, SelectedHospital?.Longitude, Clean(SourceUrl), SelectedHospital?.SourcePublishedOn,
            verifiedOn, IsDirectoryEntryActive, specialtyIds.ToArray());
        await SaveAsync(async token =>
        {
            await _api.SaveAdminHospitalAsync(token, SelectedHospital?.Id, request);
            Message = _language["admin_action_success"];
            SelectedHospital = null;
            ClearHospitalForm();
            await RefreshHospitalsAsync(token);
            await RefreshAuditAsync(token);
        });
    }

    [RelayCommand]
    private async Task SaveSpecialtyAsync()
    {
        if (string.IsNullOrWhiteSpace(SpecialtyName)) { Message = _language["admin_required_specialty_name"]; return; }
        var request = new AdminDirectorySpecialtyRequest(SpecialtyName.Trim(), Clean(SpecialtyNameKm));
        await SaveAsync(async token =>
        {
            await _api.SaveAdminSpecialtyAsync(token, SelectedSpecialty?.Id, request);
            Message = _language["admin_action_success"];
            SelectedSpecialty = null;
            SpecialtyName = SpecialtyNameKm = string.Empty;
            await RefreshSpecialtiesAsync(token);
            await RefreshAuditAsync(token);
        });
    }

    [RelayCommand]
    private void NewHospital()
    {
        SelectedHospital = null;
        ClearHospitalForm();
    }

    [RelayCommand]
    private void NewSpecialty()
    {
        SelectedSpecialty = null;
        SpecialtyName = SpecialtyNameKm = string.Empty;
    }

    [RelayCommand]
    private void SelectHospital(AdminHospitalDirectoryCard hospital) => SelectedHospital = hospital;

    [RelayCommand]
    private void SelectSpecialty(AdminSpecialtyDirectoryCard specialty) => SelectedSpecialty = specialty;

    private async Task SaveAsync(Func<string, Task> save)
    {
        if (IsBusy) return;
        IsBusy = true;
        Message = string.Empty;
        try
        {
            var token = await _auth.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token)) { Message = _language["appointments_hint"]; return; }
            await save(token);
        }
        catch (HttpRequestException) { Message = _language["admin_save_failed"]; }
        finally { IsBusy = false; }
    }

    private async Task RefreshHospitalsAsync(string token)
    {
        var result = await _api.GetAdminHospitalsAsync(token, SearchText);
        Hospitals.Clear();
        foreach (var hospital in result.Items) Hospitals.Add(WithLocalizedNssfStatus(hospital));
    }

    private AdminHospitalDirectoryCard WithLocalizedNssfStatus(AdminHospitalDirectoryCard hospital) => hospital with
    {
        NssfStatusLabel = hospital.NssfStatus switch
        {
            "NSSF" => _language["nssf_verified"],
            "NON_NSSF" => _language["nssf_not_participating"],
            "INACTIVE" => _language["facility_inactive"],
            _ => _language["verification_pending"]
        }
    };

    private async Task RefreshSpecialtiesAsync(string token)
    {
        var result = await _api.GetAdminSpecialtiesAsync(token);
        Specialties.Clear();
        foreach (var specialty in result) Specialties.Add(specialty);
    }

    private async Task RefreshAuditAsync(string token)
    {
        var result = await _api.GetAdminDirectoryAuditAsync(token);
        AuditEntries.Clear();
        foreach (var entry in result.Items) AuditEntries.Add(entry);
    }

    private void ClearHospitalForm()
    {
        HospitalName = HospitalNameKm = Address = Phone = Email = Website = OpeningHours = SourceUrl = string.Empty;
        SelectedProvince = null;
        SelectedFacilityType = FacilityTypes.FirstOrDefault(x => x.Value == "Unknown");
        SelectedOwnership = OwnershipTypes.FirstOrDefault(x => x.Value == "Unknown");
        SelectedNssfStatus = NssfStatuses.FirstOrDefault(x => x.Value == "PENDING_VERIFICATION");
        SelectedOperationalStatus = OperationalStatuses.FirstOrDefault(x => x.Value == "PENDING_VERIFICATION");
        SelectedHospitalSpecialty = null;
        VerifiedDate = DateTime.Today;
        IsEvidenceVerified = false;
        IsDirectoryEntryActive = true;
    }

    private void RebuildOptions()
    {
        FacilityTypes.Clear();
        FacilityTypes.Add(new("Unknown", _language["unknown"]));
        FacilityTypes.Add(new("NationalHospital", _language["facility_national_hospital"]));
        FacilityTypes.Add(new("ProvincialHospital", _language["facility_provincial_hospital"]));
        FacilityTypes.Add(new("ReferralHospital", _language["facility_referral_hospital"]));
        FacilityTypes.Add(new("SpecialistHospital", _language["facility_specialist_hospital"]));
        FacilityTypes.Add(new("Clinic", _language["facility_clinic"]));
        FacilityTypes.Add(new("MedicalCenter", _language["facility_medical_center"]));
        OwnershipTypes.Clear();
        OwnershipTypes.Add(new("Unknown", _language["unknown"]));
        OwnershipTypes.Add(new("Public", _language["ownership_public"]));
        OwnershipTypes.Add(new("Private", _language["ownership_private"]));
        NssfStatuses.Clear();
        NssfStatuses.Add(new("PENDING_VERIFICATION", _language["verification_pending"]));
        NssfStatuses.Add(new("NSSF", _language["nssf_verified"]));
        NssfStatuses.Add(new("NON_NSSF", _language["nssf_not_participating"]));
        OperationalStatuses.Clear();
        OperationalStatuses.Add(new("PENDING_VERIFICATION", _language["verification_pending"]));
        OperationalStatuses.Add(new("ACTIVE", _language["facility_active"]));
        OperationalStatuses.Add(new("INACTIVE", _language["facility_inactive"]));
    }

    private static string? Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
