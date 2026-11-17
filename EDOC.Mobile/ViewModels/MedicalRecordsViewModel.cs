using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EDOC.Mobile.Models;
using EDOC.Mobile.Services;

namespace EDOC.Mobile.ViewModels;

public partial class MedicalRecordsViewModel : ObservableObject
{
    private readonly AuthSessionService auth;
    private readonly EdocApiClient api;
    private readonly LanguageService language;

    public MedicalRecordsViewModel(AuthSessionService auth, EdocApiClient api, LanguageService language)
    {
        this.auth = auth;
        this.api = api;
        this.language = language;
        language.LanguageChanged += (_, _) => RefreshLabels();
    }

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool needsSignIn;
    [ObservableProperty] private string statusMessage = string.Empty;
    public ObservableCollection<ClinicalRecordCard> Records { get; } = [];
    public string Title => language["medical_records"];
    public string Intro => language["records_intro"];
    public string EmptyText => language["no_records"];
    public string SignInText => language["records_sign_in"];
    public string RecordTypeLabel => language["record_type"];
    public string RecordedLabel => language["recorded_on"];
    public string RetryText => language["retry"];

    private void RefreshLabels()
    {
        OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(Intro)); OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(SignInText)); OnPropertyChanged(nameof(RecordTypeLabel)); OnPropertyChanged(nameof(RecordedLabel));
        OnPropertyChanged(nameof(RetryText));
        if (!IsBusy && Records.Count == 0) StatusMessage = string.IsNullOrEmpty(StatusMessage) ? string.Empty : SignInText;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var token = await auth.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                Records.Clear();
                NeedsSignIn = true;
                StatusMessage = SignInText;
                return;
            }

            NeedsSignIn = false;
            var records = await api.GetClinicalRecordsAsync(token);
            Records.Clear();
            foreach (var record in records) Records.Add(record);
            StatusMessage = Records.Count == 0 ? EmptyText : string.Empty;
        }
        catch (HttpRequestException)
        {
            NeedsSignIn = false;
            Records.Clear();
            StatusMessage = language["records_unavailable"];
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SignInAsync() => await Shell.Current.GoToAsync(nameof(LoginPage));
}
