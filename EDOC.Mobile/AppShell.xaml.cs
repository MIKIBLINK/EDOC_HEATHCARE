using EDOC.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EDOC.Mobile;

public partial class AppShell : Shell
{
    private readonly LanguageService _language;
    private readonly AuthSessionService _auth;
    private string _activeRole = string.Empty;
    private string[] _tabTitleKeys = [];
    private Tab[] _tabs = [];

    public AppShell(IServiceProvider services, LanguageService language, AuthSessionService auth)
    {
        InitializeComponent();
        _language = language;
        _auth = auth;
        Routing.RegisterRoute(nameof(SplashPage), typeof(SplashPage));
        Routing.RegisterRoute(nameof(WebPortalNoticePage), typeof(WebPortalNoticePage));
        Routing.RegisterRoute(nameof(OnboardingPage), typeof(OnboardingPage));
        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
        Routing.RegisterRoute(nameof(DoctorDetailsPage), typeof(DoctorDetailsPage));
        Routing.RegisterRoute(nameof(HospitalsPage), typeof(HospitalsPage));
        Routing.RegisterRoute(nameof(HospitalDetailsPage), typeof(HospitalDetailsPage));
        Routing.RegisterRoute("records", typeof(MedicalRecordsPage));
        Routing.RegisterRoute("admin", typeof(AdminDashboardPage));
        Routing.RegisterRoute("doctor", typeof(DoctorDashboardPage));
        Routing.RegisterRoute(nameof(ClinicalEntryPage), typeof(ClinicalEntryPage));
        Routing.RegisterRoute(nameof(HealthPage), typeof(HealthPage));
        _language.LanguageChanged += (_, _) => UpdateTitles();
        _auth.SessionChanged += async (_, _) => await HandleSessionChangedAsync(services);
        ConfigureRoleTabs("Patient", services);
        Navigated += async (_, _) => await ConfigureRoleTabsAsync(services);
        Loaded += async (_, _) =>
        {
            await ConfigureRoleTabsAsync(services);
            var roles = await _auth.GetRolesAsync();
            await GoToAsync(roles.Any(IsWebOnlyRole) ? nameof(WebPortalNoticePage) : nameof(SplashPage));
        };
    }

    private async Task ConfigureRoleTabsAsync(IServiceProvider services)
    {
        var roles = await _auth.GetRolesAsync();
        var role = roles.Any(IsWebOnlyRole) ? "WebPortalOnly"
            : roles.Contains("Doctor", StringComparer.OrdinalIgnoreCase) ? "Doctor"
            : roles.Contains("Caregiver", StringComparer.OrdinalIgnoreCase) ? "Caregiver" : "Patient";
        await MainThread.InvokeOnMainThreadAsync(() => ConfigureRoleTabs(role, services));
    }

    private async Task HandleSessionChangedAsync(IServiceProvider services)
    {
        await ConfigureRoleTabsAsync(services);
        var roles = await _auth.GetRolesAsync();
        var route = roles.Any(IsWebOnlyRole) ? nameof(WebPortalNoticePage)
            : roles.Contains("Doctor", StringComparer.OrdinalIgnoreCase) ? "doctor"
            : roles.Contains("Caregiver", StringComparer.OrdinalIgnoreCase) ? "caregiver" : "home";
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (route == nameof(WebPortalNoticePage)) await GoToAsync(route);
            else await GoToAsync($"//{route}");
        });
    }

    private static bool IsWebOnlyRole(string role) => role.Equals("Administrator", StringComparison.OrdinalIgnoreCase)
        || role.Equals("HospitalStaff", StringComparison.OrdinalIgnoreCase);

    private void ConfigureRoleTabs(string role, IServiceProvider services)
    {
        if (_activeRole == role && _tabs.Length > 0) return;
        _activeRole = role;
        if (role == "WebPortalOnly")
        {
            Items.Clear();
            _tabs = [];
            _tabTitleKeys = [];
            return;
        }

        var pages = role == "Doctor"
            ? new (string Key, Page Page, string Route, string Icon)[]
            {
                ("doctor_dashboard", services.GetRequiredService<DoctorDashboardPage>(), "doctor", "doctors.svg"),
                ("profile", services.GetRequiredService<ProfilePage>(), "profile", "profile.svg")
            }
            : role == "Caregiver"
            ? new (string Key, Page Page, string Route, string Icon)[]
            {
                ("caregiver_dashboard", services.GetRequiredService<CaregiverPage>(), "caregiver", "profile.svg"),
                ("profile", services.GetRequiredService<ProfilePage>(), "profile", "profile.svg")
            }
            : new (string Key, Page Page, string Route, string Icon)[]
            {
                ("home", services.GetRequiredService<MainPage>(), "home", "home.svg"),
                ("doctors", services.GetRequiredService<DoctorsPage>(), "doctors", "doctors.svg"),
                ("appointments", services.GetRequiredService<AppointmentsPage>(), "appointments", "appointments.svg"),
                ("assistant", services.GetRequiredService<AIAssistantPage>(), "assistant", "assistant.svg"),
                ("profile", services.GetRequiredService<ProfilePage>(), "profile", "profile.svg")
            };
        _tabTitleKeys = pages.Select(x => x.Key).ToArray();
        _tabs = pages.Select(x => CreateTab(x.Key, x.Page, x.Route, x.Icon)).ToArray();
        Items.Clear();
        var bar = new TabBar();
        foreach (var tab in _tabs) bar.Items.Add(tab);
        Items.Add(bar);
        UpdateTitles();
    }

    private Tab CreateTab(string titleKey, Page page, string route, string icon) => new()
    {
        Title = _language[titleKey],
        Route = route,
        Icon = icon,
        Items = { new ShellContent { Title = _language[titleKey], Content = page, Route = route, Icon = icon } }
    };

    private void UpdateTitles()
    {
        for (var index = 0; index < _tabs.Length; index++)
        {
            var key = _tabTitleKeys[index];
            _tabs[index].Title = _language[key];
            if (_tabs[index].Items.FirstOrDefault() is ShellContent content) content.Title = _language[key];
        }
    }
}
