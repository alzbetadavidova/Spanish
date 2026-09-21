using System.Text.Json;
using Spanish.Core;

namespace Spanish.Tests;

public class AdjectiveSessionTests
{
    [Test]
    public void Settings_Defaults_IncludeAdjectivesWithAllScenarios()
    {
        var settings = new SessionSettings();

        Assert.Multiple(() =>
        {
            Assert.That(settings.IncludeAdjectives, Is.True);
            Assert.That(settings.AdjectiveScenarioTypes, Is.EqualTo(SessionSettings.DefaultAdjectiveScenarioTypes));
            Assert.That(settings.ScenarioTypesFor(WordKind.Adjective), Is.SameAs(settings.AdjectiveScenarioTypes));
        });
    }

    [Test]
    public void Settings_NullAdjectiveScenarios_BecomeEmpty()
    {
        Assert.That(new SessionSettings { AdjectiveScenarioTypes = null! }.AdjectiveScenarioTypes, Is.Empty);
    }

    [TestCase(true, true)]
    [TestCase(false, false)]
    public void Settings_Includes_FollowsAdjectiveToggle(bool include, bool expected)
    {
        var settings = new SessionSettings { IncludeAdjectives = include };

        Assert.That(settings.Includes(TestData.Bajo()), Is.EqualTo(expected));
    }

    [Test]
    public void Settings_UnknownKind_IsExcludedWithoutScenarios()
    {
        var settings = new SessionSettings();

        Assert.Multiple(() =>
        {
            Assert.That(settings.Includes(new UnknownUnit()), Is.False);
            Assert.That(settings.ScenarioTypesFor((WordKind)99), Is.Empty);
        });
    }

    [Test]
    public void Settings_WithoutAdjectiveFields_LoadDefaults()
    {
        // Settings saved before adjectives existed.
        var settings = JsonSerializer.Deserialize<SessionSettings>("""{ "IncludeNouns": false }""", JsonFileStore<SessionSettings>.Options)!;

        Assert.Multiple(() =>
        {
            Assert.That(settings.IncludeAdjectives, Is.True);
            Assert.That(settings.AdjectiveScenarioTypes, Is.EqualTo(SessionSettings.DefaultAdjectiveScenarioTypes));
        });
    }

    [Test]
    public void GetExercises_IncludesAdjectiveScenarios()
    {
        var settings = new SessionSettings { IncludeNouns = false, IncludeVerbs = false };

        var exercises = LearnSession.GetExercises(TestData.AdjectiveLibrary(), settings);

        Assert.That(exercises.Select(e => $"{e.Unit.BaseValue}:{e.Type}"),
            Is.EqualTo(new[] { "bajo:Card", "bajo:Fill", "bajo:PairWithNoun" }));
    }

    [Test]
    public void Next_PairWithNoun_UsesLibraryNouns()
    {
        var library = TestData.AdjectiveLibrary();
        var settings = new SessionSettings
        {
            IncludeNouns = false,
            IncludeVerbs = false,
            AdjectiveScenarioTypes = [ScenarioType.PairWithNoun]
        };
        var random = new FakeRandom();
        var session = new LearnSession(library, settings, new ScenarioFactory(random, library), random, new FakeClock());

        var scenario = (TypedScenario)session.Next()!;

        Assert.That(scenario.ExpectedAnswers[0], Is.EqualTo("mujer baja"));
    }
}

public class LearnSessionWordHistoryTests
{
    private readonly FakeClock _clock = new();

    private LearnSession CreateSession(LearnLibrary library, SessionSettings settings)
    {
        var random = new FakeRandom();
        return new LearnSession(library, settings, new ScenarioFactory(random, library), random, _clock);
    }

    private static readonly SessionSettings RandomCardAndGender = new()
    {
        NounScenarioTypes = [ScenarioType.Card, ScenarioType.Gender],
        IncludeVerbs = false,
        IncludeAdjectives = false,
        Order = SessionOrder.Random
    };

    [Test]
    public void WordKey_IgnoresScenarioAndCase()
    {
        var noun = new Noun { BaseValue = "Ciudad" };

        Assert.Multiple(() =>
        {
            Assert.That(new Exercise(noun, ScenarioType.Card).WordKey, Is.EqualTo("Noun:ciudad"));
            Assert.That(new Exercise(noun, ScenarioType.Gender).WordKey, Is.EqualTo("Noun:ciudad"));
        });
    }

    [Test]
    public void Next_AvoidsRecentWordInOtherScenario()
    {
        var library = new LearnLibrary { Nouns = [TestData.Ciudad(), TestData.Perro()] };
        var session = CreateSession(library, RandomCardAndGender);

        var first = session.Next()!;
        session.Record(first, true);
        var second = session.Next()!;

        Assert.Multiple(() =>
        {
            Assert.That(first.Unit.BaseValue, Is.EqualTo("ciudad"));
            // Without the word history ciudad:Gender would come next.
            Assert.That(second.Unit.BaseValue, Is.EqualTo("perro"));
        });
    }

    [Test]
    public void Next_OnlyRecentWordHasFreshExercise_FallsBackToIt()
    {
        var ciudad = TestData.Ciudad();
        var perro = TestData.Perro();
        perro.Translation = string.Empty; // perro can only be practiced as Gender
        var library = new LearnLibrary { Nouns = [ciudad, perro] };
        var session = CreateSession(library, RandomCardAndGender);

        session.Record(new GenderScenario(perro, "perro", Article.El), true);
        session.Record(new CardScenario(ciudad, Direction.EnglishToSpanish, "city", "la ciudad", null), true);
        var next = session.Next()!;

        Assert.Multiple(() =>
        {
            Assert.That(next.Unit, Is.SameAs(ciudad));
            Assert.That(next.Type, Is.EqualTo(ScenarioType.Gender));
        });
    }

    [Test]
    public void Next_SingleWord_CyclesItsScenarios()
    {
        var library = new LearnLibrary { Nouns = [TestData.Ciudad()] };
        var session = CreateSession(library, RandomCardAndGender);

        var first = session.Next()!;
        session.Record(first, true);
        var second = session.Next()!;

        Assert.That(new[] { first.Type, second.Type }, Is.EqualTo(new[] { ScenarioType.Card, ScenarioType.Gender }));
    }

    [Test]
    public void Next_WordReturnsAfterThreeOtherWords()
    {
        var library = new LearnLibrary
        {
            Nouns = Enumerable.Range(0, 5).Select(i => new Noun { BaseValue = $"n{i}", Translation = "x" }).ToList()
        };
        var session = CreateSession(library, RandomCardAndGender);

        var picked = new List<string>();
        for (var i = 0; i < LearnSession.RecentWordCapacity + 2; i++)
        {
            var scenario = session.Next()!;
            picked.Add($"{scenario.Unit.BaseValue}:{scenario.Type}");
            session.Record(scenario, true);
        }

        Assert.That(picked, Is.EqualTo(new[] { "n0:Card", "n1:Card", "n2:Card", "n3:Card", "n0:Gender" }));
    }

    private static CardScenario Card(LearnUnit unit) => new(unit, Direction.EnglishToSpanish, "front", "back", null);

    private static GenderScenario Gender(Noun noun) => new(noun, noun.BaseValue, noun.SingularArticle);

    [Test]
    public void Record_SameWordAgainAfterAnother_KeepsThreeDistinctWords()
    {
        var nouns = "ABCD".Select(c => new Noun { BaseValue = c.ToString(), Translation = "x" }).ToList();
        var session = CreateSession(new LearnLibrary { Nouns = nouns }, RandomCardAndGender);

        session.Record(Card(nouns[2]), true);
        session.Record(Card(nouns[0]), true);
        session.Record(Card(nouns[1]), true);
        session.Record(Gender(nouns[0]), true);
        var next = session.Next()!;

        // A, B and C are the last three words; keeping A twice would forget C and pick C:Gender.
        Assert.That($"{next.Unit.BaseValue}:{next.Type}", Is.EqualTo("D:Card"));
    }

    [Test]
    public void Next_TwoWordsBothRecent_AvoidsOnlyTheLastWord()
    {
        var ciudad = TestData.Ciudad();
        var perro = TestData.Perro();
        var session = CreateSession(new LearnLibrary { Nouns = [ciudad, perro] }, RandomCardAndGender);

        session.Record(Card(perro), true);
        session.Record(Card(ciudad), true);
        var next = session.Next()!;

        // With a word depth above wordCount - 1 both words would count as recent and ciudad:Gender would come next.
        Assert.That($"{next.Unit.BaseValue}:{next.Type}", Is.EqualTo("perro:Gender"));
    }

    [Test]
    public void Record_SameWordTwiceInARow_TakesOneHistorySlot()
    {
        var nouns = Enumerable.Range(0, 3).Select(i => new Noun { BaseValue = $"n{i}", Translation = "x" }).ToList();
        var session = CreateSession(new LearnLibrary { Nouns = nouns }, RandomCardAndGender);

        session.Record(Card(nouns[1]), true);
        session.Record(Card(nouns[0]), true);
        session.Record(Gender(nouns[0]), true);
        var next = session.Next()!;

        // n1 is still one of the last two words, so the only other word is picked.
        Assert.That($"{next.Unit.BaseValue}:{next.Type}", Is.EqualTo("n2:Card"));
    }

    [Test]
    public void Next_PairWhoseNounsAreMissing_IsNotOffered()
    {
        var library = new LearnLibrary { Adjectives = [TestData.Bajo()] };
        var settings = new SessionSettings { IncludeNouns = false, IncludeVerbs = false };
        var session = CreateSession(library, settings);

        Assert.Multiple(() =>
        {
            Assert.That(LearnSession.GetExercises(library, settings).Select(e => e.Type),
                Is.EqualTo(new[] { ScenarioType.Card, ScenarioType.Fill }));
            Assert.That(session.Next()!.Type, Is.EqualTo(ScenarioType.Card));
        });
    }
}
