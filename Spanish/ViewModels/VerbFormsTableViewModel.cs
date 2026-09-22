using System;
using System.Collections.Generic;
using System.Linq;
using Spanish.Core;

namespace Spanish.ViewModels;

/// <summary>One person's forms in <see cref="VerbFormsTableViewModel"/>.</summary>
public record VerbFormRow(string Person, string Present, string Preterite);

/// <summary>A verb's forms laid out like the verb editor; shown after a wrong conjugation.</summary>
public class VerbFormsTableViewModel
{
    public const string Missing = "—";

    public VerbFormsTableViewModel(Verb verb)
    {
        ArgumentNullException.ThrowIfNull(verb);
        Rows = Enumerable.Range(0, Verb.PersonCount)
            .Select(i => new VerbFormRow(Verb.PersonLabels[i], At(verb.PresentConjugations, i), At(verb.PreteriteConjugations, i)))
            .ToList();
        HasConjugations = Rows.Any(r => r.Present != Missing || r.Preterite != Missing);
        Gerund = verb.NonPersonalGerund.Trim();
    }

    /// <summary>A missing form (e.g. a tense that was left empty) is shown as <see cref="Missing"/>.</summary>
    public IReadOnlyList<VerbFormRow> Rows { get; }
    public bool HasConjugations { get; }
    public string Gerund { get; }
    public bool HasGerund => Gerund.Length > 0;

    // A hand-edited file may hold fewer forms than persons.
    private static string At(string[] forms, int index) =>
        index < forms.Length && !string.IsNullOrWhiteSpace(forms[index]) ? forms[index].Trim() : Missing;
}
