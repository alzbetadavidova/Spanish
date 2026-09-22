using Spanish.Core;

namespace Spanish.Tests;

public class EndingsScenarioFactoryTests
{
    private static EndingsScenario Create(Verb verb, ScenarioType type) =>
        (EndingsScenario)new ScenarioFactory(new FakeRandom(), new LearnLibrary()).Create(verb, type, Direction.Mixed);

    /// <summary>Root|ending per row; an empty root means the form is typed whole.</summary>
    private static IEnumerable<string> Split(EndingsScenario endings) => endings.Rows.Select(r => $"{r.Root}|{r.Ending}");

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
            Assert.That(endings.Prompt, Is.EqualTo("hablar"));
            Assert.That(endings.PromptDetail, Is.EqualTo("to speak, talk"));
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
            Assert.That(Split(endings), Is.EqualTo(new[] { "habl|é", "habl|aste", "habl|ó", "habl|amos", "habl|aron" }));
        });
    }

    [Test]
    public void Create_PreteriteEndings_RegularIrVerb()
    {
        var vivir = new Verb
        {
            BaseValue = "vivir",
            PreteriteConjugations = ["viví", "viviste", "vivió", "vivimos", "vivieron"]
        };

        Assert.That(Split(Create(vivir, ScenarioType.PreteriteEndings)),
            Is.EqualTo(new[] { "viv|í", "viv|iste", "viv|ió", "viv|imos", "viv|ieron" }));
    }

    [Test]
    public void Create_PresentEndings_IrregularVerb_TypesIrregularFormsWhole()
    {
        var endings = Create(TestData.Tener(), ScenarioType.PresentEndings);

        Assert.Multiple(() =>
        {
            Assert.That(Split(endings), Is.EqualTo(new[] { "|tengo", "|tienes", "|tiene", "ten|emos", "|tienen" }));
            Assert.That(endings.Notes.Select(n => n.Kind), Is.EqualTo(new[] { IrregularityKind.IrregularPresent }));
        });
    }

    [Test]
    public void Create_PreteriteEndings_IrregularVerb_AddsPreteriteNotes()
    {
        var endings = Create(TestData.Tener(), ScenarioType.PreteriteEndings);

        Assert.Multiple(() =>
        {
            Assert.That(Split(endings), Is.EqualTo(new[] { "|tuve", "|tuviste", "|tuvo", "|tuvimos", "|tuvieron" }));
            Assert.That(endings.Notes.Select(n => n.Kind), Is.EqualTo(new[] { IrregularityKind.IrregularPreterite }));
        });
    }

    [Test]
    public void Create_PreteriteEndings_SpellingChanges()
    {
        var buscar = new Verb
        {
            BaseValue = "buscar",
            PreteriteConjugations = ["busqué", "buscaste", "buscó", "buscamos", "buscaron"]
        };
        var llegar = new Verb
        {
            BaseValue = "llegar",
            PreteriteConjugations = ["llegué", "llegaste", "llegó", "llegamos", "llegaron"]
        };

        Assert.Multiple(() =>
        {
            // busqué changes the root, so it is typed whole; llegué keeps it.
            Assert.That(Split(Create(buscar, ScenarioType.PreteriteEndings)),
                Is.EqualTo(new[] { "|busqué", "busc|aste", "busc|ó", "busc|amos", "busc|aron" }));
            Assert.That(Split(Create(llegar, ScenarioType.PreteriteEndings)),
                Is.EqualTo(new[] { "lleg|ué", "lleg|aste", "lleg|ó", "lleg|amos", "lleg|aron" }));
        });
    }

    [TestCase(ScenarioType.PresentEndings)]
    [TestCase(ScenarioType.PreteriteEndings)]
    public void Create_NotAnInfinitive_TypesEveryFormWhole(ScenarioType type)
    {
        // Same forms in both tenses, so one expectation serves both.
        string[] forms = ["me llamo", "te llamas", "se llama", "nos llamamos", "se llaman"];
        var llamarse = new Verb { BaseValue = "llamarse", PresentConjugations = forms, PreteriteConjugations = forms };

        Assert.That(Split(Create(llamarse, type)),
            Is.EqualTo(new[] { "|me llamo", "|te llamas", "|se llama", "|nos llamamos", "|se llaman" }));
    }

    [Test]
    public void Create_PresentEndings_Incomplete_Throws()
    {
        var verb = TestData.Hablar();
        verb.PresentConjugations = Verb.EmptyConjugations();

        Assert.That(() => Create(verb, ScenarioType.PresentEndings), Throws.ArgumentException);
    }
}

public class EndingRowTests
{
    [Test]
    public void Create_RegularForm_SplitsAfterTheRoot()
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
    public void Create_IgnoresCaseAndKeepsTheFormsSpelling()
    {
        Assert.That(EndingRow.Create("yo", "habl", "Hablo", "hablo"), Is.EqualTo(new EndingRow("yo", "Habl", "o")));
    }

    [TestCase("", "voy", "voy")] // ir: no root
    [TestCase("ten", "tengo", "teno")] // irregular form
    [TestCase("est", "estás", "estas")] // accent differs from the regular form
    [TestCase("busc", "busqué", "busqué")] // regular, but the spelling changes the root
    [TestCase("habl", "habl", "habl")] // nothing left to type after the root
    [TestCase("habl", "hablo", null)] // no regular forms
    [TestCase("ten", " tengo ", "teno")] // spaces from a hand-edited file
    public void Create_OtherForms_AreTypedWhole(string root, string form, string? regular)
    {
        var row = EndingRow.Create("yo", root, form, regular);

        Assert.Multiple(() =>
        {
            Assert.That(row, Is.EqualTo(new EndingRow("yo", string.Empty, form.Trim())));
            Assert.That(row.HasRoot, Is.False);
            Assert.That(row.ExpectedAnswers, Is.EqualTo(new[] { form.Trim() }));
        });
    }

    [Test]
    public void Create_NullArguments_Throw()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => EndingRow.Create(null!, "habl", "hablo", "hablo"), Throws.ArgumentNullException);
            Assert.That(() => EndingRow.Create("yo", null!, "hablo", "hablo"), Throws.ArgumentNullException);
            Assert.That(() => EndingRow.Create("yo", "habl", null!, "hablo"), Throws.ArgumentNullException);
        });
    }
}

public class ScenarioTypeExtensionsTests
{
    [Test]
    public void IsConjugation_OnlyVerbFormScenarios()
    {
        Assert.That(Enum.GetValues<ScenarioType>().Where(t => t.IsConjugation()), Is.EquivalentTo(new[]
        {
            ScenarioType.Present, ScenarioType.Preterite, ScenarioType.Gerund,
            ScenarioType.PresentEndings, ScenarioType.PreteriteEndings
        }));
    }
}
