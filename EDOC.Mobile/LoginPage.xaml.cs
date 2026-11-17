using EDOC.Mobile.ViewModels;

namespace EDOC.Mobile;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel) { InitializeComponent(); BindingContext = viewModel; }
}
