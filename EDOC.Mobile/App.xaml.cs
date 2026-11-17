using EDOC.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EDOC.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        UserAppTheme = Preferences.Default.Get("dark_mode", false) ? AppTheme.Dark : AppTheme.Light;
    }

    protected override Window CreateWindow(IActivationState? activationState) => new(_services.GetRequiredService<AppShell>());
}
