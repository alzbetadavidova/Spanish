using System.Text.Json.Serialization;

namespace Spanish.Core;

public record ValidationError(string Field, string Message);

public class LibraryValidationException(IReadOnlyList<ValidationError> errors)
    : Exception(string.Join(" ", errors.Select(e => e.Message)))
{
    public IReadOnlyList<ValidationError> Errors { get; } = errors;
}

public class LearnLibrary : IJsonOnDeserialized
{
    // Setters replace null (possible in hand-edited JSON) with an empty value.
    private List<Noun> _nouns = [];
    public List<Noun> Nouns { get => _nouns; set => _nouns = value ?? []; }
    private List<Verb> _verbs = [];
    public List<Verb> Verbs { get => _verbs; set => _verbs = value ?? []; }
    private List<Adjective> _adjectives = [];
    public List<Adjective> Adjectives { get => _adjectives; set => _adjectives = value ?? []; }
    private List<string> _topics = [];
    public List<string> Topics { get => _topics; set => _topics = value ?? []; }

    [JsonIgnore]
    public IEnumerable<LearnUnit> Units => Nouns.Cast<LearnUnit>().Concat(Verbs).Concat(Adjectives);

    /// <summary>Removes null entries a hand-edited file may contain (e.g. <c>"Topics": [null]</c>).</summary>
    public void OnDeserialized()
    {
        Nouns.RemoveAll(n => n is null);
        Verbs.RemoveAll(v => v is null);
        Adjectives.RemoveAll(a => a is null);
        Topics.RemoveAll(t => t is null);
        foreach (var unit in Units)
        {
            unit.Topics.RemoveAll(t => t is null);
            foreach (var type in unit.Progress.Where(p => p.Value is null).Select(p => p.Key).ToList())
            {
                unit.Progress.Remove(type);
            }
            foreach (var progress in unit.Progress.Values)
            {
                progress.Recent.RemoveAll(r => r is null);
            }
        }
        foreach (var verb in Verbs)
        {
            verb.PresentConjugations = verb.PresentConjugations.Select(f => f ?? string.Empty).ToArray();
            verb.PreteriteConjugations = verb.PreteriteConjugations.Select(f => f ?? string.Empty).ToArray();
        }
        foreach (var adjective in Adjectives)
        {
            // Links to nouns that are not in the file cannot be practiced.
            NormalizeLinks(adjective);
        }
    }

    /// <summary>The nouns <paramref name="adjective"/> is linked to, in link order.</summary>
    public IReadOnlyList<Noun> LinkedNounsOf(Adjective adjective)
    {
        ArgumentNullException.ThrowIfNull(adjective);
        return adjective.LinkedNouns.Select(FindNoun).OfType<Noun>().ToList();
    }

    /// <summary>Validates a new unit (<paramref name="existing"/> null) or an edit of <paramref name="existing"/>.</summary>
    public IReadOnlyList<ValidationError> Validate(LearnUnit candidate, LearnUnit? existing = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(candidate.BaseValue))
        {
            errors.Add(new(nameof(LearnUnit.BaseValue), "Enter the Spanish word."));
        }
        else if (Units.Any(u => u.Kind == candidate.Kind && !ReferenceEquals(u, existing) && SameWord(u.BaseValue, candidate.BaseValue)))
        {
            errors.Add(new(nameof(LearnUnit.BaseValue), $"\"{candidate.BaseValue.Trim()}\" is already in your library."));
        }

        if (candidate.TranslationAlternatives.Count == 0)
        {
            errors.Add(new(nameof(LearnUnit.Translation), "Enter the English translation."));
        }

        var unknown = candidate.Topics.Where(t => !Topics.Contains(t, StringComparer.OrdinalIgnoreCase)).ToList();
        if (unknown.Count > 0)
        {
            errors.Add(new(nameof(LearnUnit.Topics), $"Unknown topic: {string.Join(", ", unknown)}."));
        }

        if (candidate is Verb verb)
        {
            AddConjugationError(errors, nameof(Verb.PresentConjugations), verb.PresentConjugations, "present");
            AddConjugationError(errors, nameof(Verb.PreteriteConjugations), verb.PreteriteConjugations, "preterite");
        }

        if (candidate is Adjective adjective)
        {
            var unknownNouns = adjective.LinkedNouns
                .Where(n => !string.IsNullOrWhiteSpace(n) && FindNoun(n) is null)
                .ToList();
            if (unknownNouns.Count > 0)
            {
                errors.Add(new(nameof(Adjective.LinkedNouns), $"Unknown noun: {string.Join(", ", unknownNouns)}."));
            }
        }

        return errors;
    }

    /// <summary>Adds <paramref name="candidate"/> or copies its content into <paramref name="existing"/>.</summary>
    /// <exception cref="LibraryValidationException">The candidate is not valid.</exception>
    public void Save(LearnUnit candidate, LearnUnit? existing = null)
    {
        var errors = Validate(candidate, existing);
        if (errors.Count > 0)
        {
            throw new LibraryValidationException(errors);
        }

        if (candidate is Adjective linking)
        {
            NormalizeLinks(linking);
        }

        if (existing is not null)
        {
            if (existing.Kind != candidate.Kind)
            {
                throw new ArgumentException("The edited unit must be of the same kind.", nameof(existing));
            }
            var oldBaseValue = existing.BaseValue;
            existing.CopyContentFrom(candidate);
            existing.BaseValue = existing.BaseValue.Trim();
            if (existing is Noun noun)
            {
                RenameNounLinks(noun, oldBaseValue);
            }
            return;
        }

        candidate.BaseValue = candidate.BaseValue.Trim();
        switch (candidate)
        {
            case Noun noun:
                Nouns.Add(noun);
                break;
            case Verb verb:
                Verbs.Add(verb);
                break;
            case Adjective adjective:
                Adjectives.Add(adjective);
                break;
            default:
                throw new ArgumentException($"Unsupported unit type {candidate.GetType().Name}.", nameof(candidate));
        }
    }

    public bool Remove(LearnUnit unit) => unit switch
    {
        Noun noun => RemoveNoun(noun),
        Verb verb => Verbs.Remove(verb),
        Adjective adjective => Adjectives.Remove(adjective),
        _ => false
    };

    private bool RemoveNoun(Noun noun)
    {
        if (!Nouns.Remove(noun))
        {
            return false;
        }
        // Another noun may still carry the name (duplicates in a hand-edited file).
        if (FindNoun(noun.BaseValue) is null)
        {
            foreach (var adjective in Adjectives)
            {
                adjective.LinkedNouns.RemoveAll(n => SameWord(n, noun.BaseValue));
            }
        }
        return true;
    }

    /// <summary>
    /// Keeps adjective links pointing at a noun whose Spanish word was edited. Like removing, it leaves the
    /// links alone while another noun still has the old name (duplicates in a hand-edited file).
    /// </summary>
    private void RenameNounLinks(Noun renamed, string oldName)
    {
        var newName = renamed.BaseValue;
        if (string.Equals(oldName, newName, StringComparison.Ordinal)
            || Nouns.Any(n => !ReferenceEquals(n, renamed) && SameWord(n.BaseValue, oldName)))
        {
            return;
        }
        foreach (var adjective in Adjectives)
        {
            adjective.LinkedNouns = adjective.LinkedNouns.Select(n => SameWord(n, oldName) ? newName : n).ToList();
        }
    }

    /// <summary>Keeps only links to nouns in the library, once each and in the noun's own spelling.</summary>
    private void NormalizeLinks(Adjective adjective) =>
        adjective.LinkedNouns = adjective.LinkedNouns
            .Select(FindNoun)
            .OfType<Noun>()
            .Select(n => n.BaseValue)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Whether <paramref name="unit"/> can be practiced as <paramref name="type"/> with this library's words,
    /// e.g. a pair needs a linked noun that is in the library.
    /// </summary>
    public bool CanPractice(LearnUnit unit, ScenarioType type)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return unit.CanPractice(type) && (unit, type) switch
        {
            (Adjective adjective, ScenarioType.PairWithNoun) => adjective.LinkedNouns.Any(n => FindNoun(n) is not null),
            _ => true
        };
    }

    private Noun? FindNoun(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : Nouns.FirstOrDefault(n => SameWord(n.BaseValue, name));

    /// <exception cref="LibraryValidationException">The name is empty or already used.</exception>
    public void AddTopic(string name)
    {
        var trimmed = ValidateTopicName(name, except: null);
        Topics.Add(trimmed);
    }

    /// <exception cref="LibraryValidationException">The new name is empty or already used.</exception>
    public void RenameTopic(string oldName, string newName)
    {
        var index = IndexOfTopic(oldName);
        var trimmed = ValidateTopicName(newName, except: Topics[index]);
        var current = Topics[index];
        Topics[index] = trimmed;
        foreach (var unit in Units)
        {
            unit.Topics = unit.Topics.Select(t => SameWord(t, current) ? trimmed : t).ToList();
        }
    }

    public void RemoveTopic(string name)
    {
        var index = IndexOfTopic(name);
        var current = Topics[index];
        Topics.RemoveAt(index);
        foreach (var unit in Units)
        {
            unit.Topics.RemoveAll(t => SameWord(t, current));
        }
    }

    public void SetTopicMembership(string topic, LearnUnit unit, bool isMember)
    {
        ArgumentNullException.ThrowIfNull(unit);
        var name = Topics[IndexOfTopic(topic)];
        if (isMember && !unit.HasTopic(name))
        {
            unit.Topics.Add(name);
        }
        else if (!isMember)
        {
            unit.Topics.RemoveAll(t => SameWord(t, name));
        }
    }

    private static void AddConjugationError(List<ValidationError> errors, string field, string[] forms, string tense)
    {
        var filled = forms.Count(f => !string.IsNullOrWhiteSpace(f));
        if (forms.Length != Verb.PersonCount || (filled > 0 && filled < Verb.PersonCount))
        {
            errors.Add(new(field, $"Fill in all {Verb.PersonCount} {tense} forms or leave them all empty."));
        }
    }

    private string ValidateTopicName(string name, string? except)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new LibraryValidationException([new("Topic", "Enter a topic name.")]);
        }
        if (Topics.Any(t => SameWord(t, trimmed) && (except is null || !SameWord(t, except))))
        {
            throw new LibraryValidationException([new("Topic", $"Topic \"{trimmed}\" already exists.")]);
        }
        return trimmed;
    }

    private int IndexOfTopic(string name)
    {
        var index = Topics.FindIndex(t => SameWord(t, name));
        if (index < 0)
        {
            throw new ArgumentException($"Topic \"{name}\" does not exist.", nameof(name));
        }
        return index;
    }

    private static bool SameWord(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
