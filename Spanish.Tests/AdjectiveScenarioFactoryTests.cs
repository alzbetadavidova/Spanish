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
