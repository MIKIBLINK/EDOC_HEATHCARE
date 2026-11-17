using EDOC.Mobile.ViewModels;

namespace EDOC.Mobile;

public partial class AdminDirectoryPage : ContentPage
{
    private readonly AdminDirectoryViewModel _viewModel;

    public AdminDirectoryPage(AdminDirectoryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.Hospitals.Count == 0) _viewModel.LoadCommand.Execute(null);
    }
}
