using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EDOC.Mobile.Models;
using EDOC.Mobile.Services;

namespace EDOC.Mobile.ViewModels;

public partial class AppointmentsViewModel : ObservableObject
{
    private readonly AuthSessionService auth;
    private readonly EdocApiClient api;
    private readonly LanguageService language;

    public AppointmentsViewModel(AuthSessionService auth, EdocApiClient api, LanguageService language)
    {
        this.auth = auth;
        this.api = api;
        this.language = language;
        language.LanguageChanged += (_, _) => RefreshLabels();
    }

    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private bool isBusy;
    public ObservableCollection<AppointmentCard> Appointments { get; } = [];
    public string Title => language["appointments"];
    public string SignInHint => language["appointments_hint"];
    public string EmptyText => language["no_appointments"];
    public string CancelText => language["cancel"];
    public string Intro => language["appointments_intro"];
    public string BookText => language["book_appointment"];

    private void RefreshLabels()
    {
        OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(SignInHint)); OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(CancelText)); OnPropertyChanged(nameof(Intro)); OnPropertyChanged(nameof(BookText));
        if (Appointments.Count == 0 && !string.IsNullOrWhiteSpace(StatusMessage))
            StatusMessage = StatusMessage == SignInHint ? SignInHint : EmptyText;
    }

    [RelayCommand]
    private async Task BookAsync() => await Shell.Current.GoToAsync("//doctors");

    [RelayCommand]
    private async Task CancelAsync(AppointmentCard appointment)
    {
        var confirmed = await Shell.Current.DisplayAlertAsync(language["cancel_appointment"], language["cancel_confirm"], language["cancel"], language["retry"]);
        if (!confirmed) return;
        var token = await auth.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) { StatusMessage = SignInHint; return; }
        try
        {
            await api.CancelAppointmentAsync(token, appointment.Id);
            await Shell.Current.DisplayAlertAsync(language["cancel_appointment"], language["cancelled"], language["retry"]);
            await LoadAsync();
        }
        catch (HttpRequestException) { StatusMessage = language["booking_error"]; }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var token = await auth.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token)) { StatusMessage = SignInHint; return; }
            var values = await api.GetAppointmentsAsync(token);
            Appointments.Clear(); foreach (var value in values) Appointments.Add(value);
            StatusMessage = Appointments.Count == 0 ? EmptyText : string.Empty;
        }
        catch (HttpRequestException) { StatusMessage = language["offline"]; }
        finally { IsBusy = false; }
    }
}
