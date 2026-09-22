using System;
using System.Collections.Generic;
using System.ComponentModel;
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

    /// <summary>All forms of the verb in a conjugation exercise, shown after a wrong answer; null for other exercises.</summary>
    public VerbFormsTableViewModel? VerbForms { get; } =
        scenario is { Unit: Verb verb } && scenario.Type.IsConjugation() ? new VerbFormsTableViewModel(verb) : null;

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
        EndingsScenario endings => new EndingsScenarioViewModel(endings, onCompleted),
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
    [NotifyPropertyChangedFor(nameof(IsChecked), nameof(IsCorrect), nameof(IsIncorrect), nameof(Feedback), nameof(ShowNotes),
        nameof(ShowVerbForms))]
    private AnswerResult? _result;

    public bool IsChecked => Result is not null;
    public bool ShowNotes => IsChecked && HasNotes;
    public bool IsCorrect => Result?.IsCorrect == true;
    public bool IsIncorrect => Result is { IsCorrect: false };
    public bool ShowVerbForms => IsIncorrect && VerbForms is not null;

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

/// <summary>One person of an endings exercise: the root is shown, the ending is typed.</summary>
public partial class EndingRowViewModel(EndingRow row) : ObservableObject
{
    public string Person => row.Person;
    public string Root => row.Root;
    public string Placeholder => row.HasRoot ? "ending" : "whole form";

    [ObservableProperty]
    private string _answer = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChecked), nameof(IsCorrect), nameof(IsIncorrect), nameof(HasAccentHint), nameof(Feedback))]
    private AnswerResult? _result;

    public bool IsChecked => Result is not null;
    public bool IsCorrect => Result?.IsCorrect == true;
    public bool IsIncorrect => Result is { IsCorrect: false };
    public bool HasAccentHint => Result?.Outcome == AnswerOutcome.CorrectWithAccentHint;

    /// <summary>A tick when right; the whole form when the accents differ or (after an arrow) when wrong.</summary>
    public string? Feedback => Result?.Outcome switch
    {
        null => null,
        AnswerOutcome.Correct => "✓",
        AnswerOutcome.CorrectWithAccentHint => $"✓ {row.Form}",
        _ => $"→ {row.Form}"
    };

    public void Check() => Result = AnswerChecker.Check(Answer, row.ExpectedAnswers);
}

public partial class EndingsScenarioViewModel : ScenarioViewModel
{
    private readonly EndingsScenario _scenario;

    public EndingsScenarioViewModel(EndingsScenario scenario, Func<Scenario, bool, Task> onCompleted)
        : base(scenario, onCompleted)
    {
        _scenario = scenario;
        Rows = scenario.Rows.Select(r => new EndingRowViewModel(r)).ToList();
        foreach (var row in Rows)
        {
            row.PropertyChanged += OnRowChanged;
        }
    }

    public override string Heading => _scenario.Instruction;
    public string Prompt => _scenario.Prompt;
    public string? PromptDetail => _scenario.PromptDetail;
    public IReadOnlyList<EndingRowViewModel> Rows { get; }

    [ObservableProperty]
    private string? _validationMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCorrect), nameof(IsIncorrect), nameof(Feedback), nameof(ShowNotes), nameof(ShowVerbForms))]
    private bool _isChecked;

    public bool ShowNotes => IsChecked && HasNotes;
    public bool IsCorrect => IsChecked && Rows.All(r => r.IsCorrect);
    public bool IsIncorrect => IsChecked && !IsCorrect;
    public bool ShowVerbForms => IsIncorrect && VerbForms is not null;

    public string? Feedback => !IsChecked
        ? null
        : IsIncorrect
            ? $"{Rows.Count(r => r.IsCorrect)} of {Rows.Count} correct"
            : Rows.Any(r => r.HasAccentHint)
                ? "Correct. Watch the accents"
                : "Correct";

    /// <summary>
    /// Where Enter moves from row <paramref name="index"/>: the next empty row (wrapping around), or null
    /// when the other rows are filled and Enter should submit. Checked answers are all filled.
    /// </summary>
    public int? NextEmptyRow(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Rows.Count);
        for (var step = 1; step < Rows.Count; step++)
        {
            var next = (index + step) % Rows.Count;
            if (string.IsNullOrWhiteSpace(Rows[next].Answer))
            {
                return next;
            }
        }
        return null;
    }

    /// <summary>Enter: checks the endings, or moves on when they were already checked.</summary>
    [RelayCommand]
    private async Task Submit()
    {
        if (IsChecked)
        {
            await CompleteAsync(IsCorrect);
            return;
        }
        if (Rows.Any(r => string.IsNullOrWhiteSpace(r.Answer)))
        {
            ValidationMessage = "Fill in every ending first.";
            return;
        }
        foreach (var row in Rows)
        {
            row.Check();
        }
        IsChecked = true;
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EndingRowViewModel.Answer))
        {
            ValidationMessage = null;
        }
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
