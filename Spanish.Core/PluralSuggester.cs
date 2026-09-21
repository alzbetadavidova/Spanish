namespace Spanish.Core;

/// <summary>Suggests the plural of a spanish noun using the regular rules. Irregular nouns need a manual fix.</summary>
public static class PluralSuggester
{
    private const string Vowels = "aeiouáéíóú";
    private const string AccentedVowels = "áéíóú";
    private const string PlainVowels = "aeiou";

    public static string Suggest(string singular)
    {
        var word = singular?.Trim() ?? string.Empty;
        if (word.Length == 0)
        {
            return string.Empty;
        }

        var plural = SuggestLowerCase(word.ToLowerInvariant());
        if (word.Length > 1 && word == word.ToUpperInvariant())
        {
            return plural.ToUpperInvariant(); // CANCIÓN -> CANCIONES
        }
        return char.IsUpper(word[0]) ? char.ToUpperInvariant(plural[0]) + plural[1..] : plural;
    }

    private static string SuggestLowerCase(string word)
    {
        var last = word[^1];
        if (last == 'z')
        {
            return word[..^1] + "ces"; // luz -> luces
        }
        if ("aeiouáéó".Contains(last))
        {
            return word + "s"; // casa -> casas, café -> cafés
        }

        var beforeLast = word.Length > 1 ? word[^2] : '\0';
        var accent = AccentedVowels.IndexOf(beforeLast);
        if (last is 'n' or 's' && accent >= 0)
        {
            // The stress moves into the new syllable, so the accent is dropped: canción -> canciones.
            return word[..^2] + PlainVowels[accent] + last + "es";
        }
        if (last == 's' && CountVowelGroups(word) > 1)
        {
            return word; // unstressed -s does not change: lunes -> lunes, crisis -> crisis
        }
        return word + "es"; // ciudad -> ciudades, rubí -> rubíes, mes -> meses
    }

    private static int CountVowelGroups(string word)
    {
        var groups = 0;
        var inGroup = false;
        foreach (var c in word)
        {
            var isVowel = Vowels.Contains(c);
            if (isVowel && !inGroup)
            {
                groups++;
            }
            inGroup = isVowel;
        }
        return groups;
    }
}
