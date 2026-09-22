using System.Text.Json;
using Spanish.Core;

namespace Spanish.Tests;

public class FakeClock : IClock
{
    public DateTime Now { get; set; } = new(2026, 9, 21, 10, 0, 0);
}

/// <summary>Returns queued values (clamped to the range); returns 0 when the queue is empty.</summary>
public class FakeRandom(params int[] values) : IRandomSource
{
    private readonly Queue<int> _values = new(values);

    public List<int> Requests { get; } = [];

    public int Next(int maxExclusive)
    {
        Requests.Add(maxExclusive);
        return _values.Count == 0 ? 0 : Math.Min(_values.Dequeue(), maxExclusive - 1);
    }
}

public class InMemoryStore<T>(T value, string? corruptBackup = null) : IFileStore<T> where T : class
{
    public T Value { get; private set; } = value;
    public int SaveCount { get; private set; }
    public Exception? LoadException { get; set; }
    public Exception? SaveException { get; set; }

    public Task<LoadResult<T>> LoadAsync(CancellationToken cancellationToken = default) =>
        LoadException is not null
            ? Task.FromException<LoadResult<T>>(LoadException)
            : Task.FromResult(new LoadResult<T>(Value, corruptBackup));

    public Task SaveAsync(T value, CancellationToken cancellationToken = default)
    {
        if (SaveException is not null)
        {
            return Task.FromException(SaveException);
        }
        Value = value;
        SaveCount++;
        return Task.CompletedTask;
    }
}

/// <summary>A store whose first save waits until <see cref="Release"/> is called.</summary>
public class GatedStore<T> : IFileStore<T> where T : class
{
    private readonly TaskCompletionSource _gate = new();
    private bool _first = true;

    public List<T> Saved { get; } = [];

    public void Release() => _gate.TrySetResult();

    public Task<LoadResult<T>> LoadAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public async Task SaveAsync(T value, CancellationToken cancellationToken = default)
    {
        if (_first)
        {
            _first = false;
            await _gate.Task;
        }
        Saved.Add(value);
    }
}

public static class TestData
{
    public static Noun Ciudad() => new()
    {
        BaseValue = "ciudad",
        Translation = "city; town",
        Gender = Gender.Feminine,
        PluralValue = "ciudades",
        Topics = ["city"]
    };

    public static Noun Perro() => new()
    {
        BaseValue = "perro",
        Translation = "dog",
        Gender = Gender.Masculine,
        PluralValue = "perros",
        Topics = ["animals"]
    };

    public static Verb Hablar() => new()
    {
        BaseValue = "hablar",
        Translation = "to speak; talk",
        PresentConjugations = ["hablo", "hablas", "habla", "hablamos", "hablan"],
        PreteriteConjugations = ["hablé", "hablaste", "habló", "hablamos", "hablaron"],
        NonPersonalGerund = "hablando",
        Topics = ["city"]
    };

    public static Noun Mujer() => new()
    {
        BaseValue = "mujer",
        Translation = "woman",
        Gender = Gender.Feminine,
        PluralValue = "mujeres"
    };

    /// <summary>Linked to "mujer" and "perro".</summary>
    public static Adjective Bajo() => new()
    {
        BaseValue = "bajo",
        Translation = "short; low",
        LinkedNouns = ["mujer", "perro"],
        Topics = ["animals"]
    };

    /// <summary>ciudad, perro and mujer, plus the adjective bajo.</summary>
    public static LearnLibrary AdjectiveLibrary() => new()
    {
        Nouns = [Ciudad(), Perro(), Mujer()],
        Adjectives = [Bajo()],
        Topics = ["city", "animals"]
    };

    /// <summary>Feminine, but takes el in the singular.</summary>
    public static Noun Agua() => new()
    {
        BaseValue = "agua",
        Translation = "water",
        Gender = Gender.Feminine,
        PluralValue = "aguas",
        TakesElInSingular = true
    };

    /// <summary>"of; from", linked to "perro" and "ciudad".</summary>
    public static Preposition De() => new()
    {
        BaseValue = "de",
        Translation = "of; from",
        LinkedNouns = ["perro", "ciudad"],
        Topics = ["animals"]
    };

    /// <summary>"from; since", linked to "ciudad"; shares "from" with <see cref="De"/>.</summary>
    public static Preposition Desde() => new()
    {
        BaseValue = "desde",
        Translation = "from; since",
        LinkedNouns = ["ciudad"]
    };

    /// <summary>ciudad, perro and agua, plus the prepositions de and desde.</summary>
    public static LearnLibrary PrepositionLibrary() => new()
    {
        Nouns = [Ciudad(), Perro(), Agua()],
        Prepositions = [De(), Desde()],
        Topics = ["city", "animals"]
    };

    /// <summary>The built-in numeral of <paramref name="category"/>, from <paramref name="library"/> or a fresh set.</summary>
    public static Numeral NumeralOf(NumeralCategory category, LearnLibrary? library = null) =>
        (library?.Numerals ?? Numeral.CreateBuiltIn()).Single(n => n.Category == category);

    public static LearnLibrary Library() => new()
    {
        Nouns = [Ciudad(), Perro()],
        Verbs = [Hablar()],
        Topics = ["city", "animals"]
    };
}

/// <summary>A unit type the library and factory do not know about.</summary>
public class UnknownUnit : LearnUnit
{
    public override WordKind Kind => (WordKind)99;
    public override IReadOnlyList<ScenarioType> SupportedScenarios { get; } = [ScenarioType.Gender, ScenarioType.Card];
}

/// <summary>The seed library.json copied next to the tests.</summary>
public static class SeedLibrary
{
    public static string Path => System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory, "library.json");

    public static LearnLibrary Load() =>
        JsonSerializer.Deserialize<LearnLibrary>(File.ReadAllText(Path), JsonFileStore<LearnLibrary>.Options)!;
}
