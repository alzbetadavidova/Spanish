namespace Spanish.Core;

/// <summary>
/// Suggests whether a feminine noun takes el in the singular because it starts with a stressed a:
/// el agua, el hacha, el águila. Exceptions such as la a or la hache need a manual fix.
/// </summary>
public static class StressedASuggester
{
    private const string Vowels = "aeiouáéíóúü";
    private const string AccentedVowels = "áéíóú";

    public static bool StartsWithStressedA(string? word)
    {
        var lower = word?.Trim().ToLowerInvariant() ?? string.Empty;
        var sound = lower.StartsWith('h') ? lower[1..] : lower; // the h is silent: hacha
        if (sound.Length == 0)
        {
            return false;
        }
        if (sound[0] == 'á')
        {
            return true; // águila, área
        }
        if (sound[0] != 'a' || sound.Any(c => AccentedVowels.Contains(c)))
        {
            return false; // a written accent elsewhere carries the stress: la amígdala
        }

        // Without a written accent, words ending in a vowel, n or s are stressed on the second to last
        // syllable (a-gua), the others on the last one (a-zul).
        var syllables = CountVowelGroups(sound);
        var stressedSyllable = EndsInVowelNOrS(sound) ? syllables - 1 : syllables;
        return stressedSyllable <= 1;
    }

    private static bool EndsInVowelNOrS(string word) => Vowels.Contains(word[^1]) || word[^1] is 'n' or 's';

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
