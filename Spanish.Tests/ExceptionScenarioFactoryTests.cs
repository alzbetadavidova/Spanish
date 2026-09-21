using Spanish.Core;

namespace Spanish.Tests;

public class ExceptionScenarioFactoryTests
{
    private static Noun Agua() => new()
    {
        BaseValue = "agua",
        Translation = "water",
        Gender = Gender.Feminine,
        PluralValue = "aguas",
        TakesElInSingular = true
    };

    private static Noun Mano() => new() { BaseValue = "mano", Translation = "hand", Gender = Gender.Feminine, PluralValue = "manos" };

    private static Verb Tener() => new()
    {
        BaseValue = "tener",
        Translation = "to have",
        PresentConjugations = ["tengo", "tienes", "tiene", "tenemos", "tienen"],
        PreteriteConjugations = ["tuve", "tuviste", "tuvo", "tuvimos", "tuvieron"],
        NonPersonalGerund = "teniendo"
    };

    private static ScenarioFactory Factory(LearnLibrary library, params int[] rolls) => new(new FakeRandom(rolls), library);

    [TestCase(0, "agua", Article.El)]
    [TestCase(1, "aguas", Article.Las)]
    public void Create_Gender_FeminineTakingEl(int roll, string word, Article expected)
    {
        var gender = (GenderScenario)Factory(new LearnLibrary(), roll).Create(Agua(), ScenarioType.Gender, Direction.Mixed);

        Assert.Multiple(() =>
        {
            Assert.That(gender.Word, Is.EqualTo(word));
            Assert.That(gender.Expected, Is.EqualTo(expected));
            Assert.That(gender.Notes.Select(n => n.Kind), Is.EqualTo(new[] { IrregularityKind.ElBeforeStressedA }));
        });
    }

    [Test]
    public void Create_Card_FeminineTakingEl_ShowsEl()
    {
        var card = (CardScenario)Factory(new LearnLibrary()).Create(Agua(), ScenarioType.Card, Direction.EnglishToSpanish);

        Assert.Multiple(() =>
        {
            Assert.That(card.Back, Is.EqualTo("el agua"));
            Assert.That(card.BackDetail, Is.EqualTo("las aguas"));
            Assert.That(card.Notes, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Create_RegularWord_HasNoNotes()
    {
        var card = Factory(new LearnLibrary()).Create(TestData.Ciudad(), ScenarioType.Card, Direction.EnglishToSpanish);

        Assert.That(card.Notes, Is.Empty);
    }

    [Test]
    public void Create_Present_KeepsOnlyThePresentNote()
    {
        var present = Factory(new LearnLibrary()).Create(Tener(), ScenarioType.Present, Direction.Mixed);

        Assert.That(present.Notes.Select(n => n.Kind), Is.EqualTo(new[] { IrregularityKind.IrregularPresent }));
    }

    [Test]
    public void Create_Pair_NounTakingEl_AgreesInTheFeminineAndExplainsTheArticle()
    {
        var frio = new Adjective { BaseValue = "frío", Translation = "cold", LinkedNouns = ["agua"] };
        var library = new LearnLibrary { Nouns = [Agua()], Adjectives = [frio] };

        var pair = (TypedScenario)Factory(library, 0, 0).Create(frio, ScenarioType.PairWithNoun, Direction.Mixed);

        Assert.Multiple(() =>
        {
            Assert.That(pair.ExpectedAnswers, Is.EqualTo(new[] { "agua fría", "el agua fría" }));
            Assert.That(pair.Notes.Select(n => n.Kind), Is.EqualTo(new[] { IrregularityKind.ElBeforeStressedA }));
        });
    }

    [Test]
    public void Create_Pair_ExplainsTheAdjectiveThenTheNoun()
    {
        var espanol = new Adjective { BaseValue = "español", Translation = "Spanish", FeminineValue = "española", LinkedNouns = ["mano"] };
        var library = new LearnLibrary { Nouns = [Mano()], Adjectives = [espanol] };

        var pair = (TypedScenario)Factory(library, 0, 1).Create(espanol, ScenarioType.PairWithNoun, Direction.Mixed);

        Assert.Multiple(() =>
        {
            Assert.That(pair.ExpectedAnswers, Is.EqualTo(new[] { "manos españolas", "las manos españolas" }));
            Assert.That(pair.Notes.Select(n => n.Kind), Is.EqualTo(new[]
            {
                IrregularityKind.IrregularAdjectiveForms, IrregularityKind.FeminineEndingInO
            }));
        });
    }

    [Test]
    public void Create_Pair_RegularWords_HaveNoNotes()
    {
        var library = TestData.AdjectiveLibrary();

        var pair = Factory(library).Create(library.Adjectives[0], ScenarioType.PairWithNoun, Direction.Mixed);

        Assert.That(pair.Notes, Is.Empty);
    }
}
