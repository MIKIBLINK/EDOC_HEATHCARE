using EDOC.Mobile.Services;
using EDOC.Mobile.ViewModels;

namespace EDOC.Mobile;

public partial class MainPage : ContentPage
{
    private readonly MainPageViewModel _viewModel;
    private readonly LanguageService _language;

    public MainPage(MainPageViewModel viewModel, LanguageService language)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _language = language;
        _language.LanguageChanged += (_, _) => RefreshTexts();
        RefreshTexts();
    }

    private void RefreshTexts()
    {
        _viewModel.AppointmentsHint = _language["appointments_hint"];
        _viewModel.HospitalsText = _language["hospitals"];
        _viewModel.RecordsText = _language["medical_records"];
        _viewModel.TelehealthText = _language["telehealth"];
        _viewModel.SignInText = _language["welcome_back"];
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.CheckConnectionCommand.ExecuteAsync(null);
    }
}
