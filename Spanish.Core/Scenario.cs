namespace Spanish.Core;

public abstract record Scenario(LearnUnit Unit, ScenarioType Type)
{
    /// <summary>How the practiced word breaks the rules, shown once the user answered.</summary>
    public IReadOnlyList<Irregularity> Notes { get; init; } = Array.Empty<Irregularity>();
}

/// <summary>Flip card; the user assesses themselves.</summary>
public sealed record CardScenario(LearnUnit Unit, Direction Direction, string Front, string Back, string? BackDetail)
    : Scenario(Unit, ScenarioType.Card);

/// <summary>The user types an answer that is checked by <see cref="AnswerChecker"/>.</summary>
public sealed record TypedScenario(
    LearnUnit Unit,
    ScenarioType Type,
    string Instruction,
    string Prompt,
    string? PromptDetail,
    IReadOnlyList<string> ExpectedAnswers) : Scenario(Unit, Type);

/// <summary>The user picks the article of a spanish noun.</summary>
public sealed record GenderScenario(Noun Noun, string Word, Article Expected) : Scenario(Noun, ScenarioType.Gender);

public static class ArticleExtensions
{
    public static string ToText(this Article article) => article.ToString().ToLowerInvariant();
}
