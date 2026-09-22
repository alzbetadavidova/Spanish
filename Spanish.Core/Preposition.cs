namespace Spanish.Core;

/// <summary>
/// A simple (de) or compound (cerca de) preposition. The pair-with-noun scenario asks for a phrase such as
/// "from the park", answered with the preposition, the noun's article and the noun: del parque.
/// </summary>
public sealed class Preposition : NounLinkedUnit
{
    private static readonly ScenarioType[] Scenarios =
        [ScenarioType.Card, ScenarioType.Fill, ScenarioType.PairWithNoun];

    // a + el and de + el contract to al and del.
    private static readonly string[] Contracting = ["a", "de"];

    public override WordKind Kind => WordKind.Preposition;
    public override IReadOnlyList<ScenarioType> SupportedScenarios => Scenarios;

    /// <summary>The English phrase of the pair names the noun, so it needs the noun's translation.</summary>
    public override bool CanPairWith(Noun noun)
    {
        ArgumentNullException.ThrowIfNull(noun);
        return noun.TranslationAlternatives.Count > 0;
    }

    /// <summary>
    /// The preposition before the singular <paramref name="noun"/> with its definite article. A preposition
    /// ending in a or de contracts with el: al parque, cerca del parque, del agua.
    /// </summary>
    public string WithNoun(Noun noun)
    {
        ArgumentNullException.ThrowIfNull(noun);
        var words = BaseValue.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (noun.SingularArticle == Article.El && words.Count > 0 && Contracting.Contains(words[^1], StringComparer.OrdinalIgnoreCase))
        {
            words[^1] += "l";
        }
        else
        {
            words.Add(noun.SingularArticle.ToText());
        }
        words.Add(noun.BaseValue.Trim());
        return string.Join(' ', words);
    }

    /// <summary>The English phrase names the preposition, so the pair needs a translation too.</summary>
    protected override bool HasDataFor(ScenarioType type) => type switch
    {
        ScenarioType.PairWithNoun => base.HasDataFor(type) && TranslationAlternatives.Count > 0,
        _ => base.HasDataFor(type)
    };
}
