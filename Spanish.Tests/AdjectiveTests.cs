using Spanish.Core;

namespace Spanish.Tests;

public class AdjectiveFormSuggesterTests
{
    [TestCase("bajo", "baja")]
    [TestCase("BAJO", "BAJA")]
    [TestCase(" bajo ", "baja")]
    [TestCase("trabajador", "trabajadora")]
    [TestCase("TRABAJADOR", "TRABAJADORA")]
    [TestCase("mejor", "mejor")]
    [TestCase("Mayor", "Mayor")]
    [TestCase("alemán", "alemana")]
    [TestCase("ALEMÁN", "ALEMANA")]
    [TestCase("inglés", "inglesa")]
    [TestCase("dormilón", "dormilona")]
    [TestCase("chiquitín", "chiquitina")]
    [TestCase("común", "común")]
    [TestCase("cortés", "cortés")]
    [TestCase("Descortés", "Descortés")]
    [TestCase("xál", "xál")] // accented vowel before a consonant other than n or s
    [TestCase("grande", "grande")]
    [TestCase("azul", "azul")]
    [TestCase("gris", "gris")]
    [TestCase("o", "o")]
    [TestCase("", "")]
    public void Feminine_FollowsRegularRules(string masculine, string expected)
    {
        Assert.That(AdjectiveFormSuggester.Feminine(masculine), Is.EqualTo(expected));
    }

    [Test]
    public void Feminine_Null_ReturnsEmpty()
    {
        Assert.That(AdjectiveFormSuggester.Feminine(null!), Is.Empty);
    }

    [TestCase("bajo", "baja", "bajos", "bajas")]
    [TestCase("feliz", "feliz", "felices", "felices")]
    [TestCase("alemán", "alemana", "alemanes", "alemanas")]
    [TestCase("", "", "", "")]
    public void Suggest_DerivesAllForms(string masculine, string feminine, string masculinePlural, string femininePlural)
    {
        Assert.That(AdjectiveFormSuggester.Suggest(masculine),
            Is.EqualTo(new AdjectiveForms(feminine, masculinePlural, femininePlural)));
    }

    [TestCase("española", "españolas")]
    [TestCase(" ", "españoles")] // a blank feminine is not a fix
    [TestCase(null, "españoles")]
    public void Suggest_GivenFeminine_DrivesFemininePlural(string? feminine, string femininePlural)
    {
        var forms = AdjectiveFormSuggester.Suggest("español", feminine);

        Assert.Multiple(() =>
        {
            Assert.That(forms.Feminine, Is.EqualTo("español"));
            Assert.That(forms.FemininePlural, Is.EqualTo(femininePlural));
        });
    }
}

public class AdjectiveTests
{
    [TestCase(Gender.Masculine, false, "bajo")]
    [TestCase(Gender.Feminine, false, "baja")]
    [TestCase(Gender.Masculine, true, "bajos")]
    [TestCase(Gender.Feminine, true, "bajas")]
    public void GetForm_NoStoredForms_UsesSuggestedForms(Gender gender, bool plural, string expected)
    {
        Assert.That(TestData.Bajo().GetForm(gender, plural), Is.EqualTo(expected));
    }

    [Test]
    public void GetForm_StoredForms_WinOverSuggestions()
    {
        var adjective = new Adjective
        {
            BaseValue = "español",
            FeminineValue = "española",
            MasculinePluralValue = "españoles"
        };

        Assert.Multiple(() =>
        {
            Assert.That(adjective.GetForm(Gender.Feminine, false), Is.EqualTo("española"));
            Assert.That(adjective.GetForm(Gender.Masculine, true), Is.EqualTo("españoles"));
            // The feminine plural is suggested from the stored feminine form.
            Assert.That(adjective.GetForm(Gender.Feminine, true), Is.EqualTo("españolas"));
        });
    }

    [Test]
    public void GetForm_StoredFemininePlural_WinsAndBlankFormsAreSuggested()
    {
        var adjective = new Adjective { BaseValue = "joven", FeminineValue = " ", FemininePluralValue = "jóvenes" };

        Assert.Multiple(() =>
        {
            Assert.That(adjective.GetForm(Gender.Feminine, false), Is.EqualTo("joven"));
            Assert.That(adjective.GetForm(Gender.Feminine, true), Is.EqualTo("jóvenes"));
        });
    }

    [Test]
    public void KindAndScenarios()
    {
        var adjective = new Adjective();

        Assert.Multiple(() =>
        {
            Assert.That(adjective.Kind, Is.EqualTo(WordKind.Adjective));
            Assert.That(adjective.SupportedScenarios,
                Is.EqualTo(new[] { ScenarioType.Card, ScenarioType.Fill, ScenarioType.PairWithNoun }));
        });
    }

    [TestCase(ScenarioType.Card, true)]
    [TestCase(ScenarioType.Fill, true)]
    [TestCase(ScenarioType.PairWithNoun, true)]
    [TestCase(ScenarioType.Gender, false)]
    public void CanPractice_CompleteAdjective(ScenarioType type, bool expected)
    {
        Assert.That(TestData.Bajo().CanPractice(type), Is.EqualTo(expected));
    }

    [TestCase(ScenarioType.Card, false)]
    [TestCase(ScenarioType.Fill, false)]
    [TestCase(ScenarioType.PairWithNoun, false)]
    public void CanPractice_NoTranslationAndNoLinks(ScenarioType type, bool expected)
    {
        Assert.That(new Adjective { BaseValue = "bajo" }.CanPractice(type), Is.EqualTo(expected));
    }

    [Test]
    public void NullValues_BecomeEmpty()
    {
        var adjective = new Adjective
        {
            FeminineValue = null!,
            MasculinePluralValue = null!,
            FemininePluralValue = null!,
            LinkedNouns = null!
        };

        Assert.Multiple(() =>
        {
            Assert.That(adjective.FeminineValue, Is.Empty);
            Assert.That(adjective.MasculinePluralValue, Is.Empty);
            Assert.That(adjective.FemininePluralValue, Is.Empty);
            Assert.That(adjective.LinkedNouns, Is.Empty);
        });
    }

    [Test]
    public void CopyContentFrom_CopiesFormsAndLinksButNotProgress()
    {
        var target = new Adjective { BaseValue = "alto" };
        target.RecordAnswer(ScenarioType.Card, true, new FakeClock().Now);
        var source = new Adjective
        {
            BaseValue = "joven",
            Translation = "young",
            FeminineValue = "joven",
            MasculinePluralValue = "jóvenes",
            FemininePluralValue = "jóvenes",
            LinkedNouns = ["mujer"]
        };

        target.CopyContentFrom(source);
        source.LinkedNouns.Add("perro");

        Assert.Multiple(() =>
        {
            Assert.That(target.BaseValue, Is.EqualTo("joven"));
            Assert.That(target.FeminineValue, Is.EqualTo("joven"));
            Assert.That(target.MasculinePluralValue, Is.EqualTo("jóvenes"));
            Assert.That(target.FemininePluralValue, Is.EqualTo("jóvenes"));
            Assert.That(target.LinkedNouns, Is.EqualTo(new[] { "mujer" }));
            Assert.That(target.GetProgress(ScenarioType.Card), Is.Not.Null);
        });
    }
}
