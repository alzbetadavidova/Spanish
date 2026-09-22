namespace Spanish.Core;

/// <summary>The forms of a verb in the order of <see cref="Verb.PersonLabels"/>.</summary>
public record VerbForms(IReadOnlyList<string> Present, IReadOnlyList<string> Preterite, string Gerund);

/// <summary>
/// Conjugates a spanish verb with the regular rules. The spelling changes -car → qué, -gar → gué and
/// -zar → cé keep the sound regular, so they count as regular.
/// </summary>
public static class VerbFormSuggester
{
    /// <summary>The regular forms, or null when <paramref name="infinitive"/> does not end in -ar, -er or -ir.</summary>
    public static VerbForms? Suggest(string? infinitive) => Split(infinitive) switch
    {
        null => null,
        (var stem, "ar") => Ar(stem),
        (var stem, "er") => ErIr(stem, "emos"),
        var (stem, _) => ErIr(stem, "imos") // -ir, -ír
    };

    /// <summary>
    /// The lowercase infinitive without -ar, -er or -ir (hablar -> habl), or null when it has none of these
    /// endings. Empty for ir.
    /// </summary>
    public static string? StemOf(string? infinitive) => Split(infinitive)?.Stem;

    /// <summary>hablar -> (habl, ar); null when the infinitive doesn't end in -ar, -er, -ir or -ír.</summary>
    private static (string Stem, string Ending)? Split(string? infinitive)
    {
        var word = infinitive?.Trim().ToLowerInvariant() ?? string.Empty;
        return word.Length >= 2 && word[^2..] is "ar" or "er" or "ir" or "ír" ? (word[..^2], word[^2..]) : null;
    }

    private static VerbForms Ar(string stem)
    {
        var preteriteYo = stem switch
        {
            [.., 'c'] => stem[..^1] + "qué", // buscar -> busqué
            [.., 'g'] => stem + "ué", // llegar -> llegué
            [.., 'z'] => stem[..^1] + "cé", // cruzar -> crucé
            _ => stem + "é"
        };
        return new VerbForms(
            [stem + "o", stem + "as", stem + "a", stem + "amos", stem + "an"],
            [preteriteYo, stem + "aste", stem + "ó", stem + "amos", stem + "aron"],
            stem + "ando");
    }

    private static VerbForms ErIr(string stem, string nosotros) => new(
        [stem + "o", stem + "es", stem + "e", stem + nosotros, stem + "en"],
        [stem + "í", stem + "iste", stem + "ió", stem + "imos", stem + "ieron"],
        stem + "iendo");
}
