using Spanish.Core;
using Spanish.ViewModels;

namespace Spanish.Tests.ViewModels;

public class PrepositionEditorViewModelTests
{
    private LearnLibrary _library = null!;
    private InMemoryStore<LearnLibrary> _store = null!;
    private LibraryContext _context = null!;
    private readonly List<string> _events = [];
    private EditorCallbacks _callbacks = null!;

    [SetUp]
    public void Setup()
    {
        _library = TestData.PrepositionLibrary();
        _store = new InMemoryStore<LearnLibrary>(_library);
        _context = new LibraryContext(_library, _store);
        _events.Clear();
        _callbacks = new EditorCallbacks(u => _events.Add($"saved:{u.BaseValue}"), () => _events.Add("deleted"), () => _events.Add("cancelled"));
    }

    private PrepositionEditorViewModel Create(Preposition? existing = null) => new(_context, existing, _callbacks);

    private static IEnumerable<string> SelectedNouns(NounLinkedEditorViewModel editor) =>
        editor.Nouns.Where(n => n.IsSelected).Select(n => n.Value);

    [Test]
    public void NewPreposition_StartsEmptyWithNounChips()
    {
        var editor = Create();

        Assert.Multiple(() =>
        {
            Assert.That(editor.IsNew, Is.True);
            Assert.That(editor.Title, Is.EqualTo("New preposition"));
            Assert.That(editor.Spanish, Is.Empty);
            Assert.That(editor.HasNouns, Is.True);
            Assert.That(editor.Nouns.Select(n => n.Label), Is.EqualTo(new[] { "agua", "ciudad", "perro" }));
            Assert.That(SelectedNouns(editor), Is.Empty);
        });
    }

    [Test]
    public void ExistingPreposition_LoadsWordAndLinks()
    {
        var editor = Create(_library.Prepositions[0]);

        Assert.Multiple(() =>
        {
            Assert.That(editor.Title, Is.EqualTo("Edit preposition"));
            Assert.That(editor.Spanish, Is.EqualTo("de"));
            Assert.That(editor.English, Is.EqualTo("of; from"));
            Assert.That(SelectedNouns(editor), Is.EqualTo(new[] { "ciudad", "perro" }));
            Assert.That(editor.HasExceptionNotes, Is.False);
        });
    }

    [Test]
    public async Task Save_NewPreposition_StoresLinksAndTopics()
    {
        var editor = Create();
        editor.Spanish = " cerca de ";
        editor.English = " near; close to ";
        editor.Nouns.Single(n => n.Value == "agua").IsSelected = true;
        editor.Topics.Single(t => t.Value == "city").IsSelected = true;

        await editor.SaveCommand.ExecuteAsync(null);

        var saved = _library.Prepositions.Single(p => p.BaseValue == "cerca de");
        Assert.Multiple(() =>
        {
            Assert.That(saved.Translation, Is.EqualTo("near; close to"));
            Assert.That(saved.LinkedNouns, Is.EqualTo(new[] { "agua" }));
            Assert.That(saved.Topics, Is.EqualTo(new[] { "city" }));
            Assert.That(_store.SaveCount, Is.EqualTo(1));
            Assert.That(_events, Is.EqualTo(new[] { "saved:cerca de" }));
        });
    }

    [Test]
    public async Task Save_ExistingPreposition_UpdatesLinks()
    {
        var de = _library.Prepositions[0];
        var editor = Create(de);
        editor.Nouns.Single(n => n.Value == "perro").IsSelected = false;

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(de.LinkedNouns, Is.EqualTo(new[] { "ciudad" }));
            Assert.That(_events, Is.EqualTo(new[] { "saved:de" }));
        });
    }

    [Test]
    public async Task Save_NounDeletedMeanwhile_ShowsNounError()
    {
        var editor = Create();
        editor.Spanish = "hacia";
        editor.English = "towards";
        editor.Nouns.Single(n => n.Value == "agua").IsSelected = true;
        _library.Nouns.RemoveAt(2);

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(editor.NounsError, Is.EqualTo("Unknown noun: agua."));
            Assert.That(_store.SaveCount, Is.Zero);
            Assert.That(_events, Is.Empty);
        });
    }

    [Test]
    public async Task Save_Duplicate_ShowsSpanishError()
    {
        var editor = Create();
        editor.Spanish = "Desde";
        editor.English = "from";

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.That(editor.SpanishError, Is.EqualTo("\"Desde\" is already in your library."));
    }
}

public class PrepositionLibraryPagesTests
{
    private LearnLibrary _library = null!;
    private LibraryContext _context = null!;

    [SetUp]
    public void Setup()
    {
        _library = TestData.PrepositionLibrary();
        _context = new LibraryContext(_library, new InMemoryStore<LearnLibrary>(_library));
    }

    [Test]
    public void WordList_Prepositions_UsePrepositionLabelsAndEditor()
    {
        var list = new WordListViewModel(_context, WordKind.Preposition);

        Assert.Multiple(() =>
        {
            Assert.That(list.Items.Select(u => u.BaseValue), Is.EqualTo(new[] { "de", "desde" }));
            Assert.That(list.CountText, Is.EqualTo("2 prepositions"));
            Assert.That(list.AddLabel, Is.EqualTo("Add preposition"));
            Assert.That(list.SearchPlaceholder, Is.EqualTo("Search prepositions"));
        });

        list.Selected = list.Items[1];
        Assert.That(list.Editor, Is.TypeOf<PrepositionEditorViewModel>());

        list.SearchText = "since";
        Assert.That(list.CountText, Is.EqualTo("1 preposition"));
    }

    [Test]
    public async Task NounDeletedOnNounsTab_RefreshesPrepositionTab()
    {
        var page = new LibraryViewModel(_context);
        page.OnActivated();
        page.Prepositions.Selected = page.Prepositions.Items.Single(u => u.BaseValue == "de");
        var editor = (PrepositionEditorViewModel)page.Prepositions.Editor!;
        _library.Prepositions.Add(new Preposition { BaseValue = "hacia", Translation = "towards" });

        page.Nouns.Selected = page.Nouns.Items.Single(u => u.BaseValue == "perro");
        await page.Nouns.Editor!.DeleteCommand.ExecuteAsync(null); // asks for confirmation
        await page.Nouns.Editor!.DeleteCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(page.Prepositions.Editor, Is.SameAs(editor));
            Assert.That(page.Prepositions.CountText, Is.EqualTo("3 prepositions"));
            Assert.That(editor.Nouns.Select(n => n.Value), Is.EqualTo(new[] { "agua", "ciudad" }));
            Assert.That(editor.Nouns.Where(n => n.IsSelected).Select(n => n.Value), Is.EqualTo(new[] { "ciudad" }));
        });
    }

    [Test]
    public void Topics_WordChipsNameTheKind()
    {
        var topics = new TopicsViewModel(_context) { SelectedTopic = "animals" };

        Assert.That(topics.Words.Single(w => w.IsSelected && w.Value is Preposition).Label, Is.EqualTo("de · preposition"));
    }

    [Test]
    public void Stats_PrepositionRowsAndFilter()
    {
        _library.Prepositions[0].RecordAnswer(ScenarioType.PairWithNoun, true, new FakeClock().Now);
        var stats = new StatsViewModel(_context, new FakeClock());

        stats.SelectedKind = StatsViewModel.KindFilters.Single(k => k.Value == WordKind.Preposition);

        Assert.Multiple(() =>
        {
            Assert.That(StatsViewModel.KindFilters.Select(k => k.Label), Does.Contain("Prepositions"));
            Assert.That(stats.Rows.Select(r => r.Word), Is.EqualTo(new[] { "desde", "de" }));
            Assert.That(stats.Rows[1].Kind, Is.EqualTo("preposition"));
            Assert.That(stats.Rows[1].Breakdown, Is.EqualTo("Pair with noun 100"));
        });
    }

    [Test]
    public void SessionSettings_LoadsAndAppliesPrepositionChoices()
    {
        var settings = new SessionSettings
        {
            IncludeNouns = false,
            IncludeVerbs = false,
            IncludeAdjectives = false,
            IncludeNumerals = false,
            PrepositionScenarioTypes = [ScenarioType.PairWithNoun]
        };

        var vm = new SessionSettingsViewModel(_library, settings);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IncludePrepositions, Is.True);
            Assert.That(vm.PrepositionScenarios.Select(s => s.Label), Is.EqualTo(new[] { "Card", "Fill", "Pair with noun" }));
            Assert.That(vm.PrepositionScenarios.Where(s => s.IsSelected).Select(s => s.Value), Is.EqualTo(new[] { ScenarioType.PairWithNoun }));
            Assert.That(vm.MatchCount, Is.EqualTo(2));
        });

        vm.PrepositionScenarios.Single(s => s.Value == ScenarioType.Card).IsSelected = true;
        Assert.That(vm.MatchCount, Is.EqualTo(4));

        vm.IncludePrepositions = false;
        var applied = vm.ToSettings();
        Assert.Multiple(() =>
        {
            Assert.That(vm.MatchCount, Is.Zero);
            Assert.That(applied.IncludePrepositions, Is.False);
            Assert.That(applied.PrepositionScenarioTypes, Is.EqualTo(new[] { ScenarioType.Card, ScenarioType.PairWithNoun }));
        });
    }

    [Test]
    public async Task Learn_PracticesPrepositionPairAndSavesProgress()
    {
        var store = new InMemoryStore<LearnLibrary>(_library);
        var context = new LibraryContext(_library, store);
        var settings = new SessionSettings
        {
            IncludeNouns = false,
            IncludeVerbs = false,
            IncludeAdjectives = false,
            IncludeNumerals = false,
            PrepositionScenarioTypes = [ScenarioType.PairWithNoun]
        };
        var learn = new LearnViewModel(context, new InMemoryStore<SessionSettings>(settings), settings, new FakeRandom(), new FakeClock());

        var typed = (TypedScenarioViewModel)learn.CurrentScenario!;
        Assert.Multiple(() =>
        {
            Assert.That(learn.SummaryChips[0], Is.EqualTo("Prepositions"));
            Assert.That(typed.Heading, Is.EqualTo(ScenarioFactory.PrepositionPairInstruction));
            Assert.That(typed.Prompt, Is.EqualTo("of the dog"));
            Assert.That(typed.PromptDetail, Is.EqualTo("perro"));
        });

        typed.Answer = "de el perro";
        await typed.SubmitCommand.ExecuteAsync(null);
        Assert.That(typed.Feedback, Is.EqualTo("The answer is del perro"));

        await typed.SubmitCommand.ExecuteAsync(null);
        Assert.Multiple(() =>
        {
            Assert.That(_library.Prepositions[0].GetProgress(ScenarioType.PairWithNoun)!.Attempts, Is.EqualTo(1));
            Assert.That(store.SaveCount, Is.EqualTo(1));
        });
    }
}
