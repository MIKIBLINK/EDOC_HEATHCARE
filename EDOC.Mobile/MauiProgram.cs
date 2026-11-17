using EDOC.Mobile.Services;
using EDOC.Mobile.ViewModels;
using EDOC.Localization;
using Microsoft.Extensions.Logging;
#if MAUI_DEVFLOW
using Microsoft.Maui.DevFlow.Agent;
#endif

namespace EDOC.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            fonts.AddFont("Khmer.ttf", "Khmer");
        });
#if MAUI_DEVFLOW
        builder.AddMauiDevFlowAgent();
#endif

        builder.Services.AddSingleton<LanguageService>();
        builder.Services.AddSingleton<AppLocalizer>();
        builder.Services.AddSingleton(provider =>
        {
#if DEBUG && ANDROID
            var defaultApiUrl = "http://10.0.2.2:5154/";
#elif DEBUG
            var defaultApiUrl = "http://localhost:5154/";
#else
            var defaultApiUrl = "https://localhost:5155/";
#endif
            var url = Preferences.Default.Get("api_base_url", defaultApiUrl);
            if (!Uri.TryCreate(url, UriKind.Absolute, out var baseUri) || baseUri.Scheme is not ("http" or "https"))
                throw new InvalidOperationException("Set a valid HTTP or HTTPS API address in api_base_url.");
            return new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(20) };
        });
        builder.Services.AddSingleton<EdocApiClient>();
        builder.Services.AddSingleton<AuthSessionService>();

        builder.Services.AddSingleton<MainPageViewModel>();
        builder.Services.AddSingleton<MedicalRecordsViewModel>();
        builder.Services.AddSingleton<AIAssistantViewModel>();
        builder.Services.AddSingleton<DoctorsViewModel>();
        builder.Services.AddSingleton<DoctorDetailsViewModel>();
        builder.Services.AddSingleton<HospitalsViewModel>();
        builder.Services.AddSingleton<HospitalDetailsViewModel>();
        builder.Services.AddSingleton<AppointmentsViewModel>();
        builder.Services.AddSingleton<ProfileViewModel>();
        builder.Services.AddSingleton<CaregiverViewModel>();
        builder.Services.AddSingleton<DoctorDashboardViewModel>();
        builder.Services.AddTransient<ClinicalEntryViewModel>();
        builder.Services.AddSingleton<HealthViewModel>();
        builder.Services.AddSingleton<AdminDashboardViewModel>();
        builder.Services.AddSingleton<AdminDirectoryViewModel>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<SplashPage>();
        builder.Services.AddTransient<WebPortalNoticePage>();
        builder.Services.AddTransient<OnboardingPage>();
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<MedicalRecordsPage>();
        builder.Services.AddSingleton<AIAssistantPage>();
        builder.Services.AddSingleton<DoctorsPage>();
        builder.Services.AddTransient<DoctorDetailsPage>();
        builder.Services.AddTransient<HospitalsPage>();
        builder.Services.AddTransient<HospitalDetailsPage>();
        builder.Services.AddSingleton<AppointmentsPage>();
        builder.Services.AddSingleton<ProfilePage>();
        builder.Services.AddSingleton<CaregiverPage>();
        builder.Services.AddSingleton<DoctorDashboardPage>();
        builder.Services.AddTransient<ClinicalEntryPage>();
        builder.Services.AddSingleton<HealthPage>();
        builder.Services.AddSingleton<AdminDashboardPage>();
        builder.Services.AddSingleton<AdminDirectoryPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
