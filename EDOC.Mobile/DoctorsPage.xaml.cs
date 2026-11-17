using EDOC.Mobile.ViewModels;

namespace EDOC.Mobile;

public partial class DoctorsPage : ContentPage
{
    private readonly DoctorsViewModel _viewModel;
    public DoctorsPage(DoctorsViewModel viewModel) { InitializeComponent(); BindingContext = _viewModel = viewModel; }
    protected override async void OnAppearing() { base.OnAppearing(); await _viewModel.LoadCommand.ExecuteAsync(null); }
}
