namespace Spanish.Core;

public class ScenarioFactory(IRandomSource random)
{
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
        var detail = unit is Noun { PluralValue.Length: > 0 } noun
            ? $"{noun.PluralArticle.ToText()} {noun.PluralValue}"
            : null;

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

    private static string SpanishDisplay(LearnUnit unit) =>
        unit is Noun noun ? $"{noun.SingularArticle.ToText()} {noun.BaseValue}" : unit.BaseValue;
}
