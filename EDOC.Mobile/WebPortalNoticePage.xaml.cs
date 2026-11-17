using EDOC.Mobile.Services;

namespace EDOC.Mobile;

public partial class WebPortalNoticePage : ContentPage
{
    private readonly LanguageService _language;
    private readonly AuthSessionService _auth;

    public WebPortalNoticePage(LanguageService language, AuthSessionService auth)
    {
        InitializeComponent();
        _language = language;
        _auth = auth;
        BindingContext = this;
        _language.LanguageChanged += (_, _) => RefreshText();
        RefreshText();
    }

    public string HeaderText => _language["web_role_mobile_title"];
    public string Description => _language["web_role_mobile_notice"];
    public string PortalHint => _language["web_portal_url_hint"];
    public string OpenPortalText => _language["web_portal_open"];
    public string SignOutText => _language["sign_out"];
    public string PortalAddress
    {
        get => Preferences.Default.Get("web_portal_url", string.Empty);
        set
        {
            Preferences.Default.Set("web_portal_url", value?.Trim() ?? string.Empty);
            OnPropertyChanged();
        }
    }

    private void RefreshText()
    {
        OnPropertyChanged(nameof(HeaderText));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(PortalHint));
        OnPropertyChanged(nameof(OpenPortalText));
        OnPropertyChanged(nameof(SignOutText));
        OnPropertyChanged(nameof(PortalAddress));
    }

    private async void OpenPortal_Clicked(object? sender, EventArgs e)
    {
        var address = PortalAddress;
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
        {
            await DisplayAlertAsync(HeaderText, PortalHint, OpenPortalText);
            return;
        }
        await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
    }

    private async void SignOut_Clicked(object? sender, EventArgs e)
    {
        await _auth.SignOutAsync();
        await Shell.Current.GoToAsync(nameof(LoginPage));
    }
}
