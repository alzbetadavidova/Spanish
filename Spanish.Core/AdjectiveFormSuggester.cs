namespace Spanish.Core;

/// <summary>
/// Suggests the forms of a spanish adjective from its masculine singular using the regular rules.
/// Irregular adjectives (e.g. español -> española, joven -> jóvenes) need a manual fix.
/// </summary>
public static class AdjectiveFormSuggester
{
    // Not ú: común and other -ún adjectives are the same for both genders.
    private const string AccentedVowels = "áéíó";
    private const string PlainVowels = "aeio";

    // Comparatives ending in -or have one form for both genders: la mejor idea.
    private static readonly HashSet<string> InvariableOrAdjectives = new(StringComparer.OrdinalIgnoreCase)
    {
        "mejor", "peor", "mayor", "menor", "superior", "inferior",
        "interior", "exterior", "anterior", "posterior", "ulterior"
    };

    public static string Feminine(string masculine)
    {
        var word = masculine?.Trim() ?? string.Empty;
        if (word.Length < 2)
        {
            return word;
        }

        var last = char.ToLowerInvariant(word[^1]);
        if (last == 'o')
        {
            return word[..^1] + Match(word[^1], 'a'); // bajo -> baja
        }
        if (word.EndsWith("or", StringComparison.OrdinalIgnoreCase) && !InvariableOrAdjectives.Contains(word))
        {
            return word + Match(word[^1], 'a'); // trabajador -> trabajadora
        }

        // The stress stays on the same syllable, so the accent is dropped: alemán -> alemana, inglés -> inglesa.
        var accent = AccentedVowels.IndexOf(char.ToLowerInvariant(word[^2]));
        if (accent >= 0 && last is 'n' or 's')
        {
            return word[..^2] + Match(word[^2], PlainVowels[accent]) + word[^1] + Match(word[^1], 'a');
        }

        // -e, -a, -ista and most consonant endings are the same for both genders: grande, azul, feliz.
        return word;
    }

    public static string MasculinePlural(string masculine) => PluralSuggester.Suggest(masculine);

    public static string FemininePlural(string feminine) => PluralSuggester.Suggest(feminine);

    /// <summary>Keeps upper case words upper case: BAJO -> BAJA.</summary>
    private static char Match(char reference, char c) => char.IsUpper(reference) ? char.ToUpperInvariant(c) : c;
}
