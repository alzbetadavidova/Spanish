using System.Text.Json;
using Spanish.Core;

namespace Spanish.Tests;

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
    public void LinkedNounsOf_NullOrBlankLink_IsSkipped()
    {
        var library = TestData.AdjectiveLibrary();
        var adjective = new Adjective { LinkedNouns = [null!, " ", "mujer"] };

        Assert.Multiple(() =>
        {
            Assert.That(library.LinkedNounsOf(adjective).Select(n => n.BaseValue), Is.EqualTo(new[] { "mujer" }));
            Assert.That(library.CanPractice(new Adjective { Translation = "x", LinkedNouns = [null!, " "] }, ScenarioType.PairWithNoun),
                Is.False);
        });
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
    public void Validate_DuplicateAdjective_IsError()
    {
        var library = TestData.AdjectiveLibrary();
        var candidate = new Adjective { BaseValue = "BAJO", Translation = "short" };

        Assert.That(library.Validate(candidate),
            Is.EqualTo(new[] { new ValidationError(nameof(LearnUnit.BaseValue), "\"BAJO\" is already in your library.") }));
    }

    [Test]
    public void Validate_BlankOrNullLinks_AreIgnored()
    {
        var library = TestData.AdjectiveLibrary();
        var candidate = new Adjective { BaseValue = "alto", Translation = "tall", LinkedNouns = [" ", null!, "mujer"] };

        Assert.That(library.Validate(candidate), Is.Empty);
    }

    [Test]
    public void Save_NormalizesLinks()
    {
        var library = TestData.AdjectiveLibrary();
        var candidate = new Adjective
        {
            BaseValue = "alto",
            Translation = "tall",
            LinkedNouns = ["MUJER", " ", null!, "mujer", " perro "]
        };

        library.Save(candidate);

        Assert.That(candidate.LinkedNouns, Is.EqualTo(new[] { "mujer", "perro" }));
    }

    [Test]
    public void Save_NounRenamedWhileDuplicateRemains_KeepsLinks()
    {
        var library = TestData.AdjectiveLibrary();
        library.Nouns.Add(TestData.Mujer());
        var edit = TestData.Mujer();
        edit.BaseValue = "señora";

        library.Save(edit, library.Nouns[2]);

        Assert.That(library.Adjectives[0].LinkedNouns, Is.EqualTo(new[] { "mujer", "perro" }));
    }

    [Test]
    public void CanPractice_PairNeedsLinkedNounInLibrary()
    {
        var library = TestData.AdjectiveLibrary();
        var dangling = new Adjective { BaseValue = "alto", Translation = "tall", LinkedNouns = ["gato"] };

        Assert.Multiple(() =>
        {
            Assert.That(library.CanPractice(library.Adjectives[0], ScenarioType.PairWithNoun), Is.True);
            Assert.That(library.CanPractice(dangling, ScenarioType.PairWithNoun), Is.False);
            Assert.That(library.CanPractice(dangling, ScenarioType.Card), Is.True);
            Assert.That(library.CanPractice(library.Nouns[0], ScenarioType.Gender), Is.True);
            Assert.That(library.CanPractice(library.Nouns[0], ScenarioType.PairWithNoun), Is.False);
        });
    }

    [Test]
    public void CanPractice_Null_Throws()
    {
        Assert.That(() => new LearnLibrary().CanPractice(null!, ScenarioType.Card), Throws.ArgumentNullException);
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
        library.Adjectives[0].LinkedNouns = ["Mujer", "perro"]; // links match nouns ignoring case
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
        library.Adjectives[0].LinkedNouns = ["Mujer", "perro"];

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
    public void Deserialize_DropsNullAdjectivesAndNormalizesLinks()
    {
        const string json = """
            {
              "Nouns": [{ "BaseValue": "mujer", "Gender": "Feminine" }],
              "Adjectives": [
                null,
                { "BaseValue": "bajo", "LinkedNouns": ["Mujer", null, "gato", " mujer "], "FeminineValue": null }
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
        var library = LoadSeedLibrary();

        Assert.That(library.Adjectives, Is.Not.Empty);
        Assert.Multiple(() =>
        {
            foreach (var adjective in library.Adjectives)
            {
                Assert.That(library.Validate(adjective, adjective), Is.Empty, adjective.BaseValue);
                Assert.That(library.CanPractice(adjective, ScenarioType.PairWithNoun), Is.True, adjective.BaseValue);
            }
        });
    }

    [Test]
    public void SeedLibrary_NounsAndVerbsAreValidAndPracticable()
    {
        var library = LoadSeedLibrary();

        Assert.Multiple(() =>
        {
            foreach (var unit in library.Nouns.Cast<LearnUnit>().Concat(library.Verbs))
            {
                Assert.That(library.Validate(unit, unit), Is.Empty, unit.BaseValue);
                // Nouns without a common plural (e.g. salud) leave it empty and skip the plural scenario.
                var scenarios = unit.SupportedScenarios.Where(t => t != ScenarioType.Plural || unit is Noun { PluralValue.Length: > 0 });
                Assert.That(scenarios.All(t => library.CanPractice(unit, t)), Is.True, unit.BaseValue);
            }
        });
    }

    [Test]
    public void SeedLibrary_FeminineNounsWithStressedA_TakeEl()
    {
        var nouns = LoadSeedLibrary().Nouns;
        var feminine = nouns.Where(n => n.Gender == Gender.Feminine).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(nouns.Where(n => n.TakesElInSingular), Has.All.Matches<Noun>(n => n.Gender == Gender.Feminine));
            Assert.That(feminine, Has.Some.Matches<Noun>(n => n.BaseValue == "agua" && n.TakesElInSingular));
            foreach (var noun in feminine)
            {
                Assert.That(noun.TakesElInSingular, Is.EqualTo(StressedASuggester.StartsWithStressedA(noun.BaseValue)), noun.BaseValue);
            }
        });
    }

    private static LearnLibrary LoadSeedLibrary()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "library.json");
        return JsonSerializer.Deserialize<LearnLibrary>(File.ReadAllText(path), JsonFileStore<LearnLibrary>.Options)!;
    }
}
