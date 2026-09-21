using Spanish.Core;
using Spanish.ViewModels;

namespace Spanish.Tests.ViewModels;

public class LearnViewModelTests
{
    private LearnLibrary _library = null!;
    private InMemoryStore<LearnLibrary> _libraryStore = null!;
    private InMemoryStore<SessionSettings> _settingsStore = null!;
    private LibraryContext _context = null!;

    [SetUp]
    public void Setup()
    {
        _library = TestData.Library();
        _libraryStore = new InMemoryStore<LearnLibrary>(_library);
        _settingsStore = new InMemoryStore<SessionSettings>(new SessionSettings());
        _context = new LibraryContext(_library, _libraryStore);
    }

    private static readonly SessionSettings GenderOnly = new()
    {
        IncludeVerbs = false,
        IncludeAdjectives = false,
        IncludeNumerals = false,
        NounScenarioTypes = [ScenarioType.Gender]
    };

    private LearnViewModel Create(SessionSettings? settings = null) =>
        new(_context, _settingsStore, settings ?? GenderOnly, new FakeRandom(), new FakeClock());

    [Test]
    public void Constructor_ShowsFirstScenario()
    {
        var learn = Create();

        Assert.Multiple(() =>
        {
            Assert.That(learn.CurrentScenario, Is.TypeOf<GenderScenarioViewModel>());
            Assert.That(learn.IsEmpty, Is.False);
            Assert.That(learn.ProgressText, Is.EqualTo("0 answered · 0 correct"));
            Assert.That(learn.SummaryChips, Is.EqualTo(new[] { "Nouns", "All topics", "Least learned first" }));
        });
    }

    [Test]
    public void Constructor_NothingMatches_IsEmpty()
    {
        var learn = Create(new SessionSettings { IncludeNouns = false, IncludeVerbs = false, IncludeAdjectives = false, IncludeNumerals = false });

        Assert.That(learn.IsEmpty, Is.True);
        Assert.That(learn.SummaryChips[0], Is.EqualTo("No word types"));
    }

    [Test]
    public async Task CompletingScenario_RecordsSavesAndShowsNext()
    {
        var learn = Create();
        var first = (GenderScenarioViewModel)learn.CurrentScenario!;
        Assert.That(first.Word, Is.EqualTo("ciudad"));

        first.ChooseCommand.Execute(Article.La);
        await first.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(learn.CurrentScenario, Is.Not.SameAs(first));
            Assert.That(learn.ProgressText, Is.EqualTo("1 answered · 1 correct"));
            Assert.That(first.Scenario.Unit.GetProgress(ScenarioType.Gender)!.Attempts, Is.EqualTo(1));
            Assert.That(_libraryStore.SaveCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void OpenAndCloseSettings_TogglePanel()
    {
        var learn = Create();

        learn.OpenSettingsCommand.Execute(null);
        Assert.That(learn.IsSettingsOpen, Is.True);

        learn.CloseSettingsCommand.Execute(null);
        Assert.That(learn.IsSettingsOpen, Is.False);
    }

    [Test]
    public async Task StartSession_AppliesAndSavesSettings()
    {
        var learn = Create();
        learn.OpenSettingsCommand.Execute(null);
        learn.Settings.IncludeNouns = false;
        learn.Settings.IncludeVerbs = true;
        learn.Settings.IncludeAdjectives = false;
        learn.Settings.Topics.Single(t => t.Value == "city").IsSelected = true;

        await learn.StartSessionCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(learn.IsSettingsOpen, Is.False);
            Assert.That(learn.CurrentScenario!.Scenario.Unit.BaseValue, Is.EqualTo("hablar"));
            Assert.That(learn.SummaryChips, Is.EqualTo(new[] { "Verbs", "Topics: city", "Least learned first" }));
            Assert.That(_settingsStore.SaveCount, Is.EqualTo(1));
            Assert.That(_settingsStore.Value.IncludeNouns, Is.False);
        });
    }

    [Test]
    public async Task StartSession_SettingsSaveFails_ReportsError()
    {
        var learn = Create();
        _settingsStore.SaveException = new IOException("disk full");

        await learn.StartSessionCommand.ExecuteAsync(null);

        Assert.That(_context.SaveError, Is.EqualTo(LibraryContext.SaveFailedMessage));
    }

    [Test]
    public async Task OldScenarioCompletedAfterSessionBecameEmpty_IsIgnored()
    {
        var learn = Create();
        var old = (GenderScenarioViewModel)learn.CurrentScenario!;
        learn.Settings.IncludeNouns = false;
        await learn.StartSessionCommand.ExecuteAsync(null);

        old.ChooseCommand.Execute(Article.El);
        await old.SubmitCommand.ExecuteAsync(null);

        Assert.That(learn.ProgressText, Is.EqualTo("0 answered · 0 correct"));
        Assert.That(_libraryStore.SaveCount, Is.Zero);
    }

    [Test]
    public async Task StartSession_ThenOldScenarioCompleted_DoesNotRecordNewScenario()
    {
        var learn = Create();
        var old = (GenderScenarioViewModel)learn.CurrentScenario!;
        learn.Settings.IncludeNouns = false;
        learn.Settings.IncludeVerbs = true;
        await learn.StartSessionCommand.ExecuteAsync(null);
        var current = learn.CurrentScenario;
        var saves = _libraryStore.SaveCount;

        old.ChooseCommand.Execute(Article.El);
        await old.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(learn.CurrentScenario, Is.SameAs(current));
            Assert.That(_library.Verbs[0].Progress, Is.Empty);
            Assert.That(learn.ProgressText, Is.EqualTo("0 answered · 0 correct"));
            Assert.That(_libraryStore.SaveCount, Is.EqualTo(saves));
        });
    }

    [Test]
    public async Task LibraryChanged_WhileEmpty_ShowsNewExercise()
    {
        _library.Nouns.Clear();
        var learn = Create();
        Assert.That(learn.IsEmpty, Is.True);

        _library.Nouns.Add(TestData.Perro());
        await _context.SaveAsync();

        var gender = (GenderScenarioViewModel)learn.CurrentScenario!;
        Assert.That(gender.Scenario.Unit.BaseValue, Is.EqualTo("perro"));
    }

    [Test]
    public async Task LibraryChanged_WhileAnswering_KeepsCurrentScenario()
    {
        var learn = Create();
        var current = learn.CurrentScenario;

        await _context.SaveAsync();

        Assert.That(learn.CurrentScenario, Is.SameAs(current));
    }

    [Test]
    public async Task LibraryChanged_CurrentWordDeleted_ShowsNext()
    {
        var learn = Create();
        var current = learn.CurrentScenario!;

        _library.Remove(current.Scenario.Unit);
        await _context.SaveAsync();

        Assert.That(learn.CurrentScenario!.Scenario.Unit, Is.Not.SameAs(current.Scenario.Unit));
    }

    [Test]
    public async Task TopicDeleted_OnlySelectedTopic_WidensToAllTopicsAndSavesSettings()
    {
        var learn = Create(GenderOnly with { Topics = ["animals"] });
        Assert.That(learn.CurrentScenario!.Scenario.Unit.BaseValue, Is.EqualTo("perro"));

        _library.RemoveTopic("animals");
        await _context.SaveAsync();

        Assert.Multiple(() =>
        {
            // "No topics selected" means all topics, so the session widens.
            Assert.That(learn.SummaryChips[1], Is.EqualTo("All topics"));
            Assert.That(learn.CurrentScenario!.Scenario.Unit.BaseValue, Is.EqualTo("ciudad"));
            Assert.That(_settingsStore.SaveCount, Is.EqualTo(1));
            Assert.That(_settingsStore.Value.Topics, Is.Empty);
        });
    }

    [Test]
    public async Task TopicDeleted_CurrentWordNoLongerMatches_ShowsNext()
    {
        var learn = Create(GenderOnly with { Topics = ["animals", "city"] });
        Assert.That(learn.CurrentScenario!.Scenario.Unit.BaseValue, Is.EqualTo("ciudad"));

        _library.RemoveTopic("city");
        await _context.SaveAsync();

        Assert.That(learn.CurrentScenario!.Scenario.Unit.BaseValue, Is.EqualTo("perro"));
        Assert.That(learn.SummaryChips[1], Is.EqualTo("Topics: animals"));
    }

    [TestCase("pets")]
    [TestCase("Animals")]
    public async Task PaneOpen_TopicRenamed_KeepsSelectionAndStartUsesNewName(string newName)
    {
        var learn = Create(GenderOnly with { Topics = ["animals"] });
        learn.OpenSettingsCommand.Execute(null);

        _context.RenameTopic("animals", newName);
        await _context.SaveAsync();
        learn.OnActivated();

        Assert.That(learn.Settings.Topics.Single(t => t.IsSelected).Value, Is.EqualTo(newName));
        await learn.StartSessionCommand.ExecuteAsync(null);
        Assert.That(_settingsStore.Value.Topics, Is.EqualTo(new[] { newName }));
    }

    [Test]
    public async Task SettingsChangedDuringSave_StayPending()
    {
        var gated = new GatedStore<SessionSettings>();
        var learn = new LearnViewModel(_context, gated, GenderOnly with { Topics = ["gone", "animals"] }, new FakeRandom(), new FakeClock());

        var firstSave = _context.SaveAsync(); // saves the cleaned settings, blocked by the gate
        _context.RenameTopic("animals", "pets"); // changes the settings while that save runs
        gated.Release();
        await firstSave;
        Assert.That(gated.Saved.Last().Topics, Is.EqualTo(new[] { "animals" }));

        await _context.SaveAsync();

        Assert.That(gated.Saved.Last().Topics, Is.EqualTo(new[] { "pets" }));
        Assert.That(learn.SummaryChips[1], Is.EqualTo("Topics: pets"));
    }

    [Test]
    public async Task TopicRenamed_SessionFollowsRename()
    {
        var learn = Create(GenderOnly with { Topics = ["animals"] });
        var current = learn.CurrentScenario;

        _context.RenameTopic("animals", "pets");
        await _context.SaveAsync();

        Assert.Multiple(() =>
        {
            Assert.That(learn.SummaryChips[1], Is.EqualTo("Topics: pets"));
            Assert.That(learn.CurrentScenario, Is.SameAs(current));
        });
        learn.OpenSettingsCommand.Execute(null);
        Assert.That(learn.Settings.Topics.Single(t => t.IsSelected).Value, Is.EqualTo("pets"));
    }

    [Test]
    public async Task SavedSettingsWithDeletedTopic_AreCleanedAndSavedLater()
    {
        var learn = Create(GenderOnly with { Topics = ["gone"] });

        Assert.That(learn.SummaryChips[1], Is.EqualTo("All topics"));
        Assert.That(learn.CurrentScenario, Is.Not.Null);

        var scenario = (GenderScenarioViewModel)learn.CurrentScenario!;
        scenario.ChooseCommand.Execute(Article.La);
        await scenario.SubmitCommand.ExecuteAsync(null);
        Assert.That(_settingsStore.SaveCount, Is.EqualTo(1));

        var next = (GenderScenarioViewModel)learn.CurrentScenario!;
        next.ChooseCommand.Execute(Article.El);
        await next.SubmitCommand.ExecuteAsync(null);
        Assert.That(_settingsStore.SaveCount, Is.EqualTo(1));
    }

    [Test]
    public async Task SettingsSaveFailure_IsRetriedAtNextAnswer()
    {
        var learn = Create();
        _settingsStore.SaveException = new IOException("locked");
        await learn.StartSessionCommand.ExecuteAsync(null);
        _settingsStore.SaveException = null;

        var scenario = (GenderScenarioViewModel)learn.CurrentScenario!;
        scenario.ChooseCommand.Execute(Article.La);
        await scenario.SubmitCommand.ExecuteAsync(null);

        Assert.That(_settingsStore.SaveCount, Is.EqualTo(1));
        Assert.That(_context.SaveError, Is.Null);
    }

    [Test]
    public void OpenSettings_DiscardsUnappliedEdits()
    {
        var learn = Create();
        learn.OpenSettingsCommand.Execute(null);
        learn.Settings.IncludeVerbs = true;
        learn.CloseSettingsCommand.Execute(null);

        learn.OpenSettingsCommand.Execute(null);

        Assert.That(learn.Settings.IncludeVerbs, Is.False);
    }

    [Test]
    public void StartSession_NoMatches_CannotExecute()
    {
        var learn = Create();
        var raised = 0;
        learn.StartSessionCommand.CanExecuteChanged += (_, _) => raised++;

        learn.Settings.IncludeNouns = false;

        Assert.That(learn.StartSessionCommand.CanExecute(null), Is.False);
        Assert.That(raised, Is.GreaterThan(0));

        learn.Settings.IncludeVerbs = true;
        Assert.That(learn.StartSessionCommand.CanExecute(null), Is.True);
    }

    [Test]
    public void SettingsChangeOtherThanMatchCount_DoesNotRaiseCanExecute()
    {
        var learn = Create();
        var raised = 0;
        learn.StartSessionCommand.CanExecuteChanged += (_, _) => raised++;

        learn.Settings.SelectedOrder = SessionSettingsViewModel.Orders[2];

        Assert.That(raised, Is.Zero);
    }

    [Test]
    public void OnActivated_RefreshesTopics()
    {
        var learn = Create();
        _library.Topics.Add("food");

        learn.OnActivated();

        Assert.That(learn.Settings.Topics.Select(t => t.Value), Does.Contain("food"));
    }

    [Test]
    public void SummaryChips_DescribeKinds()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Create(new SessionSettings()).SummaryChips[0], Is.EqualTo("Nouns, verbs, adjectives, numerals"));
            Assert.That(Create(new SessionSettings { IncludeNouns = false, IncludeNumerals = false }).SummaryChips[0], Is.EqualTo("Verbs, adjectives"));
            Assert.That(Create(new SessionSettings { IncludeNouns = false, IncludeVerbs = false, IncludeNumerals = false }).SummaryChips[0], Is.EqualTo("Adjectives"));
            Assert.That(Create(new SessionSettings { IncludeVerbs = false, IncludeAdjectives = false, IncludeNumerals = false }).SummaryChips[0], Is.EqualTo("Nouns"));
            Assert.That(Create(new SessionSettings { IncludeNouns = false, IncludeVerbs = false, IncludeAdjectives = false }).SummaryChips[0], Is.EqualTo("Numerals"));
        });
    }
}

public class SessionSettingsViewModelTests
{
    [Test]
    public void Constructor_LoadsSettings()
    {
        var library = TestData.Library();
        var settings = new SessionSettings
        {
            IncludeVerbs = false,
            IncludeNumerals = false,
            NounScenarioTypes = [ScenarioType.Plural],
            Topics = ["city"],
            Order = SessionOrder.Random,
            Direction = Direction.SpanishToEnglish
        };

        var vm = new SessionSettingsViewModel(library, settings);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IncludeNouns, Is.True);
            Assert.That(vm.IncludeVerbs, Is.False);
            Assert.That(vm.IncludeNumerals, Is.False);
            Assert.That(vm.NounScenarios.Where(s => s.IsSelected).Select(s => s.Value), Is.EqualTo(new[] { ScenarioType.Plural }));
            Assert.That(vm.VerbScenarios.Select(s => s.Label), Is.EqualTo(new[] { "Card", "Fill", "Present", "Preterite", "Gerund" }));
            Assert.That(vm.Topics.Select(t => t.Label), Is.EqualTo(new[] { "animals", "city" }));
            Assert.That(vm.Topics.Single(t => t.IsSelected).Value, Is.EqualTo("city"));
            Assert.That(vm.SelectedOrder.Value, Is.EqualTo(SessionOrder.Random));
            Assert.That(vm.SelectedDirection.Value, Is.EqualTo(Direction.SpanishToEnglish));
            Assert.That(vm.MatchCount, Is.EqualTo(1));
            Assert.That(vm.MatchText, Is.EqualTo("1 exercise matches"));
        });
    }

    [Test]
    public void Changes_UpdateMatchCount()
    {
        var vm = new SessionSettingsViewModel(TestData.Library(), new SessionSettings());
        Assert.That(vm.MatchText, Is.EqualTo("26 exercises match"));

        vm.IncludeNumerals = false;
        Assert.That(vm.MatchText, Is.EqualTo("10 exercises match"));

        vm.IncludeVerbs = false;
        Assert.That(vm.MatchCount, Is.EqualTo(6));

        vm.NounScenarios.Single(s => s.Value == ScenarioType.Plural).IsSelected = true;
        Assert.That(vm.MatchCount, Is.EqualTo(8));

        vm.IncludeNouns = false;
        Assert.That(vm.MatchCount, Is.Zero);
    }

    [Test]
    public void NumeralChoices_LoadAndApply()
    {
        var settings = new SessionSettings
        {
            IncludeNouns = false,
            IncludeVerbs = false,
            IncludeAdjectives = false,
            NumeralScenarioTypes = [ScenarioType.TextToNumber]
        };

        var vm = new SessionSettingsViewModel(TestData.Library(), settings);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IncludeNumerals, Is.True);
            Assert.That(vm.NumeralScenarios.Select(s => s.Label), Is.EqualTo(new[] { "To words", "To digits" }));
            Assert.That(vm.NumeralScenarios.Where(s => s.IsSelected).Select(s => s.Value), Is.EqualTo(new[] { ScenarioType.TextToNumber }));
            Assert.That(vm.MatchCount, Is.EqualTo(8));
        });

        vm.NumeralScenarios.Single(s => s.Value == ScenarioType.NumberToText).IsSelected = true;
        Assert.That(vm.MatchCount, Is.EqualTo(16));

        vm.IncludeNumerals = false;
        var applied = vm.ToSettings();
        Assert.Multiple(() =>
        {
            Assert.That(vm.MatchCount, Is.Zero);
            Assert.That(applied.IncludeNumerals, Is.False);
            Assert.That(applied.NumeralScenarioTypes, Is.EqualTo(new[] { ScenarioType.NumberToText, ScenarioType.TextToNumber }));
        });
    }

    [Test]
    public void ToSettings_ReflectsSelection()
    {
        var vm = new SessionSettingsViewModel(TestData.Library(), new SessionSettings());
        vm.SelectedOrder = SessionSettingsViewModel.Orders[1];
        vm.SelectedDirection = SessionSettingsViewModel.Directions[2];
        vm.VerbScenarios.Single(s => s.Value == ScenarioType.Gerund).IsSelected = true;

        var settings = vm.ToSettings();

        Assert.Multiple(() =>
        {
            Assert.That(settings.Order, Is.EqualTo(SessionOrder.LeastRecentlyPracticed));
            Assert.That(settings.Direction, Is.EqualTo(Direction.SpanishToEnglish));
            Assert.That(settings.VerbScenarioTypes, Does.Contain(ScenarioType.Gerund));
            Assert.That(settings.Topics, Is.Empty);
        });
    }

    [Test]
    public void Reset_RestoresDefaults()
    {
        var vm = new SessionSettingsViewModel(TestData.Library(), new SessionSettings { IncludeNouns = false, Order = SessionOrder.Random });

        vm.ResetCommand.Execute(null);

        Assert.That(vm.ToSettings().IncludeNouns, Is.True);
        Assert.That(vm.SelectedOrder.Value, Is.EqualTo(SessionOrder.LeastLearned));
    }

    [Test]
    public void RenameTopic_RenamesOnlyMatchingSelection()
    {
        var library = TestData.Library();
        var vm = new SessionSettingsViewModel(library, new SessionSettings { Topics = ["animals", "city"] });
        library.RenameTopic("city", "town");

        vm.RenameTopic("CITY", "town");

        Assert.That(vm.Topics.Where(t => t.IsSelected).Select(t => t.Value), Is.EqualTo(new[] { "animals", "town" }));
    }

    [Test]
    public void RefreshTopics_KeepsSelectionAndAddsNew()
    {
        var library = TestData.Library();
        var vm = new SessionSettingsViewModel(library, new SessionSettings { Topics = ["city"] });
        library.Topics.Add("food");

        vm.RefreshTopics();

        Assert.That(vm.Topics.Select(t => (t.Value, t.IsSelected)), Is.EqualTo(new[]
        {
            ("animals", false), ("city", true), ("food", false)
        }));
    }
}

public class LibraryContextTests
{
    [Test]
    public async Task SaveAsync_RaisesChangedAndSaves()
    {
        var store = new InMemoryStore<LearnLibrary>(new LearnLibrary());
        var context = new LibraryContext(TestData.Library(), store);
        var changed = 0;
        context.Changed += (_, _) => changed++;

        await context.SaveAsync();

        Assert.That(changed, Is.EqualTo(1));
        Assert.That(store.Value, Is.SameAs(context.Library));
    }

    [Test]
    public async Task SaveAsync_IoFailure_ReportsAndClearsAfterSuccess()
    {
        var store = new InMemoryStore<LearnLibrary>(new LearnLibrary()) { SaveException = new UnauthorizedAccessException() };
        var context = new LibraryContext(TestData.Library(), store);

        await context.SaveAsync();
        Assert.That(context.SaveError, Is.EqualTo(LibraryContext.SaveFailedMessage));

        store.SaveException = null;
        await context.SaveAsync();
        Assert.That(context.SaveError, Is.Null);
    }

    [Test]
    public async Task RunSaveAsync_ReportsSuccess()
    {
        var context = new LibraryContext(new LearnLibrary(), new InMemoryStore<LearnLibrary>(new LearnLibrary()));

        Assert.That(await context.RunSaveAsync("x", _ => Task.CompletedTask), Is.True);
        Assert.That(await context.RunSaveAsync("x", _ => Task.FromException(new IOException())), Is.False);
    }

    [Test]
    public async Task SaveError_IsTrackedPerTarget()
    {
        var store = new InMemoryStore<LearnLibrary>(new LearnLibrary()) { SaveException = new IOException() };
        var context = new LibraryContext(TestData.Library(), store);

        await context.SaveAsync();
        await context.RunSaveAsync("settings", _ => Task.CompletedTask);
        Assert.That(context.SaveError, Is.EqualTo(LibraryContext.SaveFailedMessage));

        store.SaveException = null;
        await context.SaveAsync();
        Assert.That(context.SaveError, Is.Null);
    }

    [Test]
    public async Task SaveAsync_RunsParticipantsAfterLibrary()
    {
        var store = new InMemoryStore<LearnLibrary>(new LearnLibrary());
        var context = new LibraryContext(TestData.Library(), store);
        var libraryCountSeen = -1;
        context.AddSaveParticipant(() =>
        {
            libraryCountSeen = store.SaveCount;
            return Task.CompletedTask;
        });

        await context.SaveAsync();

        Assert.That(libraryCountSeen, Is.EqualTo(1));
    }

    [Test]
    public void RenameTopic_RenamesAndRaisesEvent()
    {
        var context = new LibraryContext(TestData.Library(), new InMemoryStore<LearnLibrary>(new LearnLibrary()));
        TopicRenamedEventArgs? args = null;
        context.TopicRenamed += (_, e) => args = e;

        context.RenameTopic("city", " town ");

        Assert.That(context.Library.Topics, Does.Contain("town"));
        Assert.That((args!.OldName, args.NewName), Is.EqualTo(("city", "town")));
    }

    [Test]
    public void RenameTopic_Invalid_ThrowsWithoutEvent()
    {
        var context = new LibraryContext(TestData.Library(), new InMemoryStore<LearnLibrary>(new LearnLibrary()));
        var raised = false;
        context.TopicRenamed += (_, _) => raised = true;

        Assert.That(() => context.RenameTopic("city", "animals"), Throws.TypeOf<LibraryValidationException>());
        Assert.That(raised, Is.False);
    }

    [Test]
    public void SaveAsync_UnexpectedFailure_Propagates()
    {
        var store = new InMemoryStore<LearnLibrary>(new LearnLibrary()) { SaveException = new InvalidOperationException() };
        var context = new LibraryContext(TestData.Library(), store);

        Assert.That(context.SaveAsync, Throws.InvalidOperationException);
    }
}
