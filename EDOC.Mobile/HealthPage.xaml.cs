using EDOC.Mobile.ViewModels;

namespace EDOC.Mobile;

public partial class HealthPage : ContentPage
{
    public HealthPage(HealthViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is HealthViewModel viewModel) viewModel.LoadCommand.Execute(null);
    }
}
