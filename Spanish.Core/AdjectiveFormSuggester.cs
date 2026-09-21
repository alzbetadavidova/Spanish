namespace Spanish.Core;

/// <summary>The forms of an adjective besides the masculine singular.</summary>
public record AdjectiveForms(string Feminine, string MasculinePlural, string FemininePlural);

/// <summary>
/// Suggests the forms of a spanish adjective from its masculine singular using the regular rules.
/// Irregular adjectives (e.g. español -> española, joven -> jóvenes) need a manual fix.
/// </summary>
public static class AdjectiveFormSuggester
{
    // Not ú: común and other -ún adjectives are the same for both genders.
    private const string AccentedVowels = "áéíó";
    private const string PlainVowels = "aeio";

    // Adjectives with one form for both genders although their ending usually has two:
    // comparatives in -or (la mejor idea) and cortés.
    private static readonly HashSet<string> Invariable = new(StringComparer.OrdinalIgnoreCase)
    {
        "mejor", "peor", "mayor", "menor", "superior", "inferior",
        "interior", "exterior", "anterior", "posterior", "ulterior",
        "cortés", "descortés"
    };

    /// <summary>
    /// Suggests the forms of <paramref name="masculine"/>. A given <paramref name="feminine"/> (e.g. a manual
    /// fix) is used for the feminine plural instead of the suggested feminine.
    /// </summary>
    public static AdjectiveForms Suggest(string masculine, string? feminine = null)
    {
        var suggestedFeminine = Feminine(masculine);
        var baseOfFemininePlural = string.IsNullOrWhiteSpace(feminine) ? suggestedFeminine : feminine;
        return new AdjectiveForms(
            suggestedFeminine,
            PluralSuggester.Suggest(masculine),
            PluralSuggester.Suggest(baseOfFemininePlural));
    }

    public static string Feminine(string masculine)
    {
        var word = masculine?.Trim() ?? string.Empty;
        if (word.Length < 2 || Invariable.Contains(word))
        {
            return word;
        }

        var last = char.ToLowerInvariant(word[^1]);
        if (last == 'o')
        {
            return word[..^1] + Match(word[^1], 'a'); // bajo -> baja
        }
        if (word.EndsWith("or", StringComparison.OrdinalIgnoreCase))
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

    /// <summary>Keeps upper case words upper case: BAJO -> BAJA.</summary>
    private static char Match(char reference, char c) => char.IsUpper(reference) ? char.ToUpperInvariant(c) : c;
}
