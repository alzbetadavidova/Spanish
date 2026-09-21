using System.Globalization;
using Spanish.Core;
using Spanish.ViewModels;
using Spanish.Views;

namespace Spanish.Tests.ViewModels;

public class ExceptionNotesViewModelTests
{
    private static Task OnCompleted(Scenario scenario, bool correct) => Task.CompletedTask;

    [Test]
    public void Scenario_WithNotes_ExposesTheReasons()
    {
        var scenario = new GenderScenario(TestData.Ciudad(), "ciudad", Article.La)
        {
            Notes = [new Irregularity(IrregularityKind.FeminineEndingInO, "why")]
        };

        var vm = ScenarioViewModel.Create(scenario, OnCompleted);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Notes, Is.EqualTo(new[] { "why" }));
            Assert.That(vm.HasNotes, Is.True);
        });
    }

    private static readonly Irregularity[] Why = [new(IrregularityKind.FeminineEndingInO, "why")];

    [TestCase(true)]
    [TestCase(false)]
    public void Gender_ShowsNotesOnceAnswered(bool hasNotes)
    {
        var scenario = new GenderScenario(TestData.Ciudad(), "ciudad", Article.La) { Notes = hasNotes ? Why : [] };
        var vm = new GenderScenarioViewModel(scenario, OnCompleted);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var before = vm.ShowNotes;

        vm.ChooseCommand.Execute(Article.La);

        Assert.Multiple(() =>
        {
            Assert.That(before, Is.False);
            Assert.That(vm.ShowNotes, Is.EqualTo(hasNotes));
            Assert.That(changed, Does.Contain(nameof(GenderScenarioViewModel.ShowNotes)));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Typed_ShowsNotesOnceChecked(bool hasNotes)
    {
        var scenario = new TypedScenario(TestData.Ciudad(), ScenarioType.Plural, "i", "p", null, ["ciudades"])
        {
            Notes = hasNotes ? Why : []
        };
        var vm = new TypedScenarioViewModel(scenario, OnCompleted) { Answer = "ciudades" };
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var before = vm.ShowNotes;

        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(before, Is.False);
            Assert.That(vm.ShowNotes, Is.EqualTo(hasNotes));
            Assert.That(changed, Does.Contain(nameof(TypedScenarioViewModel.ShowNotes)));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Card_ShowsNotesOnceFlipped(bool hasNotes)
    {
        var scenario = new CardScenario(TestData.Ciudad(), Direction.EnglishToSpanish, "a", "b", null) { Notes = hasNotes ? Why : [] };
        var vm = new CardScenarioViewModel(scenario, OnCompleted);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var before = vm.ShowNotes;

        vm.FlipCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(before, Is.False);
            Assert.That(vm.ShowNotes, Is.EqualTo(hasNotes));
            Assert.That(changed, Does.Contain(nameof(CardScenarioViewModel.ShowNotes)));
        });
    }

    [Test]
    public void Scenario_WithoutNotes_HasNone()
    {
        var vm = ScenarioViewModel.Create(new GenderScenario(TestData.Ciudad(), "ciudad", Article.La), OnCompleted);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Notes, Is.Empty);
            Assert.That(vm.HasNotes, Is.False);
        });
    }
}

public class NounEditorExceptionTests
{
    private LearnLibrary _library = null!;
    private LibraryContext _context = null!;
    private readonly List<string> _events = [];
    private EditorCallbacks _callbacks = null!;

    [SetUp]
    public void Setup()
    {
        _library = TestData.Library();
        _context = new LibraryContext(_library, new InMemoryStore<LearnLibrary>(_library));
        _events.Clear();
        _callbacks = new EditorCallbacks(u => _events.Add($"saved:{u.BaseValue}"), () => _events.Add("deleted"), () => _events.Add("cancelled"));
    }

    private NounEditorViewModel Create(Noun? existing = null) => new(_context, existing, _callbacks);

    [Test]
    public void ExistingIrregularWord_ShowsWhy()
    {
        var mano = new Noun { BaseValue = "mano", Translation = "hand", Gender = Gender.Feminine, PluralValue = "manos" };

        var editor = Create(mano);

        Assert.Multiple(() =>
        {
            Assert.That(editor.ExceptionNotes, Is.EqualTo(new[] { "Feminine although it ends in -o: la mano." }));
            Assert.That(editor.HasExceptionNotes, Is.True);
        });
    }

    [Test]
    public void RegularOrNewWord_HasNoNotes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Create(_library.Nouns[0]).HasExceptionNotes, Is.False);
            Assert.That(Create().ExceptionNotes, Is.Empty);
        });
    }

    [Test]
    public void TakesEl_FollowsTheWordAndGender()
    {
        var editor = Create();

        editor.Spanish = "agua";
        var whileMasculine = editor.TakesElInSingular;
        editor.IsFeminine = true;
        var feminine = editor.TakesElInSingular;
        editor.Spanish = "casa";
        var otherWord = editor.TakesElInSingular;

        Assert.Multiple(() =>
        {
            Assert.That(whileMasculine, Is.False);
            Assert.That(feminine, Is.True);
            Assert.That(otherWord, Is.False);
        });
    }

    [Test]
    public void TakesEl_SetByTheUser_IsNoLongerSuggested()
    {
        var editor = Create();
        editor.IsFeminine = true;

        editor.TakesElInSingular = true;
        editor.Spanish = "casa";
        editor.IsMasculine = true;

        Assert.That(editor.TakesElInSingular, Is.True);
    }

    [Test]
    public void TakesEl_UntickedByTheUser_StaysUnticked()
    {
        var editor = Create();
        editor.IsFeminine = true;
        editor.Spanish = "agua";

        editor.TakesElInSingular = false;
        editor.Spanish = "alma";

        Assert.That(editor.TakesElInSingular, Is.False);
    }

    [Test]
    public void TakesEl_ExistingWordWithTheFlag_KeepsIt()
    {
        var agua = new Noun { BaseValue = "agua", Translation = "water", Gender = Gender.Feminine, TakesElInSingular = true };

        var editor = Create(agua);
        editor.Spanish = "casa";

        Assert.That(editor.TakesElInSingular, Is.True);
    }

    [Test]
    public void TakesEl_ExistingWordWithoutTheFlag_IsNotSuggested()
    {
        var hache = new Noun { BaseValue = "hache", Translation = "the letter h", Gender = Gender.Feminine };

        var editor = Create(hache);
        editor.Spanish = "hacha";
        editor.IsMasculine = true;
        editor.IsFeminine = true;

        Assert.That(editor.TakesElInSingular, Is.False);
    }

    [Test]
    public void TakesEl_ExistingMasculineWord_IsSuggestedWhenMadeFeminine()
    {
        var agua = new Noun { BaseValue = "agua", Translation = "water", Gender = Gender.Masculine };

        var editor = Create(agua);
        editor.IsFeminine = true;

        Assert.That(editor.TakesElInSingular, Is.True);
    }

    [Test]
    public async Task Save_Feminine_KeepsTheFlag()
    {
        var editor = Create();
        editor.IsFeminine = true;
        editor.Spanish = "agua";
        editor.English = "water";

        await editor.SaveCommand.ExecuteAsync(null);

        var saved = _library.Nouns.Single(n => n.BaseValue == "agua");
        Assert.Multiple(() =>
        {
            Assert.That(saved.TakesElInSingular, Is.True);
            Assert.That(saved.SingularArticle, Is.EqualTo(Article.El));
        });
    }

    [Test]
    public async Task Save_Masculine_DropsTheFlag()
    {
        var editor = Create();
        editor.IsFeminine = true;
        editor.TakesElInSingular = true;
        editor.IsMasculine = true;
        editor.Spanish = "arte";
        editor.English = "art";

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.That(_library.Nouns.Single(n => n.BaseValue == "arte").TakesElInSingular, Is.False);
    }
}

public class IrregularWordConverterTests
{
    private static object Convert(object? value) =>
        IrregularWordConverter.Instance.Convert(value, typeof(bool), null, CultureInfo.InvariantCulture);

    [Test]
    public void Convert_TellsWhetherAWordBreaksARule()
    {
        var mano = new Noun { BaseValue = "mano", Gender = Gender.Feminine };

        Assert.Multiple(() =>
        {
            Assert.That(Convert(mano), Is.True);
            Assert.That(Convert(TestData.Ciudad()), Is.False);
            Assert.That(Convert("mano"), Is.False);
            Assert.That(Convert(null), Is.False);
        });
    }

    [Test]
    public void ConvertBack_Throws()
    {
        Assert.That(() => IrregularWordConverter.Instance.ConvertBack(true, typeof(object), null, CultureInfo.InvariantCulture),
            Throws.TypeOf<NotSupportedException>());
    }
}
