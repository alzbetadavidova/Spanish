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

    [Test]
    public void NewAdjective_StartsEmptyWithNounChips()
    {
        var editor = Create();

        Assert.Multiple(() =>
        {
            Assert.That(editor.IsNew, Is.True);
            Assert.That(editor.Title, Is.EqualTo("New adjective"));
            Assert.That(editor.Feminine, Is.Empty);
            Assert.That(editor.MasculinePlural, Is.Empty);
            Assert.That(editor.FemininePlural, Is.Empty);
            Assert.That(editor.HasNouns, Is.True);
            Assert.That(editor.Nouns.Select(n => n.Label), Is.EqualTo(new[] { "ciudad", "mujer", "perro" }));
            Assert.That(editor.Nouns.Any(n => n.IsSelected), Is.False);
        });
    }

    [Test]
    public void ExistingAdjective_LoadsLinksAndSuggestsMissingForms()
    {
        var bajo = _library.Adjectives[0];
        bajo.MasculinePluralValue = "bajitos";

        var editor = Create(bajo);

        Assert.Multiple(() =>
        {
            Assert.That(editor.Title, Is.EqualTo("Edit adjective"));
            Assert.That(editor.Spanish, Is.EqualTo("bajo"));
            Assert.That(editor.Feminine, Is.EqualTo("baja"));
            Assert.That(editor.MasculinePlural, Is.EqualTo("bajitos"));
            Assert.That(editor.FemininePlural, Is.EqualTo("bajas"));
            Assert.That(editor.Nouns.Where(n => n.IsSelected).Select(n => n.Value), Is.EqualTo(new[] { "mujer", "perro" }));
        });
    }

    [Test]
    public void Forms_FollowSpanishUntilTyped()
    {
        var editor = Create();

        editor.Spanish = "alemán";
        Assert.That(new[] { editor.Feminine, editor.MasculinePlural, editor.FemininePlural },
            Is.EqualTo(new[] { "alemana", "alemanes", "alemanas" }));

        editor.MasculinePlural = "alemanotes";
        editor.Spanish = "bajo";
        Assert.That(new[] { editor.Feminine, editor.MasculinePlural, editor.FemininePlural },
            Is.EqualTo(new[] { "baja", "alemanotes", "bajas" }));

        editor.MasculinePlural = " ";
        editor.Spanish = "alto";
        Assert.That(editor.MasculinePlural, Is.EqualTo("altos"));
    }

    [Test]
    public void TypedFeminine_IsKeptAndDrivesFemininePlural()
    {
        var editor = Create();
        editor.Spanish = "español";

        editor.Feminine = "española";
        editor.Spanish = "españolito";

        Assert.Multiple(() =>
        {
            Assert.That(editor.Feminine, Is.EqualTo("española"));
            Assert.That(editor.FemininePlural, Is.EqualTo("españolas"));
            Assert.That(editor.MasculinePlural, Is.EqualTo("españolitos"));
        });

        editor.Feminine = string.Empty;
        Assert.That(editor.Feminine, Is.EqualTo("españolita"));
    }

    [Test]
    public void TypedFemininePlural_IsKeptUntilCleared()
    {
        var editor = Create();
        editor.Spanish = "joven";

        editor.FemininePlural = "jóvenes";
        editor.Feminine = "jovencita";
        Assert.That(editor.FemininePlural, Is.EqualTo("jóvenes"));

        editor.FemininePlural = string.Empty;
        editor.Spanish = "joven ";
        Assert.That(editor.FemininePlural, Is.EqualTo("jovencitas"));
    }

    [Test]
    public async Task Save_NewAdjective_BuildsTrimmedFormsAndLinks()
    {
        var editor = Create();
        editor.Spanish = " joven ";
        editor.English = " young ";
        editor.MasculinePlural = " jóvenes ";
        editor.FemininePlural = "jóvenes";
        editor.Nouns.Single(n => n.Value == "mujer").IsSelected = true;
        editor.Topics.Single(t => t.Value == "animals").IsSelected = true;

        await editor.SaveCommand.ExecuteAsync(null);

        var saved = _library.Adjectives.Single(a => a.BaseValue == "joven");
        Assert.Multiple(() =>
        {
            Assert.That(saved.Translation, Is.EqualTo("young"));
            Assert.That(saved.FeminineValue, Is.EqualTo("joven"));
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
    public async Task Save_NounDeletedMeanwhile_ShowsNounError()
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
    }

    [Test]
    public void RefreshTopics_UntouchedNounChipsFollowLinks_EditedChipsKeepUserChoice()
    {
        var bajo = _library.Adjectives[0];
        var editor = Create(bajo);
        editor.Nouns.Single(n => n.Value == "ciudad").IsSelected = true;

        bajo.LinkedNouns.Remove("perro");
        _library.Nouns.Add(new Noun { BaseValue = "árbol", Translation = "tree" });
        editor.RefreshTopics();

        Assert.Multiple(() =>
        {
            Assert.That(editor.Nouns.Select(n => n.Label), Is.EqualTo(new[] { "árbol", "ciudad", "mujer", "perro" }));
            Assert.That(editor.Nouns.Where(n => n.IsSelected).Select(n => n.Value), Is.EqualTo(new[] { "ciudad", "mujer" }));
        });
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
