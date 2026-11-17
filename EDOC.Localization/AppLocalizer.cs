using System.Globalization;
using System.Resources;

namespace EDOC.Localization;

public sealed class AppLocalizer
{
    private static readonly ResourceManager Resources = new("EDOC.Localization.Resources.Localization.AppResources", typeof(AppLocalizer).Assembly);
    private CultureInfo _culture = CultureInfo.GetCultureInfo("en-US");
    public string Language => _culture.TwoLetterISOLanguageName;
    public event EventHandler? LanguageChanged;

    public void SetLanguage(string language)
    {
        var next = language == "km" ? CultureInfo.GetCultureInfo("km-KH") : CultureInfo.GetCultureInfo("en-US");
        if (_culture.Name == next.Name) return;
        _culture = next;
        CultureInfo.CurrentCulture = next;
        CultureInfo.CurrentUICulture = next;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Get(string key) => Resources.GetString(key, _culture) ?? key;
}
