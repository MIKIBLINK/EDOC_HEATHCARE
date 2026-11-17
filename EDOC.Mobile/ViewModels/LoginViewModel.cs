using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EDOC.Mobile.Services;

namespace EDOC.Mobile.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthSessionService auth;
    private readonly LanguageService language;

    public LoginViewModel(AuthSessionService auth, LanguageService language)
    {
        this.auth = auth;
        this.language = language;
        IsRememberMe = Preferences.Default.Get("remember_login_identifier_enabled", Preferences.Default.Get("remember_email", false));
        Email = IsRememberMe
            ? Preferences.Default.Get("remember_login_identifier", Preferences.Default.Get("remembered_email", string.Empty))
            : string.Empty;
        language.LanguageChanged += (_, _) => RefreshLabels();
    }

    [ObservableProperty] private string fullName = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string phoneNumber = string.Empty;
    [ObservableProperty] private string password = string.Empty;
    [ObservableProperty] private string confirmPassword = string.Empty;
    [ObservableProperty] private string message = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsNotRegistering)), NotifyPropertyChangedFor(nameof(LoginIdentifierText))] private bool isRegistering;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isPasswordHidden = true;
    [ObservableProperty] private bool isRememberMe;
    [ObservableProperty] private string accountType = "Patient";
    public IReadOnlyList<string> AccountTypes { get; } = ["Patient", "Caregiver"];
    public string FullNameText => language["full_name"];
    public string AccountTypeText => language["account_type"];
    public string EmailText => language["email"];
    public string LoginIdentifierText => language[IsRegistering ? "email" : "login_identifier"];
    public string PhoneText => language["phone"];
    public string PasswordText => language["password"];
    public string ConfirmPasswordText => language["confirm_password"];
    public string ForgotPasswordText => language["forgot_password"];
    public string SubmitText => language[IsRegistering ? "register" : "sign_in"];
    public string ToggleModeText => language[IsRegistering ? "sign_in" : "register"];
    public string LoginIntro => language["login_intro"];
    public string RememberMeText => language["remember_me"];
    public string PasswordVisibilityText => language[IsPasswordHidden ? "show_password" : "hide_password"];
    public bool IsNotRegistering => !IsRegistering;

    private void RefreshLabels()
    {
        OnPropertyChanged(nameof(FullNameText)); OnPropertyChanged(nameof(AccountTypeText)); OnPropertyChanged(nameof(EmailText)); OnPropertyChanged(nameof(LoginIdentifierText)); OnPropertyChanged(nameof(PhoneText));
        OnPropertyChanged(nameof(PasswordText)); OnPropertyChanged(nameof(ConfirmPasswordText)); OnPropertyChanged(nameof(ForgotPasswordText));
        OnPropertyChanged(nameof(SubmitText)); OnPropertyChanged(nameof(ToggleModeText));
        OnPropertyChanged(nameof(LoginIntro)); OnPropertyChanged(nameof(RememberMeText)); OnPropertyChanged(nameof(PasswordVisibilityText));
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Message = string.Empty;
        try
        {
            if (IsRegistering)
            {
                if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(PhoneNumber))
                {
                    Message = language["required_fields"];
                    return;
                }
                if (!System.Net.Mail.MailAddress.TryCreate(Email.Trim(), out var address)
                    || !string.Equals(address.Address, Email.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    Message = language["invalid_email"];
                    return;
                }
                if (Password != ConfirmPassword) { Message = language["passwords_do_not_match"]; return; }
                if (Password.Length < 10 || !Password.Any(char.IsUpper) || !Password.Any(char.IsLower)
                    || !Password.Any(char.IsDigit) || !Password.Any(character => !char.IsLetterOrDigit(character)))
                {
                    Message = language["registration_password_requirements"];
                    return;
                }
                var result = await auth.RegisterAsync(FullName, Email, PhoneNumber, Password, ConfirmPassword, AccountType);
                if (result.Succeeded)
                {
                    Message = language["registration_success"];
                    Password = ConfirmPassword = string.Empty;
                }
                else Message = result.ErrorMessage ?? result.StatusCode switch
                {
                    System.Net.HttpStatusCode.ServiceUnavailable => language["registration_email_unavailable"],
                    System.Net.HttpStatusCode.Conflict => language["registration_account_exists"],
                    System.Net.HttpStatusCode.BadRequest => language["registration_invalid"],
                    _ => language["registration_unavailable"]
                };
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
                {
                    Message = language["required_fields"];
                    return;
                }
                if (await auth.LoginAsync(Email.Trim(), Password))
                {
                    Preferences.Default.Set("remember_login_identifier_enabled", IsRememberMe);
                    if (IsRememberMe) Preferences.Default.Set("remember_login_identifier", Email.Trim());
                    else Preferences.Default.Remove("remember_login_identifier");
                }
                else Message = language["login_failed"];
            }
        }
        catch (HttpRequestException) { Message = language["offline"]; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ForgotPasswordAsync()
    {
        var emailAddress = await Shell.Current.DisplayPromptAsync(language["forgot_password"], language["email"]);
        if (string.IsNullOrWhiteSpace(emailAddress)) return;
        try
        {
            var sent = await auth.ForgotPasswordAsync(emailAddress.Trim());
            Message = sent ? language["password_reset_notice"] : language["email_service_unavailable"];
        }
        catch (HttpRequestException) { Message = language["offline"]; }
    }

    [RelayCommand]
    private void ToggleMode()
    {
        IsRegistering = !IsRegistering;
        Message = string.Empty;
        OnPropertyChanged(nameof(SubmitText));
        OnPropertyChanged(nameof(ToggleModeText));
    }

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordHidden = !IsPasswordHidden;
        OnPropertyChanged(nameof(PasswordVisibilityText));
    }
}
