namespace Spanish.Core;

public record SessionSettings
{
    public static readonly IReadOnlyList<ScenarioType> DefaultNounScenarioTypes =
        [ScenarioType.Card, ScenarioType.Fill, ScenarioType.Gender];

    public static readonly IReadOnlyList<ScenarioType> DefaultVerbScenarioTypes =
        [ScenarioType.Card, ScenarioType.Fill, ScenarioType.Present, ScenarioType.Preterite];

    public static readonly IReadOnlyList<ScenarioType> DefaultAdjectiveScenarioTypes =
        [ScenarioType.Card, ScenarioType.Fill, ScenarioType.PairWithNoun];

    // Setters replace null (possible in hand-edited JSON) with an empty list.
    private readonly IReadOnlyList<ScenarioType> _nounScenarioTypes = DefaultNounScenarioTypes;
    private readonly IReadOnlyList<ScenarioType> _verbScenarioTypes = DefaultVerbScenarioTypes;
    private readonly IReadOnlyList<ScenarioType> _adjectiveScenarioTypes = DefaultAdjectiveScenarioTypes;
    private readonly IReadOnlyList<string> _topics = [];

    public bool IncludeNouns { get; init; } = true;
    public bool IncludeVerbs { get; init; } = true;
    public bool IncludeAdjectives { get; init; } = true;

    public IReadOnlyList<ScenarioType> NounScenarioTypes
    {
        get => _nounScenarioTypes;
        init => _nounScenarioTypes = value ?? [];
    }

    public IReadOnlyList<ScenarioType> VerbScenarioTypes
    {
        get => _verbScenarioTypes;
        init => _verbScenarioTypes = value ?? [];
    }

    public IReadOnlyList<ScenarioType> AdjectiveScenarioTypes
    {
        get => _adjectiveScenarioTypes;
        init => _adjectiveScenarioTypes = value ?? [];
    }

    /// <summary>Topics to practice; empty means all topics (including words without a topic).</summary>
    public IReadOnlyList<string> Topics
    {
        get => _topics;
        init => _topics = value ?? [];
    }

    public SessionOrder Order { get; init; } = SessionOrder.LeastLearned;
    public Direction Direction { get; init; } = Direction.Mixed;

    public bool Includes(LearnUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        var kindIncluded = unit.Kind switch
        {
            WordKind.Noun => IncludeNouns,
            WordKind.Verb => IncludeVerbs,
            WordKind.Adjective => IncludeAdjectives,
            _ => false
        };
        return kindIncluded && (Topics.Count == 0 || Topics.Any(unit.HasTopic));
    }

    public IReadOnlyList<ScenarioType> ScenarioTypesFor(WordKind kind) => kind switch
    {
        WordKind.Noun => NounScenarioTypes,
        WordKind.Verb => VerbScenarioTypes,
        WordKind.Adjective => AdjectiveScenarioTypes,
        _ => []
    };

    /// <summary>
    /// Drops topics the library no longer has and uses the library's spelling.
    /// Returns this instance when nothing changed.
    /// </summary>
    public SessionSettings WithTopicsFrom(IReadOnlyList<string> libraryTopics)
    {
        ArgumentNullException.ThrowIfNull(libraryTopics);
        var valid = Topics
            .Select(t => libraryTopics.FirstOrDefault(l => SameTopic(l, t)))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return valid.SequenceEqual(Topics) ? this : this with { Topics = valid };
    }

    /// <summary>Follows a topic rename. Returns this instance when the topic is not selected.</summary>
    public SessionSettings WithTopicRenamed(string oldName, string newName) =>
        Topics.Any(t => SameTopic(t, oldName))
            ? this with { Topics = Topics.Select(t => SameTopic(t, oldName) ? newName : t).ToList() }
            : this;

    private static bool SameTopic(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
