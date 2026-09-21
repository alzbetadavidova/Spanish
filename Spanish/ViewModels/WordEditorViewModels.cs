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

    protected WordEditorViewModel(LibraryContext context, LearnUnit? existing, EditorCallbacks callbacks)
    {
        _context = context;
        _callbacks = callbacks;
        Existing = existing;
        _spanish = existing?.BaseValue ?? string.Empty;
        _english = existing?.Translation ?? string.Empty;
        foreach (var topic in context.Library.Topics.Order())
        {
            Topics.Add(new ToggleOption<string>(topic, topic, existing?.HasTopic(topic) == true));
        }
    }

    public LearnUnit? Existing { get; }
    public bool IsNew => Existing is null;
    public abstract string Title { get; }
    public ObservableCollection<ToggleOption<string>> Topics { get; } = [];
    public bool HasTopics => Topics.Count > 0;

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
    private bool _suggesting;

    public NounEditorViewModel(LibraryContext context, Noun? existing, EditorCallbacks callbacks)
        : base(context, existing, callbacks)
    {
        _gender = existing?.Gender ?? Gender.Masculine;
        _plural = existing?.PluralValue ?? string.Empty;
        _pluralEditedByUser = _plural.Length > 0;
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

    protected override void OnSpanishEdited(string value)
    {
        if (_pluralEditedByUser)
        {
            return;
        }
        _suggesting = true;
        Plural = PluralSuggester.Suggest(value);
        _suggesting = false;
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
        PluralValue = Plural.Trim()
    };

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

    private static string At(string[] forms, int index) => index < forms.Length ? forms[index] : string.Empty;
}
