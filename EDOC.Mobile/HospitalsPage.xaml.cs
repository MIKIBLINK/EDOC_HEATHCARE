using EDOC.Mobile.ViewModels;

namespace EDOC.Mobile;

public partial class HospitalsPage : ContentPage
{
    private readonly HospitalsViewModel _viewModel;

    public HospitalsPage(HospitalsViewModel viewModel)
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
