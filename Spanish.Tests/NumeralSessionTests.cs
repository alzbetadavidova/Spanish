using System.Text.Json;
using Spanish.Core;

namespace Spanish.Tests;

public class NumeralSessionTests
{
    [Test]
    public void Settings_Defaults_IncludeNumeralsWithAllScenarios()
    {
        var settings = new SessionSettings();

        Assert.Multiple(() =>
        {
            Assert.That(settings.IncludeNumerals, Is.True);
            Assert.That(settings.NumeralScenarioTypes, Is.EqualTo(new[] { ScenarioType.NumberToText, ScenarioType.TextToNumber }));
            Assert.That(settings.ScenarioTypesFor(WordKind.Numeral), Is.SameAs(settings.NumeralScenarioTypes));
        });
    }

    [Test]
    public void Settings_NullNumeralScenarios_BecomeEmpty()
    {
        Assert.That(new SessionSettings { NumeralScenarioTypes = null! }.NumeralScenarioTypes, Is.Empty);
    }

    [Test]
    public void Settings_OldFileWithoutNumerals_IncludesThem()
    {
        var settings = JsonSerializer.Deserialize<SessionSettings>(
            """{ "IncludeNouns": false }""", JsonFileStore<SessionSettings>.Options)!;

        Assert.That(settings.IncludeNumerals, Is.True);
        Assert.That(settings.NumeralScenarioTypes, Is.EqualTo(SessionSettings.DefaultNumeralScenarioTypes));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Includes_Numeral_FollowsItsFlagAndIgnoresTopics(bool include)
    {
        var numeral = new LearnLibrary().Numerals[0];
        var settings = new SessionSettings { IncludeNumerals = include, Topics = ["animals"] };

        Assert.That(settings.Includes(numeral), Is.EqualTo(include));
    }

    [Test]
    public void Includes_TopicsStillFilterWords()
    {
        var settings = new SessionSettings { Topics = ["animals"] };

        Assert.That(settings.Includes(TestData.Ciudad()), Is.False);
    }

    [Test]
    public void Record_Numeral_StoresItsProgress()
    {
        var library = new LearnLibrary();
        var random = new FakeRandom();
        var clock = new FakeClock();
        var session = new LearnSession(library, new SessionSettings(), new ScenarioFactory(random, library), random, clock);

        var scenario = session.Next()!;
        session.Record(scenario, true);

        Assert.Multiple(() =>
        {
            Assert.That(new Exercise(scenario.Unit, scenario.Type).WordKey, Is.EqualTo("Numeral:números 0–20"));
            Assert.That(TestData.NumeralOf(NumeralCategory.Numbers0To20, library).GetProgress(ScenarioType.NumberToText)!.Attempts, Is.EqualTo(1));
        });
    }
}
