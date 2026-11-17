using EDOC.Mobile.ViewModels;

namespace EDOC.Mobile;

public partial class DoctorDashboardPage : ContentPage
{
    private readonly DoctorDashboardViewModel _viewModel;
    public DoctorDashboardPage(DoctorDashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
