using Spanish.Core;

namespace Spanish.Tests;

public class PrepositionTests
{
    private static Noun NounOf(string word, Gender gender, bool takesEl = false) =>
        new() { BaseValue = word, Translation = "x", Gender = gender, TakesElInSingular = takesEl };

    [Test]
    public void KindAndScenarios()
    {
        var preposition = new Preposition();

        Assert.Multiple(() =>
        {
            Assert.That(preposition.Kind, Is.EqualTo(WordKind.Preposition));
            Assert.That(preposition.SupportedScenarios,
                Is.EqualTo(new[] { ScenarioType.Card, ScenarioType.Fill, ScenarioType.PairWithNoun }));
        });
    }

    [TestCase("a", "mercado", Gender.Masculine, false, "al mercado")]
    [TestCase("de", "parque", Gender.Masculine, false, "del parque")]
    [TestCase("a", "playa", Gender.Feminine, false, "a la playa")]
    [TestCase("de", "agua", Gender.Feminine, true, "del agua")] // el agua
    [TestCase("en", "parque", Gender.Masculine, false, "en el parque")]
    [TestCase("desde", "ciudad", Gender.Feminine, false, "desde la ciudad")]
    [TestCase("cerca de", "río", Gender.Masculine, false, "cerca del río")]
    [TestCase("junto a", "lago", Gender.Masculine, false, "junto al lago")]
    [TestCase("al lado de", "banco", Gender.Masculine, false, "al lado del banco")] // only the last word contracts
    [TestCase("De", "parque", Gender.Masculine, false, "Del parque")]
    [TestCase(" cerca  de ", " parque ", Gender.Masculine, false, "cerca del parque")]
    [TestCase("", "parque", Gender.Masculine, false, "el parque")]
    public void WithNoun_AddsArticleAndContractsWithEl(string preposition, string noun, Gender gender, bool takesEl, string expected)
    {
        var unit = new Preposition { BaseValue = preposition };

        Assert.That(unit.WithNoun(NounOf(noun, gender, takesEl)), Is.EqualTo(expected));
    }

    [Test]
    public void WithNoun_Null_Throws()
    {
        Assert.That(() => TestData.De().WithNoun(null!), Throws.ArgumentNullException);
    }

    [Test]
    public void CanPairWith_NeedsNounTranslation()
    {
        var de = TestData.De();

        Assert.Multiple(() =>
        {
            Assert.That(de.CanPairWith(TestData.Perro()), Is.True);
            Assert.That(de.CanPairWith(new Noun { BaseValue = "perro", Translation = " ; " }), Is.False);
            // Adjectives name the noun in Spanish, so any linked noun will do.
            Assert.That(TestData.Bajo().CanPairWith(new Noun { BaseValue = "perro" }), Is.True);
        });
    }

    [Test]
    public void CanPairWith_Null_Throws()
    {
        Assert.That(() => TestData.De().CanPairWith(null!), Throws.ArgumentNullException);
    }

    [TestCase(ScenarioType.Card, true)]
    [TestCase(ScenarioType.Fill, true)]
    [TestCase(ScenarioType.PairWithNoun, true)]
    [TestCase(ScenarioType.Gender, false)]
    public void CanPractice_CompletePreposition(ScenarioType type, bool expected)
    {
        Assert.That(TestData.De().CanPractice(type), Is.EqualTo(expected));
    }

    [TestCase(ScenarioType.Card, false)]
    [TestCase(ScenarioType.Fill, false)]
    [TestCase(ScenarioType.PairWithNoun, false)] // the English phrase needs the translation
    public void CanPractice_LinksWithoutTranslation(ScenarioType type, bool expected)
    {
        Assert.That(new Preposition { BaseValue = "de", LinkedNouns = ["perro"] }.CanPractice(type), Is.EqualTo(expected));
    }

    [TestCase(ScenarioType.Card, true)]
    [TestCase(ScenarioType.PairWithNoun, false)]
    public void CanPractice_TranslationWithoutLinks(ScenarioType type, bool expected)
    {
        Assert.That(new Preposition { BaseValue = "entre", Translation = "between" }.CanPractice(type), Is.EqualTo(expected));
    }

    [Test]
    public void LinkedNouns_Null_BecomesEmpty()
    {
        Assert.That(new Preposition { LinkedNouns = null! }.LinkedNouns, Is.Empty);
    }

    [Test]
    public void CopyContentFrom_CopiesLinksButNotProgress()
    {
        var target = new Preposition { BaseValue = "a" };
        target.RecordAnswer(ScenarioType.PairWithNoun, true, new FakeClock().Now);
        var source = TestData.De();

        target.CopyContentFrom(source);
        source.LinkedNouns.Add("mujer");

        Assert.Multiple(() =>
        {
            Assert.That(target.BaseValue, Is.EqualTo("de"));
            Assert.That(target.Translation, Is.EqualTo("of; from"));
            Assert.That(target.Topics, Is.EqualTo(new[] { "animals" }));
            Assert.That(target.LinkedNouns, Is.EqualTo(new[] { "perro", "ciudad" }));
            Assert.That(target.GetProgress(ScenarioType.PairWithNoun), Is.Not.Null);
        });
    }
}
