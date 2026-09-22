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
            Assert.That(toSpanish.ExpectedAnswers, Is.EqualTo(new[] { "de" }));
            Assert.That(toEnglish.Prompt, Is.EqualTo("de"));
            Assert.That(toEnglish.ExpectedAnswers, Is.EqualTo(new[] { "of", "from" }));
        });
    }

    // Rolls: the linked noun (perro, ciudad), then the English alternative (of, from).
    // "desde" also means "from", so it is accepted for "from".
    [TestCase(0, 0, "of the dog", "perro", new[] { "del perro" })]
    [TestCase(0, 1, "from the dog", "perro", new[] { "del perro", "desde el perro" })]
    [TestCase(1, 1, "from the city", "ciudad", new[] { "de la ciudad", "desde la ciudad" })]
    public void Create_Pair_TranslatesEnglishPhrase(int noun, int english, string prompt, string detail, string[] answers)
    {
        var library = TestData.PrepositionLibrary();

        var pair = (TypedScenario)Factory(library, noun, english).Create(library.Prepositions[0], ScenarioType.PairWithNoun, Direction.Mixed);

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

        // Rolls: the only linked noun (ciudad: "city; town"), then "since".
        var pair = (TypedScenario)Factory(library, 0, 1).Create(library.Prepositions[1], ScenarioType.PairWithNoun, Direction.Mixed);

        Assert.That(pair.Prompt, Is.EqualTo("since the city"));
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
            Throws.ArgumentException.With.Message.Contains("not linked to a noun"));
    }

    [Test]
    public void Create_Pair_NoTranslation_Throws()
    {
        var preposition = new Preposition { BaseValue = "de", LinkedNouns = ["perro"] };

        Assert.That(() => Factory(TestData.PrepositionLibrary()).Create(preposition, ScenarioType.PairWithNoun, Direction.Mixed),
            Throws.ArgumentException.With.Message.Contains("cannot be practiced"));
    }
}
