using System.Text.Json;
using Spanish.Core;

namespace Spanish.Tests;

public class PrepositionLibraryTests
{
    [Test]
    public void Words_IncludePrepositionsAfterAdjectives()
    {
        var library = TestData.PrepositionLibrary();
        library.Adjectives.Add(TestData.Bajo());

        Assert.That(library.Words.Select(u => u.BaseValue),
            Is.EqualTo(new[] { "ciudad", "perro", "agua", "bajo", "de", "desde" }));
    }

    [Test]
    public void Prepositions_Null_BecomesEmpty()
    {
        Assert.That(new LearnLibrary { Prepositions = null! }.Prepositions, Is.Empty);
    }

    [Test]
    public void LinkedNounsOf_Preposition_ReturnsNounsInLinkOrder()
    {
        var library = TestData.PrepositionLibrary();

        Assert.That(library.LinkedNounsOf(library.Prepositions[0]).Select(n => n.BaseValue), Is.EqualTo(new[] { "perro", "ciudad" }));
    }

    [Test]
    public void Validate_UnknownLinkedNoun_IsError()
    {
        var library = TestData.PrepositionLibrary();
        var candidate = new Preposition { BaseValue = "hacia", Translation = "towards", LinkedNouns = ["Perro", "gato"] };

        Assert.That(library.Validate(candidate),
            Is.EqualTo(new[] { new ValidationError(nameof(NounLinkedUnit.LinkedNouns), "Unknown noun: gato.") }));
    }

    [Test]
    public void Validate_DuplicatePreposition_IsError()
    {
        var library = TestData.PrepositionLibrary();
        var candidate = new Preposition { BaseValue = " DE ", Translation = "of" };

        Assert.That(library.Validate(candidate),
            Is.EqualTo(new[] { new ValidationError(nameof(LearnUnit.BaseValue), "\"DE\" is already in your library.") }));
    }

    [Test]
    public void Validate_SameWordAsAdjective_IsNotDuplicate()
    {
        var library = TestData.PrepositionLibrary();
        library.Adjectives.Add(TestData.Bajo());
        var candidate = new Preposition { BaseValue = "bajo", Translation = "under" };

        Assert.That(library.Validate(candidate), Is.Empty);
    }

    [Test]
    public void Save_NewPreposition_AddsTrimmedWithNormalizedLinks()
    {
        var library = TestData.PrepositionLibrary();
        var candidate = new Preposition
        {
            BaseValue = " cerca de ",
            Translation = "near",
            LinkedNouns = ["AGUA", " ", null!, "agua", " perro "]
        };

        library.Save(candidate);

        Assert.Multiple(() =>
        {
            Assert.That(library.Prepositions, Has.Member(candidate));
            Assert.That(candidate.BaseValue, Is.EqualTo("cerca de"));
            Assert.That(candidate.LinkedNouns, Is.EqualTo(new[] { "agua", "perro" }));
        });
    }

    [Test]
    public void Save_ExistingPreposition_CopiesContent()
    {
        var library = TestData.PrepositionLibrary();
        var de = library.Prepositions[0];

        library.Save(new Preposition { BaseValue = "de", Translation = "of", LinkedNouns = ["agua"] }, de);

        Assert.Multiple(() =>
        {
            Assert.That(de.Translation, Is.EqualTo("of"));
            Assert.That(de.LinkedNouns, Is.EqualTo(new[] { "agua" }));
            Assert.That(library.Prepositions, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Save_EditAcrossNounLinkedKinds_Throws()
    {
        var library = TestData.PrepositionLibrary();
        library.Adjectives.Add(new Adjective { BaseValue = "bajo", Translation = "short" });

        Assert.Multiple(() =>
        {
            Assert.That(() => library.Save(new Preposition { BaseValue = "bajo", Translation = "under" }, library.Adjectives[0]),
                Throws.ArgumentException);
            Assert.That(() => library.Save(new Adjective { BaseValue = "de", Translation = "of" }, library.Prepositions[0]),
                Throws.ArgumentException);
            Assert.That(library.Adjectives[0].Translation, Is.EqualTo("short"));
            Assert.That(library.Prepositions[0].Translation, Is.EqualTo("of; from"));
        });
    }

    [Test]
    public void Remove_Preposition()
    {
        var library = TestData.PrepositionLibrary();
        var de = library.Prepositions[0];

        Assert.Multiple(() =>
        {
            Assert.That(library.Remove(de), Is.True);
            Assert.That(library.Prepositions.Select(p => p.BaseValue), Is.EqualTo(new[] { "desde" }));
            Assert.That(library.Remove(de), Is.False);
        });
    }

    [Test]
    public void Remove_LinkedNoun_RemovesPrepositionAndAdjectiveLinks()
    {
        var library = TestData.PrepositionLibrary();
        library.Adjectives.Add(new Adjective { BaseValue = "grande", LinkedNouns = ["ciudad", "perro"] });

        library.Remove(library.Nouns[0]);

        Assert.Multiple(() =>
        {
            Assert.That(library.Prepositions[0].LinkedNouns, Is.EqualTo(new[] { "perro" }));
            Assert.That(library.Prepositions[1].LinkedNouns, Is.Empty);
            Assert.That(library.Adjectives[0].LinkedNouns, Is.EqualTo(new[] { "perro" }));
        });
    }

    [Test]
    public void Save_NounRenamed_UpdatesPrepositionLinks()
    {
        var library = TestData.PrepositionLibrary();
        var edit = TestData.Ciudad();
        edit.BaseValue = "urbe";

        library.Save(edit, library.Nouns[0]);

        Assert.Multiple(() =>
        {
            Assert.That(library.Prepositions[0].LinkedNouns, Is.EqualTo(new[] { "perro", "urbe" }));
            Assert.That(library.Prepositions[1].LinkedNouns, Is.EqualTo(new[] { "urbe" }));
        });
    }

    [Test]
    public void CanPractice_PrepositionPair_NeedsLinkedNounWithTranslation()
    {
        var library = TestData.PrepositionLibrary();
        var desde = library.Prepositions[1];

        Assert.That(library.CanPractice(desde, ScenarioType.PairWithNoun), Is.True);

        library.Nouns[0].Translation = string.Empty;
        Assert.Multiple(() =>
        {
            Assert.That(library.CanPractice(desde, ScenarioType.PairWithNoun), Is.False);
            Assert.That(library.CanPractice(desde, ScenarioType.Card), Is.True);
            Assert.That(library.CanPractice(library.Prepositions[0], ScenarioType.PairWithNoun), Is.True, "perro is still translated");
        });
    }

    [Test]
    public void CanPractice_PrepositionPair_LinkedNounNotInLibrary_IsFalse()
    {
        var library = TestData.PrepositionLibrary();
        var dangling = new Preposition { BaseValue = "hacia", Translation = "towards", LinkedNouns = ["gato"] };

        Assert.That(library.CanPractice(dangling, ScenarioType.PairWithNoun), Is.False);
    }

    [Test]
    public void Deserialize_DropsNullPrepositionsAndNormalizesLinks()
    {
        const string json = """
            {
              "Nouns": [{ "BaseValue": "parque", "Gender": "Masculine" }],
              "Prepositions": [
                null,
                { "BaseValue": "cerca de", "LinkedNouns": ["Parque", null, "gato", " parque "], "Topics": [null] }
              ]
            }
            """;

        var library = JsonSerializer.Deserialize<LearnLibrary>(json, JsonFileStore<LearnLibrary>.Options)!;

        Assert.Multiple(() =>
        {
            Assert.That(library.Prepositions, Has.Count.EqualTo(1));
            Assert.That(library.Prepositions[0].LinkedNouns, Is.EqualTo(new[] { "parque" }));
            Assert.That(library.Prepositions[0].Topics, Is.Empty);
        });
    }

    [Test]
    public void Deserialize_WithoutPrepositions_IsEmpty()
    {
        // A library saved before prepositions existed.
        var library = JsonSerializer.Deserialize<LearnLibrary>("""{ "Nouns": [] }""", JsonFileStore<LearnLibrary>.Options)!;

        Assert.That(library.Prepositions, Is.Empty);
    }

    [Test]
    public void Serialize_RoundTripsPrepositions()
    {
        var library = TestData.PrepositionLibrary();
        library.Prepositions[0].RecordAnswer(ScenarioType.PairWithNoun, false, new FakeClock().Now);

        var json = JsonSerializer.Serialize(library, JsonFileStore<LearnLibrary>.Options);
        var loaded = JsonSerializer.Deserialize<LearnLibrary>(json, JsonFileStore<LearnLibrary>.Options)!;

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Prepositions.Select(p => p.BaseValue), Is.EqualTo(new[] { "de", "desde" }));
            Assert.That(loaded.Prepositions[0].Translation, Is.EqualTo("of; from"));
            Assert.That(loaded.Prepositions[0].LinkedNouns, Is.EqualTo(new[] { "perro", "ciudad" }));
            Assert.That(loaded.Prepositions[0].GetProgress(ScenarioType.PairWithNoun)!.Attempts, Is.EqualTo(1));
        });
    }

    [Test]
    public void SeedLibrary_PrepositionsAreValidAndPracticable()
    {
        var library = SeedLibrary.Load();

        Assert.That(library.Prepositions, Has.Count.GreaterThan(30));
        Assert.Multiple(() =>
        {
            foreach (var preposition in library.Prepositions)
            {
                Assert.That(library.Validate(preposition, preposition), Is.Empty, preposition.BaseValue);
                Assert.That(library.CanPractice(preposition, ScenarioType.Card), Is.True, preposition.BaseValue);
                Assert.That(library.CanPractice(preposition, ScenarioType.Fill), Is.True, preposition.BaseValue);
                // "entre" has no links: "between the park" needs a plural.
                Assert.That(library.CanPractice(preposition, ScenarioType.PairWithNoun),
                    Is.EqualTo(preposition.BaseValue != "entre"), preposition.BaseValue);
                // A linked noun the pair can't use would silently never be practiced.
                Assert.That(library.LinkedNounsOf(preposition), Has.All.Matches<Noun>(preposition.CanPairWith), preposition.BaseValue);
            }
        });
    }

    [Test]
    public void SeedLibrary_PrepositionLinksNameSeedNouns()
    {
        // Loading drops links to unknown nouns, so check the file itself for typos.
        using var document = JsonDocument.Parse(File.ReadAllText(SeedLibrary.Path));
        var nouns = document.RootElement.GetProperty("Nouns").EnumerateArray()
            .Select(n => n.GetProperty("BaseValue").GetString())
            .ToHashSet();
        var links = document.RootElement.GetProperty("Prepositions").EnumerateArray()
            .Where(p => p.TryGetProperty("LinkedNouns", out _))
            .SelectMany(p => p.GetProperty("LinkedNouns").EnumerateArray().Select(l => l.GetString()))
            .ToList();

        Assert.That(links, Is.Not.Empty.And.All.Matches<string>(nouns.Contains));
    }
}
