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

/// <summary>The user types the endings of every person of one tense after the verb's root.</summary>
public sealed record EndingsScenario(Verb Verb, ScenarioType Type, string Instruction, IReadOnlyList<EndingRow> Rows)
    : Scenario(Verb, Type);

/// <summary>One person of an <see cref="EndingsScenario"/>: habl + o. A form typed whole (tengo) has an empty root.</summary>
public sealed record EndingRow(string Person, string Root, string Ending)
{
    public string Form => Root + Ending;
    public bool HasRoot => Root.Length > 0;

    /// <summary>The ending, or the whole form typed in full.</summary>
    public IReadOnlyList<string> ExpectedAnswers => HasRoot ? [Ending, Form] : [Form];

    /// <summary>
    /// Splits <paramref name="form"/> after <paramref name="root"/> when it is the regular form: habl + o. Any other
    /// form is typed whole, e.g. tengo (ten + o is regular), estás (the accent) and busqué (the root changes).
    /// Compared ignoring case; the row keeps the form's spelling.
    /// </summary>
    /// <param name="regularForm">The form the regular rules give; null when there are none (not an infinitive).</param>
    public static EndingRow Create(string person, string root, string form, string? regularForm)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(form);
        var trimmed = form.Trim();
        var keepsRoot = root.Length > 0
                        && trimmed.Length > root.Length
                        && trimmed.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(trimmed, regularForm, StringComparison.OrdinalIgnoreCase);
        return keepsRoot
            ? new EndingRow(person, trimmed[..root.Length], trimmed[root.Length..])
            : new EndingRow(person, string.Empty, trimmed);
    }
}

/// <summary>The user picks the article of a spanish noun.</summary>
public sealed record GenderScenario(Noun Noun, string Word, Article Expected) : Scenario(Noun, ScenarioType.Gender);

public static class ArticleExtensions
{
    public static string ToText(this Article article) => article.ToString().ToLowerInvariant();
}
