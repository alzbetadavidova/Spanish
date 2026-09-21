using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spanish.Core;

namespace Spanish.ViewModels;

/// <summary>Callbacks from an editor to the list that owns it.</summary>
public record EditorCallbacks(Action<LearnUnit> Saved, Action Deleted, Action Cancelled);

public abstract partial class WordEditorViewModel : ObservableObject
{
    private readonly LibraryContext _context;
    private readonly EditorCallbacks _callbacks;
    private readonly Dictionary<string, bool> _topicBaseline = new(StringComparer.OrdinalIgnoreCase);

    protected WordEditorViewModel(LibraryContext context, LearnUnit? existing, EditorCallbacks callbacks)
    {
        _context = context;
        _callbacks = callbacks;
        Existing = existing;
        _spanish = existing?.BaseValue ?? string.Empty;
        _english = existing?.Translation ?? string.Empty;
        ExceptionNotes = existing is null ? [] : Irregularities.Of(existing).Select(i => i.Reason).ToList();
        RefreshTopics();
    }

    protected LibraryContext Context => _context;
    public LearnUnit? Existing { get; }
    public bool IsNew => Existing is null;
    public abstract string Title { get; }
    public ObservableCollection<ToggleOption<string>> Topics { get; } = [];
    public bool HasTopics => Topics.Count > 0;

    /// <summary>Why the saved word breaks the usual rules.</summary>
    public IReadOnlyList<string> ExceptionNotes { get; }
    public bool HasExceptionNotes => ExceptionNotes.Count > 0;

    [ObservableProperty]
    private string _spanish;

    [ObservableProperty]
    private string _english;

    [ObservableProperty]
    private string? _spanishError;

    [ObservableProperty]
    private string? _englishError;

    [ObservableProperty]
    private string? _topicsError;

    [ObservableProperty]
    private bool _isConfirmingDelete;

    public string DeleteLabel => IsConfirmingDelete ? "Confirm delete" : "Delete";

    partial void OnIsConfirmingDeleteChanged(bool value) => OnPropertyChanged(nameof(DeleteLabel));
    partial void OnSpanishChanged(string value)
    {
        SpanishError = null;
        OnSpanishEdited(value);
    }

    /// <summary>Lets editors react to the Spanish word being edited.</summary>
    protected virtual void OnSpanishEdited(string value)
    {
    }
    partial void OnEnglishChanged(string value) => EnglishError = null;

    /// <summary>
    /// Rebuilds the topic chips from the library. Chips the user changed keep their state; the others
    /// follow the word's current topics (which may have changed on the Topics tab).
    /// </summary>
    public void RefreshTopics()
    {
        var edited = Topics
            .Where(t => t.IsSelected != _topicBaseline.GetValueOrDefault(t.Value))
            .ToDictionary(t => t.Value, t => t.IsSelected, StringComparer.OrdinalIgnoreCase);

        Topics.Clear();
        _topicBaseline.Clear();
        foreach (var topic in _context.Library.Topics.Order())
        {
            var current = Existing?.HasTopic(topic) == true;
            _topicBaseline[topic] = current;
            Topics.Add(new ToggleOption<string>(topic, topic, edited.GetValueOrDefault(topic, current)));
        }
        OnPropertyChanged(nameof(HasTopics));
    }

    /// <summary>Brings every library-based choice (topics, and e.g. linked nouns) up to date.</summary>
    public virtual void RefreshChoices() => RefreshTopics();

    [RelayCommand]
    private async Task Save()
    {
        var candidate = BuildCandidate();
        candidate.BaseValue = Spanish;
        candidate.Translation = English.Trim();
        candidate.Topics = Topics.Where(t => t.IsSelected).Select(t => t.Value).ToList();

        var errors = _context.Library.Validate(candidate, Existing);
        ShowErrors(errors);
        if (errors.Count > 0)
        {
            return;
        }

        _context.Library.Save(candidate, Existing);
        await _context.SaveAsync();
        _callbacks.Saved(Existing ?? candidate);
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task Delete()
    {
        if (!IsConfirmingDelete)
        {
            IsConfirmingDelete = true;
            return;
        }
        if (Existing is null)
        {
            return;
        }
        _context.Library.Remove(Existing);
        await _context.SaveAsync();
        _callbacks.Deleted();
    }

    private bool CanDelete() => !IsNew;

    [RelayCommand]
    private void Cancel() => _callbacks.Cancelled();

    /// <summary>Creates a unit with the kind-specific fields filled in.</summary>
    protected abstract LearnUnit BuildCandidate();

    protected virtual void ShowErrors(IReadOnlyList<ValidationError> errors)
    {
        SpanishError = Message(errors, nameof(LearnUnit.BaseValue));
        EnglishError = Message(errors, nameof(LearnUnit.Translation));
        TopicsError = Message(errors, nameof(LearnUnit.Topics));
    }

    protected static string? Message(IReadOnlyList<ValidationError> errors, string field) =>
        errors.FirstOrDefault(e => e.Field == field)?.Message;
}

public partial class NounEditorViewModel : WordEditorViewModel
{
    private bool _pluralEditedByUser;
    private bool _takesElEditedByUser;
    private bool _suggesting;

    public NounEditorViewModel(LibraryContext context, Noun? existing, EditorCallbacks callbacks)
        : base(context, existing, callbacks)
    {
        _gender = existing?.Gender ?? Gender.Masculine;
        _plural = existing?.PluralValue ?? string.Empty;
        _pluralEditedByUser = _plural.Length > 0;
        _takesElInSingular = existing?.TakesElInSingular ?? false;
        // A saved feminine noun keeps its choice: la hache stays la hache.
        _takesElEditedByUser = existing is { Gender: Gender.Feminine };
    }

    public override string Title => IsNew ? "New noun" : "Edit noun";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMasculine), nameof(IsFeminine))]
    private Gender _gender;

    // Bound to toggle buttons: clicking the already selected one must not clear the gender.
    public bool IsMasculine
    {
        get => Gender == Gender.Masculine;
        set => SetGender(Gender.Masculine, value);
    }

    public bool IsFeminine
    {
        get => Gender == Gender.Feminine;
        set => SetGender(Gender.Feminine, value);
    }

    [ObservableProperty]
    private string _plural;

    /// <summary>Only offered for feminine nouns: el agua.</summary>
    [ObservableProperty]
    private bool _takesElInSingular;

    protected override void OnSpanishEdited(string value)
    {
        _suggesting = true;
        if (!_pluralEditedByUser)
        {
            Plural = PluralSuggester.Suggest(value);
        }
        SuggestTakesEl();
        _suggesting = false;
    }

    partial void OnGenderChanged(Gender value)
    {
        _suggesting = true;
        SuggestTakesEl();
        _suggesting = false;
    }

    partial void OnTakesElInSingularChanged(bool value)
    {
        if (!_suggesting)
        {
            _takesElEditedByUser = true;
        }
    }

    partial void OnPluralChanged(string value)
    {
        if (!_suggesting)
        {
            _pluralEditedByUser = value.Length > 0;
        }
    }

    protected override LearnUnit BuildCandidate() => new Noun
    {
        Gender = Gender,
        PluralValue = Plural.Trim(),
        TakesElInSingular = Gender == Gender.Feminine && TakesElInSingular
    };

    /// <summary>Follows the Spanish word and gender until the user sets the checkbox.</summary>
    private void SuggestTakesEl()
    {
        if (!_takesElEditedByUser)
        {
            TakesElInSingular = Gender == Gender.Feminine && StressedASuggester.StartsWithStressedA(Spanish);
        }
    }

    private void SetGender(Gender gender, bool selected)
    {
        if (selected)
        {
            Gender = gender;
        }
        else
        {
            // Re-sync the toggle that tried to uncheck itself.
            OnPropertyChanged(gender == Gender.Masculine ? nameof(IsMasculine) : nameof(IsFeminine));
        }
    }
}

public partial class ConjugationRow(string person, string present, string preterite) : ObservableObject
{
    public string Person { get; } = person;

    [ObservableProperty]
    private string _present = present;

    [ObservableProperty]
    private string _preterite = preterite;
}

public partial class VerbEditorViewModel : WordEditorViewModel
{
    public VerbEditorViewModel(LibraryContext context, Verb? existing, EditorCallbacks callbacks)
        : base(context, existing, callbacks)
    {
        var present = existing?.PresentConjugations ?? Verb.EmptyConjugations();
        var preterite = existing?.PreteriteConjugations ?? Verb.EmptyConjugations();
        for (var i = 0; i < Verb.PersonCount; i++)
        {
            Conjugations.Add(new ConjugationRow(Verb.PersonLabels[i], At(present, i), At(preterite, i)));
        }
        _gerund = existing?.NonPersonalGerund ?? string.Empty;
    }

    public override string Title => IsNew ? "New verb" : "Edit verb";

    public ObservableCollection<ConjugationRow> Conjugations { get; } = [];

    [ObservableProperty]
    private string _gerund;

    [ObservableProperty]
    private string? _conjugationError;

    protected override LearnUnit BuildCandidate() => new Verb
    {
        PresentConjugations = Conjugations.Select(c => c.Present.Trim()).ToArray(),
        PreteriteConjugations = Conjugations.Select(c => c.Preterite.Trim()).ToArray(),
        NonPersonalGerund = Gerund.Trim()
    };

    protected override void ShowErrors(IReadOnlyList<ValidationError> errors)
    {
        base.ShowErrors(errors);
        var messages = new[]
            {
                Message(errors, nameof(Verb.PresentConjugations)),
                Message(errors, nameof(Verb.PreteriteConjugations))
            }
            .OfType<string>()
            .ToList();
        ConjugationError = messages.Count == 0 ? null : string.Join(" ", messages);
    }

    private static string At(string[] forms, int index) => index < forms.Length ? forms[index] ?? string.Empty : string.Empty;
}

public partial class AdjectiveEditorViewModel : WordEditorViewModel
{
    private readonly HashSet<string> _nounBaseline = new(StringComparer.OrdinalIgnoreCase);

    public AdjectiveEditorViewModel(LibraryContext context, Adjective? existing, EditorCallbacks callbacks)
        : base(context, existing, callbacks)
    {
        _feminine = existing?.FeminineValue ?? string.Empty;
        _masculinePlural = existing?.MasculinePluralValue ?? string.Empty;
        _femininePlural = existing?.FemininePluralValue ?? string.Empty;
        RefreshNouns();
    }

    public override string Title => IsNew ? "New adjective" : "Edit adjective";

    /// <summary>The nouns the adjective can be paired with.</summary>
    public ObservableCollection<ToggleOption<string>> Nouns { get; } = [];
    public bool HasNouns => Nouns.Count > 0;

    // The form boxes hold only manual fixes; an empty box uses the suggestion shown as its placeholder.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Suggested))]
    private string _feminine;

    [ObservableProperty]
    private string _masculinePlural;

    [ObservableProperty]
    private string _femininePlural;

    [ObservableProperty]
    private string? _nounsError;

    /// <summary>The regular forms of the Spanish word, used where no form is typed.</summary>
    public AdjectiveForms Suggested => AdjectiveFormSuggester.Suggest(Spanish, Feminine);

    public override void RefreshChoices()
    {
        base.RefreshChoices();
        RefreshNouns();
    }

    protected override void OnSpanishEdited(string value) => OnPropertyChanged(nameof(Suggested));

    protected override LearnUnit BuildCandidate()
    {
        var suggested = Suggested;
        return new Adjective
        {
            // A typed form that matches the suggestion is not a fix; keep following the word.
            FeminineValue = Override(Feminine, suggested.Feminine),
            MasculinePluralValue = Override(MasculinePlural, suggested.MasculinePlural),
            FemininePluralValue = Override(FemininePlural, suggested.FemininePlural),
            LinkedNouns = Nouns.Where(n => n.IsSelected).Select(n => n.Value).ToList()
        };
    }

    protected override void ShowErrors(IReadOnlyList<ValidationError> errors)
    {
        base.ShowErrors(errors);
        NounsError = Message(errors, nameof(Adjective.LinkedNouns));
    }

    private static string Override(string typed, string suggested)
    {
        var form = typed.Trim();
        return form == suggested ? string.Empty : form;
    }

    /// <summary>
    /// Rebuilds the noun chips from the library. Chips the user changed keep their state; the others
    /// follow the adjective's current links.
    /// </summary>
    private void RefreshNouns()
    {
        var edited = Nouns
            .Where(n => n.IsSelected != _nounBaseline.Contains(n.Value))
            .ToDictionary(n => n.Value, n => n.IsSelected, StringComparer.OrdinalIgnoreCase);
        var linked = (Existing as Adjective)?.LinkedNouns ?? [];

        foreach (var chip in Nouns)
        {
            chip.PropertyChanged -= OnNounToggled;
        }
        Nouns.Clear();
        _nounBaseline.Clear();
        foreach (var noun in Context.Library.Nouns
                     .Select(n => n.BaseValue)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Order(StringComparer.CurrentCultureIgnoreCase))
        {
            var current = linked.Contains(noun, StringComparer.OrdinalIgnoreCase);
            if (current)
            {
                _nounBaseline.Add(noun);
            }
            var chip = new ToggleOption<string>(noun, noun, edited.GetValueOrDefault(noun, current));
            chip.PropertyChanged += OnNounToggled;
            Nouns.Add(chip);
        }
        OnPropertyChanged(nameof(HasNouns));
    }

    // IsSelected is the only property of a chip that changes.
    private void OnNounToggled(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => NounsError = null;
}
