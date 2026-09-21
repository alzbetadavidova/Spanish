using Spanish.Core;
using Spanish.ViewModels;

namespace Spanish.Tests.ViewModels;

public class StatsViewModelTests
{
    private readonly FakeClock _clock = new();
    private LearnLibrary _library = null!;
    private StatsViewModel _vm = null!;

    [SetUp]
    public void Setup()
    {
        _library = TestData.Library();
        // ciudad 100%, perro 0%, hablar never practiced
        _library.Nouns[0].RecordAnswer(ScenarioType.Card, true, _clock.Now.AddDays(-1));
        _library.Nouns[1].RecordAnswer(ScenarioType.Card, false, _clock.Now);
        _library.Nouns[1].RecordAnswer(ScenarioType.Gender, false, _clock.Now);
        _vm = new StatsViewModel(new LibraryContext(_library, new InMemoryStore<LearnLibrary>(_library)), _clock);
    }

    private IEnumerable<string> Words => _vm.Rows.Select(r => r.Word);

    [Test]
    public void Initially_SortedByLeastLearned()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Words, Is.EqualTo(new[] { "hablar", "perro", "ciudad" }));
            Assert.That(_vm.OverallHeader, Is.EqualTo("Overall ↑"));
            Assert.That(_vm.WordHeader, Is.EqualTo("Word"));
            Assert.That(_vm.IsEmpty, Is.False);
            Assert.That(_vm.TopicFilters.Select(t => t.Label), Is.EqualTo(new[] { "All topics", "animals", "city" }));
        });
    }

    [Test]
    public void SortBy_SameColumnTogglesDirection()
    {
        _vm.SortByCommand.Execute(StatsColumn.Overall);

        Assert.That(Words, Is.EqualTo(new[] { "ciudad", "perro", "hablar" }));
        Assert.That(_vm.OverallHeader, Is.EqualTo("Overall ↓"));
    }

    [TestCase(StatsColumn.Word, new[] { "ciudad", "hablar", "perro" })]
    [TestCase(StatsColumn.Translation, new[] { "ciudad", "perro", "hablar" })]
    [TestCase(StatsColumn.Attempts, new[] { "hablar", "ciudad", "perro" })]
    [TestCase(StatsColumn.LastPracticed, new[] { "hablar", "ciudad", "perro" })]
    public void SortBy_OtherColumnSortsAscending(StatsColumn column, string[] expected)
    {
        _vm.SortByCommand.Execute(column);

        Assert.That(Words, Is.EqualTo(expected));
        Assert.That(_vm.SortDescending, Is.False);
    }

    [Test]
    public void Headers_ShowArrowOnlyOnSortedColumn()
    {
        _vm.SortByCommand.Execute(StatsColumn.LastPracticed);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.LastPracticedHeader, Is.EqualTo("Last practiced ↑"));
            Assert.That(_vm.TranslationHeader, Is.EqualTo("English"));
            Assert.That(_vm.AttemptsHeader, Is.EqualTo("Attempts"));
            Assert.That(_vm.OverallHeader, Is.EqualTo("Overall"));
        });
    }

    [Test]
    public void Filters_ByKindAndTopic()
    {
        _vm.SelectedKind = StatsViewModel.KindFilters[2];
        Assert.That(Words, Is.EqualTo(new[] { "hablar" }));

        _vm.SelectedKind = StatsViewModel.KindFilters[1];
        _vm.SelectedTopic = _vm.TopicFilters.Single(t => t.Value == "city");
        Assert.That(Words, Is.EqualTo(new[] { "ciudad" }));

        _vm.SelectedTopic = null;
        Assert.That(Words.Count(), Is.EqualTo(2));

        _vm.SelectedKind = StatsViewModel.KindFilters[2];
        _vm.SelectedTopic = _vm.TopicFilters.Single(t => t.Value == "animals");
        Assert.That(_vm.IsEmpty, Is.True);
    }

    [Test]
    public void Refresh_KeepsTopicFilterWhileItExists()
    {
        _vm.SelectedTopic = _vm.TopicFilters.Single(t => t.Value == "city");
        _library.Topics.Add("food");

        _vm.OnActivated();
        Assert.That(_vm.SelectedTopic!.Value, Is.EqualTo("city"));

        _library.RemoveTopic("city");
        _vm.Refresh();
        Assert.That(_vm.SelectedTopic!.Value, Is.Null);
    }

    [Test]
    public void Row_FormatsPracticedWord()
    {
        var ciudad = _vm.Rows.Single(r => r.Word == "ciudad");
        var perro = _vm.Rows.Single(r => r.Word == "perro");

        Assert.Multiple(() =>
        {
            Assert.That(ciudad.Kind, Is.EqualTo("noun"));
            Assert.That(ciudad.Translation, Is.EqualTo("city, town"));
            Assert.That(ciudad.OverallPercent, Is.EqualTo(100));
            Assert.That(ciudad.OverallText, Is.EqualTo("100%"));
            Assert.That(ciudad.IsHigh, Is.True);
            Assert.That(ciudad.Breakdown, Is.EqualTo("Card 100"));
            Assert.That(ciudad.Attempts, Is.EqualTo(1));
            Assert.That(ciudad.LastPracticedText, Is.EqualTo("yesterday"));
            Assert.That(perro.IsLow, Is.True);
            Assert.That(perro.Breakdown, Is.EqualTo("Card 0 · Gender 0"));
            Assert.That(perro.LastPracticedText, Is.EqualTo("today"));
        });
    }

    [Test]
    public void Row_FormatsNewWord()
    {
        var hablar = _vm.Rows.Single(r => r.Word == "hablar");

        Assert.Multiple(() =>
        {
            Assert.That(hablar.Kind, Is.EqualTo("verb"));
            Assert.That(hablar.OverallText, Is.EqualTo("new"));
            Assert.That(hablar.OverallPercent, Is.Zero);
            Assert.That(hablar.IsLow || hablar.IsMedium || hablar.IsHigh, Is.False);
            Assert.That(hablar.Breakdown, Is.EqualTo("not practiced"));
            Assert.That(hablar.LastPracticedText, Is.EqualTo("—"));
        });
    }

    [Test]
    public void Row_MediumIndex()
    {
        var noun = TestData.Ciudad();
        noun.RecordAnswer(ScenarioType.Card, true, _clock.Now);
        noun.RecordAnswer(ScenarioType.Card, false, _clock.Now);

        var row = new StatsRowViewModel(LearnStats.BuildRow(noun), _clock.Now);

        Assert.That(row.IsMedium, Is.True);
        Assert.That(row.OverallText, Is.EqualTo("50%"));
    }

    [TestCase(4, "low")]
    [TestCase(5, "medium")]
    [TestCase(7, "medium")]
    [TestCase(8, "high")]
    public void Row_IndexLevelBoundaries(int correctOutOfTen, string level)
    {
        var noun = TestData.Ciudad();
        for (var i = 0; i < 10; i++)
        {
            noun.RecordAnswer(ScenarioType.Card, i < correctOutOfTen, _clock.Now);
        }

        var row = new StatsRowViewModel(LearnStats.BuildRow(noun), _clock.Now);

        Assert.That((row.IsLow, row.IsMedium, row.IsHigh), Is.EqualTo((level == "low", level == "medium", level == "high")));
    }

    [TestCase(0, "today")]
    [TestCase(-2, "today")]
    [TestCase(1, "yesterday")]
    [TestCase(5, "5 d ago")]
    public void Relative_FormatsDays(int daysAgo, string expected)
    {
        var now = new DateTime(2026, 9, 21, 8, 0, 0);

        Assert.That(StatsRowViewModel.Relative(now.AddDays(-daysAgo).AddHours(12), now.AddHours(12)), Is.EqualTo(expected));
    }
}

public class MainWindowViewModelTests
{
    private InMemoryStore<LearnLibrary> _libraryStore = null!;
    private InMemoryStore<SessionSettings> _settingsStore = null!;

    [SetUp]
    public void Setup()
    {
        _libraryStore = new InMemoryStore<LearnLibrary>(TestData.Library());
        _settingsStore = new InMemoryStore<SessionSettings>(new SessionSettings());
    }

    private MainWindowViewModel Create() => new(_libraryStore, _settingsStore, new FakeRandom(), new FakeClock());

    [Test]
    public async Task Initialize_BuildsPagesAndShowsLearn()
    {
        var vm = Create();
        Assert.That(vm.IsLoading, Is.True);

        await vm.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsLoading, Is.False);
            Assert.That(vm.NavItems.Select(n => n.Label), Is.EqualTo(new[] { "Learn", "Library", "Stats" }));
            Assert.That(vm.CurrentPage, Is.TypeOf<LearnViewModel>());
            Assert.That(vm.Context!.Library, Is.SameAs(_libraryStore.Value));
            Assert.That(vm.Notice, Is.Null);
            Assert.That(vm.LoadError, Is.Null);
        });
    }

    [Test]
    public async Task SelectingNav_ActivatesPage()
    {
        var vm = Create();
        await vm.InitializeAsync();
        vm.Context!.Library.Nouns.Clear();

        vm.SelectedNav = vm.NavItems[2];
        Assert.That(vm.CurrentPage, Is.TypeOf<StatsViewModel>());
        Assert.That(((StatsViewModel)vm.CurrentPage!).Rows, Has.Count.EqualTo(1));

        vm.SelectedNav = null;
        Assert.That(vm.CurrentPage, Is.TypeOf<StatsViewModel>());
    }

    [TestCase(typeof(IOException), false)]
    [TestCase(typeof(UnauthorizedAccessException), false)]
    [TestCase(typeof(System.Text.Json.JsonException), false)]
    [TestCase(typeof(InvalidOperationException), true)]
    public async Task Initialize_LoadFails_ShowsError(Type exceptionType, bool failLibrary)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "boom")!;
        if (failLibrary)
        {
            _libraryStore.LoadException = exception;
        }
        else
        {
            _settingsStore.LoadException = exception;
        }
        var vm = Create();

        await vm.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(vm.LoadError, Is.EqualTo("Couldn't open your library: boom"));
            Assert.That(vm.IsLoading, Is.False);
            Assert.That(vm.NavItems, Is.Empty);
        });
    }

    [Test]
    public async Task Initialize_CorruptLibrary_ShowsNoticeUntilDismissed()
    {
        _libraryStore = new InMemoryStore<LearnLibrary>(new LearnLibrary(), "library.json.bak-1");
        var vm = Create();

        await vm.InitializeAsync();
        Assert.That(vm.Notice, Does.Contain("library.json.bak-1"));

        vm.DismissNoticeCommand.Execute(null);
        Assert.That(vm.Notice, Is.Null);
    }

    [Test]
    public async Task Initialize_BothCorrupt_ShowsLibraryNotice()
    {
        _libraryStore = new InMemoryStore<LearnLibrary>(new LearnLibrary(), "library.json.bak-1");
        _settingsStore = new InMemoryStore<SessionSettings>(new SessionSettings(), "settings.json.bak-1");
        var vm = Create();

        await vm.InitializeAsync();

        Assert.That(vm.Notice, Does.StartWith("Your library file was damaged"));
    }

    [Test]
    public async Task Initialize_CorruptSettings_ShowsNotice()
    {
        _settingsStore = new InMemoryStore<SessionSettings>(new SessionSettings(), "settings.json.bak-1");
        var vm = Create();

        await vm.InitializeAsync();

        Assert.That(vm.Notice, Is.EqualTo("Your session settings were damaged and have been reset."));
    }
}
