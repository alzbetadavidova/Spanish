using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Spanish.Core;

namespace Spanish.ViewModels;

/// <summary>The built-in numerals; they can be browsed but not edited.</summary>
public partial class NumeralListViewModel(LearnLibrary library) : ObservableObject
{
    public IReadOnlyList<Numeral> Items { get; } = library.Numerals;

    // There are always several built-in numerals.
    public string CountText => $"{Items.Count} {DisplayNames.Plural(WordKind.Numeral)}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private Numeral? _selected;

    [ObservableProperty]
    private NumeralDetailsViewModel? _details;

    public bool HasSelection => Selected is not null;

    partial void OnSelectedChanged(Numeral? value) => Details = value is null ? null : new NumeralDetailsViewModel(value);
}

/// <summary>What a numeral practices, with examples.</summary>
public class NumeralDetailsViewModel(Numeral numeral)
{
    public const string BuiltInNote = "Numerals are built in and can't be edited.";

    public Numeral Numeral { get; } = numeral;
    public string Title => Numeral.BaseValue;
    public string English => Numeral.TranslationDisplay;
    public string Subtype => DisplayNames.Subtype(Numeral.Subtype);
    public string Scenarios => string.Join(" · ", Numeral.SupportedScenarios.Select(DisplayNames.Scenario));

    /// <summary>E.g. "21 → veintiuno".</summary>
    public IReadOnlyList<string> Examples { get; } = numeral.Examples.Select(e => $"{e.Digits[0]} → {e.Words[0]}").ToList();
}
