using EDOC.Mobile.ViewModels;

namespace EDOC.Mobile;

public partial class ClinicalEntryPage : ContentPage
{
    public ClinicalEntryPage(ClinicalEntryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
