namespace Spanish.Core;

public record Exercise(LearnUnit Unit, ScenarioType Type)
{
    public string Key => $"{Unit.Kind}:{Unit.BaseValue.ToLowerInvariant()}:{Type}";
}

/// <summary>Picks exercises one by one according to <see cref="SessionSettings"/> and records the answers.</summary>
public class LearnSession
{
    public const int RecentCapacity = 5;

    private readonly LearnLibrary _library;
    private readonly ScenarioFactory _factory;
    private readonly IRandomSource _random;
    private readonly IClock _clock;
    private readonly LearnCache _recent = new(RecentCapacity);

    public LearnSession(LearnLibrary library, SessionSettings settings, ScenarioFactory factory, IRandomSource random, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(clock);
        _library = library;
        Settings = settings;
        _factory = factory;
        _random = random;
        _clock = clock;
    }

    public SessionSettings Settings { get; }
    public int Answered { get; private set; }
    public int CorrectCount { get; private set; }

    public static IReadOnlyList<Exercise> GetExercises(LearnLibrary library, SessionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(settings);
        return library.Units
            .Where(settings.Includes)
            .SelectMany(u => settings.ScenarioTypesFor(u.Kind).Where(u.CanPractice).Select(t => new Exercise(u, t)))
            .ToList();
    }

    /// <summary>Returns the next scenario, or null when no exercise matches the settings.</summary>
    public Scenario? Next()
    {
        var exercises = GetExercises(_library, Settings);
        if (exercises.Count == 0)
        {
            return null;
        }

        // Avoid repeating recent exercises, but always leave at least one to choose from.
        var depth = Math.Min(RecentCapacity, exercises.Count - 1);
        var fresh = exercises.Where(e => !_recent.Has(e.Key, depth)).ToList();

        var best = fresh.Min(SortKey);
        var candidates = fresh.Where(e => SortKey(e) == best).ToList();
        var exercise = candidates[_random.Next(candidates.Count)];
        return _factory.Create(exercise.Unit, exercise.Type, Settings.Direction);
    }

    public void Record(Scenario scenario, bool correct)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Unit.RecordAnswer(scenario.Type, correct, _clock.Now);
        _recent.Add(new Exercise(scenario.Unit, scenario.Type).Key);
        Answered++;
        if (correct)
        {
            CorrectCount++;
        }
    }

    /// <summary>Lower is picked first; ties are broken at random.</summary>
    private double SortKey(Exercise exercise)
    {
        var progress = exercise.Unit.GetProgress(exercise.Type);
        return Settings.Order switch
        {
            SessionOrder.LeastLearned => progress?.Index ?? -1,
            SessionOrder.LeastRecentlyPracticed => progress?.LastPracticed?.Ticks ?? -1,
            _ => 0
        };
    }
}
