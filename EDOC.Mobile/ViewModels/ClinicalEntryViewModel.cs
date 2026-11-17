using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EDOC.Mobile.Services;

namespace EDOC.Mobile.ViewModels;

public partial class ClinicalEntryViewModel(AuthSessionService auth, EdocApiClient api, LanguageService language) : ObservableObject, IQueryAttributable
{
    [ObservableProperty] private Guid appointmentId;
    [ObservableProperty] private int selectedRecordType;
    [ObservableProperty] private string firstField = string.Empty;
    [ObservableProperty] private string secondField = string.Empty;
    [ObservableProperty] private string thirdField = string.Empty;
    [ObservableProperty] private string fourthField = string.Empty;
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string message = string.Empty;

    public string Title => language["clinical_entry_title"];
    public string Intro => language["clinical_entry_intro"];
    public string RecordTypeLabel => language["clinical_record_type"];
    public string FirstFieldLabel => SelectedRecordType switch { 1 => language["clinical_lab_name"], 2 => language["clinical_medication"], _ => language["clinical_diagnosis"] };
    public string SecondFieldLabel => SelectedRecordType switch { 1 => language["clinical_result"], 2 => language["clinical_dose"], _ => language["clinical_status"] };
    public string ThirdFieldLabel => SelectedRecordType switch { 1 => language["clinical_unit"], 2 => language["clinical_frequency"], _ => language["clinical_code"] };
    public string FourthFieldLabel => SelectedRecordType switch { 1 => language["clinical_result_status"], 2 => language["clinical_route"], _ => string.Empty };
    public bool ShowFourthField => SelectedRecordType is 1 or 2;
    public string NotesLabel => language["clinical_notes"];
    public string SaveLabel => language["save"];
    public string[] RecordTypes => [language["clinical_diagnosis_type"], language["clinical_lab_type"], language["clinical_prescription_type"]];

    partial void OnSelectedRecordTypeChanged(int value)
    {
        OnPropertyChanged(nameof(FirstFieldLabel)); OnPropertyChanged(nameof(SecondFieldLabel)); OnPropertyChanged(nameof(ThirdFieldLabel)); OnPropertyChanged(nameof(FourthFieldLabel)); OnPropertyChanged(nameof(ShowFourthField));
        FirstField = SecondField = ThirdField = FourthField = Notes = string.Empty;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("appointmentId", out var value) && Guid.TryParse(value?.ToString(), out var id)) AppointmentId = id;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        if (AppointmentId == Guid.Empty || string.IsNullOrWhiteSpace(FirstField)
            || (SelectedRecordType == 1 && (string.IsNullOrWhiteSpace(SecondField) || string.IsNullOrWhiteSpace(FourthField)))
            || (SelectedRecordType == 2 && (string.IsNullOrWhiteSpace(SecondField) || string.IsNullOrWhiteSpace(ThirdField) || string.IsNullOrWhiteSpace(FourthField))))
        { Message = language["clinical_required"]; return; }
        if ((SelectedRecordType == 0 && !string.IsNullOrWhiteSpace(SecondField) && SecondField is not ("Active" or "Resolved" or "Historical"))
            || (SelectedRecordType == 1 && FourthField is not ("Normal" or "Abnormal" or "Pending" or "Cancelled")))
        { Message = language["clinical_status_invalid"]; return; }
        var token = await auth.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) { Message = language["appointments_hint"]; return; }
        IsBusy = true; Message = string.Empty;
        try
        {
            await api.CreateClinicalRecordAsync(token, AppointmentId, SelectedRecordType, FirstField.Trim(), SecondField.Trim(), ThirdField.Trim(), FourthField.Trim(), Notes.Trim());
            await Shell.Current.DisplayAlertAsync(language["success"], language["clinical_saved"], language["ok"]);
            await Shell.Current.GoToAsync("..");
        }
        catch (HttpRequestException) { Message = language["api_error"]; }
        finally { IsBusy = false; }
    }
}
