using Spanish.Core;

namespace Spanish.Tests;

public class SpanishNumeralsTests
{
    [TestCase(0, "cero")]
    [TestCase(1, "uno")]
    [TestCase(15, "quince")]
    [TestCase(16, "dieciséis")]
    [TestCase(20, "veinte")]
    [TestCase(21, "veintiuno")]
    [TestCase(22, "veintidós")]
    [TestCase(30, "treinta")]
    [TestCase(31, "treinta y uno")]
    [TestCase(99, "noventa y nueve")]
    [TestCase(100, "cien")]
    [TestCase(101, "ciento uno")]
    [TestCase(121, "ciento veintiuno")]
    [TestCase(200, "doscientos")]
    [TestCase(555, "quinientos cincuenta y cinco")]
    [TestCase(700, "setecientos")]
    [TestCase(900, "novecientos")]
    [TestCase(1_000, "mil")]
    [TestCase(1_001, "mil uno")]
    [TestCase(1_100, "mil cien")]
    [TestCase(2_000, "dos mil")]
    [TestCase(11_000, "once mil")]
    [TestCase(21_000, "veintiún mil")]
    [TestCase(31_000, "treinta y un mil")]
    [TestCase(41_021, "cuarenta y un mil veintiuno")]
    [TestCase(100_000, "cien mil")]
    [TestCase(101_000, "ciento un mil")]
    [TestCase(121_000, "ciento veintiún mil")]
    [TestCase(200_001, "doscientos mil uno")]
    [TestCase(999_999, "novecientos noventa y nueve mil novecientos noventa y nueve")]
    [TestCase(1_000_000, "un millón")]
    [TestCase(1_000_001, "un millón uno")]
    [TestCase(1_001_000, "un millón mil")]
    [TestCase(2_000_000, "dos millones")]
    [TestCase(2_500_000, "dos millones quinientos mil")]
    [TestCase(21_000_000, "veintiún millones")]
    [TestCase(101_000_000, "ciento un millones")]
    [TestCase(SpanishNumerals.MaxNumber,
        "novecientos noventa y nueve millones novecientos noventa y nueve mil novecientos noventa y nueve")]
    public void ToWords_WritesNumber(int number, string expected)
    {
        Assert.That(SpanishNumerals.ToWords(number), Is.EqualTo(expected));
    }

    [TestCase(-1)]
    [TestCase(SpanishNumerals.MaxNumber + 1)]
    public void ToWords_OutOfRange_Throws(int number)
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => SpanishNumerals.ToWords(number), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => SpanishNumerals.Number(number), Throws.InstanceOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public void Number_BelowThousand_HasOneDigitForm()
    {
        var forms = SpanishNumerals.Number(999);

        Assert.That(forms.Digits, Is.EqualTo(new[] { "999" }));
        Assert.That(forms.Words, Is.EqualTo(new[] { "novecientos noventa y nueve" }));
    }

    [TestCase(1_000, new[] { "1.000", "1000", "1 000", "1,000" })]
    [TestCase(1_234_567, new[] { "1.234.567", "1234567", "1 234 567", "1,234,567" })]
    public void Number_FromThousand_AcceptsGroupSeparators(int number, string[] expected)
    {
        Assert.That(SpanishNumerals.Number(number).Digits, Is.EqualTo(expected));
    }

    [Test]
    public void Date_TwoDigitDay_PadsOnlyTheMonth()
    {
        var forms = SpanishNumerals.Date(new DateOnly(2025, 3, 21));

        Assert.Multiple(() =>
        {
            Assert.That(forms.Digits, Is.EqualTo(new[]
            {
                "21/3/2025", "21/03/2025", "21.3.2025", "21.03.2025", "21-3-2025", "21-03-2025"
            }));
            Assert.That(forms.Words, Is.EqualTo(new[]
            {
                "veintiuno de marzo de dos mil veinticinco", "el veintiuno de marzo de dos mil veinticinco"
            }));
        });
    }

    [Test]
    public void Date_FirstOfMonth_AcceptsUnoAndPrimero()
    {
        var forms = SpanishNumerals.Date(new DateOnly(1998, 5, 1));

        Assert.Multiple(() =>
        {
            Assert.That(forms.Digits, Has.Count.EqualTo(12));
            Assert.That(forms.Digits[0], Is.EqualTo("1/5/1998"));
            Assert.That(forms.Digits, Does.Contain("01/05/1998").And.Contain("01.5.1998").And.Contain("1-05-1998"));
            Assert.That(forms.Words, Is.EqualTo(new[]
            {
                "uno de mayo de mil novecientos noventa y ocho",
                "primero de mayo de mil novecientos noventa y ocho",
                "el uno de mayo de mil novecientos noventa y ocho",
                "el primero de mayo de mil novecientos noventa y ocho"
            }));
        });
    }

    [Test]
    public void Date_TwoDigitDayAndMonth_HasNoPaddedForms()
    {
        var forms = SpanishNumerals.Date(new DateOnly(2000, 12, 10));

        Assert.That(forms.Digits, Is.EqualTo(new[] { "10/12/2000", "10.12.2000", "10-12-2000" }));
        Assert.That(forms.Words[0], Is.EqualTo("diez de diciembre de dos mil"));
    }

    [Test]
    public void Time_HalfPast_AcceptsEverydayAnd24HourForms()
    {
        var forms = SpanishNumerals.Time(new TimeOnly(14, 30));

        Assert.Multiple(() =>
        {
            Assert.That(forms.Digits, Is.EqualTo(new[] { "14:30", "14.30" }));
            Assert.That(forms.Words, Is.EqualTo(new[]
            {
                "las dos y media de la tarde", "las dos y treinta de la tarde",
                "las dos y media", "las dos y treinta",
                "las catorce y treinta", "las catorce treinta"
            }));
        });
    }

    [Test]
    public void Time_SingleDigitHour_AcceptsLeadingZeroAndDropsDuplicates()
    {
        var forms = SpanishNumerals.Time(new TimeOnly(9, 5));

        Assert.Multiple(() =>
        {
            Assert.That(forms.Digits, Is.EqualTo(new[] { "9:05", "09:05", "9.05", "09.05" }));
            Assert.That(forms.Words, Is.EqualTo(new[]
            {
                "las nueve y cinco de la mañana", "las nueve y cinco", "las nueve cinco"
            }));
        });
    }

    [Test]
    public void Time_OnTheHour_AcceptsEnPuntoAndHoras()
    {
        Assert.That(SpanishNumerals.Time(new TimeOnly(13, 0)).Words, Is.EqualTo(new[]
        {
            "la una de la tarde", "la una en punto de la tarde", "la una", "la una en punto", "las trece horas", "las trece"
        }));
    }

    [Test]
    public void Time_Midnight_IsDoceAndCero()
    {
        var forms = SpanishNumerals.Time(new TimeOnly(0, 0));

        Assert.That(forms.Digits, Is.EqualTo(new[] { "0:00", "00:00", "0.00", "00.00" }));
        Assert.That(forms.Words, Is.EqualTo(new[]
        {
            "las doce de la noche", "las doce en punto de la noche", "las doce", "las doce en punto", "las cero horas", "las cero"
        }));
    }

    [Test]
    public void Time_QuarterPast_AcceptsCuartoAndQuince()
    {
        Assert.That(SpanishNumerals.Time(new TimeOnly(1, 15)).Words, Is.EqualTo(new[]
        {
            "la una y cuarto de la madrugada", "la una y quince de la madrugada",
            "la una y cuarto", "la una y quince", "la una quince"
        }));
    }

    [Test]
    public void Time_QuarterTo_NamesTheNextHour()
    {
        Assert.That(SpanishNumerals.Time(new TimeOnly(7, 45)).Words, Is.EqualTo(new[]
        {
            "las ocho menos cuarto de la mañana", "las siete y cuarenta y cinco de la mañana",
            "las ocho menos cuarto", "las siete y cuarenta y cinco", "las siete cuarenta y cinco"
        }));
    }

    [Test]
    public void Time_AfterHalfPast_AcceptsMenosAndY()
    {
        Assert.That(SpanishNumerals.Time(new TimeOnly(23, 40)).Words, Is.EqualTo(new[]
        {
            "las doce menos veinte de la noche", "las once y cuarenta de la noche",
            "las doce menos veinte", "las once y cuarenta",
            "las veintitrés y cuarenta", "las veintitrés cuarenta"
        }));
    }

    [TestCase(0, 0, "las doce de la noche")]
    [TestCase(1, 15, "la una y cuarto de la madrugada")]
    [TestCase(5, 20, "las cinco y veinte de la madrugada")]
    [TestCase(6, 0, "las seis de la mañana")]
    [TestCase(11, 50, "las doce menos diez del mediodía")]
    [TestCase(12, 0, "las doce del mediodía")]
    [TestCase(12, 45, "la una menos cuarto de la tarde")]
    [TestCase(13, 0, "la una de la tarde")]
    [TestCase(20, 10, "las ocho y diez de la tarde")]
    [TestCase(21, 0, "las nueve de la noche")]
    [TestCase(23, 40, "las doce menos veinte de la noche")]
    public void Time_NamesThePartOfTheDay(int hour, int minute, string expected)
    {
        Assert.That(SpanishNumerals.Time(new TimeOnly(hour, minute)).Words[0], Is.EqualTo(expected));
    }

    [Test]
    public void DateAndTime_CombinesEveryForm()
    {
        var forms = SpanishNumerals.DateAndTime(new DateOnly(2025, 3, 21), new TimeOnly(14, 30));

        Assert.Multiple(() =>
        {
            Assert.That(forms.Digits, Has.Count.EqualTo(6 * 2 * 2));
            Assert.That(forms.Digits[0], Is.EqualTo("21/3/2025 14:30"));
            Assert.That(forms.Digits, Does.Contain("21.03.2025, 14.30"));
            Assert.That(forms.Words, Has.Count.EqualTo(2 * 6));
            Assert.That(forms.Words[0], Is.EqualTo("veintiuno de marzo de dos mil veinticinco a las dos y media de la tarde"));
            Assert.That(forms.Words, Does.Contain("el veintiuno de marzo de dos mil veinticinco a las catorce treinta"));
        });
    }

    [Test]
    public void DateAndTime_One_UsesALaUna()
    {
        var forms = SpanishNumerals.DateAndTime(new DateOnly(2000, 1, 1), new TimeOnly(13, 0));

        Assert.That(forms.Words[0], Is.EqualTo("uno de enero de dos mil a la una de la tarde"));
    }

    [Test]
    public void Forms_WorkWithAnswerChecker()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AnswerChecker.Check("25 000", SpanishNumerals.Number(25_000).Digits).Outcome, Is.EqualTo(AnswerOutcome.Correct));
            Assert.That(AnswerChecker.Check("veintidos", SpanishNumerals.Number(22).Words).Outcome,
                Is.EqualTo(AnswerOutcome.CorrectWithAccentHint));
            Assert.That(AnswerChecker.Check("Las  dos y media", SpanishNumerals.Time(new TimeOnly(14, 30)).Words).Outcome,
                Is.EqualTo(AnswerOutcome.Correct));
            Assert.That(AnswerChecker.Check("2:30", SpanishNumerals.Time(new TimeOnly(14, 30)).Digits).Outcome,
                Is.EqualTo(AnswerOutcome.Incorrect));
        });
    }
}
