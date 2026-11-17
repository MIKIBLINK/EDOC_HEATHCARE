using EDOC.Mobile.Services;

namespace EDOC.Mobile;

public partial class SplashPage : ContentPage
{
    private readonly LanguageService _language;

    public SplashPage(LanguageService language)
    {
        InitializeComponent();
        _language = language;
        BindingContext = this;
        _language.LanguageChanged += (_, _) => OnPropertyChanged(nameof(Tagline));
        _language.LanguageChanged += (_, _) => OnPropertyChanged(nameof(LoadingText));
    }

    public string Tagline => _language["welcome"];
    public string LoadingText => _language["loading"];

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.Delay(1500);
        if (Preferences.Default.Get("onboarding_completed", false))
        {
            await Shell.Current.GoToAsync(nameof(LoginPage));
            return;
        }

        await Shell.Current.GoToAsync(nameof(OnboardingPage));
    }
}
