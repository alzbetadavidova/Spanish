namespace Spanish.Core;

public record SessionSettings
{
    public static readonly IReadOnlyList<ScenarioType> DefaultNounScenarioTypes =
        [ScenarioType.Card, ScenarioType.Fill, ScenarioType.Gender];

    public static readonly IReadOnlyList<ScenarioType> DefaultVerbScenarioTypes =
        [ScenarioType.Card, ScenarioType.Fill, ScenarioType.Present, ScenarioType.Preterite];

    public bool IncludeNouns { get; init; } = true;
    public bool IncludeVerbs { get; init; } = true;
    public IReadOnlyList<ScenarioType> NounScenarioTypes { get; init; } = DefaultNounScenarioTypes;
    public IReadOnlyList<ScenarioType> VerbScenarioTypes { get; init; } = DefaultVerbScenarioTypes;

    /// <summary>Topics to practice; empty means all topics (including words without a topic).</summary>
    public IReadOnlyList<string> Topics { get; init; } = [];

    public SessionOrder Order { get; init; } = SessionOrder.LeastLearned;
    public Direction Direction { get; init; } = Direction.Mixed;

    public bool Includes(LearnUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        var kindIncluded = unit.Kind == WordKind.Noun ? IncludeNouns : IncludeVerbs;
        return kindIncluded && (Topics.Count == 0 || Topics.Any(unit.HasTopic));
    }

    public IReadOnlyList<ScenarioType> ScenarioTypesFor(WordKind kind) =>
        kind == WordKind.Noun ? NounScenarioTypes : VerbScenarioTypes;
}
