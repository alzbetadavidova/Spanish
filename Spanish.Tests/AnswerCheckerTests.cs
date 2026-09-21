using Spanish.Core;

namespace Spanish.Tests;

public class AnswerCheckerTests
{
    [TestCase("canción")]
    [TestCase("  CANCIÓN ")]
    [TestCase("Canción")]
    public void Check_ExactIgnoringCaseAndSpaces_IsCorrect(string given)
    {
        var result = AnswerChecker.Check(given, ["canción"]);

        Assert.That(result, Is.EqualTo(new AnswerResult(AnswerOutcome.Correct, "canción")));
        Assert.That(result.IsCorrect, Is.True);
    }

    [Test]
    public void Check_RepeatedInnerSpaces_AreCollapsed()
    {
        Assert.That(AnswerChecker.Check("tú   hablas", ["tú hablas"]).Outcome, Is.EqualTo(AnswerOutcome.Correct));
    }

    [TestCase("cancion")]
    [TestCase("cancíon")]
    public void Check_WrongAccent_IsCorrectWithHint(string given)
    {
        var result = AnswerChecker.Check(given, ["canción"]);

        Assert.That(result, Is.EqualTo(new AnswerResult(AnswerOutcome.CorrectWithAccentHint, "canción")));
        Assert.That(result.IsCorrect, Is.True);
    }

    [Test]
    public void Check_MissingEnye_IsIncorrect()
    {
        var result = AnswerChecker.Check("ano", ["año"]);

        Assert.That(result.Outcome, Is.EqualTo(AnswerOutcome.Incorrect));
        Assert.That(result.IsCorrect, Is.False);
    }

    [Test]
    public void Check_UpperCaseEnye_IsCorrect()
    {
        Assert.That(AnswerChecker.Check("AÑO", ["año"]).Outcome, Is.EqualTo(AnswerOutcome.Correct));
    }

    [Test]
    public void Check_MatchesAnyAlternative()
    {
        var result = AnswerChecker.Check("town", ["city", "town"]);

        Assert.That(result, Is.EqualTo(new AnswerResult(AnswerOutcome.Correct, "town")));
    }

    [TestCase("está", AnswerOutcome.Correct, "está")]
    [TestCase("esta", AnswerOutcome.Correct, "esta")]
    public void Check_ExactMatchOnLaterAlternative_BeatsAccentMatchOnEarlier(string given, AnswerOutcome outcome, string expected)
    {
        var result = AnswerChecker.Check(given, ["esta", "está"]);

        Assert.That(result, Is.EqualTo(new AnswerResult(outcome, expected)));
    }

    [Test]
    public void Check_DecomposedInput_IsCorrect()
    {
        Assert.That(AnswerChecker.Check("an\u0303o", ["año"]).Outcome, Is.EqualTo(AnswerOutcome.Correct));
    }

    [TestCase("ciudades")]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void Check_WrongOrEmpty_IsIncorrectWithFirstExpected(string? given)
    {
        var result = AnswerChecker.Check(given, ["ciudad", "la ciudad"]);

        Assert.That(result, Is.EqualTo(new AnswerResult(AnswerOutcome.Incorrect, "ciudad")));
    }

    [Test]
    public void Check_NoExpectedAnswers_Throws()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => AnswerChecker.Check("x", []), Throws.ArgumentException);
            Assert.That(() => AnswerChecker.Check("x", null!), Throws.ArgumentNullException);
        });
    }

    [TestCase("ñandú", "ñandu")]
    [TestCase("Ñu", "Ñu")]
    [TestCase("pingüino", "pinguino")]
    [TestCase("̃a", "a")]
    [TestCase("tildẽ", "tilde")]
    public void RemoveAccents_KeepsEnyeOnly(string input, string expected)
    {
        Assert.That(AnswerChecker.RemoveAccents(input), Is.EqualTo(expected));
    }
}
