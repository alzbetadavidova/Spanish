using Spanish.Core;

namespace Spanish.Tests;

public class LearnStatsTests
{
    [Test]
    public void Build_OneRowPerWordThenPerNumeral()
    {
        var library = TestData.Library();

        var rows = LearnStats.Build(library);

        Assert.That(rows.Select(r => r.Word),
            Is.EqualTo(new[] { "ciudad", "perro", "hablar" }.Concat(library.Numerals.Select(n => n.BaseValue))));
        Assert.That(() => LearnStats.Build(null!), Throws.ArgumentNullException);
    }

    [Test]
    public void BuildRow_NeverPracticed_HasEmptyStats()
    {
        var row = LearnStats.BuildRow(TestData.Hablar());

        Assert.Multiple(() =>
        {
            Assert.That(row.Kind, Is.EqualTo(WordKind.Verb));
            Assert.That(row.Translation, Is.EqualTo("to speak, talk"));
            Assert.That(row.OverallIndex, Is.Null);
            Assert.That(row.Attempts, Is.Zero);
            Assert.That(row.LastPracticed, Is.Null);
            Assert.That(row.Breakdown.Select(b => b.Type), Is.EqualTo(TestData.Hablar().SupportedScenarios));
            Assert.That(row.Breakdown.All(b => b.Index is null && b.Attempts == 0), Is.True);
        });
    }

    [Test]
    public void BuildRow_Practiced_SummarizesProgress()
    {
        var noun = TestData.Ciudad();
        var day = new DateTime(2026, 9, 1);
        noun.RecordAnswer(ScenarioType.Card, true, day);
        noun.RecordAnswer(ScenarioType.Gender, false, day.AddDays(3));
        noun.RecordAnswer(ScenarioType.Gender, true, day.AddDays(1));

        var row = LearnStats.BuildRow(noun);

        Assert.Multiple(() =>
        {
            Assert.That(row.OverallIndex, Is.EqualTo(0.75));
            Assert.That(row.Attempts, Is.EqualTo(3));
            Assert.That(row.LastPracticed, Is.EqualTo(day.AddDays(3)));
            Assert.That(row.Breakdown.Single(b => b.Type == ScenarioType.Gender), Is.EqualTo(new ScenarioStat(ScenarioType.Gender, 0.5, 2)));
            Assert.That(() => LearnStats.BuildRow(null!), Throws.ArgumentNullException);
        });
    }
}

public class PluralSuggesterTests
{
    [TestCase("casa", "casas")]
    [TestCase("café", "cafés")]
    [TestCase("ciudad", "ciudades")]
    [TestCase("canción", "canciones")]
    [TestCase("luz", "luces")]
    [TestCase(" Luz ", "Luces")]
    [TestCase("rubí", "rubíes")]
    [TestCase("ratón", "ratones")]
    [TestCase("país", "países")]
    [TestCase("baúl", "baúles")]
    [TestCase("inglés", "ingleses")]
    [TestCase("autobús", "autobuses")]
    [TestCase("lunes", "lunes")]
    [TestCase("crisis", "crisis")]
    [TestCase("mes", "meses")]
    [TestCase("CANCIÓN", "CANCIONES")]
    [TestCase("Luz", "Luces")]
    [TestCase("A", "As")]
    [TestCase("s", "ses")]
    [TestCase("", "")]
    [TestCase("  ", "")]
    [TestCase(null, "")]
    public void Suggest_AppliesRegularRules(string? singular, string expected)
    {
        Assert.That(PluralSuggester.Suggest(singular!), Is.EqualTo(expected));
    }
}
