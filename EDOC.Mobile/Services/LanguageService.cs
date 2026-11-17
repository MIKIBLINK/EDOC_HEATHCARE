using CommunityToolkit.Mvvm.ComponentModel;
using EDOC.Localization;

namespace EDOC.Mobile.Services;

public sealed class LanguageService : ObservableObject
{
    private readonly AppLocalizer _localizer;
    public LanguageService(AppLocalizer localizer)
    {
        _localizer = localizer;
        _localizer.SetLanguage(Preferences.Default.Get("language", "en"));
        _localizer.LanguageChanged += (_, _) =>
        {
            if (Application.Current is { } app)
                app.Resources["AppFontFamily"] = GetFontFamily();
            OnPropertyChanged(nameof(Language));
            OnPropertyChanged("Item[]");
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        };
        if (Application.Current is { } application)
            application.Resources["AppFontFamily"] = GetFontFamily();
    }

    public string Language => _localizer.Language;
    public string this[string key] => _localizer.Get(key);
    public string Get(string key) => _localizer.Get(key);

    public void Toggle()
    {
        var next = Language == "en" ? "km" : "en";
        Preferences.Default.Set("language", next);
        _localizer.SetLanguage(next);
    }

    public event EventHandler? LanguageChanged;

    private string GetFontFamily() => Language == "km"
        ? "Khmer"
        : DeviceInfo.Platform == DevicePlatform.iOS ? string.Empty : "OpenSansRegular";
}
