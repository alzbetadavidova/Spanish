namespace Spanish.Core;

public enum IrregularityKind
{
    ElBeforeStressedA, // el agua
    MasculineEndingInA, // el día
    FeminineEndingInO, // la mano
    IrregularPlural, // exámenes
    IrregularAdjectiveForms, // española
    IrregularPresent, // tengo
    IrregularPreterite, // tuve
    IrregularGerund // yendo
}

/// <summary>A way a word breaks the usual rules, with the reason shown to the learner.</summary>
public sealed record Irregularity(IrregularityKind Kind, string Reason);

/// <summary>Finds where a word breaks the rules that the suggesters (and learners) apply.</summary>
public static class Irregularities
{
    // "él / ella / usted" -> "él"
    private static readonly string[] Subjects = Verb.PersonLabels.Select(l => l.Split(" / ")[0]).ToArray();

    private static readonly IrregularityKind[] GenderKinds =
        [IrregularityKind.ElBeforeStressedA, IrregularityKind.MasculineEndingInA, IrregularityKind.FeminineEndingInO];

    public static IReadOnlyList<Irregularity> Of(LearnUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        if (string.IsNullOrWhiteSpace(unit.BaseValue))
        {
            return [];
        }
        return unit switch
        {
            Noun noun => OfNoun(noun),
            Verb verb => OfVerb(verb),
            Adjective adjective => OfAdjective(adjective),
            _ => []
        };
    }

    /// <summary>The irregularities that matter when <paramref name="unit"/> is practiced as <paramref name="type"/>.</summary>
    public static IReadOnlyList<Irregularity> For(LearnUnit unit, ScenarioType type) =>
        Of(unit).Where(i => IsRelevant(i.Kind, type)).ToList();

    /// <summary>The gender and article irregularities of a noun, which decide how an adjective agrees with it.</summary>
    public static IReadOnlyList<Irregularity> OfPairedNoun(Noun noun) =>
        Of(noun).Where(i => GenderKinds.Contains(i.Kind)).ToList();

    private static bool IsRelevant(IrregularityKind kind, ScenarioType type) => type switch
    {
        ScenarioType.Card or ScenarioType.Fill => true,
        ScenarioType.Gender => GenderKinds.Contains(kind),
        ScenarioType.Plural => kind == IrregularityKind.IrregularPlural,
        ScenarioType.Present => kind == IrregularityKind.IrregularPresent,
        ScenarioType.Preterite => kind == IrregularityKind.IrregularPreterite,
        ScenarioType.Gerund => kind == IrregularityKind.IrregularGerund,
        ScenarioType.PairWithNoun => kind == IrregularityKind.IrregularAdjectiveForms,
        _ => false
    };

    private static List<Irregularity> OfNoun(Noun noun)
    {
        var word = noun.BaseValue.Trim();
        var plural = noun.PluralValue.Trim();
        // The rules apply to the head of a compound: fin de semana -> fines de semana.
        var head = word.Split(' ')[0];
        var result = new List<Irregularity>();

        if (noun.Gender == Gender.Feminine && noun.TakesElInSingular)
        {
            var pluralPart = plural.Length > 0 ? $", las {plural}" : string.Empty;
            result.Add(new(IrregularityKind.ElBeforeStressedA,
                $"Feminine, but takes el in the singular because it starts with a stressed a: el {word}{pluralPart}."));
        }
        if (noun.Gender == Gender.Masculine && EndsWith(head, 'a'))
        {
            result.Add(new(IrregularityKind.MasculineEndingInA, $"Masculine although it ends in -a: el {word}."));
        }
        if (noun.Gender == Gender.Feminine && EndsWith(head, 'o'))
        {
            result.Add(new(IrregularityKind.FeminineEndingInO, $"Feminine although it ends in -o: la {word}."));
        }

        var regularPlural = PluralSuggester.Suggest(head) + word[head.Length..];
        if (plural.Length > 0 && !Same(plural, regularPlural))
        {
            result.Add(new(IrregularityKind.IrregularPlural,
                $"Irregular plural: {plural} (the regular rule gives {regularPlural})."));
        }
        return result;
    }

    private static List<Irregularity> OfAdjective(Adjective adjective)
    {
        var regular = AdjectiveFormSuggester.Suggest(adjective.BaseValue);
        var forms = new[]
        {
            (Label: "feminine", Form: adjective.GetForm(Gender.Feminine, false), Regular: regular.Feminine),
            (Label: "masculine plural", Form: adjective.GetForm(Gender.Masculine, true), Regular: regular.MasculinePlural),
            (Label: "feminine plural", Form: adjective.GetForm(Gender.Feminine, true), Regular: regular.FemininePlural)
        };
        var irregular = forms.Where(f => !Same(f.Form, f.Regular)).Select(f => $"{f.Form.Trim()} ({f.Label})").ToList();
        return irregular.Count == 0
            ? []
            : [new(IrregularityKind.IrregularAdjectiveForms, $"Irregular forms: {string.Join(", ", irregular)}.")];
    }

    private static List<Irregularity> OfVerb(Verb verb)
    {
        var regular = VerbFormSuggester.Suggest(verb.BaseValue);
        var result = new List<Irregularity>();
        if (regular is null)
        {
            return result;
        }

        AddTense(result, IrregularityKind.IrregularPresent, "present", verb.PresentConjugations, regular.Present);
        AddTense(result, IrregularityKind.IrregularPreterite, "preterite", verb.PreteriteConjugations, regular.Preterite);
        var gerund = verb.NonPersonalGerund.Trim();
        if (gerund.Length > 0 && !Same(gerund, regular.Gerund))
        {
            result.Add(new(IrregularityKind.IrregularGerund, $"Irregular gerund: {gerund}."));
        }
        return result;
    }

    /// <summary>Lists the persons whose form differs from the regular one; incomplete tenses are skipped.</summary>
    private static void AddTense(List<Irregularity> result, IrregularityKind kind, string tense, string[] forms,
        IReadOnlyList<string> regular)
    {
        if (!Verb.IsComplete(forms))
        {
            return;
        }
        var irregular = Enumerable.Range(0, Verb.PersonCount)
            .Where(i => !Same(forms[i], regular[i]))
            .Select(i => $"{Subjects[i]} {forms[i].Trim()}")
            .ToList();
        if (irregular.Count > 0)
        {
            result.Add(new(kind, $"Irregular {tense}: {string.Join(" · ", irregular)}."));
        }
    }

    private static bool EndsWith(string word, char letter) => char.ToLowerInvariant(word[^1]) == letter;

    private static bool Same(string form, string regular) =>
        string.Equals(form.Trim(), regular, StringComparison.OrdinalIgnoreCase);
}
