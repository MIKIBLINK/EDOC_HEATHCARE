using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EDOC.Mobile.Models;
using EDOC.Mobile.Services;

namespace EDOC.Mobile.ViewModels;

public partial class HealthViewModel : ObservableObject
{
    private readonly AuthSessionService auth;
    private readonly EdocApiClient api;
    private readonly LanguageService language;

    public HealthViewModel(AuthSessionService auth, EdocApiClient api, LanguageService language)
    {
        this.auth = auth;
        this.api = api;
        this.language = language;
        language.LanguageChanged += (_, _) => { RefreshLabels(); RelabelData(); };
    }

    [ObservableProperty] private int selectedMetricIndex;
    [ObservableProperty] private string value = string.Empty;
    [ObservableProperty] private string secondaryValue = string.Empty;
    [ObservableProperty] private string unit = "mmHg";
    [ObservableProperty] private string measurementNotes = string.Empty;
    [ObservableProperty] private string medicationName = string.Empty;
    [ObservableProperty] private string dosage = string.Empty;
    [ObservableProperty] private string reminderInstructions = string.Empty;
    [ObservableProperty] private DateTime reminderDate = DateTime.Now.AddHours(1).Date;
    [ObservableProperty] private TimeSpan reminderTime = DateTime.Now.AddHours(1).TimeOfDay;
    [ObservableProperty] private int selectedReminderRepeatIndex;
    [ObservableProperty] private string message = string.Empty;
    [ObservableProperty] private bool isBusy;

    public ObservableCollection<HealthMeasurementCard> Measurements { get; } = [];
    public ObservableCollection<MedicationReminderCard> Reminders { get; } = [];
    public string Title => language["health_title"];
    public string Intro => language["health_intro"];
    public string MeasurementTitle => language["health_measurements"];
    public string MetricLabel => language["health_metric"];
    public string ValueLabel => language["health_value"];
    public string SecondaryValueLabel => language["health_diastolic"];
    public string UnitLabel => language["health_unit"];
    public string NotesLabel => language["clinical_notes"];
    public string SaveMeasurementText => language["health_save_measurement"];
    public string ReminderTitle => language["medication_reminders"];
    public string MedicationLabel => language["clinical_medication"];
    public string DosageLabel => language["clinical_dose"];
    public string InstructionsLabel => language["clinical_notes"];
    public string ScheduleLabel => language["health_schedule"];
    public string AddReminderText => language["health_add_reminder"];
    public string TakenText => language["health_taken"];
    public string MissedText => language["health_missed"];
    public string CancelReminderText => language["health_cancel_reminder"];
    public string HistoryEmpty => language["health_empty"];
    public string RemindersEmpty => language["health_empty_reminders"];
    public string[] MetricTypes => [language["health_blood_pressure"], language["health_blood_glucose"], language["health_weight"], language["health_height"]];
    public string[] ReminderRepeatOptions => [language["health_repeat_once"], language["health_repeat_daily"], language["health_repeat_weekly"]];
    public bool NeedsSecondaryValue => SelectedMetricIndex == 0;

    partial void OnSelectedMetricIndexChanged(int value)
    {
        Unit = value switch { 0 => "mmHg", 1 => "mg/dL", 2 => "kg", _ => "cm" };
        OnPropertyChanged(nameof(NeedsSecondaryValue));
    }

    public void RefreshLabels()
    {
        foreach (var property in new[] { nameof(Title), nameof(Intro), nameof(MeasurementTitle), nameof(MetricLabel), nameof(ValueLabel),
            nameof(SecondaryValueLabel), nameof(UnitLabel), nameof(NotesLabel), nameof(SaveMeasurementText), nameof(ReminderTitle),
            nameof(MedicationLabel), nameof(DosageLabel), nameof(InstructionsLabel), nameof(ScheduleLabel), nameof(AddReminderText),
            nameof(TakenText), nameof(MissedText), nameof(CancelReminderText), nameof(HistoryEmpty), nameof(RemindersEmpty), nameof(MetricTypes), nameof(ReminderRepeatOptions) }) OnPropertyChanged(property);
    }

    private void RelabelData()
    {
        for (var i = 0; i < Measurements.Count; i++) Measurements[i] = Measurements[i] with { LocalizedMetricType = TranslateMetric(Measurements[i].MetricType) };
        for (var i = 0; i < Reminders.Count; i++) Reminders[i] = Reminders[i] with
        {
            LocalizedStatus = TranslateReminderStatus(Reminders[i].Status),
            LocalizedLastDoseStatus = Reminders[i].LastDoseStatus is null ? null : TranslateReminderStatus(Reminders[i].LastDoseStatus!),
            LocalizedSchedule = TranslateReminderSchedule(Reminders[i].RepeatIntervalDays)
        };
    }

    private string TranslateMetric(string metric) => language[metric switch
    {
        "BloodPressure" => "health_blood_pressure", "BloodGlucose" => "health_blood_glucose",
        "Weight" => "health_weight", "Height" => "health_height", _ => "health_metric"
    }];

    private string TranslateReminderStatus(string status) => language[status switch
    {
        "Scheduled" => "health_status_scheduled", "Taken" => "health_status_taken",
        "Missed" => "health_status_missed", "Cancelled" => "health_status_cancelled", _ => "health_status_scheduled"
    }];

    private string TranslateReminderSchedule(int? interval) => language[interval switch
    {
        null => "health_repeat_once", 1 => "health_repeat_daily", 7 => "health_repeat_weekly", _ => "health_repeat_custom"
    }];

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true; Message = string.Empty;
        try
        {
            var token = await auth.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token)) { Message = language["appointments_hint"]; return; }
            var measurements = await api.GetHealthMeasurementsAsync(token);
            var reminders = await api.GetMedicationRemindersAsync(token);
            Measurements.Clear(); foreach (var item in measurements) Measurements.Add(item with { LocalizedMetricType = TranslateMetric(item.MetricType) });
            Reminders.Clear(); foreach (var item in reminders) Reminders.Add(item with
            {
                LocalizedStatus = TranslateReminderStatus(item.Status),
                LocalizedLastDoseStatus = item.LastDoseStatus is null ? null : TranslateReminderStatus(item.LastDoseStatus),
                LocalizedSchedule = TranslateReminderSchedule(item.RepeatIntervalDays)
            });
        }
        catch (HttpRequestException) { Message = language["api_error"]; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveMeasurementAsync()
    {
        if (!TryNumber(Value, out var primary) || (NeedsSecondaryValue && !TryNumber(SecondaryValue, out _)) || string.IsNullOrWhiteSpace(Unit))
        { Message = language["health_invalid_measurement"]; return; }
        decimal? secondary = NeedsSecondaryValue && TryNumber(SecondaryValue, out var parsedSecondary) ? parsedSecondary : null;
        var token = await auth.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) { Message = language["appointments_hint"]; return; }
        IsBusy = true; Message = string.Empty;
        try
        {
            var metric = SelectedMetricIndex switch { 0 => "BloodPressure", 1 => "BloodGlucose", 2 => "Weight", _ => "Height" };
            await api.AddHealthMeasurementAsync(token, metric, primary, secondary, Unit.Trim(), DateTimeOffset.Now, MeasurementNotes.Trim());
            Value = SecondaryValue = MeasurementNotes = string.Empty;
            Message = language["health_saved"];
            IsBusy = false;
            await LoadAsync();
        }
        catch (HttpRequestException) { Message = language["health_invalid_measurement"]; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddReminderAsync()
    {
        if (string.IsNullOrWhiteSpace(MedicationName) || string.IsNullOrWhiteSpace(Dosage)) { Message = language["health_required"]; return; }
        var scheduledLocal = ReminderDate.Date + ReminderTime;
        var scheduledAt = new DateTimeOffset(DateTime.SpecifyKind(scheduledLocal, DateTimeKind.Unspecified), TimeZoneInfo.Local.GetUtcOffset(scheduledLocal)).ToUniversalTime();
        if (scheduledAt <= DateTimeOffset.UtcNow) { Message = language["health_reminder_future"]; return; }
        var token = await auth.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) { Message = language["appointments_hint"]; return; }
        IsBusy = true; Message = string.Empty;
        try
        {
            var repeatDays = SelectedReminderRepeatIndex switch { 1 => 1, 2 => 7, _ => (int?)null };
            await api.AddMedicationReminderAsync(token, MedicationName.Trim(), Dosage.Trim(), ReminderInstructions.Trim(), scheduledAt, repeatDays);
            MedicationName = Dosage = ReminderInstructions = string.Empty;
            Message = language["health_saved"];
            IsBusy = false;
            await LoadAsync();
        }
        catch (HttpRequestException) { Message = language["api_error"]; }
        finally { IsBusy = false; }
    }

    [RelayCommand] private Task MarkTakenAsync(MedicationReminderCard reminder) => UpdateReminderAsync(reminder, "Taken");
    [RelayCommand] private Task MarkMissedAsync(MedicationReminderCard reminder) => UpdateReminderAsync(reminder, "Missed");
    [RelayCommand] private Task CancelReminderAsync(MedicationReminderCard reminder) => UpdateReminderAsync(reminder, "Cancelled");

    private async Task UpdateReminderAsync(MedicationReminderCard reminder, string status)
    {
        var token = await auth.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) { Message = language["appointments_hint"]; return; }
        IsBusy = true;
        try { await api.UpdateMedicationReminderAsync(token, reminder.Id, status); IsBusy = false; await LoadAsync(); }
        catch (HttpRequestException) { Message = language["api_error"]; }
        finally { IsBusy = false; }
    }

    private static bool TryNumber(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
        || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
}
