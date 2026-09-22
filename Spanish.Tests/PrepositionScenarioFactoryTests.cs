using Spanish.Core;

namespace Spanish.Tests;

public class PrepositionScenarioFactoryTests
{
    private static ScenarioFactory Factory(LearnLibrary library, params int[] rolls) => new(new FakeRandom(rolls), library);

    [Test]
    public void Create_CardBothDirections_HasNoDetail()
    {
        var library = TestData.PrepositionLibrary();
        var de = library.Prepositions[0];

        var toSpanish = (CardScenario)Factory(library).Create(de, ScenarioType.Card, Direction.EnglishToSpanish);
        var toEnglish = (CardScenario)Factory(library).Create(de, ScenarioType.Card, Direction.SpanishToEnglish);

        Assert.Multiple(() =>
        {
            Assert.That(toSpanish.Front, Is.EqualTo("of, from"));
            Assert.That(toSpanish.Back, Is.EqualTo("de"));
            Assert.That(toSpanish.BackDetail, Is.Null);
            Assert.That(toEnglish.Front, Is.EqualTo("de"));
            Assert.That(toEnglish.Back, Is.EqualTo("of, from"));
        });
    }

    [Test]
    public void Create_FillBothDirections()
    {
        var library = TestData.PrepositionLibrary();
        var de = library.Prepositions[0];

        var toSpanish = (TypedScenario)Factory(library).Create(de, ScenarioType.Fill, Direction.EnglishToSpanish);
        var toEnglish = (TypedScenario)Factory(library).Create(de, ScenarioType.Fill, Direction.SpanishToEnglish);

        Assert.Multiple(() =>
        {
            Assert.That(toSpanish.Prompt, Is.EqualTo("of, from"));
            // "desde" also means "from".
            Assert.That(toSpanish.ExpectedAnswers, Is.EqualTo(new[] { "de", "desde" }));
            Assert.That(toEnglish.Prompt, Is.EqualTo("de"));
            Assert.That(toEnglish.ExpectedAnswers, Is.EqualTo(new[] { "of", "from" }));
        });
    }

    [Test]
    public void Create_FillToSpanish_AcceptsPrepositionsSharingAMeaning()
    {
        var library = new LearnLibrary
        {
            Prepositions =
            [
                new Preposition { BaseValue = "tras", Translation = "after" },
                new Preposition { BaseValue = "después de", Translation = "After" },
                new Preposition { BaseValue = " ", Translation = "after" }, // blank in a hand-edited file
                new Preposition { BaseValue = "ante", Translation = "in front of; in the presence of" },
                new Preposition { BaseValue = "delante de", Translation = "in front of" },
                new Preposition { BaseValue = "antes de", Translation = "before" }
            ]
        };

        TypedScenario FillOf(int index) =>
            (TypedScenario)Factory(library).Create(library.Prepositions[index], ScenarioType.Fill, Direction.EnglishToSpanish);

        Assert.Multiple(() =>
        {
            Assert.That(FillOf(0).ExpectedAnswers, Is.EqualTo(new[] { "tras", "después de" }));
            Assert.That(FillOf(4).ExpectedAnswers, Is.EqualTo(new[] { "delante de", "ante" }));
            Assert.That(FillOf(5).ExpectedAnswers, Is.EqualTo(new[] { "antes de" }));
        });
    }

    // Rolls: the linked noun (perro, ciudad), then the English alternative (of, from), then one more that would
    // pick the noun's second translation if it were drawn too. "desde" also means "from", so it is accepted for "from".
    [TestCase(0, 0, "of the dog", "perro", new[] { "del perro" })]
    [TestCase(0, 1, "from the dog", "perro", new[] { "del perro", "desde el perro" })]
    [TestCase(1, 1, "from the city", "ciudad", new[] { "de la ciudad", "desde la ciudad" })]
    public void Create_Pair_TranslatesEnglishPhrase(int noun, int english, string prompt, string detail, string[] answers)
    {
        var library = TestData.PrepositionLibrary();

        var pair = (TypedScenario)Factory(library, noun, english, 1).Create(library.Prepositions[0], ScenarioType.PairWithNoun, Direction.Mixed);

        Assert.Multiple(() =>
        {
            Assert.That(pair.Type, Is.EqualTo(ScenarioType.PairWithNoun));
            Assert.That(pair.Instruction, Is.EqualTo(ScenarioFactory.PrepositionPairInstruction));
            Assert.That(pair.Prompt, Is.EqualTo(prompt));
            Assert.That(pair.PromptDetail, Is.EqualTo(detail));
            Assert.That(pair.ExpectedAnswers, Is.EqualTo(answers));
            Assert.That(pair.Notes, Is.Empty);
        });
    }

    [Test]
    public void Create_Pair_UsesFirstNounTranslation()
    {
        var library = TestData.PrepositionLibrary();

        // Rolls: the only linked noun (ciudad: "city; town"), then "since", then one that would pick "town".
        var random = new FakeRandom(0, 1, 1);

        var pair = (TypedScenario)new ScenarioFactory(random, library)
            .Create(library.Prepositions[1], ScenarioType.PairWithNoun, Direction.Mixed);

        Assert.Multiple(() =>
        {
            Assert.That(pair.Prompt, Is.EqualTo("since the city"));
            Assert.That(random.Requests, Is.EqualTo(new[] { 1, 2 }));
        });
    }

    [Test]
    public void Create_Pair_MatchesSynonymsIgnoringCaseAndSkipsBlankPrepositions()
    {
        var library = TestData.PrepositionLibrary();
        library.Prepositions[1].Translation = "From; since";
        library.Prepositions.Add(new Preposition { BaseValue = " ", Translation = "from" });

        var pair = (TypedScenario)Factory(library, 0, 1).Create(library.Prepositions[0], ScenarioType.PairWithNoun, Direction.Mixed);

        Assert.That(pair.ExpectedAnswers, Is.EqualTo(new[] { "del perro", "desde el perro" }));
    }

    [Test]
    public void Create_Pair_SynonymWithSamePhrase_IsListedOnce()
    {
        var library = TestData.PrepositionLibrary();
        // A hand-edited file may hold the same preposition twice.
        library.Prepositions.Add(new Preposition { BaseValue = "DE", Translation = "from" });

        var pair = (TypedScenario)Factory(library, 0, 1).Create(library.Prepositions[0], ScenarioType.PairWithNoun, Direction.Mixed);

        Assert.That(pair.ExpectedAnswers, Is.EqualTo(new[] { "del perro", "desde el perro" }));
    }

    [Test]
    public void Create_Pair_PrepositionNotInLibrary_AcceptsOnlyItsOwnPhrase()
    {
        var library = new LearnLibrary { Nouns = [TestData.Perro()] };

        var pair = (TypedScenario)Factory(library, 0, 1).Create(TestData.De(), ScenarioType.PairWithNoun, Direction.Mixed);

        Assert.That(pair.ExpectedAnswers, Is.EqualTo(new[] { "del perro" }));
    }

    [Test]
    public void Create_Pair_ExplainsTheNounsArticle()
    {
        var library = TestData.PrepositionLibrary();
        var de = library.Prepositions[0];
        de.LinkedNouns = ["agua"];

        var pair = (TypedScenario)Factory(library).Create(de, ScenarioType.PairWithNoun, Direction.Mixed);

        Assert.Multiple(() =>
        {
            Assert.That(pair.Prompt, Is.EqualTo("of the water"));
            Assert.That(pair.ExpectedAnswers, Is.EqualTo(new[] { "del agua" }));
            Assert.That(pair.Notes.Select(n => n.Kind), Is.EqualTo(new[] { IrregularityKind.ElBeforeStressedA }));
        });
    }

    [Test]
    public void Create_Pair_NotesOnlyTheNounsGender()
    {
        // Masculine although it ends in -a, with an irregular plural that a singular pair never shows.
        var dia = new Noun { BaseValue = "día", Translation = "day", Gender = Gender.Masculine, PluralValue = "diases" };
        var library = new LearnLibrary { Nouns = [dia] };
        var preposition = new Preposition { BaseValue = "de", Translation = "of", LinkedNouns = ["día"] };

        var pair = (TypedScenario)Factory(library).Create(preposition, ScenarioType.PairWithNoun, Direction.Mixed);

        Assert.Multiple(() =>
        {
            Assert.That(Irregularities.Of(dia).Select(n => n.Kind), Has.Member(IrregularityKind.IrregularPlural));
            Assert.That(pair.ExpectedAnswers, Is.EqualTo(new[] { "del día" }));
            Assert.That(pair.Notes.Select(n => n.Kind), Is.EqualTo(new[] { IrregularityKind.MasculineEndingInA }));
        });
    }

    [Test]
    public void Create_Pair_SkipsNounsWithoutTranslation()
    {
        var library = TestData.PrepositionLibrary();
        library.Nouns[1].Translation = string.Empty; // perro
        var random = new FakeRandom();

        var pair = (TypedScenario)new ScenarioFactory(random, library)
            .Create(library.Prepositions[0], ScenarioType.PairWithNoun, Direction.Mixed);

        Assert.Multiple(() =>
        {
            Assert.That(pair.PromptDetail, Is.EqualTo("ciudad"));
            Assert.That(random.Requests, Is.EqualTo(new[] { 1, 2 }));
        });
    }

    [Test]
    public void Create_Pair_LinkedNounNotInLibrary_Throws()
    {
        Assert.That(() => Factory(new LearnLibrary()).Create(TestData.De(), ScenarioType.PairWithNoun, Direction.Mixed),
            Throws.ArgumentException.With.Message.Contains("no linked noun"));
    }

    [Test]
    public void Create_Pair_NoTranslation_Throws()
    {
        var preposition = new Preposition { BaseValue = "de", LinkedNouns = ["perro"] };

        Assert.That(() => Factory(TestData.PrepositionLibrary()).Create(preposition, ScenarioType.PairWithNoun, Direction.Mixed),
            Throws.ArgumentException.With.Message.Contains("cannot be practiced"));
    }
}
