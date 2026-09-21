using System.Text.Json;
using Spanish.Core;

namespace Spanish.Tests;

public class NumeralLibraryTests
{
    private static Numeral Get(LearnLibrary library, NumeralCategory category) =>
        library.Numerals.Single(n => n.Category == category);

    [Test]
    public void Units_AreWordsThenNumerals()
    {
        var library = TestData.Library();

        Assert.Multiple(() =>
        {
            Assert.That(library.Numerals.Select(n => n.Category), Is.EqualTo(Enum.GetValues<NumeralCategory>()));
            Assert.That(library.Units, Is.EqualTo(library.Words.Concat(library.Numerals)));
            Assert.That(library.Words.OfType<Numeral>(), Is.Empty);
        });
    }

    [Test]
    public void Numerals_AreNotSharedBetweenLibraries()
    {
        var first = new LearnLibrary();
        var second = new LearnLibrary();

        Assert.That(first.Numerals[0], Is.Not.SameAs(second.Numerals[0]));
    }

    [Test]
    public void ValidateAndSave_Numeral_AreRefused()
    {
        var library = TestData.Library();
        var dates = Get(library, NumeralCategory.Dates);

        Assert.Multiple(() =>
        {
            Assert.That(library.Validate(dates, dates),
                Is.EqualTo(new[] { new ValidationError(nameof(LearnUnit.BaseValue), "Numerals are built in and can't be edited.") }));
            Assert.That(() => library.Save(dates), Throws.InstanceOf<LibraryValidationException>());
            Assert.That(library.Numerals, Has.Count.EqualTo(Enum.GetValues<NumeralCategory>().Length));
        });
    }

    [Test]
    public void Remove_Numeral_KeepsIt()
    {
        var library = TestData.Library();
        var dates = Get(library, NumeralCategory.Dates);

        Assert.That(library.Remove(dates), Is.False);
        Assert.That(library.Units, Does.Contain(dates));
    }

    [Test]
    public void SetTopicMembership_Numeral_Throws()
    {
        var library = TestData.Library();
        var dates = Get(library, NumeralCategory.Dates);

        Assert.That(() => library.SetTopicMembership("city", dates, true), Throws.ArgumentException);
        Assert.That(dates.Topics, Is.Empty);
    }

    [Test]
    public void NumeralProgress_OnlyPracticedNumerals()
    {
        var library = new LearnLibrary();
        Assert.That(library.NumeralProgress, Is.Empty);

        Get(library, NumeralCategory.Times).RecordAnswer(ScenarioType.TextToNumber, true, new FakeClock().Now);

        Assert.That(library.NumeralProgress.Keys, Is.EqualTo(new[] { NumeralCategory.Times }));
        Assert.That(library.NumeralProgress[NumeralCategory.Times], Is.SameAs(Get(library, NumeralCategory.Times).Progress));
    }

    [Test]
    public void NumeralProgress_Set_ReplacesEveryNumeralsProgress()
    {
        var library = new LearnLibrary();
        Get(library, NumeralCategory.Dates).RecordAnswer(ScenarioType.NumberToText, true, new FakeClock().Now);
        var progress = new Dictionary<ScenarioType, LearnProgress> { [ScenarioType.TextToNumber] = new() };

        library.NumeralProgress = new() { [NumeralCategory.Millions] = progress };

        Assert.Multiple(() =>
        {
            Assert.That(Get(library, NumeralCategory.Millions).Progress, Is.SameAs(progress));
            Assert.That(Get(library, NumeralCategory.Dates).Progress, Is.Empty);
        });
    }

    [Test]
    public void Serialize_RoundTripsNumeralProgress()
    {
        var library = TestData.Library();
        Get(library, NumeralCategory.Dates).RecordAnswer(ScenarioType.TextToNumber, false, new FakeClock().Now);

        var json = JsonSerializer.Serialize(library, JsonFileStore<LearnLibrary>.Options);
        var loaded = JsonSerializer.Deserialize<LearnLibrary>(json, JsonFileStore<LearnLibrary>.Options)!;

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"NumeralProgress\"").And.Contain("\"Dates\"").And.Not.Contain("\"Numerals\""));
            Assert.That(Get(loaded, NumeralCategory.Dates).GetProgress(ScenarioType.TextToNumber)!.Attempts, Is.EqualTo(1));
            Assert.That(loaded.Numerals.Where(n => n.Category != NumeralCategory.Dates).All(n => n.Progress.Count == 0), Is.True);
        });
    }

    [TestCase("""{ "NumeralProgress": null }""")]
    [TestCase("""{ "NumeralProgress": { "Times": null } }""")]
    [TestCase("""{ "NumeralProgress": { "Times": { "NumberToText": null } } }""")]
    [TestCase("""{ }""")]
    public void Deserialize_MissingOrNullNumeralProgress_MeansNotPracticed(string json)
    {
        var library = JsonSerializer.Deserialize<LearnLibrary>(json, JsonFileStore<LearnLibrary>.Options)!;

        Assert.That(library.Numerals.All(n => n.Progress.Count == 0), Is.True);
    }

    [Test]
    public void Deserialize_NullAnswerInNumeralProgress_IsRemoved()
    {
        const string json = """{ "NumeralProgress": { "Times": { "NumberToText": { "Recent": [null], "Attempts": 1 } } } }""";

        var library = JsonSerializer.Deserialize<LearnLibrary>(json, JsonFileStore<LearnLibrary>.Options)!;

        Assert.That(Get(library, NumeralCategory.Times).GetProgress(ScenarioType.NumberToText)!.Recent, Is.Empty);
    }
}
