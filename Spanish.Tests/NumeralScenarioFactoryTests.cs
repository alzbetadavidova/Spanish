using Spanish.Core;

namespace Spanish.Tests;

public class NumeralScenarioFactoryTests
{
    private static TypedScenario Create(Numeral numeral, ScenarioType type, params int[] random) =>
        (TypedScenario)new ScenarioFactory(new FakeRandom(random), new LearnLibrary()).Create(numeral, type, Direction.Mixed);

    [Test]
    public void Create_NumberToText_ShowsDigitsAndExpectsWords()
    {
        var numeral = TestData.NumeralOf(NumeralCategory.Numbers21To100);

        var scenario = Create(numeral, ScenarioType.NumberToText, 1);

        Assert.Multiple(() =>
        {
            Assert.That(scenario.Unit, Is.SameAs(numeral));
            Assert.That(scenario.Type, Is.EqualTo(ScenarioType.NumberToText));
            Assert.That(scenario.Instruction, Is.EqualTo(ScenarioFactory.NumberToTextInstruction));
            Assert.That(scenario.Prompt, Is.EqualTo("22"));
            Assert.That(scenario.PromptDetail, Is.Null);
            Assert.That(scenario.ExpectedAnswers, Is.EqualTo(new[] { "veintidós" }));
            Assert.That(scenario.Notes, Is.Empty);
        });
    }

    [Test]
    public void Create_TextToNumber_ShowsWordsAndExpectsDigits()
    {
        var scenario = Create(TestData.NumeralOf(NumeralCategory.Thousands), ScenarioType.TextToNumber, 24_000);

        Assert.Multiple(() =>
        {
            Assert.That(scenario.Type, Is.EqualTo(ScenarioType.TextToNumber));
            Assert.That(scenario.Instruction, Is.EqualTo(ScenarioFactory.TextToNumberInstruction));
            Assert.That(scenario.Prompt, Is.EqualTo("veinticinco mil"));
            Assert.That(scenario.PromptDetail, Is.Null);
            Assert.That(scenario.ExpectedAnswers, Is.EqualTo(new[] { "25.000", "25000", "25 000", "25,000" }));
        });
    }

    [TestCase(NumeralCategory.Dates, "day/month/year")]
    [TestCase(NumeralCategory.Times, "hour:minute, 24-hour clock")]
    [TestCase(NumeralCategory.DatesWithTimes, "day/month/year hour:minute")]
    public void Create_TextToNumber_DescribesTheDigitFormat(NumeralCategory category, string expected)
    {
        Assert.That(Create(TestData.NumeralOf(category), ScenarioType.TextToNumber).PromptDetail, Is.EqualTo(expected));
    }

    [Test]
    public void Create_Time_BothDirections()
    {
        var toText = Create(TestData.NumeralOf(NumeralCategory.Times), ScenarioType.NumberToText, 14, 6);
        var toNumber = Create(TestData.NumeralOf(NumeralCategory.Times), ScenarioType.TextToNumber, 14, 6);

        Assert.Multiple(() =>
        {
            Assert.That(toText.Prompt, Is.EqualTo("14:30"));
            Assert.That(toText.ExpectedAnswers, Does.Contain("las catorce y treinta"));
            Assert.That(toNumber.Prompt, Is.EqualTo("las dos y media de la tarde"));
            Assert.That(toNumber.ExpectedAnswers, Is.EqualTo(new[] { "14:30", "14.30" }));
        });
    }

    [Test]
    public void Create_NumeralAsWordScenario_Throws()
    {
        Assert.That(() => Create(TestData.NumeralOf(NumeralCategory.Dates), ScenarioType.Card), Throws.ArgumentException);
    }
}
