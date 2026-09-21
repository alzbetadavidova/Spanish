using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spanish.Core;

namespace Spanish.ViewModels;

public abstract partial class ScenarioViewModel(Scenario scenario, Func<Scenario, bool, Task> onCompleted) : ObservableObject
{
    public Scenario Scenario { get; } = scenario;

    public abstract string Heading { get; }

    /// <summary>Why the word breaks the usual rules; shown once the user answered.</summary>
    public IReadOnlyList<string> Notes { get; } = scenario.Notes.Select(n => n.Reason).ToList();
    public bool HasNotes => Notes.Count > 0;

    /// <summary>True once the answer was handed over; further input is ignored.</summary>
    public bool IsCompleted { get; private set; }

    protected async Task CompleteAsync(bool correct)
    {
        if (IsCompleted)
        {
            return;
        }
        IsCompleted = true;
        await onCompleted(Scenario, correct);
    }

    public static ScenarioViewModel Create(Scenario scenario, Func<Scenario, bool, Task> onCompleted) => scenario switch
    {
        CardScenario card => new CardScenarioViewModel(card, onCompleted),
        TypedScenario typed => new TypedScenarioViewModel(typed, onCompleted),
        GenderScenario gender => new GenderScenarioViewModel(gender, onCompleted),
        _ => throw new ArgumentException($"Unsupported scenario {scenario.GetType().Name}.", nameof(scenario))
    };

    protected static string DirectionLabel(Direction direction) =>
        direction == Direction.EnglishToSpanish ? "English to Spanish" : "Spanish to English";
}

public partial class CardScenarioViewModel(CardScenario scenario, Func<Scenario, bool, Task> onCompleted)
    : ScenarioViewModel(scenario, onCompleted)
{
    public override string Heading => $"Card · {DirectionLabel(scenario.Direction)}";
    public string Front => scenario.Front;
    public string Back => scenario.Back;
    public string? BackDetail => scenario.BackDetail;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(KnewItCommand), nameof(DidNotKnowCommand))]
    [NotifyPropertyChangedFor(nameof(ShowNotes))]
    private bool _isFlipped;

    public bool ShowNotes => IsFlipped && HasNotes;

    [RelayCommand]
    private void Flip() => IsFlipped = true;

    [RelayCommand(CanExecute = nameof(IsFlipped))]
    private Task KnewIt() => CompleteAsync(true);

    [RelayCommand(CanExecute = nameof(IsFlipped))]
    private Task DidNotKnow() => CompleteAsync(false);
}

public partial class TypedScenarioViewModel(TypedScenario scenario, Func<Scenario, bool, Task> onCompleted)
    : ScenarioViewModel(scenario, onCompleted)
{
    public override string Heading => scenario.Instruction;
    public string Prompt => scenario.Prompt;
    public string? PromptDetail => scenario.PromptDetail;

    [ObservableProperty]
    private string _answer = string.Empty;

    [ObservableProperty]
    private string? _validationMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChecked), nameof(IsCorrect), nameof(IsIncorrect), nameof(Feedback), nameof(ShowNotes))]
    private AnswerResult? _result;

    public bool IsChecked => Result is not null;
    public bool ShowNotes => IsChecked && HasNotes;
    public bool IsCorrect => Result?.IsCorrect == true;
    public bool IsIncorrect => Result is { IsCorrect: false };

    public string? Feedback => Result switch
    {
        null => null,
        { Outcome: AnswerOutcome.Correct } => "Correct",
        { Outcome: AnswerOutcome.CorrectWithAccentHint } r => $"Correct. Watch the accent: {r.ExpectedAnswer}",
        var r => $"The answer is {r.ExpectedAnswer}"
    };

    partial void OnAnswerChanged(string value) => ValidationMessage = null;

    /// <summary>Enter: checks the answer, or moves on when it was already checked.</summary>
    [RelayCommand]
    private async Task Submit()
    {
        if (Result is not null)
        {
            await CompleteAsync(Result.IsCorrect);
            return;
        }
        if (string.IsNullOrWhiteSpace(Answer))
        {
            ValidationMessage = "Type an answer first.";
            return;
        }
        Result = AnswerChecker.Check(Answer, scenario.ExpectedAnswers);
    }
}

public partial class GenderScenarioViewModel(GenderScenario scenario, Func<Scenario, bool, Task> onCompleted)
    : ScenarioViewModel(scenario, onCompleted)
{
    public static IReadOnlyList<Option<Article>> Articles { get; } =
    [
        new(Article.El, "el"), new(Article.La, "la"), new(Article.Los, "los"), new(Article.Las, "las")
    ];

    public override string Heading => "Pick the article";
    public string Word => scenario.Word;
    public string Translation => scenario.Noun.TranslationDisplay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChecked), nameof(IsCorrect), nameof(IsIncorrect), nameof(Feedback), nameof(ShowNotes))]
    private Article? _chosen;

    public bool IsChecked => Chosen is not null;
    public bool ShowNotes => IsChecked && HasNotes;
    public bool IsCorrect => Chosen == scenario.Expected;
    public bool IsIncorrect => IsChecked && !IsCorrect;

    public string? Feedback => Chosen is null
        ? null
        : IsCorrect
            ? $"Correct: {scenario.Expected.ToText()} {Word}"
            : $"It's {scenario.Expected.ToText()} {Word}";

    [RelayCommand]
    private void Choose(Article article) => Chosen ??= article;

    /// <summary>Enter: moves on once an article was chosen.</summary>
    [RelayCommand]
    private Task Submit() => Chosen is null ? Task.CompletedTask : CompleteAsync(IsCorrect);
}
