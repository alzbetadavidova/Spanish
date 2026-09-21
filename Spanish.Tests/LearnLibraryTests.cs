using Spanish.Core;

namespace Spanish.Tests;

public class LearnLibraryTests
{
    private LearnLibrary _library = null!;

    [SetUp]
    public void Setup()
    {
        _library = TestData.Library();
    }

    [Test]
    public void Units_ContainsNounsThenVerbs()
    {
        Assert.That(_library.Units.Select(u => u.BaseValue), Is.EqualTo(new[] { "ciudad", "perro", "hablar" }));
    }

    [Test]
    public void Validate_ValidNewNoun_NoErrors()
    {
        var noun = new Noun { BaseValue = "mesa", Translation = "table", Topics = ["CITY"] };

        Assert.That(_library.Validate(noun), Is.Empty);
    }

    [Test]
    public void Validate_Null_Throws()
    {
        Assert.That(() => _library.Validate(null!), Throws.ArgumentNullException);
    }

    [Test]
    public void Validate_EmptyWordAndTranslation_ReportsBoth()
    {
        var errors = _library.Validate(new Noun { BaseValue = "  ", Translation = " ; " });

        Assert.That(errors.Select(e => e.Field),
            Is.EquivalentTo(new[] { nameof(LearnUnit.BaseValue), nameof(LearnUnit.Translation) }));
    }

    [Test]
    public void Validate_DuplicateOfSameKind_ReportsDuplicate()
    {
        var errors = _library.Validate(new Noun { BaseValue = " Ciudad ", Translation = "city" });

        Assert.That(errors.Single().Message, Is.EqualTo("\"Ciudad\" is already in your library."));
    }

    [Test]
    public void Validate_SameWordOfOtherKind_IsAllowed()
    {
        var verb = new Verb { BaseValue = "ciudad", Translation = "x" };

        Assert.That(_library.Validate(verb), Is.Empty);
    }

    [Test]
    public void Validate_EditKeepingOwnWord_IsNotDuplicate()
    {
        var existing = _library.Nouns[0];

        Assert.That(_library.Validate(TestData.Ciudad(), existing), Is.Empty);
    }

    [Test]
    public void Validate_UnknownTopic_ReportsTopic()
    {
        var noun = new Noun { BaseValue = "mesa", Translation = "table", Topics = ["home", "city"] };

        var error = _library.Validate(noun).Single();

        Assert.That(error, Is.EqualTo(new ValidationError(nameof(LearnUnit.Topics), "Unknown topic: home.")));
    }

    [Test]
    public void Validate_VerbWithPartialConjugations_ReportsEachTense()
    {
        var verb = new Verb
        {
            BaseValue = "comer",
            Translation = "to eat",
            PresentConjugations = ["como", "", "", "", ""],
            PreteriteConjugations = ["comí"]
        };

        var errors = _library.Validate(verb);

        Assert.That(errors.Select(e => e.Field), Is.EquivalentTo(new[]
        {
            nameof(Verb.PresentConjugations), nameof(Verb.PreteriteConjugations)
        }));
    }

    [Test]
    public void Validate_VerbWithoutConjugations_IsValid()
    {
        Assert.That(_library.Validate(new Verb { BaseValue = "comer", Translation = "to eat" }), Is.Empty);
    }

    [Test]
    public void Save_NewNoun_AddsTrimmed()
    {
        var noun = new Noun { BaseValue = " mesa ", Translation = "table" };

        _library.Save(noun);

        Assert.That(_library.Nouns, Does.Contain(noun));
        Assert.That(noun.BaseValue, Is.EqualTo("mesa"));
    }

    [Test]
    public void Save_NewVerb_AddsToVerbs()
    {
        var verb = new Verb { BaseValue = "comer", Translation = "to eat" };

        _library.Save(verb);

        Assert.That(_library.Verbs, Does.Contain(verb));
    }

    [Test]
    public void Save_Edit_CopiesIntoExistingAndKeepsProgress()
    {
        var existing = _library.Nouns[0];
        existing.RecordAnswer(ScenarioType.Card, true, DateTime.Today);
        var edited = new Noun { BaseValue = " urbe ", Translation = "metropolis", Gender = Gender.Feminine };

        _library.Save(edited, existing);

        Assert.Multiple(() =>
        {
            Assert.That(_library.Nouns, Has.Count.EqualTo(2));
            Assert.That(existing.BaseValue, Is.EqualTo("urbe"));
            Assert.That(existing.Translation, Is.EqualTo("metropolis"));
            Assert.That(existing.GetProgress(ScenarioType.Card), Is.Not.Null);
        });
    }

    [Test]
    public void Save_Invalid_ThrowsWithErrors()
    {
        var exception = Assert.Throws<LibraryValidationException>(() => _library.Save(new Noun()));

        Assert.That(exception!.Errors, Has.Count.EqualTo(2));
        Assert.That(exception.Message, Is.EqualTo("Enter the Spanish word. Enter the English translation."));
    }

    [Test]
    public void Save_EditWithOtherKind_Throws()
    {
        var verb = new Verb { BaseValue = "nuevo", Translation = "new" };

        Assert.That(() => _library.Save(verb, _library.Nouns[0]), Throws.ArgumentException);
    }

    [Test]
    public void Save_UnsupportedUnit_Throws()
    {
        var unit = new UnknownUnit { BaseValue = "x", Translation = "x" };

        Assert.That(() => _library.Save(unit), Throws.ArgumentException);
    }

    [Test]
    public void Remove_RemovesNounOrVerb()
    {
        var noun = _library.Nouns[0];
        var verb = _library.Verbs[0];

        Assert.Multiple(() =>
        {
            Assert.That(_library.Remove(noun), Is.True);
            Assert.That(_library.Remove(verb), Is.True);
            Assert.That(_library.Remove(new UnknownUnit()), Is.False);
            Assert.That(_library.Units.Count(), Is.EqualTo(1));
        });
    }

    [Test]
    public void AddTopic_AddsTrimmed()
    {
        _library.AddTopic("  food ");

        Assert.That(_library.Topics, Does.Contain("food"));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void AddTopic_Empty_Throws(string? name)
    {
        var exception = Assert.Throws<LibraryValidationException>(() => _library.AddTopic(name!));

        Assert.That(exception!.Errors[0].Message, Is.EqualTo("Enter a topic name."));
    }

    [Test]
    public void AddTopic_Duplicate_Throws()
    {
        var exception = Assert.Throws<LibraryValidationException>(() => _library.AddTopic("CITY"));

        Assert.That(exception!.Errors[0].Message, Is.EqualTo("Topic \"CITY\" already exists."));
    }

    [Test]
    public void RenameTopic_RenamesTopicAndWords()
    {
        _library.RenameTopic("City", "town");

        Assert.Multiple(() =>
        {
            Assert.That(_library.Topics, Is.EqualTo(new[] { "town", "animals" }));
            Assert.That(_library.Nouns[0].Topics, Is.EqualTo(new[] { "town" }));
            Assert.That(_library.Verbs[0].Topics, Is.EqualTo(new[] { "town" }));
            Assert.That(_library.Nouns[1].Topics, Is.EqualTo(new[] { "animals" }));
        });
    }

    [Test]
    public void RenameTopic_ChangingOnlyCase_IsAllowed()
    {
        _library.RenameTopic("city", "City");

        Assert.That(_library.Topics[0], Is.EqualTo("City"));
    }

    [Test]
    public void RenameTopic_ToOtherExistingTopic_Throws()
    {
        Assert.That(() => _library.RenameTopic("city", "animals"), Throws.TypeOf<LibraryValidationException>());
    }

    [Test]
    public void RenameTopic_Missing_Throws()
    {
        Assert.That(() => _library.RenameTopic("food", "x"), Throws.ArgumentException);
    }

    [Test]
    public void RemoveTopic_RemovesFromLibraryAndWords()
    {
        _library.RemoveTopic("CITY");

        Assert.Multiple(() =>
        {
            Assert.That(_library.Topics, Is.EqualTo(new[] { "animals" }));
            Assert.That(_library.Nouns[0].Topics, Is.Empty);
            Assert.That(_library.Verbs[0].Topics, Is.Empty);
        });
    }

    [Test]
    public void SetTopicMembership_AddsOnceAndRemoves()
    {
        var perro = _library.Nouns[1];

        _library.SetTopicMembership("CITY", perro, true);
        _library.SetTopicMembership("city", perro, true);
        Assert.That(perro.Topics, Is.EqualTo(new[] { "animals", "city" }));

        _library.SetTopicMembership("city", perro, false);
        Assert.That(perro.Topics, Is.EqualTo(new[] { "animals" }));
    }

    [Test]
    public void SetTopicMembership_NullUnit_Throws()
    {
        Assert.That(() => _library.SetTopicMembership("city", null!, true), Throws.ArgumentNullException);
    }
}
