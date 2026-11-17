using EDOC.Mobile.ViewModels;

namespace EDOC.Mobile;

public partial class HospitalDetailsPage : ContentPage, IQueryAttributable
{
    public HospitalDetailsPage(HospitalDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is IQueryAttributable receiver)
            receiver.ApplyQueryAttributes(query);
    }
}
