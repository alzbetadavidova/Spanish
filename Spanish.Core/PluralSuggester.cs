namespace Spanish.Core;

/// <summary>Suggests the plural of a spanish noun using the regular rules. Irregular nouns need a manual fix.</summary>
public static class PluralSuggester
{
    public static string Suggest(string singular)
    {
        var word = singular?.Trim() ?? string.Empty;
        if (word.Length == 0)
        {
            return string.Empty;
        }

        var last = char.ToLowerInvariant(word[^1]);
        if (word.EndsWith("ión", StringComparison.OrdinalIgnoreCase))
        {
            return word[..^3] + "iones"; // canción -> canciones
        }
        if (last == 'z')
        {
            return word[..^1] + "ces"; // luz -> luces
        }
        if ("aeiouáéó".Contains(last))
        {
            return word + "s";
        }
        return word + "es";
    }
}
