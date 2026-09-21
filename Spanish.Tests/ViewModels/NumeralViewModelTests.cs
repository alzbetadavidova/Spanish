using Spanish.Core;
using Spanish.ViewModels;

namespace Spanish.Tests.ViewModels;

public class NumeralDisplayNamesTests
{
    [Test]
    public void Numeral_SingularAndPlural()
    {
        Assert.That(DisplayNames.Singular(WordKind.Numeral), Is.EqualTo("numeral"));
        Assert.That(DisplayNames.Plural(WordKind.Numeral), Is.EqualTo("numerals"));
    }

    [TestCase(ScenarioType.NumberToText, "To words")]
    [TestCase(ScenarioType.TextToNumber, "To digits")]
    public void Scenario_Label(ScenarioType type, string expected)
    {
        Assert.That(DisplayNames.Scenario(type), Is.EqualTo(expected));
    }

    [TestCase(NumeralSubtype.Number, "Number")]
    [TestCase(NumeralSubtype.Date, "Date")]
    [TestCase(NumeralSubtype.Time, "Time")]
    [TestCase(NumeralSubtype.DateAndTime, "Date and time")]
    public void Subtype_Label(NumeralSubtype subtype, string expected)
    {
        Assert.That(DisplayNames.Subtype(subtype), Is.EqualTo(expected));
    }
}

public class NumeralListViewModelTests
{
    private LearnLibrary _library = null!;
    private NumeralListViewModel _vm = null!;

    [SetUp]
    public void Setup()
    {
        _library = TestData.Library();
        _vm = new NumeralListViewModel(_library);
    }

    [Test]
    public void Constructor_ListsBuiltInNumeralsWithoutSelection()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_vm.Items, Is.SameAs(_library.Numerals));
            Assert.That(_vm.CountText, Is.EqualTo("8 numerals"));
            Assert.That(_vm.Selected, Is.Null);
            Assert.That(_vm.Details, Is.Null);
            Assert.That(_vm.HasSelection, Is.False);
        });
    }

    [Test]
    public void Selecting_ShowsDetails()
    {
        var changed = new List<string?>();
        _vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var thousands = _library.Numerals.Single(n => n.Category == NumeralCategory.Thousands);

        _vm.Selected = thousands;

        var details = _vm.Details!;
        Assert.Multiple(() =>
        {
            Assert.That(_vm.HasSelection, Is.True);
            Assert.That(changed, Does.Contain(nameof(NumeralListViewModel.HasSelection)).And.Contain(nameof(NumeralListViewModel.Details)));
            Assert.That(details.Numeral, Is.SameAs(thousands));
            Assert.That(details.Title, Is.EqualTo("miles"));
            Assert.That(details.English, Is.EqualTo("thousands"));
            Assert.That(details.Subtype, Is.EqualTo("Number"));
            Assert.That(details.Scenarios, Is.EqualTo("To words · To digits"));
            Assert.That(details.Examples, Is.EqualTo(new[] { "21.000 → veintiún mil", "3.500 → tres mil quinientos" }));
        });
    }

    [Test]
    public void ClearingSelection_HidesDetails()
    {
        _vm.Selected = _library.Numerals[0];

        _vm.Selected = null;

        Assert.That(_vm.Details, Is.Null);
        Assert.That(_vm.HasSelection, Is.False);
    }
}

public class NumeralPagesTests
{
    private LearnLibrary _library = null!;
    private LibraryContext _context = null!;

    [SetUp]
    public void Setup()
    {
        _library = TestData.Library();
        _context = new LibraryContext(_library, new InMemoryStore<LearnLibrary>(_library));
    }

    [Test]
    public void LibraryPage_HasReadOnlyNumeralsTab()
    {
        var page = new LibraryViewModel(_context);

        Assert.That(page.Numerals.Items, Is.SameAs(_library.Numerals));
    }

    [Test]
    public void TopicsTab_OffersOnlyWords()
    {
        var topics = new TopicsViewModel(_context) { SelectedTopic = "city" };

        Assert.That(topics.Words.Select(w => w.Value), Is.EquivalentTo(_library.Words));
    }

    [Test]
    public void Stats_FilterByNumerals()
    {
        var stats = new StatsViewModel(_context, new FakeClock());

        stats.SelectedKind = StatsViewModel.KindFilters.Single(k => k.Value == WordKind.Numeral);

        Assert.Multiple(() =>
        {
            Assert.That(stats.SelectedKind.Label, Is.EqualTo("Numerals"));
            Assert.That(stats.Rows, Has.Count.EqualTo(_library.Numerals.Count));
            Assert.That(stats.Rows.All(r => r.Kind == "numeral"), Is.True);
        });
    }

    private static readonly SessionSettings NumeralsOnly = new()
    {
        IncludeNouns = false,
        IncludeVerbs = false,
        IncludeAdjectives = false
    };

    [Test]
    public async Task Learn_PracticesNumeralsAndSavesTheirProgress()
    {
        var store = new InMemoryStore<LearnLibrary>(_library);
        var context = new LibraryContext(_library, store);
        var learn = new LearnViewModel(context, new InMemoryStore<SessionSettings>(NumeralsOnly), NumeralsOnly,
            new FakeRandom(), new FakeClock());

        // Least learned first: every numeral ties, and the random source picks the first one, 0.
        var typed = (TypedScenarioViewModel)learn.CurrentScenario!;
        Assert.Multiple(() =>
        {
            Assert.That(typed.Heading, Is.EqualTo(ScenarioFactory.NumberToTextInstruction));
            Assert.That(typed.Prompt, Is.EqualTo("0"));
            Assert.That(learn.SummaryChips[0], Is.EqualTo("Numerals"));
        });

        typed.Answer = "cero";
        await typed.SubmitCommand.ExecuteAsync(null);
        await typed.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(typed.IsCorrect, Is.True);
            Assert.That(learn.CurrentScenario, Is.Not.SameAs(typed));
            Assert.That(store.SaveCount, Is.EqualTo(1));
            Assert.That(TestData.NumeralOf(NumeralCategory.Numbers0To20, _library).GetProgress(ScenarioType.NumberToText)!.Attempts, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Learn_LibraryChangedWhileAnsweringNumeral_KeepsCurrentScenario()
    {
        var learn = new LearnViewModel(_context, new InMemoryStore<SessionSettings>(NumeralsOnly), NumeralsOnly,
            new FakeRandom(), new FakeClock());
        var current = learn.CurrentScenario;
        Assert.That(current?.Scenario.Unit, Is.TypeOf<Numeral>());

        await _context.SaveAsync();

        Assert.That(learn.CurrentScenario, Is.SameAs(current));
    }
}
