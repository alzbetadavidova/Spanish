namespace Spanish.Core;

public record ScenarioStat(ScenarioType Type, double? Index, int Attempts);

public record StatsRow(
    LearnUnit Unit,
    double? OverallIndex,
    IReadOnlyList<ScenarioStat> Breakdown,
    int Attempts,
    DateTime? LastPracticed)
{
    public string Word => Unit.BaseValue;
    public WordKind Kind => Unit.Kind;
    public string Translation => Unit.TranslationDisplay;
}

public static class LearnStats
{
    public static IReadOnlyList<StatsRow> Build(LearnLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        return library.Units.Select(BuildRow).ToList();
    }

    public static StatsRow BuildRow(LearnUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        var breakdown = unit.SupportedScenarios
            .Select(t => unit.GetProgress(t) is { } p ? new ScenarioStat(t, p.Index, p.Attempts) : new ScenarioStat(t, null, 0))
            .ToList();
        var lastPracticed = unit.Progress.Values.Select(p => p.LastPracticed).Max();
        return new StatsRow(unit, unit.OverallIndex, breakdown, breakdown.Sum(b => b.Attempts), lastPracticed);
    }
}
