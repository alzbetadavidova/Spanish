using Spanish.Core;

namespace Spanish.Tests;

public class EndingsScenarioFactoryTests
{
    private static EndingsScenario Create(Verb verb, ScenarioType type) =>
        (EndingsScenario)new ScenarioFactory(new FakeRandom(), new LearnLibrary()).Create(verb, type, Direction.Mixed);

    private static Verb Tener() => new()
    {
        BaseValue = "tener",
        Translation = "to have",
        PresentConjugations = ["tengo", "tienes", "tiene", "tenemos", "tienen"],
        PreteriteConjugations = ["tuve", "tuviste", "tuvo", "tuvimos", "tuvieron"]
    };

    [Test]
    public void Create_PresentEndings_RegularVerb_KeepsTheRootInEveryRow()
    {
        var verb = TestData.Hablar();

        var endings = Create(verb, ScenarioType.PresentEndings);

        Assert.Multiple(() =>
        {
            Assert.That(endings.Verb, Is.SameAs(verb));
            Assert.That(endings.Unit, Is.SameAs(verb));
            Assert.That(endings.Type, Is.EqualTo(ScenarioType.PresentEndings));
            Assert.That(endings.Instruction, Is.EqualTo("Fill in the endings · present"));
            Assert.That(endings.Rows, Is.EqualTo(new[]
            {
                new EndingRow("yo", "habl", "o"),
                new EndingRow("tú", "habl", "as"),
                new EndingRow("él / ella / usted", "habl", "a"),
                new EndingRow("nosotros / nosotras", "habl", "amos"),
                new EndingRow("ellos / ellas / ustedes", "habl", "an")
            }));
            Assert.That(endings.Notes, Is.Empty);
        });
    }

    [Test]
    public void Create_PreteriteEndings_UsesPreteriteForms()
    {
        var endings = Create(TestData.Hablar(), ScenarioType.PreteriteEndings);

        Assert.Multiple(() =>
        {
            Assert.That(endings.Type, Is.EqualTo(ScenarioType.PreteriteEndings));
            Assert.That(endings.Instruction, Is.EqualTo("Fill in the endings · preterite (past)"));
            Assert.That(endings.Rows.Select(r => r.Root), Is.All.EqualTo("habl"));
            Assert.That(endings.Rows.Select(r => r.Ending), Is.EqualTo(new[] { "é", "aste", "ó", "amos", "aron" }));
        });
    }

    [Test]
    public void Create_PresentEndings_IrregularVerb_TypesIrregularFormsWhole()
    {
        var endings = Create(Tener(), ScenarioType.PresentEndings);

        Assert.Multiple(() =>
        {
            Assert.That(endings.Rows.Select(r => $"{r.Root}|{r.Ending}"),
                Is.EqualTo(new[] { "|tengo", "|tienes", "|tiene", "ten|emos", "|tienen" }));
            Assert.That(endings.Notes.Select(n => n.Kind), Is.EqualTo(new[] { IrregularityKind.IrregularPresent }));
        });
    }

    [Test]
    public void Create_PreteriteEndings_IrregularVerb_AddsPreteriteNotes()
    {
        var endings = Create(Tener(), ScenarioType.PreteriteEndings);

        Assert.Multiple(() =>
        {
            Assert.That(endings.Rows.All(r => !r.HasRoot), Is.True);
            Assert.That(endings.Notes.Select(n => n.Kind), Is.EqualTo(new[] { IrregularityKind.IrregularPreterite }));
        });
    }

    [Test]
    public void Create_PreteriteEndings_SpellingChangeOfTheRoot_TypesThatFormWhole()
    {
        var buscar = new Verb
        {
            BaseValue = "buscar",
            PreteriteConjugations = ["busqué", "buscaste", "buscó", "buscamos", "buscaron"]
        };

        var endings = Create(buscar, ScenarioType.PreteriteEndings);

        Assert.That(endings.Rows.Select(r => $"{r.Root}|{r.Ending}"),
            Is.EqualTo(new[] { "|busqué", "busc|aste", "busc|ó", "busc|amos", "busc|aron" }));
    }

    [TestCase(ScenarioType.PresentEndings)]
    [TestCase(ScenarioType.PreteriteEndings)]
    public void Create_NotAnInfinitive_TypesEveryFormWhole(ScenarioType type)
    {
        // Same forms in both tenses, so one expectation serves both.
        string[] forms = ["me llamo", "te llamas", "se llama", "nos llamamos", "se llaman"];
        var llamarse = new Verb { BaseValue = "llamarse", PresentConjugations = forms, PreteriteConjugations = forms };

        var endings = Create(llamarse, type);

        Assert.That(endings.Rows.Select(r => $"{r.Root}|{r.Ending}"),
            Is.EqualTo(new[] { "|me llamo", "|te llamas", "|se llama", "|nos llamamos", "|se llaman" }));
    }

    [Test]
    public void Create_PresentEndings_Incomplete_Throws()
    {
        var verb = TestData.Hablar();
        verb.PresentConjugations = Verb.EmptyConjugations();

        Assert.That(() => Create(verb, ScenarioType.PresentEndings), Throws.ArgumentException);
    }

    [Test]
    public void EndingRow_Create_RegularForm_SplitsAfterTheRoot()
    {
        var row = EndingRow.Create("tú", "habl", " hablas ", "hablas");

        Assert.Multiple(() =>
        {
            Assert.That(row, Is.EqualTo(new EndingRow("tú", "habl", "as")));
            Assert.That(row.Form, Is.EqualTo("hablas"));
            Assert.That(row.HasRoot, Is.True);
            Assert.That(row.ExpectedAnswers, Is.EqualTo(new[] { "as", "hablas" }));
        });
    }

    [Test]
    public void EndingRow_Create_IgnoresCaseAndKeepsTheFormsSpelling()
    {
        Assert.That(EndingRow.Create("yo", "habl", "Hablo", "hablo"), Is.EqualTo(new EndingRow("yo", "Habl", "o")));
    }

    [TestCase("", "voy", "voy")] // ir: no root
    [TestCase("ten", "tengo", "teno")] // irregular form
    [TestCase("est", "estás", "estas")] // accent differs from the regular form
    [TestCase("busc", "busqué", "busqué")] // regular, but the spelling changes the root
    [TestCase("habl", "habl", "habl")] // nothing left to type after the root
    [TestCase("habl", "hablo", null)] // no regular forms
    public void EndingRow_Create_OtherForms_AreTypedWhole(string root, string form, string? regular)
    {
        var row = EndingRow.Create("yo", root, form, regular);

        Assert.Multiple(() =>
        {
            Assert.That(row, Is.EqualTo(new EndingRow("yo", string.Empty, form)));
            Assert.That(row.HasRoot, Is.False);
            Assert.That(row.ExpectedAnswers, Is.EqualTo(new[] { form }));
        });
    }

    [Test]
    public void EndingRow_Create_NullArguments_Throw()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => EndingRow.Create(null!, "habl", "hablo", "hablo"), Throws.ArgumentNullException);
            Assert.That(() => EndingRow.Create("yo", null!, "hablo", "hablo"), Throws.ArgumentNullException);
            Assert.That(() => EndingRow.Create("yo", "habl", null!, "hablo"), Throws.ArgumentNullException);
        });
    }
}
