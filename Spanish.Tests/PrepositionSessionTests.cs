using System.Text.Json;
using Spanish.Core;

namespace Spanish.Tests;

public class PrepositionSessionTests
{
    private static readonly SessionSettings PrepositionsOnly = new()
    {
        IncludeNouns = false,
        IncludeVerbs = false,
        IncludeAdjectives = false,
        IncludeNumerals = false
    };

    [Test]
    public void Settings_Defaults_IncludePrepositionsWithAllScenarios()
    {
        var settings = new SessionSettings();

        Assert.Multiple(() =>
        {
            Assert.That(settings.IncludePrepositions, Is.True);
            Assert.That(settings.PrepositionScenarioTypes, Is.EqualTo(new Preposition().SupportedScenarios));
            Assert.That(settings.ScenarioTypesFor(WordKind.Preposition), Is.SameAs(settings.PrepositionScenarioTypes));
        });
    }

    [Test]
    public void Settings_NullPrepositionScenarios_BecomeEmpty()
    {
        Assert.That(new SessionSettings { PrepositionScenarioTypes = null! }.PrepositionScenarioTypes, Is.Empty);
    }

    [TestCase(true, true)]
    [TestCase(false, false)]
    public void Settings_Includes_FollowsPrepositionToggle(bool include, bool expected)
    {
        var settings = new SessionSettings { IncludePrepositions = include };

        Assert.That(settings.Includes(TestData.De()), Is.EqualTo(expected));
    }

    [Test]
    public void Settings_WithoutPrepositionFields_LoadDefaults()
    {
        // Settings saved before prepositions existed.
        var settings = JsonSerializer.Deserialize<SessionSettings>("""{ "IncludeAdjectives": false }""", JsonFileStore<SessionSettings>.Options)!;

        Assert.Multiple(() =>
        {
            Assert.That(settings.IncludePrepositions, Is.True);
            Assert.That(settings.PrepositionScenarioTypes, Is.EqualTo(SessionSettings.DefaultPrepositionScenarioTypes));
        });
    }

    [Test]
    public void Settings_RoundTripPrepositionChoices()
    {
        var settings = new SessionSettings { IncludePrepositions = false, PrepositionScenarioTypes = [ScenarioType.PairWithNoun] };

        var json = JsonSerializer.Serialize(settings, JsonFileStore<SessionSettings>.Options);
        var loaded = JsonSerializer.Deserialize<SessionSettings>(json, JsonFileStore<SessionSettings>.Options)!;

        Assert.Multiple(() =>
        {
            Assert.That(loaded.IncludePrepositions, Is.False);
            Assert.That(loaded.PrepositionScenarioTypes, Is.EqualTo(new[] { ScenarioType.PairWithNoun }));
        });
    }

    [Test]
    public void GetExercises_IncludesPrepositionScenarios()
    {
        var exercises = LearnSession.GetExercises(TestData.PrepositionLibrary(), PrepositionsOnly);

        Assert.That(exercises.Select(e => $"{e.Unit.BaseValue}:{e.Type}"), Is.EqualTo(new[]
        {
            "de:Card", "de:Fill", "de:PairWithNoun", "desde:Card", "desde:Fill", "desde:PairWithNoun"
        }));
    }

    [Test]
    public void Next_PrepositionPair_AsksForThePhrase()
    {
        var library = TestData.PrepositionLibrary();
        var settings = PrepositionsOnly with { PrepositionScenarioTypes = [ScenarioType.PairWithNoun] };
        var random = new FakeRandom();
        var session = new LearnSession(library, settings, new ScenarioFactory(random, library), random, new FakeClock());

        var scenario = (TypedScenario)session.Next()!;

        Assert.Multiple(() =>
        {
            Assert.That(scenario.Prompt, Is.EqualTo("of the dog"));
            Assert.That(scenario.ExpectedAnswers, Is.EqualTo(new[] { "del perro" }));
        });
    }
}
