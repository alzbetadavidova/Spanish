using Spanish.Core;

namespace Spanish.Tests;

public class ScenarioFactoryTests
{
    [Test]
    public void Create_CardEnglishToSpanish_Noun_ShowsEnglishFrontAndArticleBack()
    {
        var noun = TestData.Ciudad();

        var card = (CardScenario)new ScenarioFactory(new FakeRandom()).Create(noun, ScenarioType.Card, Direction.EnglishToSpanish);

        Assert.That(card, Is.EqualTo(new CardScenario(noun, Direction.EnglishToSpanish, "city, town", "la ciudad", "las ciudades")));
    }

    [Test]
    public void Create_CardEnglishToSpanish_NounWithoutPlural_HasNoDetail()
    {
        var noun = TestData.Perro();
        noun.PluralValue = string.Empty;

        var card = (CardScenario)new ScenarioFactory(new FakeRandom()).Create(noun, ScenarioType.Card, Direction.EnglishToSpanish);

        Assert.That(card.BackDetail, Is.Null);
    }

    [Test]
    public void Create_CardSpanishToEnglish_Verb_ShowsInfinitiveFront()
    {
        var verb = TestData.Hablar();

        var card = (CardScenario)new ScenarioFactory(new FakeRandom()).Create(verb, ScenarioType.Card, Direction.SpanishToEnglish);

        Assert.That(card, Is.EqualTo(new CardScenario(verb, Direction.SpanishToEnglish, "hablar", "to speak, talk", null)));
    }

    [TestCase(0, Direction.EnglishToSpanish)]
    [TestCase(1, Direction.SpanishToEnglish)]
    public void Create_MixedDirection_PicksAtRandom(int roll, Direction expected)
    {
        var card = (CardScenario)new ScenarioFactory(new FakeRandom(roll)).Create(TestData.Ciudad(), ScenarioType.Card, Direction.Mixed);

        Assert.That(card.Direction, Is.EqualTo(expected));
    }

    [Test]
    public void Create_FillEnglishToSpanish_Noun_AcceptsWithOrWithoutArticle()
    {
        var fill = (TypedScenario)new ScenarioFactory(new FakeRandom()).Create(TestData.Ciudad(), ScenarioType.Fill, Direction.EnglishToSpanish);

        Assert.Multiple(() =>
        {
            Assert.That(fill.Instruction, Is.EqualTo("Translate to Spanish"));
            Assert.That(fill.Prompt, Is.EqualTo("city, town"));
            Assert.That(fill.PromptDetail, Is.Null);
            Assert.That(fill.ExpectedAnswers, Is.EqualTo(new[] { "ciudad", "la ciudad" }));
        });
    }

    [Test]
    public void Create_FillEnglishToSpanish_Verb_ExpectsInfinitive()
    {
        var fill = (TypedScenario)new ScenarioFactory(new FakeRandom()).Create(TestData.Hablar(), ScenarioType.Fill, Direction.EnglishToSpanish);

        Assert.That(fill.ExpectedAnswers, Is.EqualTo(new[] { "hablar" }));
    }

    [Test]
    public void Create_FillSpanishToEnglish_Noun_ExpectsTranslations()
    {
        var fill = (TypedScenario)new ScenarioFactory(new FakeRandom()).Create(TestData.Ciudad(), ScenarioType.Fill, Direction.SpanishToEnglish);

        Assert.Multiple(() =>
        {
            Assert.That(fill.Instruction, Is.EqualTo("Translate to English"));
            Assert.That(fill.Prompt, Is.EqualTo("la ciudad"));
            Assert.That(fill.ExpectedAnswers, Is.EqualTo(new[] { "city", "town" }));
        });
    }

    [Test]
    public void Create_FillSpanishToEnglish_Verb_MakesLeadingToOptional()
    {
        var fill = (TypedScenario)new ScenarioFactory(new FakeRandom()).Create(TestData.Hablar(), ScenarioType.Fill, Direction.SpanishToEnglish);

        Assert.That(fill.ExpectedAnswers, Is.EqualTo(new[] { "to speak", "talk", "speak", "to talk" }));
    }

    [TestCase(0, "ciudad", Article.La)]
    [TestCase(1, "ciudades", Article.Las)]
    public void Create_Gender_SingularOrPluralAtRandom(int roll, string word, Article expected)
    {
        var noun = TestData.Ciudad();

        var gender = (GenderScenario)new ScenarioFactory(new FakeRandom(roll)).Create(noun, ScenarioType.Gender, Direction.Mixed);

        Assert.That(gender, Is.EqualTo(new GenderScenario(noun, word, expected)));
    }

    [Test]
    public void Create_Gender_NoPlural_AlwaysSingular()
    {
        var noun = TestData.Perro();
        noun.PluralValue = string.Empty;
        var random = new FakeRandom(1);

        var gender = (GenderScenario)new ScenarioFactory(random).Create(noun, ScenarioType.Gender, Direction.Mixed);

        Assert.That(gender.Expected, Is.EqualTo(Article.El));
        Assert.That(random.Requests, Is.Empty);
    }

    [Test]
    public void Create_Plural_AcceptsWithOrWithoutArticle()
    {
        var plural = (TypedScenario)new ScenarioFactory(new FakeRandom()).Create(TestData.Perro(), ScenarioType.Plural, Direction.Mixed);

        Assert.Multiple(() =>
        {
            Assert.That(plural.Instruction, Is.EqualTo("Type the plural"));
            Assert.That(plural.Prompt, Is.EqualTo("perro"));
            Assert.That(plural.PromptDetail, Is.EqualTo("dog"));
            Assert.That(plural.ExpectedAnswers, Is.EqualTo(new[] { "perros", "los perros" }));
        });
    }

    [TestCase(0, "yo", "hablo")]
    [TestCase(1, "tú", "hablas")]
    [TestCase(6, "nosotras", "hablamos")]
    [TestCase(9, "ustedes", "hablan")]
    public void Create_Present_RandomSubject(int roll, string subject, string form)
    {
        var present = (TypedScenario)new ScenarioFactory(new FakeRandom(roll)).Create(TestData.Hablar(), ScenarioType.Present, Direction.Mixed);

        Assert.Multiple(() =>
        {
            Assert.That(present.Instruction, Is.EqualTo("Conjugate · present"));
            Assert.That(present.Prompt, Is.EqualTo("hablar"));
            Assert.That(present.PromptDetail, Is.EqualTo(subject));
            Assert.That(present.ExpectedAnswers[0], Is.EqualTo(form));
            Assert.That(AnswerChecker.Check($"{subject} {form}", present.ExpectedAnswers).IsCorrect, Is.True);
        });
    }

    [Test]
    public void Create_Preterite_UsesPreteriteForms()
    {
        var preterite = (TypedScenario)new ScenarioFactory(new FakeRandom(2)).Create(TestData.Hablar(), ScenarioType.Preterite, Direction.Mixed);

        Assert.That(preterite.Instruction, Is.EqualTo("Conjugate · preterite (past)"));
        Assert.That(preterite.ExpectedAnswers, Is.EqualTo(new[] { "habló", "Él habló" }));
    }

    [Test]
    public void Create_Gerund_ExpectsGerund()
    {
        var gerund = (TypedScenario)new ScenarioFactory(new FakeRandom()).Create(TestData.Hablar(), ScenarioType.Gerund, Direction.Mixed);

        Assert.Multiple(() =>
        {
            Assert.That(gerund.Instruction, Is.EqualTo("Type the gerund"));
            Assert.That(gerund.PromptDetail, Is.EqualTo("to speak, talk"));
            Assert.That(gerund.ExpectedAnswers, Is.EqualTo(new[] { "hablando" }));
        });
    }

    [Test]
    public void Create_CannotPractice_Throws()
    {
        var factory = new ScenarioFactory(new FakeRandom());

        Assert.Multiple(() =>
        {
            Assert.That(() => factory.Create(TestData.Ciudad(), ScenarioType.Present, Direction.Mixed), Throws.ArgumentException);
            Assert.That(() => factory.Create(null!, ScenarioType.Card, Direction.Mixed), Throws.ArgumentNullException);
        });
    }

    [Test]
    public void Create_UnknownUnitWithNounOnlyScenario_Throws()
    {
        var unit = new UnknownUnit { BaseValue = "x" };

        Assert.That(() => new ScenarioFactory(new FakeRandom()).Create(unit, ScenarioType.Gender, Direction.Mixed),
            Throws.ArgumentException.With.Message.Contains("Unsupported scenario"));
    }
}
