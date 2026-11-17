using EDOC.Mobile.ViewModels;

namespace EDOC.Mobile;

public partial class CaregiverPage : ContentPage
{
    private readonly CaregiverViewModel _viewModel;

    public CaregiverPage(CaregiverViewModel viewModel)
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
