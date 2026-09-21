using System.Globalization;

namespace Spanish.Core;

/// <summary>
/// The written forms of one number, date or time. The first form of each list is the one shown to the learner;
/// all of them are accepted as answers.
/// </summary>
public sealed record NumeralForms(IReadOnlyList<string> Digits, IReadOnlyList<string> Words);

/// <summary>Writes numbers, dates and times in digits and in spanish words.</summary>
public static class SpanishNumerals
{
    public const int MaxNumber = 999_999_999;

    private static readonly string[] BelowThirty =
    [
        "cero", "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve",
        "diez", "once", "doce", "trece", "catorce", "quince", "dieciséis", "diecisiete", "dieciocho", "diecinueve",
        "veinte", "veintiuno", "veintidós", "veintitrés", "veinticuatro",
        "veinticinco", "veintiséis", "veintisiete", "veintiocho", "veintinueve"
    ];

    private static readonly string[] Tens =
        ["", "", "", "treinta", "cuarenta", "cincuenta", "sesenta", "setenta", "ochenta", "noventa"];

    private static readonly string[] Hundreds =
    [
        "", "ciento", "doscientos", "trescientos", "cuatrocientos",
        "quinientos", "seiscientos", "setecientos", "ochocientos", "novecientos"
    ];

    private static readonly string[] Months =
    [
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"
    ];

    private static readonly string[] DateSeparators = ["/", ".", "-"];
    private static readonly string[] TimeSeparators = [":", "."];
    private static readonly string[] DateTimeSeparators = [" ", ", "];

    /// <summary>The number in words, e.g. 21 -> veintiuno, 21 000 -> veintiún mil.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The number is negative or above <see cref="MaxNumber"/>.</exception>
    public static string ToWords(int number)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(number);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, MaxNumber);
        if (number == 0)
        {
            return BelowThirty[0];
        }

        var millions = number / 1_000_000;
        var thousands = number / 1_000 % 1_000;
        var rest = number % 1_000;
        var parts = new List<string>();
        if (millions > 0)
        {
            parts.Add(millions == 1 ? "un millón" : $"{BelowThousand(millions, beforeNoun: true)} millones");
        }
        if (thousands > 0)
        {
            parts.Add(thousands == 1 ? "mil" : $"{BelowThousand(thousands, beforeNoun: true)} mil");
        }
        if (rest > 0)
        {
            parts.Add(BelowThousand(rest, beforeNoun: false));
        }
        return string.Join(' ', parts);
    }

    /// <summary>25 000 is written 25.000; 25000, 25 000 and 25,000 are accepted too.</summary>
    public static NumeralForms Number(int number)
    {
        var words = ToWords(number);
        var plain = number.ToString(CultureInfo.InvariantCulture);
        if (number < 1_000)
        {
            return new NumeralForms([plain], [words]);
        }
        var grouped = number.ToString("#,0", CultureInfo.InvariantCulture);
        return new NumeralForms([grouped.Replace(',', '.'), plain, grouped.Replace(',', ' '), grouped], [words]);
    }

    /// <summary>
    /// 21/3/2025 is veintiuno de marzo de dos mil veinticinco. The day and month may have a leading zero and
    /// be separated by a dot or a dash; the words may start with el.
    /// </summary>
    public static NumeralForms Date(DateOnly date)
    {
        var digits =
            from separator in DateSeparators
            from day in WithLeadingZero(date.Day)
            from month in WithLeadingZero(date.Month)
            select $"{day}{separator}{month}{separator}{Invariant(date.Year)}";

        // Spain says uno de mayo, Latin America primero de mayo.
        string[] days = date.Day == 1 ? ["uno", "primero"] : [ToWords(date.Day)];
        var words = days.Select(day => $"{day} de {Months[date.Month - 1]} de {ToWords(date.Year)}").ToList();
        return new NumeralForms(digits.ToList(), [..words, ..words.Select(w => $"el {w}")]);
    }

    /// <summary>
    /// 14:30 is las dos y media de la tarde. The part of the day is optional, and the 24-hour clock of timetables
    /// (las catorce y treinta, las catorce treinta) is accepted too. The hour may have a leading zero and be
    /// separated by a dot.
    /// </summary>
    public static NumeralForms Time(TimeOnly time)
    {
        var minute = time.Minute.ToString("00", CultureInfo.InvariantCulture);
        var digits =
            from separator in TimeSeparators
            from hour in WithLeadingZero(time.Hour)
            select $"{hour}{separator}{minute}";
        return new NumeralForms(digits.ToList(), TimeWords(time.Hour, time.Minute));
    }

    /// <summary>21/3/2025 14:30 is veintiuno de marzo de dos mil veinticinco a las dos y media de la tarde.</summary>
    public static NumeralForms DateAndTime(DateOnly date, TimeOnly time)
    {
        var dateForms = Date(date);
        var timeForms = Time(time);
        var digits =
            from d in dateForms.Digits
            from separator in DateTimeSeparators
            from t in timeForms.Digits
            select $"{d}{separator}{t}";
        var words =
            from d in dateForms.Words
            from t in timeForms.Words
            select $"{d} a {t}";
        return new NumeralForms(digits.ToList(), words.ToList());
    }

    private static List<string> TimeWords(int hour, int minute)
    {
        // Everyday forms with the hour they name; the first is the usual one. From :31 the next hour is named.
        var everyday = new List<(string Phrase, int Hour)>();
        switch (minute)
        {
            case 0:
                everyday.Add((HourOnClock(hour), hour));
                everyday.Add(($"{HourOnClock(hour)} en punto", hour));
                break;
            case 15:
                everyday.Add(($"{HourOnClock(hour)} y cuarto", hour));
                everyday.Add(($"{HourOnClock(hour)} y quince", hour));
                break;
            case 30:
                everyday.Add(($"{HourOnClock(hour)} y media", hour));
                everyday.Add(($"{HourOnClock(hour)} y treinta", hour));
                break;
            case 45:
                everyday.Add(($"{HourOnClock(hour + 1)} menos cuarto", hour + 1));
                everyday.Add(($"{HourOnClock(hour)} y cuarenta y cinco", hour));
                break;
            case > 30:
                everyday.Add(($"{HourOnClock(hour + 1)} menos {ToWords(60 - minute)}", hour + 1));
                everyday.Add(($"{HourOnClock(hour)} y {ToWords(minute)}", hour));
                break;
            default:
                everyday.Add(($"{HourOnClock(hour)} y {ToWords(minute)}", hour));
                break;
        }

        var words = everyday.Select(e => $"{e.Phrase} {PartOfDay(e.Hour % 24)}").ToList();
        words.AddRange(everyday.Select(e => e.Phrase));

        var hour24 = WithArticle(hour);
        if (minute == 0)
        {
            words.Add($"{hour24} horas");
            words.Add(hour24);
        }
        else
        {
            words.Add($"{hour24} y {ToWords(minute)}");
            words.Add($"{hour24} {ToWords(minute)}");
        }
        return words.Distinct().ToList();
    }

    /// <summary>The hour on a 12-hour clock: 0 and 12 are las doce, 13 is la una.</summary>
    private static string HourOnClock(int hour) => WithArticle(hour % 12 == 0 ? 12 : hour % 12);

    private static string WithArticle(int hour) => hour == 1 ? "la una" : $"las {ToWords(hour)}";

    private static string PartOfDay(int hour) => hour switch
    {
        >= 1 and <= 5 => "de la madrugada",
        >= 6 and <= 11 => "de la mañana",
        12 => "del mediodía",
        >= 13 and <= 20 => "de la tarde",
        _ => "de la noche"
    };

    /// <param name="beforeNoun">Before mil and millones, uno becomes un: veintiún mil, ciento un millones.</param>
    private static string BelowThousand(int number, bool beforeNoun)
    {
        if (number == 100)
        {
            return "cien";
        }
        var hundreds = number / 100;
        var rest = number % 100;
        var parts = new List<string>();
        if (hundreds > 0)
        {
            parts.Add(Hundreds[hundreds]);
        }
        if (rest > 0)
        {
            parts.Add(BelowHundred(rest, beforeNoun));
        }
        return string.Join(' ', parts);
    }

    private static string BelowHundred(int number, bool beforeNoun)
    {
        var words = number < BelowThirty.Length ? BelowThirty[number]
            : number % 10 == 0 ? Tens[number / 10]
            : $"{Tens[number / 10]} y {BelowThirty[number % 10]}";
        if (!beforeNoun || !words.EndsWith("uno", StringComparison.Ordinal))
        {
            return words;
        }
        // The stress stays on the last syllable, so veintiun takes an accent.
        return number == 21 ? "veintiún" : words[..^1];
    }

    private static string[] WithLeadingZero(int value) =>
        value < 10 ? [Invariant(value), value.ToString("00", CultureInfo.InvariantCulture)] : [Invariant(value)];

    private static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);
}
