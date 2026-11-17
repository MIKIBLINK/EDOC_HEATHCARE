using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EDOC.Mobile.Services;

namespace EDOC.Mobile.ViewModels;

public partial class AIAssistantViewModel : ObservableObject
{
    private readonly LanguageService language;
    public AIAssistantViewModel(LanguageService language)
    {
        this.language = language;
        language.LanguageChanged += (_, _) => RefreshLabels();
    }

    [ObservableProperty] private string prompt = string.Empty;
    [ObservableProperty] private string statusMessage = string.Empty;
    public string Title => language["assistant"];
    public string Intro => language["assistant_intro"];
    public string Disclaimer => language["ai_disclaimer"];
    public string EmergencyText => language["emergency_warning"];
    public string PromptHint => language["ai_prompt"];
    public string SendText => language["send"];
    public string SuggestionOne => language["ai_question_appointment"];
    public string SuggestionTwo => language["ai_question_results"];
    public string SuggestionThree => language["ai_question_doctor"];
    public string SuggestedQuestionsText => language["suggested_questions"];
    public string UnavailableText => language["ai_unavailable"];

    private void RefreshLabels()
    {
        OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(Intro)); OnPropertyChanged(nameof(Disclaimer));
        OnPropertyChanged(nameof(EmergencyText)); OnPropertyChanged(nameof(PromptHint)); OnPropertyChanged(nameof(SendText));
        OnPropertyChanged(nameof(SuggestionOne)); OnPropertyChanged(nameof(SuggestionTwo)); OnPropertyChanged(nameof(SuggestionThree));
        OnPropertyChanged(nameof(SuggestedQuestionsText));
        OnPropertyChanged(nameof(UnavailableText));
        if (!string.IsNullOrWhiteSpace(StatusMessage)) StatusMessage = UnavailableText;
    }

    [RelayCommand]
    private void Send()
    {
        if (string.IsNullOrWhiteSpace(Prompt)) return;
        // No AI provider is configured in this project, so never fabricate a response or imply delivery.
        StatusMessage = UnavailableText;
    }

    [RelayCommand]
    private void SelectQuestion(string question) => Prompt = question;
}
