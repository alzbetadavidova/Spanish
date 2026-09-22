using Spanish.Core;
using Spanish.ViewModels;

namespace Spanish.Tests.ViewModels;

public class WordEditorViewModelTests
{
    private LearnLibrary _library = null!;
    private InMemoryStore<LearnLibrary> _store = null!;
    private LibraryContext _context = null!;
    private readonly List<string> _events = [];
    private EditorCallbacks _callbacks = null!;

    [SetUp]
    public void Setup()
    {
        _library = TestData.Library();
        _store = new InMemoryStore<LearnLibrary>(_library);
        _context = new LibraryContext(_library, _store);
        _events.Clear();
        _callbacks = new EditorCallbacks(u => _events.Add($"saved:{u.BaseValue}"), () => _events.Add("deleted"), () => _events.Add("cancelled"));
    }

    [Test]
    public void NewNoun_StartsEmptyWithTopics()
    {
        var editor = new NounEditorViewModel(_context, null, _callbacks);

        Assert.Multiple(() =>
        {
            Assert.That(editor.IsNew, Is.True);
            Assert.That(editor.Title, Is.EqualTo("New noun"));
            Assert.That(editor.Spanish, Is.Empty);
            Assert.That(editor.IsMasculine, Is.True);
            Assert.That(editor.HasTopics, Is.True);
            Assert.That(editor.Topics.Select(t => t.Label), Is.EqualTo(new[] { "animals", "city" }));
            Assert.That(editor.Topics.Any(t => t.IsSelected), Is.False);
            Assert.That(editor.DeleteCommand.CanExecute(null), Is.False);
            Assert.That(editor.DeleteLabel, Is.EqualTo("Delete"));
        });
    }

    [Test]
    public void NewNoun_SuggestsPluralUntilUserEditsIt()
    {
        var editor = new NounEditorViewModel(_context, null, _callbacks);

        editor.Spanish = "canción";
        Assert.That(editor.Plural, Is.EqualTo("canciones"));

        editor.Plural = "cancioncitas";
        editor.Spanish = "luz";
        Assert.That(editor.Plural, Is.EqualTo("cancioncitas"));

        editor.Plural = string.Empty;
        editor.Spanish = "luz ";
        Assert.That(editor.Plural, Is.EqualTo("luces"));
    }

    [Test]
    public void ExistingNoun_LoadsFieldsAndKeepsPlural()
    {
        var ciudad = _library.Nouns[0];
        var editor = new NounEditorViewModel(_context, ciudad, _callbacks);

        editor.Spanish = "urbe";

        Assert.Multiple(() =>
        {
            Assert.That(editor.Title, Is.EqualTo("Edit noun"));
            Assert.That(editor.English, Is.EqualTo("city; town"));
            Assert.That(editor.IsFeminine, Is.True);
            Assert.That(editor.Plural, Is.EqualTo("ciudades"));
            Assert.That(editor.Topics.Single(t => t.IsSelected).Value, Is.EqualTo("city"));
        });
    }

    [Test]
    public void Gender_ClickingSelectedToggleKeepsGender()
    {
        var editor = new NounEditorViewModel(_context, null, _callbacks);
        var changed = new List<string?>();
        editor.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        editor.IsMasculine = false;
        Assert.That(editor.IsMasculine, Is.True);
        Assert.That(changed, Is.EqualTo(new[] { nameof(NounEditorViewModel.IsMasculine) }));

        editor.IsFeminine = true;
        Assert.That(editor.Gender, Is.EqualTo(Gender.Feminine));

        editor.IsFeminine = false;
        editor.IsMasculine = true;
        Assert.That(editor.Gender, Is.EqualTo(Gender.Masculine));
    }

    [Test]
    public async Task Save_Invalid_ShowsErrorsWithoutSaving()
    {
        var editor = new NounEditorViewModel(_context, null, _callbacks);
        editor.Spanish = "perro";

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(editor.SpanishError, Is.EqualTo("\"perro\" is already in your library."));
            Assert.That(editor.EnglishError, Is.EqualTo("Enter the English translation."));
            Assert.That(editor.TopicsError, Is.Null);
            Assert.That(_store.SaveCount, Is.Zero);
            Assert.That(_events, Is.Empty);
        });

        editor.Spanish = "gato";
        editor.English = "cat";
        Assert.That(editor.SpanishError, Is.Null);
        Assert.That(editor.EnglishError, Is.Null);
    }

    [Test]
    public async Task Save_TopicDeletedMeanwhile_ShowsTopicError()
    {
        var editor = new NounEditorViewModel(_context, null, _callbacks);
        editor.Spanish = "mesa";
        editor.English = "table";
        editor.Topics.Single(t => t.Value == "animals").IsSelected = true;
        _library.RemoveTopic("animals");

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.That(editor.TopicsError, Is.EqualTo("Unknown topic: animals."));
    }

    [Test]
    public async Task Save_NewNoun_AddsAndSaves()
    {
        var editor = new NounEditorViewModel(_context, null, _callbacks);
        editor.Spanish = "mesa";
        editor.English = " table ";
        editor.IsFeminine = true;
        editor.Plural = " mesas ";
        editor.Topics.Single(t => t.Value == "city").IsSelected = true;

        await editor.SaveCommand.ExecuteAsync(null);

        var mesa = _library.Nouns.Single(n => n.BaseValue == "mesa");
        Assert.Multiple(() =>
        {
            Assert.That(mesa.Translation, Is.EqualTo("table"));
            Assert.That(mesa.Gender, Is.EqualTo(Gender.Feminine));
            Assert.That(mesa.PluralValue, Is.EqualTo("mesas"));
            Assert.That(mesa.Topics, Is.EqualTo(new[] { "city" }));
            Assert.That(_store.SaveCount, Is.EqualTo(1));
            Assert.That(_events, Is.EqualTo(new[] { "saved:mesa" }));
        });
    }

    [Test]
    public async Task Save_ExistingNoun_UpdatesInPlace()
    {
        var perro = _library.Nouns[1];
        var editor = new NounEditorViewModel(_context, perro, _callbacks);
        editor.English = "dog; hound";

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.That(perro.Translation, Is.EqualTo("dog; hound"));
        Assert.That(_events, Is.EqualTo(new[] { "saved:perro" }));
    }

    [Test]
    public async Task Save_EditToOtherExistingWord_ShowsErrorAndKeepsWord()
    {
        var ciudad = _library.Nouns[0];
        var editor = new NounEditorViewModel(_context, ciudad, _callbacks);
        editor.Spanish = "perro";

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(editor.SpanishError, Is.EqualTo("\"perro\" is already in your library."));
            Assert.That(ciudad.BaseValue, Is.EqualTo("ciudad"));
            Assert.That(_store.SaveCount, Is.Zero);
        });
    }

    [Test]
    public void RefreshTopics_UntouchedChipsFollowWord_EditedChipsKeepUserChoice()
    {
        _library.Topics.Add("food");
        var ciudad = _library.Nouns[0];
        var editor = new NounEditorViewModel(_context, ciudad, _callbacks);
        editor.Topics.Single(t => t.Value == "animals").IsSelected = true;

        // Changed elsewhere (e.g. on the Topics tab): ciudad joins food and leaves city.
        _library.SetTopicMembership("food", ciudad, true);
        _library.SetTopicMembership("city", ciudad, false);
        _library.AddTopic("home");
        editor.RefreshTopics();

        Assert.That(editor.Topics.Select(t => (t.Value, t.IsSelected)), Is.EqualTo(new[]
        {
            ("animals", true), ("city", false), ("food", true), ("home", false)
        }));
    }

    [Test]
    public async Task Delete_AsksForConfirmationFirst()
    {
        var perro = _library.Nouns[1];
        var editor = new NounEditorViewModel(_context, perro, _callbacks);

        await editor.DeleteCommand.ExecuteAsync(null);
        Assert.Multiple(() =>
        {
            Assert.That(editor.IsConfirmingDelete, Is.True);
            Assert.That(editor.DeleteLabel, Is.EqualTo("Confirm delete"));
            Assert.That(_library.Nouns, Does.Contain(perro));
        });

        await editor.DeleteCommand.ExecuteAsync(null);
        Assert.Multiple(() =>
        {
            Assert.That(_library.Nouns, Does.Not.Contain(perro));
            Assert.That(_store.SaveCount, Is.EqualTo(1));
            Assert.That(_events, Is.EqualTo(new[] { "deleted" }));
        });
    }

    [Test]
    public async Task Delete_NewWord_NeverRemovesAnything()
    {
        var editor = new NounEditorViewModel(_context, null, _callbacks);

        await editor.DeleteCommand.ExecuteAsync(null);
        await editor.DeleteCommand.ExecuteAsync(null);

        Assert.That(_store.SaveCount, Is.Zero);
        Assert.That(_events, Is.Empty);
    }

    [Test]
    public void Cancel_NotifiesOwner()
    {
        new NounEditorViewModel(_context, null, _callbacks).CancelCommand.Execute(null);

        Assert.That(_events, Is.EqualTo(new[] { "cancelled" }));
    }

    [Test]
    public void NoTopics_HasTopicsIsFalse()
    {
        _library.Topics.Clear();

        Assert.That(new VerbEditorViewModel(_context, null, _callbacks).HasTopics, Is.False);
    }

    [Test]
    public void NewVerb_HasEmptyConjugationRows()
    {
        var editor = new VerbEditorViewModel(_context, null, _callbacks);

        Assert.Multiple(() =>
        {
            Assert.That(editor.Title, Is.EqualTo("New verb"));
            Assert.That(editor.Conjugations.Select(c => c.Person), Is.EqualTo(Verb.PersonLabels));
            Assert.That(editor.Conjugations.All(c => c.Present == "" && c.Preterite == ""), Is.True);
            Assert.That(editor.Gerund, Is.Empty);
        });
    }

    [Test]
    public void ExistingVerb_LoadsFormsAndPadsShortArrays()
    {
        var verb = _library.Verbs[0];
        verb.PreteriteConjugations = ["hablé"];

        var editor = new VerbEditorViewModel(_context, verb, _callbacks);

        Assert.Multiple(() =>
        {
            Assert.That(editor.Title, Is.EqualTo("Edit verb"));
            Assert.That(editor.Conjugations[3].Present, Is.EqualTo("hablamos"));
            Assert.That(editor.Conjugations[0].Preterite, Is.EqualTo("hablé"));
            Assert.That(editor.Conjugations[4].Preterite, Is.Empty);
            Assert.That(editor.Gerund, Is.EqualTo("hablando"));
        });
    }

    [Test]
    public async Task SaveVerb_NullForm_ShowsConjugationError()
    {
        var verb = _library.Verbs[0];
        verb.PresentConjugations = ["hablo", null!, "habla", "hablamos", "hablan"];
        var editor = new VerbEditorViewModel(_context, verb, _callbacks);

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.That(editor.Conjugations[1].Present, Is.Empty);
        Assert.That(editor.ConjugationError, Is.EqualTo("Fill in all 5 present forms or leave them all empty."));
    }

    [Test]
    public async Task SaveVerb_PartialForms_ShowsConjugationError()
    {
        var editor = new VerbEditorViewModel(_context, null, _callbacks);
        editor.Spanish = "comer";
        editor.English = "to eat";
        editor.Conjugations[0].Present = "como";
        editor.Conjugations[0].Preterite = "comí";

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.That(editor.ConjugationError, Is.EqualTo(
            "Fill in all 5 present forms or leave them all empty. Fill in all 5 preterite forms or leave them all empty."));
    }

    [Test]
    public async Task SaveVerb_Valid_BuildsTrimmedForms()
    {
        var editor = new VerbEditorViewModel(_context, null, _callbacks);
        editor.Spanish = "comer";
        editor.English = "to eat";
        string[] present = ["como", "comes", "come", "comemos", "comen"];
        for (var i = 0; i < present.Length; i++)
        {
            editor.Conjugations[i].Present = $" {present[i]} ";
        }
        editor.Gerund = " comiendo ";

        await editor.SaveCommand.ExecuteAsync(null);

        var comer = _library.Verbs.Single(v => v.BaseValue == "comer");
        Assert.Multiple(() =>
        {
            Assert.That(editor.ConjugationError, Is.Null);
            Assert.That(comer.PresentConjugations, Is.EqualTo(present));
            Assert.That(comer.PreteriteConjugations, Is.EqualTo(Verb.EmptyConjugations()));
            Assert.That(comer.NonPersonalGerund, Is.EqualTo("comiendo"));
        });
    }
}

public class WordListViewModelTests
{
    private LearnLibrary _library = null!;
    private LibraryContext _context = null!;

    [SetUp]
    public void Setup()
    {
        _library = TestData.Library();
        _library.Nouns.Add(new Noun { BaseValue = "árbol", Translation = "tree" });
        _context = new LibraryContext(_library, new InMemoryStore<LearnLibrary>(_library));
    }

    [Test]
    public void Nouns_SortedWithCountAndLabels()
    {
        var list = new WordListViewModel(_context, WordKind.Noun);

        Assert.Multiple(() =>
        {
            Assert.That(list.Items.Select(u => u.BaseValue), Is.EqualTo(new[] { "árbol", "ciudad", "perro" }));
            Assert.That(list.CountText, Is.EqualTo("3 nouns"));
            Assert.That(list.AddLabel, Is.EqualTo("Add noun"));
            Assert.That(list.SearchPlaceholder, Is.EqualTo("Search nouns"));
            Assert.That(list.HasEditor, Is.False);
        });
    }

    [Test]
    public void Verbs_UseVerbLabels()
    {
        var list = new WordListViewModel(_context, WordKind.Verb);

        Assert.Multiple(() =>
        {
            Assert.That(list.CountText, Is.EqualTo("1 verb"));
            Assert.That(list.AddLabel, Is.EqualTo("Add verb"));
            Assert.That(list.SearchPlaceholder, Is.EqualTo("Search verbs"));
        });
    }

    [TestCase("CIU", "ciudad")]
    [TestCase("dog", "perro")]
    public void Search_MatchesSpanishOrEnglish(string search, string expected)
    {
        var list = new WordListViewModel(_context, WordKind.Noun) { SearchText = search };

        Assert.That(list.Items.Single().BaseValue, Is.EqualTo(expected));
    }

    [Test]
    public void Select_CreatesEditorOfKind()
    {
        var nouns = new WordListViewModel(_context, WordKind.Noun);
        var verbs = new WordListViewModel(_context, WordKind.Verb);

        nouns.Selected = nouns.Items[1];
        verbs.Selected = verbs.Items[0];

        Assert.That(nouns.Editor, Is.TypeOf<NounEditorViewModel>());
        Assert.That(nouns.Editor!.Existing, Is.SameAs(_library.Nouns[0]));
        Assert.That(verbs.Editor, Is.TypeOf<VerbEditorViewModel>());
    }

    [Test]
    public void Add_ClearsSelectionAndOpensNewEditor()
    {
        var list = new WordListViewModel(_context, WordKind.Noun);
        list.Selected = list.Items[0];

        list.AddCommand.Execute(null);

        Assert.That(list.Selected, Is.Null);
        Assert.That(list.Editor!.IsNew, Is.True);
    }

    [Test]
    public async Task SavingNewWord_SelectsIt()
    {
        var list = new WordListViewModel(_context, WordKind.Noun);
        list.AddCommand.Execute(null);
        list.Editor!.Spanish = "mesa";
        list.Editor.English = "table";

        await list.Editor.SaveCommand.ExecuteAsync(null);

        Assert.That(list.Selected!.BaseValue, Is.EqualTo("mesa"));
        Assert.That(list.Editor!.Existing, Is.SameAs(list.Selected));
        Assert.That(list.CountText, Is.EqualTo("4 nouns"));
    }

    [Test]
    public async Task DeletingWord_ClosesEditor()
    {
        var list = new WordListViewModel(_context, WordKind.Noun);
        list.Selected = list.Items[0];

        await list.Editor!.DeleteCommand.ExecuteAsync(null);
        await list.Editor!.DeleteCommand.ExecuteAsync(null);

        Assert.That(list.Editor, Is.Null);
        Assert.That(list.Items, Has.Count.EqualTo(2));
    }

    [Test]
    public void Cancel_NewWord_ClosesEditor_ExistingWord_ResetsEditor()
    {
        var list = new WordListViewModel(_context, WordKind.Noun);
        list.AddCommand.Execute(null);
        list.Editor!.CancelCommand.Execute(null);
        Assert.That(list.Editor, Is.Null);

        list.Selected = list.Items[0];
        var editor = list.Editor!;
        editor.Spanish = "changed";
        editor.CancelCommand.Execute(null);
        Assert.That(list.Editor, Is.Not.SameAs(editor));
        Assert.That(list.Editor!.Spanish, Is.EqualTo("árbol"));
    }

    [Test]
    public void Refresh_KeepsSelectionAndEditor()
    {
        var list = new WordListViewModel(_context, WordKind.Noun);
        list.Selected = list.Items[1];
        var editor = list.Editor;

        list.Refresh();

        Assert.That(list.Selected, Is.SameAs(_library.Nouns[0]));
        Assert.That(list.Editor, Is.SameAs(editor));
    }

    [Test]
    public void Refresh_SelectionFilteredOut_ClearsSelection()
    {
        var list = new WordListViewModel(_context, WordKind.Noun);
        list.Selected = list.Items[1];
        var editor = list.Editor;

        list.SearchText = "perro";

        Assert.That(list.Selected, Is.Null);
        Assert.That(list.Editor, Is.SameAs(editor));
    }
}

public class LibraryViewModelTests
{
    [Test]
    public void Tabs_ExposeCurrentListAndAddButton()
    {
        var library = TestData.Library();
        var vm = new LibraryViewModel(new LibraryContext(library, new InMemoryStore<LearnLibrary>(library)));

        Assert.Multiple(() =>
        {
            Assert.That(vm.CurrentList, Is.SameAs(vm.Nouns));
            Assert.That(vm.AddLabel, Is.EqualTo("Add noun"));
            Assert.That(vm.IsAddVisible, Is.True);
        });

        vm.SelectedTabIndex = 1;
        vm.AddCommand.Execute(null);
        Assert.That(vm.CurrentList, Is.SameAs(vm.Verbs));
        Assert.That(vm.Verbs.Editor!.IsNew, Is.True);

        vm.SelectedTabIndex = 2;
        vm.AddCommand.Execute(null);
        Assert.Multiple(() =>
        {
            Assert.That(vm.CurrentList, Is.SameAs(vm.Adjectives));
            Assert.That(vm.AddLabel, Is.EqualTo("Add adjective"));
            Assert.That(vm.Adjectives.Editor, Is.TypeOf<AdjectiveEditorViewModel>());
        });

        vm.SelectedTabIndex = 3;
        vm.AddCommand.Execute(null);
        Assert.Multiple(() =>
        {
            Assert.That(vm.CurrentList, Is.SameAs(vm.Prepositions));
            Assert.That(vm.AddLabel, Is.EqualTo("Add preposition"));
            Assert.That(vm.Prepositions.Editor, Is.TypeOf<PrepositionEditorViewModel>());
        });

        // Numerals and topics have no word list.
        foreach (var tab in new[] { 4, 5 })
        {
            vm.SelectedTabIndex = tab;
            vm.AddCommand.Execute(null);
            Assert.Multiple(() =>
            {
                Assert.That(vm.CurrentList, Is.Null, $"tab {tab}");
                Assert.That(vm.IsAddVisible, Is.False, $"tab {tab}");
                Assert.That(vm.AddLabel, Is.Empty, $"tab {tab}");
            });
        }
    }

    [Test]
    public async Task LibraryChanged_RefreshesOtherTabsAndOpenEditors()
    {
        var library = TestData.Library();
        var context = new LibraryContext(library, new InMemoryStore<LearnLibrary>(library));
        var vm = new LibraryViewModel(context);
        vm.OnActivated();
        vm.Nouns.Selected = vm.Nouns.Items.Single(n => n.BaseValue == "perro");
        var perroEditor = vm.Nouns.Editor!;
        vm.Topics.SelectedTopic = "city";

        // Topics tab: add perro to "city" while its editor is open on the Nouns tab.
        var perroChip = vm.Topics.Words.Single(w => w.Value.BaseValue == "perro");
        perroChip.IsSelected = true;
        await vm.Topics.ToggleWordCommand.ExecuteAsync(perroChip);

        Assert.That(vm.Nouns.Editor, Is.SameAs(perroEditor));
        Assert.That(perroEditor.Topics.Single(t => t.Value == "city").IsSelected, Is.True);

        // Nouns tab: a new word shows up on the Topics tab.
        vm.Nouns.AddCommand.Execute(null);
        vm.Nouns.Editor!.Spanish = "mesa";
        vm.Nouns.Editor.English = "table";
        await vm.Nouns.Editor.SaveCommand.ExecuteAsync(null);

        Assert.That(vm.Topics.Words.Select(w => w.Value.BaseValue), Does.Contain("mesa"));
    }

    [Test]
    public async Task LibraryChanged_WhileHidden_RefreshesOnlyWhenShown()
    {
        var library = TestData.Library();
        var context = new LibraryContext(library, new InMemoryStore<LearnLibrary>(library));
        var vm = new LibraryViewModel(context);
        vm.OnActivated();
        vm.OnDeactivated();

        library.Nouns.Add(new Noun { BaseValue = "mesa" });
        await context.SaveAsync();
        Assert.That(vm.Nouns.Items, Has.Count.EqualTo(2));

        vm.OnActivated();
        Assert.That(vm.Nouns.Items, Has.Count.EqualTo(3));
    }

    [Test]
    public void OnActivated_RefreshesAllTabs()
    {
        var library = TestData.Library();
        var vm = new LibraryViewModel(new LibraryContext(library, new InMemoryStore<LearnLibrary>(library)));
        library.Nouns.Add(new Noun { BaseValue = "mesa" });
        library.Verbs.Clear();
        library.Topics.Add("food");

        vm.OnActivated();

        Assert.Multiple(() =>
        {
            Assert.That(vm.Nouns.Items, Has.Count.EqualTo(3));
            Assert.That(vm.Verbs.Items, Is.Empty);
            Assert.That(vm.Topics.Topics, Does.Contain("food"));
        });
    }
}

public class TopicsViewModelTests
{
    private LearnLibrary _library = null!;
    private InMemoryStore<LearnLibrary> _store = null!;
    private TopicsViewModel _vm = null!;

    [SetUp]
    public void Setup()
    {
        _library = TestData.Library();
        _store = new InMemoryStore<LearnLibrary>(_library);
        _vm = new TopicsViewModel(new LibraryContext(_library, _store));
    }

    [Test]
    public void Initially_ListsTopicsWithoutSelection()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_vm.Topics, Is.EqualTo(new[] { "animals", "city" }));
            Assert.That(_vm.HasSelection, Is.False);
            Assert.That(_vm.Words, Is.Empty);
            Assert.That(_vm.DeleteLabel, Is.EqualTo("Delete topic"));
        });
    }

    [Test]
    public void SelectTopic_ShowsWordsWithMembership()
    {
        _vm.SelectedTopic = "city";

        Assert.Multiple(() =>
        {
            Assert.That(_vm.RenameText, Is.EqualTo("city"));
            Assert.That(_vm.Words.Select(w => w.Label), Is.EqualTo(new[] { "ciudad · noun", "hablar · verb", "perro · noun" }));
            Assert.That(_vm.Words.Where(w => w.IsSelected).Select(w => w.Value.BaseValue), Is.EqualTo(new[] { "ciudad", "hablar" }));
        });
    }

    [Test]
    public async Task AddTopic_AddsSelectsAndSaves()
    {
        _vm.NewTopicName = " food ";

        await _vm.AddTopicCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.Topics, Is.EqualTo(new[] { "animals", "city", "food" }));
            Assert.That(_vm.SelectedTopic, Is.EqualTo("food"));
            Assert.That(_vm.NewTopicName, Is.Empty);
            Assert.That(_store.SaveCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task AddTopic_Invalid_ShowsErrorUntilTyping()
    {
        _vm.NewTopicName = "City";

        await _vm.AddTopicCommand.ExecuteAsync(null);
        Assert.That(_vm.TopicError, Is.EqualTo("Topic \"City\" already exists."));
        Assert.That(_store.SaveCount, Is.Zero);

        _vm.NewTopicName = "Cities";
        Assert.That(_vm.TopicError, Is.Null);
    }

    [Test]
    public async Task RenameTopic_RenamesAndKeepsSelection()
    {
        await _vm.RenameTopicCommand.ExecuteAsync(null);
        Assert.That(_store.SaveCount, Is.Zero);

        _vm.SelectedTopic = "city";
        _vm.RenameText = "town";
        await _vm.RenameTopicCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_vm.Topics, Is.EqualTo(new[] { "animals", "town" }));
            Assert.That(_vm.SelectedTopic, Is.EqualTo("town"));
            Assert.That(_library.Nouns[0].Topics, Is.EqualTo(new[] { "town" }));
        });
    }

    [Test]
    public async Task RenameTopic_Invalid_ShowsErrorUntilTyping()
    {
        _vm.SelectedTopic = "city";
        _vm.RenameText = " ";

        await _vm.RenameTopicCommand.ExecuteAsync(null);
        Assert.That(_vm.TopicError, Is.EqualTo("Enter a topic name."));

        _vm.RenameText = "x";
        Assert.That(_vm.TopicError, Is.Null);
    }

    [Test]
    public async Task DeleteTopic_AsksForConfirmationFirst()
    {
        await _vm.DeleteTopicCommand.ExecuteAsync(null);
        Assert.That(_vm.IsConfirmingDelete, Is.False);

        _vm.SelectedTopic = "city";
        await _vm.DeleteTopicCommand.ExecuteAsync(null);
        Assert.That(_vm.DeleteLabel, Is.EqualTo("Confirm delete"));
        Assert.That(_library.Topics, Does.Contain("city"));

        await _vm.DeleteTopicCommand.ExecuteAsync(null);
        Assert.Multiple(() =>
        {
            Assert.That(_vm.Topics, Is.EqualTo(new[] { "animals" }));
            Assert.That(_vm.SelectedTopic, Is.Null);
            Assert.That(_library.Nouns[0].Topics, Is.Empty);
            Assert.That(_store.SaveCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void SelectingOtherTopic_ResetsDeleteConfirmation()
    {
        _vm.SelectedTopic = "city";
        _vm.IsConfirmingDelete = true;

        _vm.SelectedTopic = "animals";

        Assert.That(_vm.IsConfirmingDelete, Is.False);
    }

    [Test]
    public async Task ToggleWord_UpdatesMembershipAndSaves()
    {
        var perro = new ToggleOption<LearnUnit>(_library.Nouns[1], "perro", isSelected: true);
        await _vm.ToggleWordCommand.ExecuteAsync(perro);
        Assert.That(_store.SaveCount, Is.Zero);

        _vm.SelectedTopic = "city";
        await _vm.ToggleWordCommand.ExecuteAsync(perro);

        Assert.That(_library.Nouns[1].Topics, Is.EqualTo(new[] { "animals", "city" }));
        Assert.That(_store.SaveCount, Is.EqualTo(1));
    }

    [Test]
    public async Task ToggleWord_KeepsChipsSelectionAndTypedName()
    {
        _vm.SelectedTopic = "city";
        _vm.RenameText = "town (draft)";
        var chips = _vm.Words.ToList();
        var perro = chips.Single(w => w.Value.BaseValue == "perro");
        perro.IsSelected = true;
        // Rebuilding the topic list would reset the ListBox selection (and the rename box) in the view.
        var topicListEvents = 0;
        _vm.Topics.CollectionChanged += (_, _) => topicListEvents++;

        await _vm.ToggleWordCommand.ExecuteAsync(perro);
        _vm.Refresh();

        Assert.Multiple(() =>
        {
            Assert.That(topicListEvents, Is.Zero);
            Assert.That(_vm.Words, Is.EqualTo(chips));
            Assert.That(perro.IsSelected, Is.True);
            Assert.That(_vm.SelectedTopic, Is.EqualTo("city"));
            Assert.That(_vm.RenameText, Is.EqualTo("town (draft)"));
        });
    }

    [Test]
    public void Refresh_TopicAdded_RebuildsListAndKeepsSelection()
    {
        _vm.SelectedTopic = "city";
        var topicListEvents = 0;
        _vm.Topics.CollectionChanged += (_, _) => topicListEvents++;

        _library.AddTopic("food");
        _vm.Refresh();

        Assert.That(topicListEvents, Is.GreaterThan(0));
        Assert.That(_vm.SelectedTopic, Is.EqualTo("city"));
    }

    [Test]
    public void Refresh_MembershipChangedElsewhere_UpdatesChipsInPlace()
    {
        _vm.SelectedTopic = "city";
        var chips = _vm.Words.ToList();

        _library.SetTopicMembership("city", _library.Nouns[0], false);
        _vm.Refresh();

        Assert.That(_vm.Words, Is.EqualTo(chips));
        Assert.That(chips.Single(w => w.Value.BaseValue == "ciudad").IsSelected, Is.False);
    }

    [Test]
    public void Refresh_WordRenamed_RebuildsChips()
    {
        _vm.SelectedTopic = "city";
        _library.Nouns[0].BaseValue = "urbe";

        _vm.Refresh();

        Assert.That(_vm.Words.Select(w => w.Label), Does.Contain("urbe · noun"));
    }

    [Test]
    public void Refresh_SelectedTopicStillExists_KeepsSelection()
    {
        _vm.SelectedTopic = "city";
        _library.Topics.Add("food");

        _vm.Refresh();

        Assert.That(_vm.SelectedTopic, Is.EqualTo("city"));
        Assert.That(_vm.Topics, Has.Count.EqualTo(3));
    }

    [Test]
    public void Refresh_SelectedTopicRemoved_ClearsSelection()
    {
        _vm.SelectedTopic = "city";
        _library.Topics.Remove("city");

        _vm.Refresh();

        Assert.That(_vm.SelectedTopic, Is.Null);
        Assert.That(_vm.Words, Is.Empty);
    }
}
