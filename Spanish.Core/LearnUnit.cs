using System.Text.Json.Serialization;

namespace Spanish.Core;

public abstract class LearnUnit
{
    // Setters replace null (possible in hand-edited JSON) with an empty value.
    private string _baseValue = string.Empty;
    private string _translation = string.Empty;
    private List<string> _topics = [];
    private Dictionary<ScenarioType, LearnProgress> _progress = [];

    /// <summary>The spanish word. For verbs the infinitive.</summary>
    public string BaseValue { get => _baseValue; set => _baseValue = value ?? string.Empty; }

    /// <summary>English translation; alternatives are separated by a semicolon.</summary>
    public string Translation { get => _translation; set => _translation = value ?? string.Empty; }

    public List<string> Topics { get => _topics; set => _topics = value ?? []; }

    public Dictionary<ScenarioType, LearnProgress> Progress { get => _progress; set => _progress = value ?? []; }

    /// <summary>The translation alternatives for display, e.g. "city, town".</summary>
    [JsonIgnore]
    public string TranslationDisplay => string.Join(", ", TranslationAlternatives);

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
    private string _pluralValue = string.Empty;
    public string PluralValue { get => _pluralValue; set => _pluralValue = value ?? string.Empty; }

    /// <summary>
    /// A feminine noun starting with a stressed a takes el in the singular (el agua, las aguas).
    /// Adjectives still agree in the feminine: el agua fría. Ignored for masculine nouns.
    /// </summary>
    public bool TakesElInSingular { get; set; }

    public override WordKind Kind => WordKind.Noun;
    public override IReadOnlyList<ScenarioType> SupportedScenarios => Scenarios;

    [JsonIgnore]
    public Article SingularArticle => Gender == Gender.Masculine || TakesElInSingular ? Article.El : Article.La;

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
        TakesElInSingular = noun.TakesElInSingular;
    }
}

public class Verb : LearnUnit
{
    public const int PersonCount = 5;

    private static readonly ScenarioType[] Scenarios =
    [
        ScenarioType.Card, ScenarioType.Fill, ScenarioType.Present, ScenarioType.Preterite, ScenarioType.Gerund,
        ScenarioType.PresentEndings, ScenarioType.PreteriteEndings
    ];

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

    private string[] _presentConjugations = EmptyConjugations();
    public string[] PresentConjugations { get => _presentConjugations; set => _presentConjugations = value ?? EmptyConjugations(); }
    private string[] _preteriteConjugations = EmptyConjugations();
    public string[] PreteriteConjugations { get => _preteriteConjugations; set => _preteriteConjugations = value ?? EmptyConjugations(); }
    private string _nonPersonalGerund = string.Empty;
    public string NonPersonalGerund { get => _nonPersonalGerund; set => _nonPersonalGerund = value ?? string.Empty; }

    public override WordKind Kind => WordKind.Verb;
    public override IReadOnlyList<ScenarioType> SupportedScenarios => Scenarios;

    public static string[] EmptyConjugations() => Enumerable.Repeat(string.Empty, PersonCount).ToArray();

    public static bool IsComplete(string[] conjugations) =>
        conjugations.Length == PersonCount && conjugations.All(c => !string.IsNullOrWhiteSpace(c));

    protected override bool HasDataFor(ScenarioType type) => type switch
    {
        ScenarioType.Present or ScenarioType.PresentEndings => IsComplete(PresentConjugations),
        ScenarioType.Preterite or ScenarioType.PreteriteEndings => IsComplete(PreteriteConjugations),
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

/// <summary>A word practiced together with nouns in the pair-with-noun scenario: adjectives and prepositions.</summary>
public abstract class NounLinkedUnit : LearnUnit
{
    // The setter replaces null (possible in hand-edited JSON) with an empty list.
    private List<string> _linkedNouns = [];
    /// <summary>Base values of the nouns this word is practiced with.</summary>
    public List<string> LinkedNouns { get => _linkedNouns; set => _linkedNouns = value ?? []; }

    /// <summary>Whether the pair-with-noun scenario can use <paramref name="noun"/>, one of the linked nouns.</summary>
    public virtual bool CanPairWith(Noun noun) => true;

    protected override bool HasDataFor(ScenarioType type) => type switch
    {
        ScenarioType.PairWithNoun => LinkedNouns.Count > 0,
        _ => base.HasDataFor(type)
    };

    public override void CopyContentFrom(LearnUnit other)
    {
        base.CopyContentFrom(other);
        LinkedNouns = [..((NounLinkedUnit)other).LinkedNouns];
    }
}

public class Adjective : NounLinkedUnit
{
    private static readonly ScenarioType[] Scenarios =
        [ScenarioType.Card, ScenarioType.Fill, ScenarioType.PairWithNoun];

    // Setters replace null (possible in hand-edited JSON) with an empty value.
    // An empty form means the regular form suggested by AdjectiveFormSuggester.
    private string _feminineValue = string.Empty;
    public string FeminineValue { get => _feminineValue; set => _feminineValue = value ?? string.Empty; }
    private string _masculinePluralValue = string.Empty;
    public string MasculinePluralValue { get => _masculinePluralValue; set => _masculinePluralValue = value ?? string.Empty; }
    private string _femininePluralValue = string.Empty;
    public string FemininePluralValue { get => _femininePluralValue; set => _femininePluralValue = value ?? string.Empty; }

    public override WordKind Kind => WordKind.Adjective;
    public override IReadOnlyList<ScenarioType> SupportedScenarios => Scenarios;

    /// <summary>The form agreeing with a noun of <paramref name="gender"/>; <see cref="LearnUnit.BaseValue"/> is the masculine singular.</summary>
    public string GetForm(Gender gender, bool plural)
    {
        var suggested = AdjectiveFormSuggester.Suggest(BaseValue, FeminineValue);
        return (gender, plural) switch
        {
            (Gender.Masculine, false) => BaseValue,
            (Gender.Feminine, false) => OrSuggested(FeminineValue, suggested.Feminine),
            (Gender.Masculine, true) => OrSuggested(MasculinePluralValue, suggested.MasculinePlural),
            _ => OrSuggested(FemininePluralValue, suggested.FemininePlural)
        };
    }

    public override void CopyContentFrom(LearnUnit other)
    {
        base.CopyContentFrom(other);
        var adjective = (Adjective)other;
        FeminineValue = adjective.FeminineValue;
        MasculinePluralValue = adjective.MasculinePluralValue;
        FemininePluralValue = adjective.FemininePluralValue;
    }

    private static string OrSuggested(string value, string suggested) =>
        string.IsNullOrWhiteSpace(value) ? suggested : value;
}

[JsonConverter(typeof(JsonStringEnumConverter<Gender>))]
public enum Gender
{
    Masculine,
    Feminine
}
