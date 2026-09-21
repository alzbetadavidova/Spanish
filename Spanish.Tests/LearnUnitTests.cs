using Spanish.Core;

namespace Spanish.Tests;

public class LearnProgressTests
{
    [Test]
    public void Index_NoAnswers_IsNull()
    {
        var progress = new LearnProgress();

        Assert.Multiple(() =>
        {
            Assert.That(progress.Index, Is.Null);
            Assert.That(progress.LastPracticed, Is.Null);
            Assert.That(progress.Attempts, Is.Zero);
        });
    }

    [Test]
    public void Record_MixedAnswers_IndexIsAccuracy()
    {
        var progress = new LearnProgress();
        var start = new DateTime(2026, 1, 1);

        progress.Record(true, start);
        progress.Record(false, start.AddDays(2));
        progress.Record(true, start.AddDays(1));
        progress.Record(true, start.AddDays(1));

        Assert.Multiple(() =>
        {
            Assert.That(progress.Index, Is.EqualTo(0.75));
            Assert.That(progress.LastPracticed, Is.EqualTo(start.AddDays(2)));
            Assert.That(progress.Attempts, Is.EqualTo(4));
        });
    }

    [Test]
    public void Record_MoreThanWindow_KeepsOnlyRecentAnswers()
    {
        var progress = new LearnProgress();
        var at = new DateTime(2026, 1, 1);
        for (var i = 0; i < LearnProgress.WindowSize; i++)
        {
            progress.Record(false, at);
        }

        progress.Record(true, at);

        Assert.Multiple(() =>
        {
            Assert.That(progress.Recent, Has.Count.EqualTo(LearnProgress.WindowSize));
            Assert.That(progress.Index, Is.EqualTo(0.1).Within(1e-9));
            Assert.That(progress.Attempts, Is.EqualTo(LearnProgress.WindowSize + 1));
        });
    }
}

public class LearnUnitTests
{
    [Test]
    public void TranslationAlternatives_SplitsTrimsAndDropsEmpty()
    {
        var noun = new Noun { Translation = " city ;; town ; " };

        Assert.That(noun.TranslationAlternatives, Is.EqualTo(new[] { "city", "town" }));
    }

    [Test]
    public void OverallIndex_NothingPracticed_IsNull()
    {
        Assert.That(TestData.Ciudad().OverallIndex, Is.Null);
    }

    [Test]
    public void OverallIndex_AveragesPracticedScenariosOnly()
    {
        var noun = TestData.Ciudad();
        var at = new DateTime(2026, 1, 1);
        noun.RecordAnswer(ScenarioType.Card, true, at);
        noun.RecordAnswer(ScenarioType.Gender, false, at);
        noun.Progress[ScenarioType.Fill] = new LearnProgress();

        Assert.That(noun.OverallIndex, Is.EqualTo(0.5));
    }

    [Test]
    public void RecordAnswer_ExistingProgress_Appends()
    {
        var noun = TestData.Ciudad();
        var at = new DateTime(2026, 1, 1);

        noun.RecordAnswer(ScenarioType.Card, true, at);
        noun.RecordAnswer(ScenarioType.Card, false, at);

        Assert.That(noun.GetProgress(ScenarioType.Card)!.Attempts, Is.EqualTo(2));
        Assert.That(noun.GetProgress(ScenarioType.Gender), Is.Null);
    }

    [TestCase("city", true)]
    [TestCase("CITY", true)]
    [TestCase("food", false)]
    public void HasTopic_IgnoresCase(string topic, bool expected)
    {
        Assert.That(TestData.Ciudad().HasTopic(topic), Is.EqualTo(expected));
    }

    [TestCase(ScenarioType.Card, true)]
    [TestCase(ScenarioType.Fill, true)]
    [TestCase(ScenarioType.Gender, true)]
    [TestCase(ScenarioType.Plural, true)]
    [TestCase(ScenarioType.Present, false)]
    public void Noun_CanPractice_CompleteNoun(ScenarioType type, bool expected)
    {
        Assert.That(TestData.Ciudad().CanPractice(type), Is.EqualTo(expected));
    }

    [TestCase(ScenarioType.Card, false)]
    [TestCase(ScenarioType.Fill, false)]
    [TestCase(ScenarioType.Gender, true)]
    [TestCase(ScenarioType.Plural, false)]
    public void Noun_CanPractice_MissingTranslationAndPlural(ScenarioType type, bool expected)
    {
        var noun = new Noun { BaseValue = "agua", PluralValue = " " };

        Assert.That(noun.CanPractice(type), Is.EqualTo(expected));
    }

    [TestCase(ScenarioType.Card, true)]
    [TestCase(ScenarioType.Present, true)]
    [TestCase(ScenarioType.Preterite, true)]
    [TestCase(ScenarioType.Gerund, true)]
    [TestCase(ScenarioType.Gender, false)]
    public void Verb_CanPractice_CompleteVerb(ScenarioType type, bool expected)
    {
        Assert.That(TestData.Hablar().CanPractice(type), Is.EqualTo(expected));
    }

    [TestCase(ScenarioType.Present, false)]
    [TestCase(ScenarioType.Preterite, false)]
    [TestCase(ScenarioType.Gerund, false)]
    [TestCase(ScenarioType.Fill, true)]
    public void Verb_CanPractice_MissingForms(ScenarioType type, bool expected)
    {
        var verb = new Verb
        {
            BaseValue = "comer",
            Translation = "to eat",
            PresentConjugations = ["como", "", "come", "comemos", "comen"],
            PreteriteConjugations = ["comí"]
        };

        Assert.That(verb.CanPractice(type), Is.EqualTo(expected));
    }

    [Test]
    public void Noun_Articles_FollowGender()
    {
        var masculine = TestData.Perro();
        var feminine = TestData.Ciudad();

        Assert.Multiple(() =>
        {
            Assert.That(masculine.SingularArticle, Is.EqualTo(Article.El));
            Assert.That(masculine.PluralArticle, Is.EqualTo(Article.Los));
            Assert.That(feminine.SingularArticle, Is.EqualTo(Article.La));
            Assert.That(feminine.PluralArticle, Is.EqualTo(Article.Las));
            Assert.That(Article.Las.ToText(), Is.EqualTo("las"));
        });
    }

    [Test]
    public void Noun_TakesElInSingular_OnlyChangesTheSingularArticle()
    {
        var agua = new Noun { BaseValue = "agua", Gender = Gender.Feminine, TakesElInSingular = true };
        var masculine = TestData.Perro();
        masculine.TakesElInSingular = true;

        Assert.Multiple(() =>
        {
            Assert.That(agua.SingularArticle, Is.EqualTo(Article.El));
            Assert.That(agua.PluralArticle, Is.EqualTo(Article.Las));
            Assert.That(masculine.SingularArticle, Is.EqualTo(Article.El));
            Assert.That(masculine.PluralArticle, Is.EqualTo(Article.Los));
        });
    }

    [Test]
    public void Noun_CopyContentFrom_CopiesTakesElInSingular()
    {
        var target = TestData.Ciudad();

        target.CopyContentFrom(new Noun { BaseValue = "agua", Gender = Gender.Feminine, TakesElInSingular = true });

        Assert.That(target.TakesElInSingular, Is.True);
    }

    [Test]
    public void Noun_CopyContentFrom_CopiesContentButNotProgress()
    {
        var target = TestData.Perro();
        target.RecordAnswer(ScenarioType.Card, true, DateTime.Today);
        var source = TestData.Ciudad();

        target.CopyContentFrom(source);
        source.Topics.Add("changed later");

        Assert.Multiple(() =>
        {
            Assert.That(target.BaseValue, Is.EqualTo("ciudad"));
            Assert.That(target.Translation, Is.EqualTo("city; town"));
            Assert.That(target.Gender, Is.EqualTo(Gender.Feminine));
            Assert.That(target.PluralValue, Is.EqualTo("ciudades"));
            Assert.That(target.Topics, Is.EqualTo(new[] { "city" }));
            Assert.That(target.GetProgress(ScenarioType.Card), Is.Not.Null);
        });
    }

    [Test]
    public void Verb_CopyContentFrom_CopiesFormsAsNewArrays()
    {
        var target = new Verb();
        var source = TestData.Hablar();

        target.CopyContentFrom(source);
        source.PresentConjugations[0] = "changed";

        Assert.Multiple(() =>
        {
            Assert.That(target.PresentConjugations[0], Is.EqualTo("hablo"));
            Assert.That(target.PreteriteConjugations, Is.EqualTo(TestData.Hablar().PreteriteConjugations));
            Assert.That(target.NonPersonalGerund, Is.EqualTo("hablando"));
        });
    }

    [Test]
    public void CopyContentFrom_Null_Throws()
    {
        Assert.That(() => new Noun().CopyContentFrom(null!), Throws.ArgumentNullException);
    }

    [Test]
    public void Verb_IsComplete_ChecksLengthAndEmptyForms()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Verb.IsComplete(TestData.Hablar().PresentConjugations), Is.True);
            Assert.That(Verb.IsComplete(["a", "b"]), Is.False);
            Assert.That(Verb.IsComplete(Verb.EmptyConjugations()), Is.False);
        });
    }
}
