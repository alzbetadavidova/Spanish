namespace Spanish.Core;

public class ScenarioFactory(IRandomSource random, LearnLibrary library)
{
    public const string PairInstruction = "Put the adjective with the noun";

    private static readonly string[] Subjects = Verb.ConjugationsDefinitions.Keys.ToArray();

    /// <exception cref="ArgumentException">The unit cannot be practiced with <paramref name="type"/>.</exception>
    public Scenario Create(LearnUnit unit, ScenarioType type, Direction direction)
    {
        ArgumentNullException.ThrowIfNull(unit);
        if (!unit.CanPractice(type))
        {
            throw new ArgumentException($"\"{unit.BaseValue}\" cannot be practiced as {type}.", nameof(type));
        }

        return (unit, type) switch
        {
            (_, ScenarioType.Card) => CreateCard(unit, Resolve(direction)),
            (_, ScenarioType.Fill) => CreateFill(unit, Resolve(direction)),
            (Noun noun, ScenarioType.Gender) => CreateGender(noun),
            (Noun noun, ScenarioType.Plural) => CreatePlural(noun),
            (Verb verb, ScenarioType.Present) => CreateConjugation(verb, type, "Conjugate · present", verb.PresentConjugations),
            (Verb verb, ScenarioType.Preterite) => CreateConjugation(verb, type, "Conjugate · preterite (past)", verb.PreteriteConjugations),
            (Verb verb, ScenarioType.Gerund) => CreateGerund(verb),
            (Adjective adjective, ScenarioType.PairWithNoun) => CreatePair(adjective),
            _ => throw new ArgumentException($"Unsupported scenario {type}.", nameof(type))
        };
    }

    private Direction Resolve(Direction direction) => direction == Direction.Mixed
        ? random.Next(2) == 0 ? Direction.EnglishToSpanish : Direction.SpanishToEnglish
        : direction;

    private static CardScenario CreateCard(LearnUnit unit, Direction direction)
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
            _ => null
        };

        return direction == Direction.EnglishToSpanish
            ? new CardScenario(unit, direction, english, spanish, detail)
            : new CardScenario(unit, direction, spanish, english, null);
    }

    private static TypedScenario CreateFill(LearnUnit unit, Direction direction)
    {
        if (direction == Direction.EnglishToSpanish)
        {
            var expected = unit is Noun noun
                ? new[] { noun.BaseValue, SpanishDisplay(noun) }
                : new[] { unit.BaseValue };
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

    /// <summary>bajo + mujeres: the user types "mujeres bajas" (the article is optional).</summary>
    private TypedScenario CreatePair(Adjective adjective)
    {
        var nouns = library.LinkedNounsOf(adjective);
        if (nouns.Count == 0)
        {
            // Links are kept in sync with the library, so this means the adjective is not in it.
            throw new ArgumentException($"\"{adjective.BaseValue}\" is not linked to a noun in the library.", nameof(adjective));
        }

        var noun = nouns[random.Next(nouns.Count)];
        var plural = noun.PluralValue.Length > 0 && random.Next(2) == 1;
        var nounForm = plural ? noun.PluralValue : noun.BaseValue;
        var article = plural ? noun.PluralArticle : noun.SingularArticle;
        var phrase = $"{nounForm} {adjective.GetForm(noun.Gender, plural)}";

        var detail = adjective.TranslationAlternatives.Count == 0 || noun.TranslationAlternatives.Count == 0
            ? null
            : $"{adjective.TranslationAlternatives[0]} + {noun.TranslationAlternatives[0]}{(plural ? " (plural)" : string.Empty)}";

        return new TypedScenario(adjective, ScenarioType.PairWithNoun, PairInstruction,
            $"{adjective.BaseValue} + {nounForm}", detail, [phrase, $"{article.ToText()} {phrase}"]);
    }

    private static string SpanishDisplay(LearnUnit unit) =>
        unit is Noun noun ? $"{noun.SingularArticle.ToText()} {noun.BaseValue}" : unit.BaseValue;
}
