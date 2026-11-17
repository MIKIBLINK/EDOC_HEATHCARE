using EDOC.Mobile.ViewModels;

namespace EDOC.Mobile;

public partial class AIAssistantPage : ContentPage
{
    public AIAssistantPage(AIAssistantViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
