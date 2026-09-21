namespace Spanish.Core;

/// <summary>The names are stored as keys of <see cref="LearnLibrary.NumeralProgress"/>; renaming one loses its progress.</summary>
public enum NumeralCategory
{
    Numbers0To20,
    Numbers21To100,
    Numbers101To999,
    Thousands,
    Millions,
    Dates,
    Times,
    DatesWithTimes
}

public enum NumeralSubtype
{
    Number,
    Date,
    Time,
    DateAndTime
}

/// <summary>
/// A built-in category of numerals, e.g. the numbers 21 to 100 or dates. Each exercise practices a random value
/// of the category. Numerals can't be edited; the library stores only their progress.
/// </summary>
public sealed class Numeral : LearnUnit
{
    public const string BuiltInMessage = "Numerals are built in and can't be edited.";
    public const int MinYear = 1900;
    public const int MaxYear = 2099;
    public const int MinuteStep = 5;

    public static readonly IReadOnlyList<ScenarioType> ScenarioTypes = [ScenarioType.NumberToText, ScenarioType.TextToNumber];

    // The range of the number categories.
    private readonly int _min;
    private readonly int _max;

    private Numeral(NumeralCategory category, string spanish, string english, int min = 0, int max = 0)
    {
        Category = category;
        BaseValue = spanish;
        Translation = english;
        _min = min;
        _max = max;
    }

    public NumeralCategory Category { get; }

    public NumeralSubtype Subtype => Category switch
    {
        NumeralCategory.Dates => NumeralSubtype.Date,
        NumeralCategory.Times => NumeralSubtype.Time,
        NumeralCategory.DatesWithTimes => NumeralSubtype.DateAndTime,
        _ => NumeralSubtype.Number
    };

    public override WordKind Kind => WordKind.Numeral;
    public override IReadOnlyList<ScenarioType> SupportedScenarios => ScenarioTypes;

    /// <summary>Fixed values that show what the category practices.</summary>
    public IReadOnlyList<NumeralForms> Examples => Category switch
    {
        NumeralCategory.Numbers0To20 => [SpanishNumerals.Number(7), SpanishNumerals.Number(16)],
        NumeralCategory.Numbers21To100 => [SpanishNumerals.Number(21), SpanishNumerals.Number(45)],
        NumeralCategory.Numbers101To999 => [SpanishNumerals.Number(115), SpanishNumerals.Number(780)],
        NumeralCategory.Thousands => [SpanishNumerals.Number(21_000), SpanishNumerals.Number(3_500)],
        NumeralCategory.Millions => [SpanishNumerals.Number(1_000_000), SpanishNumerals.Number(2_500_000)],
        NumeralCategory.Dates => [SpanishNumerals.Date(new DateOnly(1998, 5, 1)), SpanishNumerals.Date(new DateOnly(2025, 3, 21))],
        NumeralCategory.Times => [SpanishNumerals.Time(new TimeOnly(14, 30)), SpanishNumerals.Time(new TimeOnly(7, 45))],
        _ => [SpanishNumerals.DateAndTime(new DateOnly(2025, 3, 21), new TimeOnly(14, 30))]
    };

    /// <summary>One numeral of each category, without progress.</summary>
    public static IReadOnlyList<Numeral> CreateBuiltIn() =>
    [
        new(NumeralCategory.Numbers0To20, "números 0–20", "numbers 0–20", 0, 20),
        new(NumeralCategory.Numbers21To100, "números 21–100", "numbers 21–100", 21, 100),
        new(NumeralCategory.Numbers101To999, "números 101–999", "numbers 101–999", 101, 999),
        new(NumeralCategory.Thousands, "miles", "thousands", 1_000, 999_999),
        new(NumeralCategory.Millions, "millones", "millions", 1_000_000, SpanishNumerals.MaxNumber),
        new(NumeralCategory.Dates, "fechas", "dates"),
        new(NumeralCategory.Times, "horas", "times"),
        new(NumeralCategory.DatesWithTimes, "fechas y horas", "dates and times")
    ];

    /// <summary>
    /// A random value of the category. Dates fall between <see cref="MinYear"/> and <see cref="MaxYear"/>;
    /// times are on a <see cref="MinuteStep"/>-minute step.
    /// </summary>
    public NumeralForms Draw(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return Subtype switch
        {
            NumeralSubtype.Date => SpanishNumerals.Date(DrawDate(random)),
            NumeralSubtype.Time => SpanishNumerals.Time(DrawTime(random)),
            NumeralSubtype.DateAndTime => SpanishNumerals.DateAndTime(DrawDate(random), DrawTime(random)),
            _ => SpanishNumerals.Number(_min + random.Next(_max - _min + 1))
        };
    }

    /// <exception cref="NotSupportedException">Always: numerals are built in.</exception>
    public override void CopyContentFrom(LearnUnit other) => throw new NotSupportedException(BuiltInMessage);

    private static DateOnly DrawDate(IRandomSource random)
    {
        var year = MinYear + random.Next(MaxYear - MinYear + 1);
        var month = 1 + random.Next(12);
        var day = 1 + random.Next(DateTime.DaysInMonth(year, month));
        return new DateOnly(year, month, day);
    }

    private static TimeOnly DrawTime(IRandomSource random) =>
        new(random.Next(24), random.Next(60 / MinuteStep) * MinuteStep);
}
