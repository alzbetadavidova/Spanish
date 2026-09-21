using System.Text.Json;
using Spanish.Core;

namespace Spanish.Tests;

public class AdjectiveScenarioFactoryTests
{
    private static ScenarioFactory Factory(LearnLibrary library, params int[] rolls) => new(new FakeRandom(rolls), library);

    [Test]
    public void Create_CardEnglishToSpanish_ShowsOtherFormsAsDetail()
    {
        var library = TestData.AdjectiveLibrary();

        var card = (CardScenario)Factory(library).Create(library.Adjectives[0], ScenarioType.Card, Direction.EnglishToSpanish);

        Assert.Multiple(() =>
        {
            Assert.That(card.Front, Is.EqualTo("short, low"));
            Assert.That(card.Back, Is.EqualTo("bajo"));
            Assert.That(card.BackDetail, Is.EqualTo("baja · bajos · bajas"));
        });
    }

    [Test]
    public void Create_CardSpanishToEnglish_HasNoDetail()
    {
        var library = TestData.AdjectiveLibrary();

        var card = (CardScenario)Factory(library).Create(library.Adjectives[0], ScenarioType.Card, Direction.SpanishToEnglish);

        Assert.Multiple(() =>
        {
            Assert.That(card.Front, Is.EqualTo("bajo"));
            Assert.That(card.BackDetail, Is.Null);
        });
    }

    [Test]
    public void Create_FillBothDirections()
    {
        var library = TestData.AdjectiveLibrary();
        var bajo = library.Adjectives[0];

        var toSpanish = (TypedScenario)Factory(library).Create(bajo, ScenarioType.Fill, Direction.EnglishToSpanish);
        var toEnglish = (TypedScenario)Factory(library).Create(bajo, ScenarioType.Fill, Direction.SpanishToEnglish);

        Assert.Multiple(() =>
        {
            Assert.That(toSpanish.Prompt, Is.EqualTo("short, low"));
            Assert.That(toSpanish.ExpectedAnswers, Is.EqualTo(new[] { "bajo" }));
            Assert.That(toEnglish.Prompt, Is.EqualTo("bajo"));
            Assert.That(toEnglish.ExpectedAnswers, Is.EqualTo(new[] { "short", "low" }));
        });
    }

    // Rolls: the linked noun (mujer, perro), then singular (0) or plural (1).
    [TestCase(0, 0, "bajo + mujer", "short + woman", "mujer baja", "la mujer baja")]
    [TestCase(0, 1, "bajo + mujeres", "short + woman (plural)", "mujeres bajas", "las mujeres bajas")]
    [TestCase(1, 0, "bajo + perro", "short + dog", "perro bajo", "el perro bajo")]
    [TestCase(1, 1, "bajo + perros", "short + dog (plural)", "perros bajos", "los perros bajos")]
    public void Create_Pair_AgreesWithNoun(int noun, int plural, string prompt, string detail, string answer, string withArticle)
    {
        var library = TestData.AdjectiveLibrary();

        var pair = (TypedScenario)Factory(library, noun, plural).Create(library.Adjectives[0], ScenarioType.PairWithNoun, Direction.Mixed);

        Assert.Multiple(() =>
        {
            Assert.That(pair.Type, Is.EqualTo(ScenarioType.PairWithNoun));
            Assert.That(pair.Instruction, Is.EqualTo(ScenarioFactory.PairInstruction));
            Assert.That(pair.Prompt, Is.EqualTo(prompt));
            Assert.That(pair.PromptDetail, Is.EqualTo(detail));
            Assert.That(pair.ExpectedAnswers, Is.EqualTo(new[] { answer, withArticle }));
        });
    }

    [Test]
    public void Create_Pair_NounWithoutPlural_AlwaysSingular()
    {
        var library = TestData.AdjectiveLibrary();
        library.Nouns[2].PluralValue = string.Empty;
        library.Adjectives[0].LinkedNouns = ["mujer"];
        var random = new FakeRandom(0, 1);

        var pair = (TypedScenario)new ScenarioFactory(random, library)
            .Create(library.Adjectives[0], ScenarioType.PairWithNoun, Direction.Mixed);

        Assert.Multiple(() =>
        {
            Assert.That(pair.Prompt, Is.EqualTo("bajo + mujer"));
            Assert.That(random.Requests, Is.EqualTo(new[] { 1 }));
        });
    }

    [Test]
    public void Create_Pair_MissingTranslation_HasNoDetail()
    {
        var library = TestData.AdjectiveLibrary();
        library.Nouns[2].Translation = string.Empty;
        library.Adjectives[0].LinkedNouns = ["mujer", "perro"];
        var noNounTranslation = (TypedScenario)Factory(library).Create(library.Adjectives[0], ScenarioType.PairWithNoun, Direction.Mixed);

        library.Nouns[2].Translation = "woman";
        library.Adjectives[0].Translation = string.Empty;
        var noAdjectiveTranslation = (TypedScenario)Factory(library).Create(library.Adjectives[0], ScenarioType.PairWithNoun, Direction.Mixed);

        Assert.Multiple(() =>
        {
            Assert.That(noNounTranslation.PromptDetail, Is.Null);
            Assert.That(noAdjectiveTranslation.PromptDetail, Is.Null);
        });
    }

    [Test]
    public void Create_Pair_LinkedNounNotInLibrary_Throws()
    {
        var adjective = TestData.Bajo();

        Assert.That(() => Factory(new LearnLibrary()).Create(adjective, ScenarioType.PairWithNoun, Direction.Mixed),
            Throws.ArgumentException.With.Message.Contains("not linked to a noun"));
    }

    [Test]
    public void Create_Pair_NoLinks_Throws()
    {
        var adjective = new Adjective { BaseValue = "alto", Translation = "tall" };

        Assert.That(() => Factory(new LearnLibrary()).Create(adjective, ScenarioType.PairWithNoun, Direction.Mixed),
            Throws.ArgumentException.With.Message.Contains("cannot be practiced"));
    }
}

public class AdjectiveLibraryTests
{
    [Test]
    public void Units_IncludeAdjectives()
    {
        var library = TestData.AdjectiveLibrary();

        Assert.That(library.Units.Select(u => u.BaseValue), Is.EqualTo(new[] { "ciudad", "perro", "mujer", "bajo" }));
    }

    [Test]
    public void Adjectives_Null_BecomesEmpty()
    {
        var library = new LearnLibrary { Adjectives = null! };

        Assert.That(library.Adjectives, Is.Empty);
    }

    [Test]
    public void LinkedNounsOf_ReturnsNounsInLinkOrderSkippingUnknown()
    {
        var library = TestData.AdjectiveLibrary();
        var adjective = new Adjective { LinkedNouns = ["Perro", "gato", "mujer"] };

        Assert.That(library.LinkedNounsOf(adjective).Select(n => n.BaseValue), Is.EqualTo(new[] { "perro", "mujer" }));
    }

    [Test]
    public void LinkedNounsOf_Null_Throws()
    {
        Assert.That(() => new LearnLibrary().LinkedNounsOf(null!), Throws.ArgumentNullException);
    }

    [Test]
    public void Validate_UnknownLinkedNoun_IsError()
    {
        var library = TestData.AdjectiveLibrary();
        var candidate = new Adjective { BaseValue = "alto", Translation = "tall", LinkedNouns = ["MUJER", "gato", "casa"] };

        var errors = library.Validate(candidate);

        Assert.That(errors, Is.EqualTo(new[] { new ValidationError(nameof(Adjective.LinkedNouns), "Unknown noun: gato, casa.") }));
    }

    [Test]
    public void Validate_KnownLinkedNouns_IsValid()
    {
        var library = TestData.AdjectiveLibrary();
        var candidate = new Adjective { BaseValue = "alto", Translation = "tall", LinkedNouns = ["Mujer"] };

        Assert.That(library.Validate(candidate), Is.Empty);
    }

    [Test]
    public void Validate_SameWordAsNoun_IsNotDuplicate()
    {
        var library = TestData.AdjectiveLibrary();
        var candidate = new Adjective { BaseValue = "perro", Translation = "dog-like" };

        Assert.That(library.Validate(candidate), Is.Empty);
    }

    [Test]
    public void Save_NewAdjective_AddsTrimmed()
    {
        var library = TestData.AdjectiveLibrary();
        var candidate = new Adjective { BaseValue = " alto ", Translation = "tall" };

        library.Save(candidate);

        Assert.Multiple(() =>
        {
            Assert.That(library.Adjectives, Has.Member(candidate));
            Assert.That(candidate.BaseValue, Is.EqualTo("alto"));
        });
    }

    [Test]
    public void Save_ExistingAdjective_CopiesContent()
    {
        var library = TestData.AdjectiveLibrary();
        var bajo = library.Adjectives[0];

        library.Save(new Adjective { BaseValue = "bajo", Translation = "short", LinkedNouns = ["ciudad"] }, bajo);

        Assert.Multiple(() =>
        {
            Assert.That(bajo.Translation, Is.EqualTo("short"));
            Assert.That(bajo.LinkedNouns, Is.EqualTo(new[] { "ciudad" }));
        });
    }

    [Test]
    public void Save_NounRenamed_UpdatesLinks()
    {
        var library = TestData.AdjectiveLibrary();
        var mujer = library.Nouns[2];
        var edit = TestData.Mujer();
        edit.BaseValue = " señora ";

        library.Save(edit, mujer);

        Assert.That(library.Adjectives[0].LinkedNouns, Is.EqualTo(new[] { "señora", "perro" }));
    }

    [Test]
    public void Save_NounCaseChanged_FollowsNewSpelling()
    {
        var library = TestData.AdjectiveLibrary();
        var edit = TestData.Mujer();
        edit.BaseValue = "Mujer";

        library.Save(edit, library.Nouns[2]);

        Assert.That(library.Adjectives[0].LinkedNouns, Is.EqualTo(new[] { "Mujer", "perro" }));
    }

    [Test]
    public void Save_NounNameUnchanged_KeepsLinks()
    {
        var library = TestData.AdjectiveLibrary();
        var links = library.Adjectives[0].LinkedNouns;
        var edit = TestData.Mujer();
        edit.Translation = "lady";

        library.Save(edit, library.Nouns[2]);

        Assert.That(library.Adjectives[0].LinkedNouns, Is.SameAs(links));
    }

    [Test]
    public void Remove_Adjective()
    {
        var library = TestData.AdjectiveLibrary();
        var bajo = library.Adjectives[0];

        Assert.Multiple(() =>
        {
            Assert.That(library.Remove(bajo), Is.True);
            Assert.That(library.Adjectives, Is.Empty);
            Assert.That(library.Remove(bajo), Is.False);
        });
    }

    [Test]
    public void Remove_LinkedNoun_RemovesLinks()
    {
        var library = TestData.AdjectiveLibrary();

        Assert.Multiple(() =>
        {
            Assert.That(library.Remove(library.Nouns[2]), Is.True);
            Assert.That(library.Adjectives[0].LinkedNouns, Is.EqualTo(new[] { "perro" }));
        });
    }

    [Test]
    public void Remove_DuplicateNoun_KeepsLinksToRemainingNoun()
    {
        var library = TestData.AdjectiveLibrary();
        library.Nouns.Add(TestData.Mujer());

        library.Remove(library.Nouns[2]);

        Assert.That(library.Adjectives[0].LinkedNouns, Is.EqualTo(new[] { "mujer", "perro" }));
    }

    [Test]
    public void Remove_NounNotInLibrary_ReturnsFalseAndKeepsLinks()
    {
        var library = TestData.AdjectiveLibrary();

        Assert.Multiple(() =>
        {
            Assert.That(library.Remove(TestData.Mujer()), Is.False);
            Assert.That(library.Adjectives[0].LinkedNouns, Is.EqualTo(new[] { "mujer", "perro" }));
        });
    }

    [Test]
    public void Deserialize_DropsNullAdjectivesAndDanglingLinks()
    {
        const string json = """
            {
              "Nouns": [{ "BaseValue": "mujer", "Gender": "Feminine" }],
              "Adjectives": [
                null,
                { "BaseValue": "bajo", "LinkedNouns": ["mujer", null, "gato"], "FeminineValue": null }
              ]
            }
            """;

        var library = JsonSerializer.Deserialize<LearnLibrary>(json, JsonFileStore<LearnLibrary>.Options)!;

        Assert.Multiple(() =>
        {
            Assert.That(library.Adjectives, Has.Count.EqualTo(1));
            Assert.That(library.Adjectives[0].LinkedNouns, Is.EqualTo(new[] { "mujer" }));
            Assert.That(library.Adjectives[0].FeminineValue, Is.Empty);
        });
    }

    [Test]
    public void Serialize_RoundTripsAdjectives()
    {
        var library = TestData.AdjectiveLibrary();
        library.Adjectives[0].MasculinePluralValue = "bajitos";

        var json = JsonSerializer.Serialize(library, JsonFileStore<LearnLibrary>.Options);
        var loaded = JsonSerializer.Deserialize<LearnLibrary>(json, JsonFileStore<LearnLibrary>.Options)!;

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Adjectives[0].BaseValue, Is.EqualTo("bajo"));
            Assert.That(loaded.Adjectives[0].MasculinePluralValue, Is.EqualTo("bajitos"));
            Assert.That(loaded.Adjectives[0].LinkedNouns, Is.EqualTo(new[] { "mujer", "perro" }));
        });
    }

    [Test]
    public void SeedLibrary_AdjectivesAreValidAndPracticable()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "library.json");
        var library = JsonSerializer.Deserialize<LearnLibrary>(File.ReadAllText(path), JsonFileStore<LearnLibrary>.Options)!;

        Assert.That(library.Adjectives, Is.Not.Empty);
        Assert.Multiple(() =>
        {
            foreach (var adjective in library.Adjectives)
            {
                Assert.That(library.Validate(adjective, adjective), Is.Empty, adjective.BaseValue);
                Assert.That(adjective.CanPractice(ScenarioType.PairWithNoun), Is.True, adjective.BaseValue);
            }
        });
    }
}

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
}
