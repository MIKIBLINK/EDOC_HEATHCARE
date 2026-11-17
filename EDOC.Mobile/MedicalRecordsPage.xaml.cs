using EDOC.Mobile.ViewModels;

namespace EDOC.Mobile;

public partial class MedicalRecordsPage : ContentPage
{
    private readonly MedicalRecordsViewModel _viewModel;

    public MedicalRecordsPage(MedicalRecordsViewModel viewModel)
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
