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
        WordKind.Numeral => "numeral",
        _ => "word"
    };

    public static string Plural(WordKind kind) => $"{Singular(kind)}s";

    public static string Subtype(NumeralSubtype subtype) => subtype switch
    {
        NumeralSubtype.Date => "Date",
        NumeralSubtype.Time => "Time",
        NumeralSubtype.DateAndTime => "Date and time",
        _ => "Number"
    };

    public static string Scenario(ScenarioType type) => type switch
    {
        ScenarioType.PairWithNoun => "Pair with noun",
        ScenarioType.NumberToText => "Number to text",
        ScenarioType.TextToNumber => "Text to number",
        _ => type.ToString()
    };
}
