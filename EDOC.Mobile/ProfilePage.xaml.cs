using EDOC.Mobile.ViewModels;

namespace EDOC.Mobile;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;
    public ProfilePage(ProfileViewModel viewModel) { InitializeComponent(); BindingContext = _viewModel = viewModel; }
    protected override async void OnAppearing() { base.OnAppearing(); await _viewModel.RefreshCommand.ExecuteAsync(null); }
}
