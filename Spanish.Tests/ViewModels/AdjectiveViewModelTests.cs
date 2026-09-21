using Spanish.Core;
using Spanish.ViewModels;

namespace Spanish.Tests.ViewModels;

public class DisplayNamesTests
{
    [TestCase(WordKind.Noun, "noun", "nouns")]
    [TestCase(WordKind.Verb, "verb", "verbs")]
    [TestCase(WordKind.Adjective, "adjective", "adjectives")]
    [TestCase((WordKind)99, "word", "words")]
    public void Kind_SingularAndPlural(WordKind kind, string singular, string plural)
    {
        Assert.Multiple(() =>
        {
            Assert.That(DisplayNames.Singular(kind), Is.EqualTo(singular));
            Assert.That(DisplayNames.Plural(kind), Is.EqualTo(plural));
        });
    }

    [TestCase(ScenarioType.PairWithNoun, "Pair with noun")]
    [TestCase(ScenarioType.Gender, "Gender")]
    public void Scenario_Label(ScenarioType type, string expected)
    {
        Assert.That(DisplayNames.Scenario(type), Is.EqualTo(expected));
    }
}

public class AdjectiveEditorViewModelTests
{
    private LearnLibrary _library = null!;
    private InMemoryStore<LearnLibrary> _store = null!;
    private LibraryContext _context = null!;
    private readonly List<string> _events = [];
    private EditorCallbacks _callbacks = null!;

    [SetUp]
    public void Setup()
    {
        _library = TestData.AdjectiveLibrary();
        _store = new InMemoryStore<LearnLibrary>(_library);
        _context = new LibraryContext(_library, _store);
        _events.Clear();
        _callbacks = new EditorCallbacks(u => _events.Add($"saved:{u.BaseValue}"), () => _events.Add("deleted"), () => _events.Add("cancelled"));
    }

    private AdjectiveEditorViewModel Create(Adjective? existing = null) => new(_context, existing, _callbacks);

    private static AdjectiveForms Typed(AdjectiveEditorViewModel editor) =>
        new(editor.Feminine, editor.MasculinePlural, editor.FemininePlural);

    [Test]
    public void NewAdjective_StartsEmptyWithNounChips()
    {
        var editor = Create();

        Assert.Multiple(() =>
        {
            Assert.That(editor.IsNew, Is.True);
            Assert.That(editor.Title, Is.EqualTo("New adjective"));
            Assert.That(Typed(editor), Is.EqualTo(new AdjectiveForms("", "", "")));
            Assert.That(editor.Suggested, Is.EqualTo(new AdjectiveForms("", "", "")));
            Assert.That(editor.HasNouns, Is.True);
            Assert.That(editor.Nouns.Select(n => n.Label), Is.EqualTo(new[] { "ciudad", "mujer", "perro" }));
            Assert.That(editor.Nouns.Any(n => n.IsSelected), Is.False);
        });
    }

    [Test]
    public void ExistingAdjective_LoadsStoredFormsAndLinks()
    {
        var bajo = _library.Adjectives[0];
        bajo.MasculinePluralValue = "bajitos";

        var editor = Create(bajo);

        Assert.Multiple(() =>
        {
            Assert.That(editor.Title, Is.EqualTo("Edit adjective"));
            Assert.That(editor.Spanish, Is.EqualTo("bajo"));
            Assert.That(Typed(editor), Is.EqualTo(new AdjectiveForms("", "bajitos", "")));
            Assert.That(editor.Suggested, Is.EqualTo(new AdjectiveForms("baja", "bajos", "bajas")));
            Assert.That(editor.Nouns.Where(n => n.IsSelected).Select(n => n.Value), Is.EqualTo(new[] { "mujer", "perro" }));
        });
    }

    [Test]
    public void Suggested_FollowsSpanishAndTypedFeminine()
    {
        var editor = Create();
        var changed = new List<string?>();
        editor.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        editor.Spanish = "español";
        Assert.That(editor.Suggested, Is.EqualTo(new AdjectiveForms("español", "españoles", "españoles")));

        editor.Feminine = "española";
        Assert.Multiple(() =>
        {
            Assert.That(editor.Suggested.FemininePlural, Is.EqualTo("españolas"));
            Assert.That(changed.Count(c => c == nameof(AdjectiveEditorViewModel.Suggested)), Is.EqualTo(2));
        });

        // Clearing a box keeps it empty; the suggestion is only shown as its placeholder.
        editor.Feminine = string.Empty;
        Assert.Multiple(() =>
        {
            Assert.That(editor.Feminine, Is.Empty);
            Assert.That(editor.Suggested.FemininePlural, Is.EqualTo("españoles"));
        });
    }

    [Test]
    public async Task Save_TypedFormsAreFixes_EmptyOrSuggestedFormsAreNotStored()
    {
        var editor = Create();
        editor.Spanish = " joven ";
        editor.English = " young ";
        editor.Feminine = " joven "; // same as the suggestion
        editor.MasculinePlural = " jóvenes ";
        editor.FemininePlural = "jóvenes";
        editor.Nouns.Single(n => n.Value == "mujer").IsSelected = true;
        editor.Topics.Single(t => t.Value == "animals").IsSelected = true;

        await editor.SaveCommand.ExecuteAsync(null);

        var saved = _library.Adjectives.Single(a => a.BaseValue == "joven");
        Assert.Multiple(() =>
        {
            Assert.That(saved.Translation, Is.EqualTo("young"));
            Assert.That(saved.FeminineValue, Is.Empty);
            Assert.That(saved.MasculinePluralValue, Is.EqualTo("jóvenes"));
            Assert.That(saved.FemininePluralValue, Is.EqualTo("jóvenes"));
            Assert.That(saved.LinkedNouns, Is.EqualTo(new[] { "mujer" }));
            Assert.That(saved.Topics, Is.EqualTo(new[] { "animals" }));
            Assert.That(_store.SaveCount, Is.EqualTo(1));
            Assert.That(_events, Is.EqualTo(new[] { "saved:joven" }));
            Assert.That(editor.NounsError, Is.Null);
        });
    }

    [Test]
    public async Task Save_FemininePluralDerivedFromTypedFeminine_IsNotStored()
    {
        var editor = Create();
        editor.Spanish = "español";
        editor.English = "Spanish";
        editor.Feminine = "española";
        editor.FemininePlural = "españolas";

        await editor.SaveCommand.ExecuteAsync(null);

        var saved = _library.Adjectives.Single(a => a.BaseValue == "español");
        Assert.Multiple(() =>
        {
            Assert.That(saved.FeminineValue, Is.EqualTo("española"));
            Assert.That(saved.FemininePluralValue, Is.Empty);
            Assert.That(saved.GetForm(Gender.Feminine, true), Is.EqualTo("españolas"));
        });
    }

    [Test]
    public async Task SavedAdjective_Reopened_FormsStillFollowSpanish()
    {
        var editor = Create();
        editor.Spanish = "bjo"; // a typo, fixed after reopening
        editor.English = "short";
        await editor.SaveCommand.ExecuteAsync(null);
        var saved = _library.Adjectives.Single(a => a.BaseValue == "bjo");

        var reopened = Create(saved);
        reopened.Spanish = "alto";
        await reopened.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(Typed(reopened), Is.EqualTo(new AdjectiveForms("", "", "")));
            Assert.That(reopened.Suggested, Is.EqualTo(new AdjectiveForms("alta", "altos", "altas")));
            Assert.That(saved.GetForm(Gender.Feminine, true), Is.EqualTo("altas"));
        });
    }

    [Test]
    public async Task Save_NounDeletedMeanwhile_ShowsNounErrorUntilChipToggled()
    {
        var editor = Create();
        editor.Spanish = "alto";
        editor.English = "tall";
        editor.Nouns.Single(n => n.Value == "ciudad").IsSelected = true;
        _library.Nouns.RemoveAt(0);

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(editor.NounsError, Is.EqualTo("Unknown noun: ciudad."));
            Assert.That(_store.SaveCount, Is.Zero);
            Assert.That(_events, Is.Empty);
        });

        editor.Nouns.Single(n => n.Value == "ciudad").IsSelected = false;
        Assert.That(editor.NounsError, Is.Null);
    }

    [Test]
    public void RefreshChoices_UntouchedNounChipsFollowLinks_EditedChipsKeepUserChoice()
    {
        var bajo = _library.Adjectives[0];
        var editor = Create(bajo);
        editor.Nouns.Single(n => n.Value == "ciudad").IsSelected = true;

        bajo.LinkedNouns.Remove("perro");
        _library.Nouns.Add(new Noun { BaseValue = "árbol", Translation = "tree" });
        editor.RefreshChoices();

        Assert.Multiple(() =>
        {
            Assert.That(editor.Nouns.Select(n => n.Label), Is.EqualTo(new[] { "árbol", "ciudad", "mujer", "perro" }));
            Assert.That(editor.Nouns.Where(n => n.IsSelected).Select(n => n.Value), Is.EqualTo(new[] { "ciudad", "mujer" }));
        });
    }

    [Test]
    public void RefreshChoices_ReplacedChipsNoLongerClearTheError()
    {
        var editor = Create();
        var oldChip = editor.Nouns[0];
        editor.RefreshChoices();
        editor.NounsError = "Unknown noun: ciudad.";

        oldChip.IsSelected = true;

        Assert.That(editor.NounsError, Is.EqualTo("Unknown noun: ciudad."));
    }

    [Test]
    public void DuplicateNounsInFile_ShowOneChip()
    {
        _library.Nouns.Add(TestData.Mujer());

        var editor = Create();

        Assert.That(editor.Nouns.Select(n => n.Value), Is.EqualTo(new[] { "ciudad", "mujer", "perro" }));
    }

    [Test]
    public void NoNouns_HasNounsIsFalse()
    {
        _library.Nouns.Clear();

        Assert.That(Create().HasNouns, Is.False);
    }
}

public class AdjectiveLibraryPagesTests
{
    private LearnLibrary _library = null!;
    private LibraryContext _context = null!;

    [SetUp]
    public void Setup()
    {
        _library = TestData.AdjectiveLibrary();
        _context = new LibraryContext(_library, new InMemoryStore<LearnLibrary>(_library));
    }

    [Test]
    public void WordList_Adjectives_UseAdjectiveLabelsAndEditor()
    {
        var list = new WordListViewModel(_context, WordKind.Adjective);

        Assert.Multiple(() =>
        {
            Assert.That(list.Items.Select(u => u.BaseValue), Is.EqualTo(new[] { "bajo" }));
            Assert.That(list.CountText, Is.EqualTo("1 adjective"));
            Assert.That(list.AddLabel, Is.EqualTo("Add adjective"));
            Assert.That(list.SearchPlaceholder, Is.EqualTo("Search adjectives"));
        });

        list.Selected = list.Items[0];
        Assert.That(list.Editor, Is.TypeOf<AdjectiveEditorViewModel>());

        _library.Adjectives.Add(new Adjective { BaseValue = "alto", Translation = "tall" });
        list.Refresh();
        Assert.That(list.CountText, Is.EqualTo("2 adjectives"));
    }

    [Test]
    public async Task NounDeletedOnNounsTab_OpenAdjectiveEditorDropsItsChip()
    {
        var page = new LibraryViewModel(_context);
        page.OnActivated();
        page.Adjectives.Selected = page.Adjectives.Items.Single();
        var adjectiveEditor = (AdjectiveEditorViewModel)page.Adjectives.Editor!;

        page.Nouns.Selected = page.Nouns.Items.Single(u => u.BaseValue == "mujer");
        await page.Nouns.Editor!.DeleteCommand.ExecuteAsync(null); // asks for confirmation
        await page.Nouns.Editor!.DeleteCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(page.Adjectives.Editor, Is.SameAs(adjectiveEditor));
            Assert.That(adjectiveEditor.Nouns.Select(n => n.Value), Is.EqualTo(new[] { "ciudad", "perro" }));
            Assert.That(adjectiveEditor.Nouns.Where(n => n.IsSelected).Select(n => n.Value), Is.EqualTo(new[] { "perro" }));
        });
    }

    [Test]
    public void Topics_WordChipsNameTheKind()
    {
        var topics = new TopicsViewModel(_context) { SelectedTopic = "animals" };

        Assert.That(topics.Words.Select(w => w.Label), Does.Contain("bajo · adjective"));
    }

    [Test]
    public void Stats_AdjectiveRowsAndFilter()
    {
        _library.Adjectives[0].RecordAnswer(ScenarioType.PairWithNoun, true, new FakeClock().Now);
        var stats = new StatsViewModel(_context, new FakeClock());

        stats.SelectedKind = StatsViewModel.KindFilters.Single(k => k.Value == WordKind.Adjective);

        var row = stats.Rows.Single();
        Assert.Multiple(() =>
        {
            Assert.That(StatsViewModel.KindFilters.Select(k => k.Label), Does.Contain("Adjectives"));
            Assert.That(row.Word, Is.EqualTo("bajo"));
            Assert.That(row.Kind, Is.EqualTo("adjective"));
            Assert.That(row.Breakdown, Is.EqualTo("Pair with noun 100"));
        });
    }

    [Test]
    public void SessionSettings_LoadsAndAppliesAdjectiveChoices()
    {
        var settings = new SessionSettings
        {
            IncludeNouns = false,
            IncludeVerbs = false,
            IncludeNumerals = false,
            AdjectiveScenarioTypes = [ScenarioType.PairWithNoun]
        };

        var vm = new SessionSettingsViewModel(_library, settings);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IncludeAdjectives, Is.True);
            Assert.That(vm.AdjectiveScenarios.Select(s => s.Label), Is.EqualTo(new[] { "Card", "Fill", "Pair with noun" }));
            Assert.That(vm.AdjectiveScenarios.Where(s => s.IsSelected).Select(s => s.Value), Is.EqualTo(new[] { ScenarioType.PairWithNoun }));
            Assert.That(vm.MatchCount, Is.EqualTo(1));
        });

        vm.AdjectiveScenarios.Single(s => s.Value == ScenarioType.Card).IsSelected = true;
        Assert.That(vm.MatchCount, Is.EqualTo(2));

        vm.IncludeAdjectives = false;
        var applied = vm.ToSettings();
        Assert.Multiple(() =>
        {
            Assert.That(vm.MatchCount, Is.Zero);
            Assert.That(applied.IncludeAdjectives, Is.False);
            Assert.That(applied.AdjectiveScenarioTypes, Is.EqualTo(new[] { ScenarioType.Card, ScenarioType.PairWithNoun }));
        });
    }
}
