using Spanish.Core;
using Spanish.ViewModels;

namespace Spanish.Tests.ViewModels;

public class EndingsScenarioViewModelTests
{
    private readonly List<bool> _completions = [];

    [SetUp]
    public void Setup() => _completions.Clear();

    private Task OnCompleted(Scenario scenario, bool correct)
    {
        _completions.Add(correct);
        return Task.CompletedTask;
    }

    /// <summary>hablar in the preterite: every row keeps the root, habl + é, aste, ó, amos, aron.</summary>
    private EndingsScenarioViewModel Hablar()
    {
        var scenario = new ScenarioFactory(new FakeRandom(), new LearnLibrary())
            .Create(TestData.Hablar(), ScenarioType.PreteriteEndings, Direction.Mixed);
        return new EndingsScenarioViewModel((EndingsScenario)scenario, OnCompleted);
    }

    private static void Type(EndingsScenarioViewModel vm, params string[] answers)
    {
        for (var i = 0; i < answers.Length; i++)
        {
            vm.Rows[i].Answer = answers[i];
        }
    }

    [Test]
    public void Constructor_ExposesTheVerbAndItsRows()
    {
        var vm = Hablar();

        Assert.Multiple(() =>
        {
            Assert.That(vm.Heading, Is.EqualTo("Fill in the endings · preterite (past)"));
            Assert.That(vm.Prompt, Is.EqualTo("hablar"));
            Assert.That(vm.PromptDetail, Is.EqualTo("to speak, talk"));
            Assert.That(vm.Rows.Select(r => r.Person), Is.EqualTo(Verb.PersonLabels));
            Assert.That(vm.Rows.Select(r => r.Root), Is.All.EqualTo("habl"));
            Assert.That(vm.Rows.Select(r => r.Placeholder), Is.All.EqualTo("ending"));
            Assert.That(vm.VerbForms, Is.Not.Null);
            Assert.That(vm.IsChecked, Is.False);
            Assert.That(vm.IsCorrect, Is.False);
            Assert.That(vm.IsIncorrect, Is.False);
            Assert.That(vm.Feedback, Is.Null);
            Assert.That(vm.ShowNotes, Is.False);
            Assert.That(vm.ShowVerbForms, Is.False);
        });
    }

    [Test]
    public async Task Submit_EmptyRow_AsksToFillEverythingUntilTyping()
    {
        var vm = Hablar();
        Type(vm, "é", "aste", "ó", "amos");

        await vm.SubmitCommand.ExecuteAsync(null);
        Assert.Multiple(() =>
        {
            Assert.That(vm.ValidationMessage, Is.EqualTo("Fill in every ending first."));
            Assert.That(vm.IsChecked, Is.False);
            Assert.That(vm.Rows[0].IsChecked, Is.False);
        });

        vm.Rows[4].Answer = "aron";
        Assert.That(vm.ValidationMessage, Is.Null);
    }

    [Test]
    public async Task Submit_AllRight_ChecksThenCompletesOnce()
    {
        var vm = Hablar();
        Type(vm, "é", "aste", "ó", "amos", "hablaron");

        await vm.SubmitCommand.ExecuteAsync(null);
        Assert.Multiple(() =>
        {
            Assert.That(vm.IsChecked, Is.True);
            Assert.That(vm.IsCorrect, Is.True);
            Assert.That(vm.IsIncorrect, Is.False);
            Assert.That(vm.Feedback, Is.EqualTo("Correct"));
            Assert.That(vm.ShowVerbForms, Is.False);
            Assert.That(vm.Rows.Select(r => r.Feedback), Is.All.EqualTo("✓"));
            Assert.That(vm.ValidationMessage, Is.Null);
            Assert.That(_completions, Is.Empty);
        });

        await vm.SubmitCommand.ExecuteAsync(null);
        await vm.SubmitCommand.ExecuteAsync(null);
        Assert.That(_completions, Is.EqualTo(new[] { true }));
    }

    [Test]
    public async Task Submit_MissingAccent_IsCorrectWithAHint()
    {
        var vm = Hablar();
        Type(vm, "é", "aste", "o", "amos", "aron");

        await vm.SubmitCommand.ExecuteAsync(null);
        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsCorrect, Is.True);
            Assert.That(vm.Feedback, Is.EqualTo("Correct. Watch the accents"));
            Assert.That(vm.Rows[2].HasAccentHint, Is.True);
            Assert.That(vm.Rows[2].Feedback, Is.EqualTo("✓ habló"));
            Assert.That(vm.Rows[0].HasAccentHint, Is.False);
            Assert.That(_completions, Is.EqualTo(new[] { true }));
        });
    }

    [Test]
    public async Task Submit_WrongRow_CountsTheRightOnesAndShowsTheForms()
    {
        var vm = Hablar();
        Type(vm, "é", "aste", "ó", "emos", "ieron");

        await vm.SubmitCommand.ExecuteAsync(null);
        Assert.Multiple(() =>
        {
            Assert.That(vm.IsCorrect, Is.False);
            Assert.That(vm.IsIncorrect, Is.True);
            Assert.That(vm.Feedback, Is.EqualTo("3 of 5 correct"));
            Assert.That(vm.ShowVerbForms, Is.True);
            Assert.That(vm.Rows[3].IsIncorrect, Is.True);
            Assert.That(vm.Rows[3].IsCorrect, Is.False);
            Assert.That(vm.Rows[3].Feedback, Is.EqualTo("hablamos"));
            Assert.That(vm.Rows[4].Feedback, Is.EqualTo("hablaron"));
            Assert.That(vm.Rows[0].IsIncorrect, Is.False);
        });

        await vm.SubmitCommand.ExecuteAsync(null);
        Assert.That(_completions, Is.EqualTo(new[] { false }));
    }

    [Test]
    public async Task Submit_IrregularVerb_ChecksWholeFormsAndShowsNotes()
    {
        var tener = new Verb
        {
            BaseValue = "tener",
            Translation = "to have",
            PresentConjugations = ["tengo", "tienes", "tiene", "tenemos", "tienen"]
        };
        var scenario = new ScenarioFactory(new FakeRandom(), new LearnLibrary()).Create(tener, ScenarioType.PresentEndings, Direction.Mixed);
        var vm = new EndingsScenarioViewModel((EndingsScenario)scenario, OnCompleted);
        Type(vm, "tengo", "tienes", "tiene", "emos", "tienen");

        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Rows[0].Root, Is.Empty);
            Assert.That(vm.Rows[0].Placeholder, Is.EqualTo("whole form"));
            Assert.That(vm.Rows[3].Placeholder, Is.EqualTo("ending"));
            Assert.That(vm.IsCorrect, Is.True);
            Assert.That(vm.HasNotes, Is.True);
            Assert.That(vm.ShowNotes, Is.True);
        });
    }

    [Test]
    public void Row_BeforeCheck_HasNoResult()
    {
        var row = new EndingRowViewModel(new EndingRow("yo", "habl", "o"));

        Assert.Multiple(() =>
        {
            Assert.That(row.IsChecked, Is.False);
            Assert.That(row.IsCorrect, Is.False);
            Assert.That(row.IsIncorrect, Is.False);
            Assert.That(row.HasAccentHint, Is.False);
            Assert.That(row.Feedback, Is.Null);
        });
    }

    [Test]
    public void Row_Check_AcceptsTheWholeForm()
    {
        var row = new EndingRowViewModel(new EndingRow("yo", "habl", "o")) { Answer = "hablo" };

        row.Check();

        Assert.Multiple(() =>
        {
            Assert.That(row.IsChecked, Is.True);
            Assert.That(row.IsCorrect, Is.True);
            Assert.That(row.Feedback, Is.EqualTo("✓"));
        });
    }

    [Test]
    public void NextEmptyRow_FindsTheNextEmptyRowAndWrapsAround()
    {
        var vm = Hablar();
        Type(vm, "é", "", "ó", "", "aron");

        Assert.Multiple(() =>
        {
            Assert.That(vm.NextEmptyRow(0), Is.EqualTo(1));
            Assert.That(vm.NextEmptyRow(1), Is.EqualTo(3));
            Assert.That(vm.NextEmptyRow(4), Is.EqualTo(1));
        });
    }

    [Test]
    public void NextEmptyRow_OnlyTheCurrentRowIsEmpty_SubmitsInstead()
    {
        var vm = Hablar();
        Type(vm, "é", " ", "ó", "amos", "aron");

        Assert.Multiple(() =>
        {
            Assert.That(vm.NextEmptyRow(1), Is.Null);
            Assert.That(vm.NextEmptyRow(0), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task NextEmptyRow_AfterCheck_SubmitsInstead()
    {
        var vm = Hablar();
        Type(vm, "é", "aste", "ó", "amos", "aron");
        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.That(vm.NextEmptyRow(0), Is.Null);
    }

    [TestCase(-1)]
    [TestCase(5)]
    public void NextEmptyRow_OutOfRange_Throws(int index)
    {
        Assert.That(() => Hablar().NextEmptyRow(index), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }
}
