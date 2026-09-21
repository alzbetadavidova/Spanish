using System.Globalization;
using System.Text;

namespace Spanish.Core;

public enum AnswerOutcome
{
    Correct,
    CorrectWithAccentHint, // right letters, wrong or missing accents
    Incorrect
}

public record AnswerResult(AnswerOutcome Outcome, string ExpectedAnswer)
{
    public bool IsCorrect => Outcome != AnswerOutcome.Incorrect;
}

public static class AnswerChecker
{
    private const char CombiningTilde = '̃';

    /// <summary>
    /// Compares ignoring case and surrounding/repeated spaces. Accents are lenient (reported as a hint),
    /// but ñ is a separate letter and must match.
    /// </summary>
    public static AnswerResult Check(string? given, IReadOnlyList<string> expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (expected.Count == 0)
        {
            throw new ArgumentException("At least one expected answer is required.", nameof(expected));
        }

        var answer = Normalize(given ?? string.Empty);
        if (answer.Length > 0)
        {
            var exact = expected.FirstOrDefault(e => Normalize(e) == answer);
            if (exact is not null)
            {
                return new AnswerResult(AnswerOutcome.Correct, exact);
            }

            var withoutAccents = RemoveAccents(answer);
            var close = expected.FirstOrDefault(e => RemoveAccents(Normalize(e)) == withoutAccents);
            if (close is not null)
            {
                return new AnswerResult(AnswerOutcome.CorrectWithAccentHint, close);
            }
        }

        return new AnswerResult(AnswerOutcome.Incorrect, expected[0]);
    }

    public static string Normalize(string value) =>
        string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormC);

    public static string RemoveAccents(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        for (var i = 0; i < decomposed.Length; i++)
        {
            var c = decomposed[i];
            var isMark = CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark;
            var isEnye = c == CombiningTilde && i > 0 && decomposed[i - 1] is 'n' or 'N';
            if (!isMark || isEnye)
            {
                builder.Append(c);
            }
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
