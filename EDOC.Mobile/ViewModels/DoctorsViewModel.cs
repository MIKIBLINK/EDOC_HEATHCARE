using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EDOC.Mobile.Models;
using EDOC.Mobile.Services;

namespace EDOC.Mobile.ViewModels;

public partial class DoctorsViewModel : ObservableObject
{
    private readonly EdocApiClient api;
    private readonly AuthSessionService auth;
    private readonly LanguageService language;

    public DoctorsViewModel(EdocApiClient api, AuthSessionService auth, LanguageService language)
    {
        this.api = api;
        this.auth = auth;
        this.language = language;
        language.LanguageChanged += (_, _) => RefreshLabels();
    }

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private DirectoryFilterOption? selectedSpecialty;
    [ObservableProperty] private DirectoryFilterOption? selectedHospital;
    public ObservableCollection<DoctorCard> Doctors { get; } = [];
    public ObservableCollection<DoctorCard> FilteredDoctors { get; } = [];
    public ObservableCollection<DirectoryFilterOption> SpecialtyOptions { get; } = [];
    public ObservableCollection<DirectoryFilterOption> HospitalOptions { get; } = [];
    private bool filtersInitialized;
    private bool reloadPending;
    public string SearchTitle => language["search_doctors"];
    public string EmptyText => language["no_doctors"];
    public string BookText => language["book"];
    public string Intro => language["doctor_directory_intro"];
    public string SearchHint => language["doctor_search_hint"];
    public string DetailsText => language["details"];
    public string SpecialtyFilterLabel => language["specialty"];
    public string HospitalFilterLabel => language["hospital_affiliation"];

    private void RefreshLabels()
    {
        OnPropertyChanged(nameof(SearchTitle)); OnPropertyChanged(nameof(EmptyText)); OnPropertyChanged(nameof(BookText));
        OnPropertyChanged(nameof(Intro)); OnPropertyChanged(nameof(SearchHint)); OnPropertyChanged(nameof(DetailsText));
        OnPropertyChanged(nameof(SpecialtyFilterLabel)); OnPropertyChanged(nameof(HospitalFilterLabel));
        if (filtersInitialized) RebuildFilterLabels();
        RefreshFilteredDoctors();
    }

    partial void OnSearchTextChanged(string value) => RefreshFilteredDoctors();
    partial void OnSelectedSpecialtyChanged(DirectoryFilterOption? value) => RequestReloadForFilterChange();
    partial void OnSelectedHospitalChanged(DirectoryFilterOption? value) => RequestReloadForFilterChange();

    private void RequestReloadForFilterChange()
    {
        if (filtersInitialized) _ = LoadDoctorsAsync(SearchText);
    }

    private void RebuildFilterLabels()
    {
        var specialtyValue = SelectedSpecialty?.Value;
        var hospitalValue = SelectedHospital?.Value;
        if (SpecialtyOptions.Count > 0) SpecialtyOptions[0] = new DirectoryFilterOption(string.Empty, language["directory_all"]);
        if (HospitalOptions.Count > 0) HospitalOptions[0] = new DirectoryFilterOption(string.Empty, language["directory_all"]);
        SelectedSpecialty = SpecialtyOptions.FirstOrDefault(x => x.Value == specialtyValue) ?? SpecialtyOptions.FirstOrDefault();
        SelectedHospital = HospitalOptions.FirstOrDefault(x => x.Value == hospitalValue) ?? HospitalOptions.FirstOrDefault();
    }

    private void RefreshFilteredDoctors()
    {
        var term = SearchText.Trim();
        var filtered = string.IsNullOrEmpty(term)
            ? Doctors.ToList()
            : Doctors.Where(x => x.FullName.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                || x.Specialty.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                || (x.Hospital?.Contains(term, StringComparison.CurrentCultureIgnoreCase) ?? false)).ToList();
        FilteredDoctors.Clear();
        foreach (var doctor in filtered) FilteredDoctors.Add(doctor);
        if (!IsBusy) StatusMessage = filtered.Count == 0 ? EmptyText : string.Empty;
    }

    [RelayCommand]
    private async Task BookDoctorAsync(DoctorCard doctor) => await BookDoctorCoreAsync(doctor);

    public Task BookAsync(DoctorCard doctor) => BookDoctorCoreAsync(doctor);

    [RelayCommand]
    private async Task OpenDetailsAsync(DoctorCard doctor) =>
        await Shell.Current.GoToAsync(nameof(DoctorDetailsPage), new Dictionary<string, object> { ["Doctor"] = doctor });

    private async Task BookDoctorCoreAsync(DoctorCard doctor)
    {
        try
        {
            var token = await auth.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                await Shell.Current.DisplayAlertAsync(language["sign_in"], language["appointments_hint"], language["retry"]);
                await Shell.Current.GoToAsync(nameof(LoginPage));
                return;
            }

            var slots = await api.GetAvailableSlotsAsync(doctor.Id);
            if (slots.Count == 0)
            {
                await Shell.Current.DisplayAlertAsync(language["book_appointment"], language["no_slots"], language["retry"]);
                return;
            }

            var labels = slots.Select(slot => slot.StartsAt.ToLocalTime().ToString("ddd, dd MMM yyyy • HH:mm")).ToArray();
            var selected = await Shell.Current.DisplayActionSheetAsync(language["choose_time"], language["cancel"], null, labels);
            var index = Array.IndexOf(labels, selected);
            if (index < 0) return;

            var reason = await Shell.Current.DisplayPromptAsync(language["book_appointment"], language["reason_optional"], language["book"], language["cancel"], maxLength: 1000);
            if (reason is null) return;
            await api.BookAppointmentAsync(token, doctor.Id, slots[index].Id, reason);
            await Shell.Current.DisplayAlertAsync(language["book_appointment"], language["booking_success"], language["retry"]);
            await Shell.Current.GoToAsync("//appointments");
        }
        catch (HttpRequestException)
        {
            await Shell.Current.DisplayAlertAsync(language["book_appointment"], language["booking_error"], language["retry"]);
        }
        catch (TaskCanceledException)
        {
            await Shell.Current.DisplayAlertAsync(language["book_appointment"], language["api_error"], language["retry"]);
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
        => await LoadDoctorsAsync(string.Empty);

    [RelayCommand]
    private async Task SearchAsync()
        => await LoadDoctorsAsync(SearchText);

    private async Task LoadDoctorsAsync(string? search)
    {
        if (IsBusy) { reloadPending = true; return; }
        IsBusy = true;
        StatusMessage = language["loading"];
        try
        {
            if (!filtersInitialized)
            {
                var specialties = await api.GetDoctorSpecialtiesAsync();
                var hospitals = await api.GetHospitalsAsync();
                SpecialtyOptions.Clear();
                HospitalOptions.Clear();
                SpecialtyOptions.Add(new DirectoryFilterOption(string.Empty, language["directory_all"]));
                HospitalOptions.Add(new DirectoryFilterOption(string.Empty, language["directory_all"]));
                foreach (var specialty in specialties)
                    SpecialtyOptions.Add(new DirectoryFilterOption(specialty.Id.ToString(), language.Language == "km" && !string.IsNullOrWhiteSpace(specialty.NameKm) ? specialty.NameKm : specialty.Name));
                foreach (var hospital in hospitals)
                    HospitalOptions.Add(new DirectoryFilterOption(hospital.Id.ToString(), language.Language == "km" && !string.IsNullOrWhiteSpace(hospital.NameKm) ? hospital.NameKm : hospital.Name));
                SelectedSpecialty = SpecialtyOptions.FirstOrDefault();
                SelectedHospital = HospitalOptions.FirstOrDefault();
                filtersInitialized = true;
            }
            Guid? specialtyId = Guid.TryParse(SelectedSpecialty?.Value, out var parsedSpecialty) ? parsedSpecialty : null;
            Guid? hospitalId = Guid.TryParse(SelectedHospital?.Value, out var parsedHospital) ? parsedHospital : null;
            var doctors = await api.GetDoctorsAsync(search, specialtyId, hospitalId);
            Doctors.Clear();
            foreach (var doctor in doctors) Doctors.Add(doctor);
            RefreshFilteredDoctors();
        }
        catch (HttpRequestException)
        {
            Doctors.Clear(); FilteredDoctors.Clear();
            StatusMessage = language["offline"];
        }
        finally { IsBusy = false; }
        if (reloadPending)
        {
            reloadPending = false;
            await LoadDoctorsAsync(SearchText);
        }
    }
}
