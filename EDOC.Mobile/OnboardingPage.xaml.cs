using EDOC.Mobile.Services;

namespace EDOC.Mobile;

public partial class OnboardingPage : ContentPage
{
    private readonly LanguageService _language;

    public OnboardingPage(LanguageService language)
    {
        InitializeComponent();
        _language = language;
        BindingContext = this;
        _language.LanguageChanged += (_, _) => RefreshText();
        RefreshText();
    }

    public string HeaderText => _language["app_name"];
    public string StepOneTitle => _language["onboarding_title_1"];
    public string StepOneSubtitle => _language["onboarding_subtitle_1"];
    public string StepTwoTitle => _language["onboarding_title_2"];
    public string StepTwoSubtitle => _language["onboarding_subtitle_2"];
    public string StepThreeTitle => _language["onboarding_title_3"];
    public string StepThreeSubtitle => _language["onboarding_subtitle_3"];
    public string GetStartedText => _language["onboarding_get_started"];
    public string SkipText => _language["onboarding_skip"];

    private void RefreshText()
    {
        OnPropertyChanged(nameof(HeaderText));
        OnPropertyChanged(nameof(StepOneTitle));
        OnPropertyChanged(nameof(StepOneSubtitle));
        OnPropertyChanged(nameof(StepTwoTitle));
        OnPropertyChanged(nameof(StepTwoSubtitle));
        OnPropertyChanged(nameof(StepThreeTitle));
        OnPropertyChanged(nameof(StepThreeSubtitle));
        OnPropertyChanged(nameof(GetStartedText));
        OnPropertyChanged(nameof(SkipText));
    }

    private async void GetStarted_Clicked(object? sender, EventArgs e)
    {
        Preferences.Default.Set("onboarding_completed", true);
        await Shell.Current.GoToAsync(nameof(LoginPage));
    }

    private async void Skip_Clicked(object? sender, EventArgs e)
    {
        Preferences.Default.Set("onboarding_completed", true);
        await Shell.Current.GoToAsync(nameof(LoginPage));
    }
}
