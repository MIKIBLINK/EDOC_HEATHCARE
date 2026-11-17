using EDOC.Mobile.ViewModels;

namespace EDOC.Mobile;

public partial class DoctorDetailsPage : ContentPage, IQueryAttributable
{
    public DoctorDetailsPage(DoctorDetailsViewModel viewModel)
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
