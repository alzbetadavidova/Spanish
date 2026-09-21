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
