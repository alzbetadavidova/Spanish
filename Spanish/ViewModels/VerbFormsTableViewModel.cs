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
    /// <summary>Shown for a form the verb doesn't have (e.g. a tense that was left empty).</summary>
    public const string Missing = "—";

    public VerbFormsTableViewModel(Verb verb)
    {
        ArgumentNullException.ThrowIfNull(verb);
        Rows = Enumerable.Range(0, Verb.PersonCount)
            .Select(i => new VerbFormRow(Verb.PersonLabels[i], At(verb.PresentConjugations, i), At(verb.PreteriteConjugations, i)))
            .ToList();
        Gerund = OrMissing(verb.NonPersonalGerund);
    }

    public IReadOnlyList<VerbFormRow> Rows { get; }
    public string Gerund { get; }

    // A hand-edited file may hold fewer forms than persons.
    private static string At(string[] forms, int index) => index < forms.Length ? OrMissing(forms[index]) : Missing;

    private static string OrMissing(string? form) => string.IsNullOrWhiteSpace(form) ? Missing : form.Trim();
}
