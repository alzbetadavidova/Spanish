using System.Text.Json.Serialization;

namespace Spanish.Core;

public abstract class LearnUnit
{
    /// <summary>The spanish word. For verbs the infinitive.</summary>
    public string BaseValue { get; set; } = string.Empty;

    /// <summary>English translation; alternatives are separated by a semicolon.</summary>
    public string Translation { get; set; } = string.Empty;

    public List<string> Topics { get; set; } = [];

    public Dictionary<ScenarioType, LearnProgress> Progress { get; set; } = [];

    [JsonIgnore]
    public abstract WordKind Kind { get; }

    [JsonIgnore]
    public abstract IReadOnlyList<ScenarioType> SupportedScenarios { get; }

    [JsonIgnore]
    public IReadOnlyList<string> TranslationAlternatives =>
        Translation.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Average index of the practiced scenarios, or null when nothing was practiced yet.</summary>
    [JsonIgnore]
    public double? OverallIndex
    {
        get
        {
            var indexes = Progress.Values.Select(p => p.Index).OfType<double>().ToList();
            return indexes.Count == 0 ? null : indexes.Average();
        }
    }

    public LearnProgress? GetProgress(ScenarioType type) => Progress.GetValueOrDefault(type);

    public void RecordAnswer(ScenarioType type, bool correct, DateTime at)
    {
        if (!Progress.TryGetValue(type, out var progress))
        {
            progress = new LearnProgress();
            Progress[type] = progress;
        }
        progress.Record(correct, at);
    }

    public bool HasTopic(string topic) => Topics.Contains(topic, StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the unit supports the scenario type and has all the data it needs.</summary>
    public bool CanPractice(ScenarioType type) => SupportedScenarios.Contains(type) && HasDataFor(type);

    protected virtual bool HasDataFor(ScenarioType type) => type switch
    {
        ScenarioType.Card or ScenarioType.Fill => TranslationAlternatives.Count > 0,
        _ => true
    };

    /// <summary>Copies the editable content (not the progress) from another unit of the same kind.</summary>
    public virtual void CopyContentFrom(LearnUnit other)
    {
        ArgumentNullException.ThrowIfNull(other);
        BaseValue = other.BaseValue;
        Translation = other.Translation;
        Topics = [..other.Topics];
    }
}

public class Noun : LearnUnit
{
    private static readonly ScenarioType[] Scenarios =
        [ScenarioType.Card, ScenarioType.Fill, ScenarioType.Gender, ScenarioType.Plural];

    public Gender Gender { get; set; }
    public string PluralValue { get; set; } = string.Empty;

    public override WordKind Kind => WordKind.Noun;
    public override IReadOnlyList<ScenarioType> SupportedScenarios => Scenarios;

    [JsonIgnore]
    public Article SingularArticle => Gender == Gender.Masculine ? Article.El : Article.La;

    [JsonIgnore]
    public Article PluralArticle => Gender == Gender.Masculine ? Article.Los : Article.Las;

    protected override bool HasDataFor(ScenarioType type) => type switch
    {
        ScenarioType.Plural => !string.IsNullOrWhiteSpace(PluralValue),
        _ => base.HasDataFor(type)
    };

    public override void CopyContentFrom(LearnUnit other)
    {
        base.CopyContentFrom(other);
        var noun = (Noun)other;
        Gender = noun.Gender;
        PluralValue = noun.PluralValue;
    }
}

public class Verb : LearnUnit
{
    public const int PersonCount = 5;

    private static readonly ScenarioType[] Scenarios =
        [ScenarioType.Card, ScenarioType.Fill, ScenarioType.Present, ScenarioType.Preterite, ScenarioType.Gerund];

    /// <summary>Maps each subject to its index in the conjugation arrays.</summary>
    public static readonly IReadOnlyDictionary<string, int> ConjugationsDefinitions = new Dictionary<string, int>
    {
        {"Yo", 0},
        {"Tú", 1},
        {"Él", 2},
        {"Ella", 2},
        {"Usted", 2},
        {"Nosotros", 3},
        {"Nosotras", 3},
        {"Ellos", 4},
        {"Ellas", 4},
        {"Ustedes", 4},
    };

    /// <summary>Display label for each conjugation index.</summary>
    public static readonly IReadOnlyList<string> PersonLabels =
        ["yo", "tú", "él / ella / usted", "nosotros / nosotras", "ellos / ellas / ustedes"];

    public string[] PresentConjugations { get; set; } = EmptyConjugations();
    public string[] PreteriteConjugations { get; set; } = EmptyConjugations();
    public string NonPersonalGerund { get; set; } = string.Empty;

    public override WordKind Kind => WordKind.Verb;
    public override IReadOnlyList<ScenarioType> SupportedScenarios => Scenarios;

    public static string[] EmptyConjugations() => Enumerable.Repeat(string.Empty, PersonCount).ToArray();

    public static bool IsComplete(string[] conjugations) =>
        conjugations.Length == PersonCount && conjugations.All(c => !string.IsNullOrWhiteSpace(c));

    protected override bool HasDataFor(ScenarioType type) => type switch
    {
        ScenarioType.Present => IsComplete(PresentConjugations),
        ScenarioType.Preterite => IsComplete(PreteriteConjugations),
        ScenarioType.Gerund => !string.IsNullOrWhiteSpace(NonPersonalGerund),
        _ => base.HasDataFor(type)
    };

    public override void CopyContentFrom(LearnUnit other)
    {
        base.CopyContentFrom(other);
        var verb = (Verb)other;
        PresentConjugations = [..verb.PresentConjugations];
        PreteriteConjugations = [..verb.PreteriteConjugations];
        NonPersonalGerund = verb.NonPersonalGerund;
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<Gender>))]
public enum Gender
{
    Masculine,
    Feminine
}
