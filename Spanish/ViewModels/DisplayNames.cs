using Spanish.Core;

namespace Spanish.ViewModels;

/// <summary>User-facing names of word kinds and scenario types.</summary>
public static class DisplayNames
{
    public static string Singular(WordKind kind) => kind switch
    {
        WordKind.Noun => "noun",
        WordKind.Verb => "verb",
        WordKind.Adjective => "adjective",
        _ => "word"
    };

    public static string Plural(WordKind kind) => $"{Singular(kind)}s";

    public static string Scenario(ScenarioType type) => type switch
    {
        ScenarioType.PairWithNoun => "Pair with noun",
        _ => type.ToString()
    };
}
