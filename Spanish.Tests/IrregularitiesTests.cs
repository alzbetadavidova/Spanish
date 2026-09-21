using Spanish.Core;

namespace Spanish.Tests;

public class IrregularitiesTests
{
    private static Noun Noun(string word, Gender gender, string plural = "", bool takesEl = false) => new()
    {
        BaseValue = word,
        Translation = "x",
        Gender = gender,
        PluralValue = plural,
        TakesElInSingular = takesEl
    };

    private static Verb Ir() => new()
    {
        BaseValue = "ir",
        Translation = "to go",
        PresentConjugations = ["voy", "vas", "va", "vamos", "van"],
        PreteriteConjugations = ["fui", "fuiste", "fue", "fuimos", "fueron"],
        NonPersonalGerund = "yendo"
    };

    private static Adjective Espanol() => new() { BaseValue = "español", Translation = "Spanish", FeminineValue = "española" };

    private static string[] Reasons(IReadOnlyList<Irregularity> irregularities) =>
        irregularities.Select(i => i.Reason).ToArray();

    [Test]
    public void Of_Null_Throws()
    {
        Assert.That(() => Irregularities.Of(null!), Throws.ArgumentNullException);
    }

    [TestCase("")]
    [TestCase("  ")]
    public void Of_EmptyWord_HasNone(string word)
    {
        Assert.That(Irregularities.Of(Noun(word, Gender.Masculine, "as")), Is.Empty);
    }

    [Test]
    public void Of_UnknownUnit_HasNone()
    {
        Assert.That(Irregularities.Of(new UnknownUnit { BaseValue = "x" }), Is.Empty);
    }

    [Test]
    public void Of_RegularWords_HaveNone()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Irregularities.Of(TestData.Ciudad()), Is.Empty);
            Assert.That(Irregularities.Of(TestData.Perro()), Is.Empty);
            Assert.That(Irregularities.Of(TestData.Hablar()), Is.Empty);
            Assert.That(Irregularities.Of(TestData.Bajo()), Is.Empty);
        });
    }

    [Test]
    public void Of_FeminineTakingEl_ExplainsWithPlural()
    {
        var result = Irregularities.Of(Noun("agua", Gender.Feminine, "aguas", takesEl: true));

        Assert.That(result, Is.EqualTo(new[]
        {
            new Irregularity(IrregularityKind.ElBeforeStressedA,
                "Feminine, but takes el in the singular because it starts with a stressed a: el agua, las aguas.")
        }));
    }

    [Test]
    public void Of_FeminineTakingElWithoutPlural_LeavesThePluralOut()
    {
        var result = Irregularities.Of(Noun("hambre", Gender.Feminine, takesEl: true));

        Assert.That(Reasons(result), Is.EqualTo(new[]
        {
            "Feminine, but takes el in the singular because it starts with a stressed a: el hambre."
        }));
    }

    [Test]
    public void Of_MasculineTakingEl_IgnoresTheFlag()
    {
        var result = Irregularities.Of(Noun("día", Gender.Masculine, "días", takesEl: true));

        Assert.That(result.Select(i => i.Kind), Is.EqualTo(new[] { IrregularityKind.MasculineEndingInA }));
    }

    [TestCase("día", "Masculine although it ends in -a: el día.")]
    [TestCase("MAPA", "Masculine although it ends in -a: el MAPA.")]
    public void Of_MasculineEndingInA(string word, string reason)
    {
        Assert.That(Reasons(Irregularities.Of(Noun(word, Gender.Masculine))), Is.EqualTo(new[] { reason }));
    }

    [Test]
    public void Of_FeminineEndingInO()
    {
        Assert.That(Reasons(Irregularities.Of(Noun("mano", Gender.Feminine, "manos"))),
            Is.EqualTo(new[] { "Feminine although it ends in -o: la mano." }));
    }

    [Test]
    public void Of_IrregularPlural()
    {
        Assert.That(Reasons(Irregularities.Of(Noun("examen", Gender.Masculine, "exámenes"))),
            Is.EqualTo(new[] { "Irregular plural: exámenes (the regular rule gives examenes)." }));
    }

    [Test]
    public void Of_Compound_UsesItsHead()
    {
        // The ending and the plural rule apply to "fin", not to "semana".
        Assert.That(Irregularities.Of(Noun("fin de semana", Gender.Masculine, "fines de semana")), Is.Empty);
    }

    [Test]
    public void Of_IrregularAdjectiveForms_ListsTheFormsThatDiffer()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Reasons(Irregularities.Of(Espanol())),
                Is.EqualTo(new[] { "Irregular forms: española (feminine), españolas (feminine plural)." }));
            Assert.That(Irregularities.Of(Espanol()).Single().Kind, Is.EqualTo(IrregularityKind.IrregularAdjectiveForms));
        });
    }

    [Test]
    public void Of_IrregularVerb_ListsEachTense()
    {
        Assert.That(Irregularities.Of(Ir()), Is.EqualTo(new[]
        {
            new Irregularity(IrregularityKind.IrregularPresent,
                "Irregular present: yo voy · tú vas · él va · nosotros vamos · ellos van."),
            new Irregularity(IrregularityKind.IrregularPreterite,
                "Irregular preterite: yo fui · tú fuiste · él fue · nosotros fuimos · ellos fueron."),
            new Irregularity(IrregularityKind.IrregularGerund, "Irregular gerund: yendo.")
        }));
    }

    [Test]
    public void Of_Verb_ListsOnlyThePersonsThatDiffer()
    {
        var hacer = new Verb
        {
            BaseValue = "hacer",
            Translation = "to do",
            PresentConjugations = ["hago", "haces", "hace", "hacemos", "hacen"]
        };

        Assert.That(Reasons(Irregularities.Of(hacer)), Is.EqualTo(new[] { "Irregular present: yo hago." }));
    }

    [Test]
    public void Of_VerbWithEmptyForms_SkipsThem()
    {
        var verb = new Verb { BaseValue = "tener", Translation = "to have" };

        Assert.That(Irregularities.Of(verb), Is.Empty);
    }

    [Test]
    public void Of_VerbThatIsNotAnInfinitive_HasNone()
    {
        var verb = Ir();
        verb.BaseValue = "vete";

        Assert.That(Irregularities.Of(verb), Is.Empty);
    }

    [TestCase(ScenarioType.Card, 3)]
    [TestCase(ScenarioType.Fill, 3)]
    [TestCase(ScenarioType.Present, 1)]
    [TestCase(ScenarioType.Preterite, 1)]
    [TestCase(ScenarioType.Gerund, 1)]
    [TestCase(ScenarioType.Gender, 0)]
    [TestCase(ScenarioType.Plural, 0)]
    [TestCase(ScenarioType.PairWithNoun, 0)]
    [TestCase((ScenarioType)99, 0)]
    public void For_Verb_KeepsWhatTheScenarioPractices(ScenarioType type, int count)
    {
        var result = Irregularities.For(Ir(), type);

        Assert.That(result, Has.Count.EqualTo(count));
        if (count == 1)
        {
            Assert.That(result[0].Kind.ToString(), Does.EndWith(type.ToString()));
        }
    }

    [TestCase(ScenarioType.Gender, new[] { IrregularityKind.FeminineEndingInO })]
    [TestCase(ScenarioType.Plural, new[] { IrregularityKind.IrregularPlural })]
    [TestCase(ScenarioType.Card, new[] { IrregularityKind.FeminineEndingInO, IrregularityKind.IrregularPlural })]
    public void For_Noun_KeepsWhatTheScenarioPractices(ScenarioType type, IrregularityKind[] expected)
    {
        var result = Irregularities.For(Noun("radio", Gender.Feminine, "radioes"), type);

        Assert.That(result.Select(i => i.Kind), Is.EqualTo(expected));
    }

    [Test]
    public void For_Adjective_Pair_KeepsItsForms()
    {
        Assert.That(Irregularities.For(Espanol(), ScenarioType.PairWithNoun).Select(i => i.Kind),
            Is.EqualTo(new[] { IrregularityKind.IrregularAdjectiveForms }));
    }

    [Test]
    public void OfPairedNoun_KeepsOnlyGenderAndArticle()
    {
        var result = Irregularities.OfPairedNoun(Noun("radio", Gender.Feminine, "radioes"));

        Assert.That(result.Select(i => i.Kind), Is.EqualTo(new[] { IrregularityKind.FeminineEndingInO }));
    }
}
