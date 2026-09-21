using Spanish.Core;

namespace Spanish.Tests;

public class LearnSessionTests
{
    private readonly FakeClock _clock = new();

    private LearnSession CreateSession(LearnLibrary library, SessionSettings settings, FakeRandom? random = null)
    {
        random ??= new FakeRandom();
        return new LearnSession(library, settings, new ScenarioFactory(random), random, _clock);
    }

    [Test]
    public void Constructor_NullArguments_Throw()
    {
        var library = new LearnLibrary();
        var settings = new SessionSettings();
        var factory = new ScenarioFactory(new FakeRandom());
        var random = new FakeRandom();

        Assert.Multiple(() =>
        {
            Assert.That(() => new LearnSession(null!, settings, factory, random, _clock), Throws.ArgumentNullException);
            Assert.That(() => new LearnSession(library, null!, factory, random, _clock), Throws.ArgumentNullException);
            Assert.That(() => new LearnSession(library, settings, null!, random, _clock), Throws.ArgumentNullException);
            Assert.That(() => new LearnSession(library, settings, factory, null!, _clock), Throws.ArgumentNullException);
            Assert.That(() => new LearnSession(library, settings, factory, random, null!), Throws.ArgumentNullException);
        });
    }

    [Test]
    public void GetExercises_DefaultSettings_PairsWordsWithPracticableScenarios()
    {
        var exercises = LearnSession.GetExercises(TestData.Library(), new SessionSettings());

        Assert.That(exercises.Select(e => $"{e.Unit.BaseValue}:{e.Type}"), Is.EqualTo(new[]
        {
            "ciudad:Card", "ciudad:Fill", "ciudad:Gender",
            "perro:Card", "perro:Fill", "perro:Gender",
            "hablar:Card", "hablar:Fill", "hablar:Present", "hablar:Preterite"
        }));
    }

    [Test]
    public void GetExercises_FiltersKindTopicAndScenario()
    {
        var settings = new SessionSettings
        {
            IncludeVerbs = false,
            NounScenarioTypes = [ScenarioType.Plural],
            Topics = ["animals"]
        };

        var exercises = LearnSession.GetExercises(TestData.Library(), settings);

        Assert.That(exercises, Is.EqualTo(new[] { new Exercise(exercises[0].Unit, ScenarioType.Plural) }));
        Assert.That(exercises[0].Unit.BaseValue, Is.EqualTo("perro"));
    }

    [Test]
    public void GetExercises_NullArguments_Throw()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => LearnSession.GetExercises(null!, new SessionSettings()), Throws.ArgumentNullException);
            Assert.That(() => LearnSession.GetExercises(new LearnLibrary(), null!), Throws.ArgumentNullException);
        });
    }

    [Test]
    public void Next_NoMatchingExercises_ReturnsNull()
    {
        var session = CreateSession(new LearnLibrary(), new SessionSettings());

        Assert.That(session.Next(), Is.Null);
    }

    [Test]
    public void Next_LeastLearned_PrefersNeverPracticedThenLowestIndex()
    {
        var library = new LearnLibrary { Nouns = [TestData.Ciudad(), TestData.Perro()] };
        var settings = new SessionSettings { NounScenarioTypes = [ScenarioType.Gender] };
        library.Nouns[0].RecordAnswer(ScenarioType.Gender, false, _clock.Now);
        var session = CreateSession(library, settings);

        Assert.That(session.Next()!.Unit.BaseValue, Is.EqualTo("perro"));

        library.Nouns[1].RecordAnswer(ScenarioType.Gender, true, _clock.Now);
        Assert.That(session.Next()!.Unit.BaseValue, Is.EqualTo("ciudad"));
    }

    [Test]
    public void Next_LeastRecentlyPracticed_PrefersOldest()
    {
        var library = new LearnLibrary { Nouns = [TestData.Ciudad(), TestData.Perro()] };
        var settings = new SessionSettings
        {
            NounScenarioTypes = [ScenarioType.Gender],
            Order = SessionOrder.LeastRecentlyPracticed
        };
        library.Nouns[0].RecordAnswer(ScenarioType.Gender, false, _clock.Now);
        library.Nouns[1].RecordAnswer(ScenarioType.Gender, true, _clock.Now.AddDays(-3));

        var next = CreateSession(library, settings).Next();

        Assert.That(next!.Unit.BaseValue, Is.EqualTo("perro"));
    }

    [Test]
    public void Next_LeastRecentlyPracticed_NeverPracticedComesFirst()
    {
        var library = new LearnLibrary { Nouns = [TestData.Ciudad(), TestData.Perro(), new Noun { BaseValue = "mesa" }] };
        var settings = new SessionSettings
        {
            NounScenarioTypes = [ScenarioType.Gender],
            Order = SessionOrder.LeastRecentlyPracticed
        };
        library.Nouns[0].RecordAnswer(ScenarioType.Gender, true, _clock.Now.AddYears(-1));
        library.Nouns[1].Progress[ScenarioType.Gender] = new LearnProgress(); // progress without answers
        // mesa has no progress at all; perro and mesa tie, the random source picks the second.
        var random = new FakeRandom(1);

        var next = CreateSession(library, settings, random).Next();

        Assert.That(next!.Unit.BaseValue, Is.EqualTo("mesa"));
        Assert.That(random.Requests[0], Is.EqualTo(2));
    }

    [Test]
    public void Next_Random_PicksAmongAllWithRandomSource()
    {
        var library = new LearnLibrary { Nouns = [TestData.Ciudad(), TestData.Perro()] };
        var settings = new SessionSettings
        {
            NounScenarioTypes = [ScenarioType.Gender],
            Order = SessionOrder.Random
        };
        library.Nouns[1].RecordAnswer(ScenarioType.Gender, true, _clock.Now);
        // First value picks the candidate, second decides singular/plural.
        var random = new FakeRandom(1, 0);

        var next = CreateSession(library, settings, random).Next();

        Assert.That(next!.Unit.BaseValue, Is.EqualTo("perro"));
        Assert.That(random.Requests[0], Is.EqualTo(2));
    }

    [Test]
    public void Next_AvoidsRecentlyAnsweredExercises()
    {
        var library = new LearnLibrary { Nouns = [TestData.Ciudad(), TestData.Perro()] };
        var settings = new SessionSettings { NounScenarioTypes = [ScenarioType.Gender], Order = SessionOrder.Random };
        var session = CreateSession(library, settings);

        var first = session.Next()!;
        session.Record(first, true);
        var second = session.Next()!;

        Assert.That(second.Unit, Is.Not.SameAs(first.Unit));
    }

    [Test]
    public void Next_SingleExercise_RepeatsIt()
    {
        var library = new LearnLibrary { Nouns = [TestData.Ciudad()] };
        var settings = new SessionSettings { NounScenarioTypes = [ScenarioType.Gender] };
        var session = CreateSession(library, settings);

        session.Record(session.Next()!, true);

        Assert.That(session.Next()!.Unit.BaseValue, Is.EqualTo("ciudad"));
    }

    [Test]
    public void Next_UsesSettingsDirection()
    {
        var library = new LearnLibrary { Nouns = [TestData.Ciudad()] };
        var settings = new SessionSettings { NounScenarioTypes = [ScenarioType.Card], Direction = Direction.SpanishToEnglish };

        var card = (CardScenario)CreateSession(library, settings).Next()!;

        Assert.That(card.Direction, Is.EqualTo(Direction.SpanishToEnglish));
    }

    [Test]
    public void Record_UpdatesProgressAndCounters()
    {
        var library = new LearnLibrary { Nouns = [TestData.Ciudad()] };
        var session = CreateSession(library, new SessionSettings { NounScenarioTypes = [ScenarioType.Gender] });
        var scenario = session.Next()!;

        session.Record(scenario, true);
        session.Record(scenario, false);

        Assert.Multiple(() =>
        {
            Assert.That(session.Answered, Is.EqualTo(2));
            Assert.That(session.CorrectCount, Is.EqualTo(1));
            Assert.That(library.Nouns[0].GetProgress(ScenarioType.Gender)!.Index, Is.EqualTo(0.5));
            Assert.That(library.Nouns[0].GetProgress(ScenarioType.Gender)!.LastPracticed, Is.EqualTo(_clock.Now));
        });
    }

    [Test]
    public void Record_Null_Throws()
    {
        Assert.That(() => CreateSession(new LearnLibrary(), new SessionSettings()).Record(null!, true),
            Throws.ArgumentNullException);
    }

    [Test]
    public void Exercise_Key_IgnoresCase()
    {
        var upper = new Noun { BaseValue = "Ciudad" };

        Assert.That(new Exercise(upper, ScenarioType.Card).Key, Is.EqualTo("Noun:ciudad:Card"));
    }
}

public class SessionSettingsTests
{
    [Test]
    public void Includes_ChecksKindAndTopics()
    {
        var nounsOnly = new SessionSettings { IncludeVerbs = false };
        var animals = new SessionSettings { Topics = ["ANIMALS"] };
        var nothing = new SessionSettings { IncludeNouns = false };

        Assert.Multiple(() =>
        {
            Assert.That(nounsOnly.Includes(TestData.Ciudad()), Is.True);
            Assert.That(nounsOnly.Includes(TestData.Hablar()), Is.False);
            Assert.That(animals.Includes(TestData.Perro()), Is.True);
            Assert.That(animals.Includes(TestData.Ciudad()), Is.False);
            Assert.That(nothing.Includes(TestData.Perro()), Is.False);
            Assert.That(() => nothing.Includes(null!), Throws.ArgumentNullException);
        });
    }

    [Test]
    public void ScenarioTypesFor_ReturnsListOfKind()
    {
        var settings = new SessionSettings();

        Assert.Multiple(() =>
        {
            Assert.That(settings.ScenarioTypesFor(WordKind.Noun), Is.EqualTo(SessionSettings.DefaultNounScenarioTypes));
            Assert.That(settings.ScenarioTypesFor(WordKind.Verb), Is.EqualTo(SessionSettings.DefaultVerbScenarioTypes));
        });
    }
}

public class LearnCacheTests
{
    [Test]
    public void Has_OnlyLooksAtRequestedDepth()
    {
        var cache = new LearnCache(3);
        cache.Add("a");
        cache.Add("b");

        Assert.Multiple(() =>
        {
            Assert.That(cache.Has("a", 2), Is.True);
            Assert.That(cache.Has("a", 1), Is.False);
            Assert.That(cache.Has("b", 1), Is.True);
            Assert.That(cache.Has("b", 0), Is.False);
            Assert.That(cache.Has("c", 5), Is.False);
        });
    }

    [Test]
    public void Add_OverCapacity_DropsOldest()
    {
        var cache = new LearnCache(2);
        cache.Add("a");
        cache.Add("b");
        cache.Add("c");

        Assert.That(cache.Has("a", 10), Is.False);
        Assert.That(cache.Has("b", 10), Is.True);
    }

    [Test]
    public void Constructor_NonPositiveCapacity_Throws()
    {
        Assert.That(() => new LearnCache(0), Throws.TypeOf<ArgumentOutOfRangeException>());
    }
}
