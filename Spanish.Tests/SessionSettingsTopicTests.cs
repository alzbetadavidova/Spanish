using Spanish.Core;

namespace Spanish.Tests;

public class SessionSettingsTopicTests
{
    [Test]
    public void WithTopicsFrom_AllTopicsExist_ReturnsSameInstance()
    {
        var settings = new SessionSettings { Topics = ["city"] };

        Assert.That(settings.WithTopicsFrom(["animals", "city"]), Is.SameAs(settings));
    }

    [Test]
    public void WithTopicsFrom_DropsMissingAndUsesLibrarySpelling()
    {
        var settings = new SessionSettings { Topics = ["CITY", "food", "City"] };

        var cleaned = settings.WithTopicsFrom(["animals", "city"]);

        Assert.That(cleaned.Topics, Is.EqualTo(new[] { "city" }));
        Assert.That(cleaned.Order, Is.EqualTo(settings.Order));
    }

    [Test]
    public void WithTopicsFrom_OnlyCaseDiffers_UsesLibrarySpelling()
    {
        var settings = new SessionSettings { Topics = ["CITY"] };

        var cleaned = settings.WithTopicsFrom(["city"]);

        Assert.That(cleaned, Is.Not.SameAs(settings));
        Assert.That(cleaned.Topics, Is.EqualTo(new[] { "city" }));
    }

    [Test]
    public void WithTopicsFrom_Null_Throws()
    {
        Assert.That(() => new SessionSettings().WithTopicsFrom(null!), Throws.ArgumentNullException);
    }

    [Test]
    public void WithTopicRenamed_SelectedTopic_IsRenamed()
    {
        var settings = new SessionSettings { Topics = ["animals", "City"] };

        Assert.That(settings.WithTopicRenamed("city", "town").Topics, Is.EqualTo(new[] { "animals", "town" }));
    }

    [Test]
    public void WithTopicRenamed_NotSelected_ReturnsSameInstance()
    {
        var settings = new SessionSettings { Topics = ["animals"] };

        Assert.That(settings.WithTopicRenamed("city", "town"), Is.SameAs(settings));
    }

    [Test]
    public void NullLists_BecomeEmpty()
    {
        var settings = new SessionSettings { Topics = null!, NounScenarioTypes = null!, VerbScenarioTypes = null! };

        Assert.Multiple(() =>
        {
            Assert.That(settings.Topics, Is.Empty);
            Assert.That(settings.NounScenarioTypes, Is.Empty);
            Assert.That(settings.VerbScenarioTypes, Is.Empty);
        });
    }
}

public class NullSafeModelTests
{
    [Test]
    public void Setters_ReplaceNullWithEmpty()
    {
        var library = new LearnLibrary { Nouns = null!, Verbs = null!, Topics = null! };
        var noun = new Noun { BaseValue = null!, Translation = null!, Topics = null!, Progress = null!, PluralValue = null! };
        var verb = new Verb { PresentConjugations = null!, PreteriteConjugations = null!, NonPersonalGerund = null! };
        var progress = new LearnProgress { Recent = null! };

        Assert.Multiple(() =>
        {
            Assert.That(library.Units, Is.Empty);
            Assert.That(library.Topics, Is.Empty);
            Assert.That(noun.BaseValue, Is.Empty);
            Assert.That(noun.TranslationAlternatives, Is.Empty);
            Assert.That(noun.HasTopic("x"), Is.False);
            Assert.That(noun.OverallIndex, Is.Null);
            Assert.That(noun.PluralValue, Is.Empty);
            Assert.That(verb.PresentConjugations, Is.EqualTo(Verb.EmptyConjugations()));
            Assert.That(verb.PreteriteConjugations, Is.EqualTo(Verb.EmptyConjugations()));
            Assert.That(verb.NonPersonalGerund, Is.Empty);
            Assert.That(progress.Index, Is.Null);
        });
    }

    [Test]
    public void TranslationDisplay_JoinsAlternatives()
    {
        Assert.That(TestData.Ciudad().TranslationDisplay, Is.EqualTo("city, town"));
    }
}
