namespace Spanish.Core;

public class ScenarioFactory(IRandomSource random, LearnLibrary library)
{
    public const string AdjectivePairInstruction = "Put the adjective with the noun";
    public const string PrepositionPairInstruction = "Translate the phrase to Spanish";
    public const string NumberToTextInstruction = "Write it in Spanish words";
    public const string TextToNumberInstruction = "Write it in digits";

    private static readonly string[] Subjects = Verb.ConjugationsDefinitions.Keys.ToArray();

    /// <exception cref="ArgumentException">The unit cannot be practiced with <paramref name="type"/>.</exception>
    public Scenario Create(LearnUnit unit, ScenarioType type, Direction direction)
    {
        ArgumentNullException.ThrowIfNull(unit);
        if (!unit.CanPractice(type))
        {
            throw new ArgumentException($"\"{unit.BaseValue}\" cannot be practiced as {type}.", nameof(type));
        }

        Scenario scenario = (unit, type) switch
        {
            (_, ScenarioType.Card) => CreateCard(unit, Resolve(direction)),
            (_, ScenarioType.Fill) => CreateFill(unit, Resolve(direction)),
            (Noun noun, ScenarioType.Gender) => CreateGender(noun),
            (Noun noun, ScenarioType.Plural) => CreatePlural(noun),
            (Verb verb, ScenarioType.Present) => CreateConjugation(verb, type, "Conjugate · present", verb.PresentConjugations),
            (Verb verb, ScenarioType.Preterite) => CreateConjugation(verb, type, "Conjugate · preterite (past)", verb.PreteriteConjugations),
            (Verb verb, ScenarioType.Gerund) => CreateGerund(verb),
            (Verb verb, ScenarioType.PresentEndings) => CreateEndings(verb, type, "Fill in the endings · present",
                verb.PresentConjugations, VerbFormSuggester.Suggest(verb.BaseValue)?.Present),
            (Verb verb, ScenarioType.PreteriteEndings) => CreateEndings(verb, type, "Fill in the endings · preterite (past)",
                verb.PreteriteConjugations, VerbFormSuggester.Suggest(verb.BaseValue)?.Preterite),
            (Adjective adjective, ScenarioType.PairWithNoun) => CreatePair(adjective),
            (Preposition preposition, ScenarioType.PairWithNoun) => CreatePrepositionPair(preposition),
            (Numeral numeral, ScenarioType.NumberToText or ScenarioType.TextToNumber) => CreateNumeral(numeral, type),
            _ => throw new ArgumentException($"Unsupported scenario {type}.", nameof(type))
        };
        // A pair also explains its noun, so it adds its own notes.
        return type == ScenarioType.PairWithNoun ? scenario : WithNotes(scenario, Irregularities.For(unit, type));
    }

    private Direction Resolve(Direction direction) => direction == Direction.Mixed
        ? random.Next(2) == 0 ? Direction.EnglishToSpanish : Direction.SpanishToEnglish
        : direction;

    private CardScenario CreateCard(LearnUnit unit, Direction direction)
    {
        var english = unit.TranslationDisplay;
        var spanish = SpanishDisplay(unit);
        var detail = unit switch
        {
            Noun { PluralValue.Length: > 0 } noun => $"{noun.PluralArticle.ToText()} {noun.PluralValue}",
            Adjective adjective => string.Join(" · ",
                adjective.GetForm(Gender.Feminine, false),
                adjective.GetForm(Gender.Masculine, true),
                adjective.GetForm(Gender.Feminine, true)),
            // The learner may have recalled another preposition with the same meaning.
            Preposition preposition when SynonymsOf(preposition) is { Count: > 0 } synonyms =>
                $"also: {string.Join(", ", synonyms)}",
            _ => null
        };

        return direction == Direction.EnglishToSpanish
            ? new CardScenario(unit, direction, english, spanish, detail)
            : new CardScenario(unit, direction, spanish, english, null);
    }

    private TypedScenario CreateFill(LearnUnit unit, Direction direction)
    {
        if (direction == Direction.EnglishToSpanish)
        {
            IReadOnlyList<string> expected = unit switch
            {
                Noun noun => [noun.BaseValue, SpanishDisplay(noun)],
                Preposition preposition => [preposition.BaseValue.Trim(), ..SynonymsOf(preposition)],
                _ => [unit.BaseValue]
            };
            return new TypedScenario(unit, ScenarioType.Fill, "Translate to Spanish",
                unit.TranslationDisplay, null, expected);
        }

        var english = unit.TranslationAlternatives.ToList();
        if (unit is Verb)
        {
            // "to speak" and "speak" are both accepted.
            english.AddRange(unit.TranslationAlternatives.Select(t =>
                t.StartsWith("to ", StringComparison.OrdinalIgnoreCase) ? t[3..].Trim() : $"to {t}"));
        }
        return new TypedScenario(unit, ScenarioType.Fill, "Translate to English", SpanishDisplay(unit), null, english);
    }

    private GenderScenario CreateGender(Noun noun)
    {
        var usePlural = noun.PluralValue.Length > 0 && random.Next(2) == 1;
        return usePlural
            ? new GenderScenario(noun, noun.PluralValue, noun.PluralArticle)
            : new GenderScenario(noun, noun.BaseValue, noun.SingularArticle);
    }

    private static TypedScenario CreatePlural(Noun noun) =>
        new(noun, ScenarioType.Plural, "Type the plural", noun.BaseValue, noun.TranslationDisplay,
            [noun.PluralValue, $"{noun.PluralArticle.ToText()} {noun.PluralValue}"]);

    private TypedScenario CreateConjugation(Verb verb, ScenarioType type, string instruction, string[] forms)
    {
        var subject = Subjects[random.Next(Subjects.Length)];
        var form = forms[Verb.ConjugationsDefinitions[subject]];
        return new TypedScenario(verb, type, instruction, verb.BaseValue, subject.ToLowerInvariant(),
            [form, $"{subject} {form}"]);
    }

    private static TypedScenario CreateGerund(Verb verb) =>
        new(verb, ScenarioType.Gerund, "Type the gerund", verb.BaseValue, verb.TranslationDisplay, [verb.NonPersonalGerund]);

    /// <summary>
    /// hablar: habl + o, habl + as, … Irregular forms are typed whole. A verb whose infinitive doesn't end in
    /// -ar, -er or -ir has no root and no <paramref name="regularForms"/>, so all its forms are typed whole.
    /// </summary>
    private static EndingsScenario CreateEndings(Verb verb, ScenarioType type, string instruction, string[] forms,
        IReadOnlyList<string>? regularForms)
    {
        var root = VerbFormSuggester.StemOf(verb.BaseValue) ?? string.Empty;
        var rows = forms.Select((form, i) => EndingRow.Create(Verb.PersonLabels[i], root, form, regularForms?[i])).ToList();
        return new EndingsScenario(verb, type, instruction, verb.BaseValue, verb.TranslationDisplay, rows);
    }

    /// <summary>bajo + mujeres: the user types "mujeres bajas" (the article is optional).</summary>
    private Scenario CreatePair(Adjective adjective)
    {
        var noun = DrawLinkedNoun(adjective);
        var plural = noun.PluralValue.Length > 0 && random.Next(2) == 1;
        var nounForm = plural ? noun.PluralValue : noun.BaseValue;
        var article = plural ? noun.PluralArticle : noun.SingularArticle;
        var phrase = $"{nounForm} {adjective.GetForm(noun.Gender, plural)}";

        var detail = adjective.TranslationAlternatives.Count == 0 || noun.TranslationAlternatives.Count == 0
            ? null
            : $"{adjective.TranslationAlternatives[0]} + {noun.TranslationAlternatives[0]}{(plural ? " (plural)" : string.Empty)}";

        var pair = new TypedScenario(adjective, ScenarioType.PairWithNoun, AdjectivePairInstruction,
            $"{adjective.BaseValue} + {nounForm}", detail, [phrase, $"{article.ToText()} {phrase}"]);
        return WithNotes(pair, [..Irregularities.For(adjective, ScenarioType.PairWithNoun), ..Irregularities.OfPairedNoun(noun)]);
    }

    /// <summary>
    /// de + parque: the user translates "from the park" to "del parque"; the Spanish noun is shown so that only
    /// the preposition and the article are asked. Other prepositions meaning "from" are accepted too (desde el parque).
    /// Only singular nouns are used: the English plural isn't known, and a/de contract only with el.
    /// </summary>
    private Scenario CreatePrepositionPair(Preposition preposition)
    {
        var noun = DrawLinkedNoun(preposition);
        var english = preposition.TranslationAlternatives[random.Next(preposition.TranslationAlternatives.Count)];
        var answers = PrepositionsMeaning([english])
            .Select(p => p.WithNoun(noun))
            .Prepend(preposition.WithNoun(noun))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var pair = new TypedScenario(preposition, ScenarioType.PairWithNoun, PrepositionPairInstruction,
            $"{english} the {noun.TranslationAlternatives[0]}", noun.BaseValue, answers);
        return WithNotes(pair, Irregularities.OfPairedNoun(noun));
    }

    /// <summary>The other prepositions of the library meaning one of <paramref name="preposition"/>'s alternatives.</summary>
    private List<string> SynonymsOf(Preposition preposition)
    {
        var word = preposition.BaseValue.Trim();
        return PrepositionsMeaning(preposition.TranslationAlternatives)
            .Select(p => p.BaseValue.Trim())
            .Where(p => !string.Equals(p, word, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The library's prepositions meaning one of <paramref name="english"/>. Prepositions often share a meaning
    /// (after: tras, después de), so each of them is a right answer. Alternatives must match exactly: "on the table"
    /// accepts sobre la mesa only while en has no "on" (which would give prompts like "on the house").
    /// </summary>
    private IEnumerable<Preposition> PrepositionsMeaning(IReadOnlyList<string> english) =>
        library.Prepositions.Where(p => !string.IsNullOrWhiteSpace(p.BaseValue)
                                        && p.TranslationAlternatives.Any(t => english.Contains(t, StringComparer.OrdinalIgnoreCase)));

    /// <summary>A random linked noun the pair can use.</summary>
    private Noun DrawLinkedNoun(NounLinkedUnit unit)
    {
        var nouns = library.LinkedNounsOf(unit).Where(unit.CanPairWith).ToList();
        if (nouns.Count == 0)
        {
            // Links are kept in sync with the library, so the word is not in it, or (a preposition) none of its
            // nouns has a translation.
            throw new ArgumentException($"\"{unit.BaseValue}\" has no linked noun in the library to pair with.", nameof(unit));
        }
        return nouns[random.Next(nouns.Count)];
    }

    /// <summary>A random value of the numeral's category, e.g. 21 -> veintiuno or the other way round.</summary>
    private TypedScenario CreateNumeral(Numeral numeral, ScenarioType type)
    {
        var forms = numeral.Draw(random);
        return type == ScenarioType.NumberToText
            ? new TypedScenario(numeral, type, NumberToTextInstruction, forms.Digits[0], null, forms.Words)
            : new TypedScenario(numeral, type, TextToNumberInstruction, forms.Words[0], DigitsFormat(numeral.Subtype), forms.Digits);
    }

    private static string? DigitsFormat(NumeralSubtype subtype) => subtype switch
    {
        NumeralSubtype.Date => "day/month/year",
        NumeralSubtype.Time => "hour:minute, 24-hour clock",
        NumeralSubtype.DateAndTime => "day/month/year hour:minute",
        _ => null
    };

    /// <summary>Scenarios without notes keep the shared empty list, so they compare equal.</summary>
    private static Scenario WithNotes(Scenario scenario, IReadOnlyList<Irregularity> notes) =>
        notes.Count == 0 ? scenario : scenario with { Notes = notes };

    private static string SpanishDisplay(LearnUnit unit) =>
        unit is Noun noun ? $"{noun.SingularArticle.ToText()} {noun.BaseValue}" : unit.BaseValue;
}
