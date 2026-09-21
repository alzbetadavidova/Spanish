using Spanish.Core;

namespace Spanish.Tests;

public class NumeralTests
{
    private static Numeral Get(NumeralCategory category) => Numeral.CreateBuiltIn().Single(n => n.Category == category);

    [Test]
    public void CreateBuiltIn_OneFreshNumeralPerCategory()
    {
        var numerals = Numeral.CreateBuiltIn();

        Assert.Multiple(() =>
        {
            Assert.That(numerals.Select(n => n.Category), Is.EqualTo(Enum.GetValues<NumeralCategory>()));
            Assert.That(numerals.Select(n => n.BaseValue), Is.EqualTo(new[]
            {
                "números 0–20", "números 21–100", "números 101–999", "miles", "millones", "fechas", "horas", "fechas y horas"
            }));
            Assert.That(numerals[3].TranslationDisplay, Is.EqualTo("thousands"));
            Assert.That(numerals.All(n => n.Kind == WordKind.Numeral), Is.True);
            Assert.That(numerals.All(n => n.SupportedScenarios.SequenceEqual(Numeral.ScenarioTypes)), Is.True);
            Assert.That(numerals.All(n => n.Progress.Count == 0 && n.Topics.Count == 0), Is.True);
            Assert.That(Numeral.CreateBuiltIn()[0], Is.Not.SameAs(numerals[0]));
        });
    }

    [TestCase(NumeralCategory.Numbers0To20, NumeralSubtype.Number)]
    [TestCase(NumeralCategory.Millions, NumeralSubtype.Number)]
    [TestCase(NumeralCategory.Dates, NumeralSubtype.Date)]
    [TestCase(NumeralCategory.Times, NumeralSubtype.Time)]
    [TestCase(NumeralCategory.DatesWithTimes, NumeralSubtype.DateAndTime)]
    public void Subtype_FollowsCategory(NumeralCategory category, NumeralSubtype expected)
    {
        Assert.That(Get(category).Subtype, Is.EqualTo(expected));
    }

    [TestCase(NumeralCategory.Numbers0To20, 0, 20)]
    [TestCase(NumeralCategory.Numbers21To100, 21, 100)]
    [TestCase(NumeralCategory.Numbers101To999, 101, 999)]
    [TestCase(NumeralCategory.Thousands, 1_000, 999_999)]
    [TestCase(NumeralCategory.Millions, 1_000_000, SpanishNumerals.MaxNumber)]
    public void Draw_Number_StaysWithinTheRange(NumeralCategory category, int min, int max)
    {
        var numeral = Get(category);
        var random = new FakeRandom(0, int.MaxValue);

        var lowest = numeral.Draw(random);
        var highest = numeral.Draw(random);

        Assert.Multiple(() =>
        {
            Assert.That(lowest, Is.EqualTo(SpanishNumerals.Number(min)).Using<NumeralForms>(SameForms));
            Assert.That(highest, Is.EqualTo(SpanishNumerals.Number(max)).Using<NumeralForms>(SameForms));
            Assert.That(random.Requests, Is.EqualTo(new[] { max - min + 1, max - min + 1 }));
        });
    }

    [Test]
    public void Draw_Date_StaysWithinYearsAndMonthLength()
    {
        var numeral = Get(NumeralCategory.Dates);

        Assert.Multiple(() =>
        {
            Assert.That(numeral.Draw(new FakeRandom()).Digits[0], Is.EqualTo("1/1/1900"));
            Assert.That(numeral.Draw(new FakeRandom(int.MaxValue, int.MaxValue, int.MaxValue)).Digits[0], Is.EqualTo("31/12/2099"));
            Assert.That(numeral.Draw(new FakeRandom(100, 1, int.MaxValue)).Digits[0], Is.EqualTo("29/2/2000"));
            Assert.That(numeral.Draw(new FakeRandom(101, 1, int.MaxValue)).Digits[0], Is.EqualTo("28/2/2001"));
        });
    }

    [Test]
    public void Draw_Time_UsesFiveMinuteSteps()
    {
        var numeral = Get(NumeralCategory.Times);
        var random = new FakeRandom(int.MaxValue, int.MaxValue);

        Assert.That(numeral.Draw(new FakeRandom()).Digits[0], Is.EqualTo("0:00"));
        Assert.That(numeral.Draw(random).Digits[0], Is.EqualTo("23:55"));
        Assert.That(random.Requests, Is.EqualTo(new[] { 24, 12 }));
    }

    [Test]
    public void Draw_DateWithTime_DrawsDateThenTime()
    {
        var random = new FakeRandom(125, 2, 20, 14, 6);

        var forms = Get(NumeralCategory.DatesWithTimes).Draw(random);

        Assert.That(forms.Digits[0], Is.EqualTo("21/3/2025 14:30"));
        Assert.That(random.Requests, Is.EqualTo(new[] { 200, 12, 31, 24, 12 }));
    }

    [Test]
    public void Draw_NullRandom_Throws()
    {
        Assert.That(() => Get(NumeralCategory.Times).Draw(null!), Throws.ArgumentNullException);
    }

    [TestCase(NumeralCategory.Numbers0To20, "7", "siete")]
    [TestCase(NumeralCategory.Numbers21To100, "21", "veintiuno")]
    [TestCase(NumeralCategory.Numbers101To999, "115", "ciento quince")]
    [TestCase(NumeralCategory.Thousands, "21.000", "veintiún mil")]
    [TestCase(NumeralCategory.Millions, "1.000.000", "un millón")]
    [TestCase(NumeralCategory.Dates, "1/5/1998", "uno de mayo de mil novecientos noventa y ocho")]
    [TestCase(NumeralCategory.Times, "14:30", "las dos y media de la tarde")]
    [TestCase(NumeralCategory.DatesWithTimes, "21/3/2025 14:30",
        "veintiuno de marzo de dos mil veinticinco a las dos y media de la tarde")]
    public void Examples_ShowTheCategory(NumeralCategory category, string digits, string words)
    {
        var example = Get(category).Examples[0];

        Assert.That(example.Digits[0], Is.EqualTo(digits));
        Assert.That(example.Words[0], Is.EqualTo(words));
    }

    [Test]
    public void CopyContentFrom_Throws()
    {
        var numeral = Get(NumeralCategory.Dates);

        Assert.That(() => numeral.CopyContentFrom(Get(NumeralCategory.Times)), Throws.InstanceOf<NotSupportedException>());
        Assert.That(numeral.BaseValue, Is.EqualTo("fechas"));
    }

    private static bool SameForms(NumeralForms a, NumeralForms b) =>
        a.Digits.SequenceEqual(b.Digits) && a.Words.SequenceEqual(b.Words);
}
