namespace Spanish.Core;

public record Exercise(LearnUnit Unit, ScenarioType Type)
{
    public string Key => $"{WordKey}:{Type}";

    /// <summary>Identifies the word regardless of the scenario.</summary>
    public string WordKey => $"{Unit.Kind}:{Unit.BaseValue.ToLowerInvariant()}";
}

/// <summary>Picks exercises one by one according to <see cref="SessionSettings"/> and records the answers.</summary>
public class LearnSession
{
    public const int RecentCapacity = 5;
    public const int RecentWordCapacity = 3;

    private readonly LearnLibrary _library;
    private readonly ScenarioFactory _factory;
    private readonly IRandomSource _random;
    private readonly IClock _clock;
    private readonly LearnCache _recent = new(RecentCapacity);
    private readonly LearnCache _recentWords = new(RecentWordCapacity);

    public LearnSession(LearnLibrary library, SessionSettings settings, ScenarioFactory factory, IRandomSource random, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(clock);
        _library = library;
        _settings = settings;
        _factory = factory;
        _random = random;
        _clock = clock;
    }

    private SessionSettings _settings;

    /// <summary>Can be replaced mid-session (e.g. after a topic rename) without losing the counters.</summary>
    public SessionSettings Settings
    {
        get => _settings;
        set => _settings = value ?? throw new ArgumentNullException(nameof(value));
    }
    public int Answered { get; private set; }
    public int CorrectCount { get; private set; }

    public static IReadOnlyList<Exercise> GetExercises(LearnLibrary library, SessionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(settings);
        return library.Units
            .Where(settings.Includes)
            .SelectMany(u => settings.ScenarioTypesFor(u.Kind)
                .Where(t => library.CanPractice(u, t))
                .Select(t => new Exercise(u, t)))
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

        // Candidates, from most to least preferred: neither a recent exercise nor a recent word, then not a
        // recent exercise, then anything. Never repeating an exercise straight away wins over word variety.
        // The depths always leave at least one exercise and one word to choose from.
        var depth = Math.Min(RecentCapacity, exercises.Count - 1);
        var fresh = exercises.Where(e => !_recent.Has(e.Key, depth)).ToList();
        if (fresh.Count == 0)
        {
            // Only possible when exercises share a key, e.g. duplicate words in a hand-edited file.
            fresh = exercises.ToList();
        }

        // Prefer another word too, so a word doesn't come back straight away in a different scenario.
        var wordCount = exercises.Select(e => e.WordKey).Distinct().Count();
        var wordDepth = Math.Min(RecentWordCapacity, wordCount - 1);
        var freshWords = fresh.Where(e => !_recentWords.Has(e.WordKey, wordDepth)).ToList();
        if (freshWords.Count > 0)
        {
            fresh = freshWords;
        }

        var best = fresh.Min(SortKey);
        var candidates = fresh.Where(e => SortKey(e) == best).ToList();
        var exercise = candidates[_random.Next(candidates.Count)];
        return _factory.Create(exercise.Unit, exercise.Type, Settings.Direction);
    }

    public void Record(Scenario scenario, bool correct)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Unit.RecordAnswer(scenario.Type, correct, _clock.Now);
        var exercise = new Exercise(scenario.Unit, scenario.Type);
        _recent.Add(exercise.Key);
        // A word repeated by the fallback takes one slot, so the history keeps distinct recent words.
        if (!_recentWords.Has(exercise.WordKey, 1))
        {
            _recentWords.Add(exercise.WordKey);
        }
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
