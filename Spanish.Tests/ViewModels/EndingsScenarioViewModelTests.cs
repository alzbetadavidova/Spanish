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

    private EndingsScenarioViewModel Create(Verb verb, ScenarioType type)
    {
        var scenario = new ScenarioFactory(new FakeRandom(), new LearnLibrary()).Create(verb, type, Direction.Mixed);
        return new EndingsScenarioViewModel((EndingsScenario)scenario, OnCompleted);
    }

    /// <summary>Every row keeps the root: habl + é, aste, ó, amos, aron.</summary>
    private EndingsScenarioViewModel HablarPreterite() => Create(TestData.Hablar(), ScenarioType.PreteriteEndings);

    private static void FillIn(EndingsScenarioViewModel vm, params string[] answers)
    {
        for (var i = 0; i < answers.Length; i++)
        {
            vm.Rows[i].Answer = answers[i];
        }
    }

    [Test]
    public void Constructor_ExposesTheScenarioAndItsRows()
    {
        var vm = HablarPreterite();

        Assert.Multiple(() =>
        {
            Assert.That(vm.Heading, Is.EqualTo("Fill in the endings · preterite (past)"));
            Assert.That(vm.Prompt, Is.EqualTo("hablar"));
            Assert.That(vm.PromptDetail, Is.EqualTo("to speak, talk"));
            Assert.That(vm.Rows.Select(r => r.Person), Is.EqualTo(Verb.PersonLabels));
            Assert.That(vm.Rows.Select(r => r.Root), Is.All.EqualTo("habl"));
            Assert.That(vm.Rows.Select(r => r.Placeholder), Is.All.EqualTo("ending"));
            Assert.That(vm.VerbForms!.Rows[0], Is.EqualTo(new VerbFormRow("yo", "hablo", "hablé")));
            Assert.That(vm.IsChecked, Is.False);
            Assert.That(vm.IsCorrect, Is.False);
            Assert.That(vm.IsIncorrect, Is.False);
            Assert.That(vm.Feedback, Is.Null);
            Assert.That(vm.ShowNotes, Is.False);
            Assert.That(vm.ShowVerbForms, Is.False);
        });
    }

    [Test]
    public void Constructor_ShowsThePromptTheScenarioWasMadeWith()
    {
        var verb = TestData.Hablar();
        var vm = new EndingsScenarioViewModel(
            new EndingsScenario(verb, ScenarioType.PresentEndings, "i", "hablar", "to speak", [new EndingRow("yo", "habl", "o")]),
            OnCompleted);

        // An edit in the library while the exercise is shown doesn't change it.
        verb.BaseValue = "charlar";
        verb.Translation = "to chat";

        Assert.Multiple(() =>
        {
            Assert.That(vm.Prompt, Is.EqualTo("hablar"));
            Assert.That(vm.PromptDetail, Is.EqualTo("to speak"));
        });
    }

    [Test]
    public async Task Submit_BlankRow_AsksToFillEverythingUntilTyping()
    {
        var vm = HablarPreterite();
        FillIn(vm, "é", "aste", "ó", "amos", " ");

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
        var vm = HablarPreterite();
        FillIn(vm, "é", "aste", "ó", "amos", "hablaron");

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
        var vm = HablarPreterite();
        FillIn(vm, "é", "aste", "o", "amos", "aron");

        await vm.SubmitCommand.ExecuteAsync(null);
        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsCorrect, Is.True);
            Assert.That(vm.Feedback, Is.EqualTo("Correct. Watch the accents"));
            Assert.That(vm.ShowVerbForms, Is.False);
            Assert.That(vm.Rows[2].HasAccentHint, Is.True);
            Assert.That(vm.Rows[2].Feedback, Is.EqualTo("✓ habló"));
            Assert.That(vm.Rows[0].HasAccentHint, Is.False);
            Assert.That(_completions, Is.EqualTo(new[] { true }));
        });
    }

    [Test]
    public async Task Submit_WrongRows_CountTheRightOnesAndShowTheForms()
    {
        var vm = HablarPreterite();
        FillIn(vm, "é", "aste", "ó", "emos", "ieron");
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await vm.SubmitCommand.ExecuteAsync(null);
        Assert.Multiple(() =>
        {
            Assert.That(vm.IsCorrect, Is.False);
            Assert.That(vm.IsIncorrect, Is.True);
            Assert.That(vm.Feedback, Is.EqualTo("3 of 5 correct"));
            Assert.That(vm.ShowVerbForms, Is.True);
            Assert.That(changed, Is.SupersetOf(new[]
            {
                nameof(EndingsScenarioViewModel.IsChecked), nameof(EndingsScenarioViewModel.IsCorrect),
                nameof(EndingsScenarioViewModel.IsIncorrect), nameof(EndingsScenarioViewModel.Feedback),
                nameof(EndingsScenarioViewModel.ShowVerbForms)
            }));
            Assert.That(vm.Rows[3].IsIncorrect, Is.True);
            Assert.That(vm.Rows[3].IsCorrect, Is.False);
            Assert.That(vm.Rows[3].Feedback, Is.EqualTo("→ hablamos"));
            Assert.That(vm.Rows[4].Feedback, Is.EqualTo("→ hablaron"));
            Assert.That(vm.Rows[0].IsIncorrect, Is.False);
        });

        await vm.SubmitCommand.ExecuteAsync(null);
        Assert.That(_completions, Is.EqualTo(new[] { false }));
    }

    [Test]
    public async Task Submit_WrongRowAndMissingAccent_CountsTheAccentRowAsCorrect()
    {
        var vm = HablarPreterite();
        FillIn(vm, "é", "aste", "o", "emos", "aron");

        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsIncorrect, Is.True);
            Assert.That(vm.Feedback, Is.EqualTo("4 of 5 correct"));
            Assert.That(vm.Rows[2].Feedback, Is.EqualTo("✓ habló"));
            Assert.That(vm.ShowVerbForms, Is.True);
        });
    }

    [Test]
    public async Task Submit_IrregularVerb_ChecksWholeFormsAndShowsNotes()
    {
        var vm = Create(TestData.Tener(), ScenarioType.PresentEndings);
        FillIn(vm, "tengo", "tienes", "tiene", "emos", "tienen");

        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Rows[0].Root, Is.Empty);
            Assert.That(vm.Rows[0].Placeholder, Is.EqualTo("whole form"));
            Assert.That(vm.Rows[3].Placeholder, Is.EqualTo("ending"));
            Assert.That(vm.IsCorrect, Is.True);
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
    public void Row_Check_NotifiesTheBoundProperties()
    {
        var row = new EndingRowViewModel(new EndingRow("yo", "habl", "o")) { Answer = "a" };
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        row.Check();

        Assert.Multiple(() =>
        {
            Assert.That(row.Feedback, Is.EqualTo("→ hablo"));
            Assert.That(changed, Is.SupersetOf(new[]
            {
                nameof(EndingRowViewModel.IsChecked), nameof(EndingRowViewModel.IsCorrect),
                nameof(EndingRowViewModel.IsIncorrect), nameof(EndingRowViewModel.HasAccentHint),
                nameof(EndingRowViewModel.Feedback)
            }));
        });
    }

    [Test]
    public void Row_AccessibleName_SaysThePersonAndTheRoot()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new EndingRowViewModel(new EndingRow("yo", "habl", "o")).AccessibleName, Is.EqualTo("yo, habl…"));
            Assert.That(new EndingRowViewModel(new EndingRow("yo", "", "tengo")).AccessibleName, Is.EqualTo("yo, whole form"));
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
        var vm = HablarPreterite();
        FillIn(vm, "é", "", "ó", "", "aron");
        var rows = vm.Rows;

        Assert.Multiple(() =>
        {
            Assert.That(vm.NextEmptyRow(rows[0]), Is.SameAs(rows[1]));
            Assert.That(vm.NextEmptyRow(rows[1]), Is.SameAs(rows[3]));
            Assert.That(vm.NextEmptyRow(rows[4]), Is.SameAs(rows[1]));
        });
    }

    [Test]
    public void NextEmptyRow_OnlyTheCurrentRowIsBlank_SubmitsInstead()
    {
        var vm = HablarPreterite();
        FillIn(vm, "é", " ", "ó", "amos", "aron");

        Assert.Multiple(() =>
        {
            Assert.That(vm.NextEmptyRow(vm.Rows[1]), Is.Null);
            Assert.That(vm.NextEmptyRow(vm.Rows[0]), Is.SameAs(vm.Rows[1]));
        });
    }

    [Test]
    public void NextEmptyRow_RowOfAnotherExercise_Throws()
    {
        var vm = HablarPreterite();

        Assert.Multiple(() =>
        {
            Assert.That(() => vm.NextEmptyRow(HablarPreterite().Rows[0]), Throws.ArgumentException);
            Assert.That(() => vm.NextEmptyRow(null!), Throws.ArgumentNullException);
        });
    }
}
