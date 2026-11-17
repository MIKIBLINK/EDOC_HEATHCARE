using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EDOC.Mobile.Models;
using EDOC.Mobile.Services;

namespace EDOC.Mobile.ViewModels;

public partial class DoctorDetailsViewModel : ObservableObject, IQueryAttributable
{
    private readonly LanguageService language;
    private readonly DoctorsViewModel doctors;

    public DoctorDetailsViewModel(LanguageService language, DoctorsViewModel doctors)
    {
        this.language = language;
        this.doctors = doctors;
        language.LanguageChanged += (_, _) => RefreshLabels();
    }

    [ObservableProperty] private DoctorCard? doctor;
    public string SpecialtyLabel => language["specialty"];
    public string HospitalLabel => language["hospital_affiliation"];
    public string QualificationsLabel => language["qualifications"];
    public string ExperienceLabel => language["experience"];
    public string FeeLabel => language["consultation_fee"];
    public string BookText => language["book_appointment"];

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Doctor", out var value) && value is DoctorCard selected)
            Doctor = selected;
    }

    private void RefreshLabels()
    {
        OnPropertyChanged(nameof(SpecialtyLabel)); OnPropertyChanged(nameof(HospitalLabel));
        OnPropertyChanged(nameof(QualificationsLabel)); OnPropertyChanged(nameof(ExperienceLabel));
        OnPropertyChanged(nameof(FeeLabel)); OnPropertyChanged(nameof(BookText));
    }

    [RelayCommand]
    private async Task BookAsync()
    {
        if (Doctor is not null) await doctors.BookAsync(Doctor);
    }
}
