using Spanish.Core;
using Spanish.ViewModels;

namespace Spanish.Tests.ViewModels;

public class ScenarioViewModelTests
{
    private readonly List<bool> _completions = [];

    [SetUp]
    public void Setup() => _completions.Clear();

    private Task OnCompleted(bool correct)
    {
        _completions.Add(correct);
        return Task.CompletedTask;
    }

    private sealed record OtherScenario(LearnUnit Unit) : Scenario(Unit, ScenarioType.Card);

    [Test]
    public void Create_MapsEachScenarioType()
    {
        var noun = TestData.Ciudad();

        Assert.Multiple(() =>
        {
            Assert.That(ScenarioViewModel.Create(new CardScenario(noun, Direction.EnglishToSpanish, "a", "b", null), OnCompleted),
                Is.TypeOf<CardScenarioViewModel>());
            Assert.That(ScenarioViewModel.Create(new TypedScenario(noun, ScenarioType.Fill, "i", "p", null, ["x"]), OnCompleted),
                Is.TypeOf<TypedScenarioViewModel>());
            Assert.That(ScenarioViewModel.Create(new GenderScenario(noun, "ciudad", Article.La), OnCompleted),
                Is.TypeOf<GenderScenarioViewModel>());
            Assert.That(() => ScenarioViewModel.Create(new OtherScenario(noun), OnCompleted), Throws.ArgumentException);
        });
    }

    [TestCase(Direction.EnglishToSpanish, "Card · English to Spanish")]
    [TestCase(Direction.SpanishToEnglish, "Card · Spanish to English")]
    public void Card_Heading_ShowsDirection(Direction direction, string heading)
    {
        var card = new CardScenarioViewModel(new CardScenario(TestData.Ciudad(), direction, "city", "la ciudad", "las ciudades"), OnCompleted);

        Assert.Multiple(() =>
        {
            Assert.That(card.Heading, Is.EqualTo(heading));
            Assert.That(card.Front, Is.EqualTo("city"));
            Assert.That(card.Back, Is.EqualTo("la ciudad"));
            Assert.That(card.BackDetail, Is.EqualTo("las ciudades"));
        });
    }

    [Test]
    public async Task Card_AssessmentOnlyAfterFlip_AndOnlyOnce()
    {
        var card = new CardScenarioViewModel(new CardScenario(TestData.Ciudad(), Direction.EnglishToSpanish, "a", "b", null), OnCompleted);
        Assert.That(card.KnewItCommand.CanExecute(null), Is.False);

        card.FlipCommand.Execute(null);
        await card.KnewItCommand.ExecuteAsync(null);
        await card.DidNotKnowCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(card.IsFlipped, Is.True);
            Assert.That(card.DidNotKnowCommand.CanExecute(null), Is.True);
            Assert.That(card.IsCompleted, Is.True);
            Assert.That(_completions, Is.EqualTo(new[] { true }));
        });
    }

    [Test]
    public async Task Card_DidNotKnow_CompletesIncorrect()
    {
        var card = new CardScenarioViewModel(new CardScenario(TestData.Ciudad(), Direction.EnglishToSpanish, "a", "b", null), OnCompleted);
        card.FlipCommand.Execute(null);

        await card.DidNotKnowCommand.ExecuteAsync(null);

        Assert.That(_completions, Is.EqualTo(new[] { false }));
    }

    private TypedScenarioViewModel Typed() => new(
        new TypedScenario(TestData.Hablar(), ScenarioType.Present, "Conjugate · present", "hablar", "tú", ["hablás"]),
        OnCompleted);

    [Test]
    public void Typed_ExposesScenarioText()
    {
        var typed = Typed();

        Assert.Multiple(() =>
        {
            Assert.That(typed.Heading, Is.EqualTo("Conjugate · present"));
            Assert.That(typed.Prompt, Is.EqualTo("hablar"));
            Assert.That(typed.PromptDetail, Is.EqualTo("tú"));
            Assert.That(typed.Feedback, Is.Null);
            Assert.That(typed.IsChecked, Is.False);
            Assert.That(typed.IsCorrect, Is.False);
            Assert.That(typed.IsIncorrect, Is.False);
        });
    }

    [Test]
    public async Task Typed_SubmitEmpty_AsksForAnswerUntilTyping()
    {
        var typed = Typed();

        await typed.SubmitCommand.ExecuteAsync(null);
        Assert.That(typed.ValidationMessage, Is.EqualTo("Type an answer first."));

        typed.Answer = "h";
        Assert.That(typed.ValidationMessage, Is.Null);
        Assert.That(typed.IsChecked, Is.False);
    }

    [Test]
    public async Task Typed_Correct_ChecksThenCompletesOnce()
    {
        var typed = Typed();
        typed.Answer = "hablás";

        await typed.SubmitCommand.ExecuteAsync(null);
        Assert.Multiple(() =>
        {
            Assert.That(typed.IsCorrect, Is.True);
            Assert.That(typed.IsIncorrect, Is.False);
            Assert.That(typed.Feedback, Is.EqualTo("Correct"));
            Assert.That(_completions, Is.Empty);
        });

        await typed.SubmitCommand.ExecuteAsync(null);
        await typed.SubmitCommand.ExecuteAsync(null);
        Assert.That(_completions, Is.EqualTo(new[] { true }));
    }

    [Test]
    public async Task Typed_AccentMismatch_ShowsHint()
    {
        var typed = Typed();
        typed.Answer = "hablas";

        await typed.SubmitCommand.ExecuteAsync(null);

        Assert.That(typed.Feedback, Is.EqualTo("Correct. Watch the accent: hablás"));
        Assert.That(typed.IsCorrect, Is.True);
    }

    [Test]
    public async Task Typed_Wrong_ShowsAnswerAndCompletesIncorrect()
    {
        var typed = Typed();
        typed.Answer = "hablo";

        await typed.SubmitCommand.ExecuteAsync(null);
        await typed.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(typed.IsIncorrect, Is.True);
            Assert.That(typed.Feedback, Is.EqualTo("The answer is hablás"));
            Assert.That(_completions, Is.EqualTo(new[] { false }));
        });
    }

    private GenderScenarioViewModel Gender() =>
        new(new GenderScenario(TestData.Ciudad(), "ciudades", Article.Las), OnCompleted);

    [Test]
    public async Task Gender_SubmitBeforeChoosing_DoesNothing()
    {
        var gender = Gender();

        await gender.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_completions, Is.Empty);
            Assert.That(gender.Feedback, Is.Null);
            Assert.That(gender.IsIncorrect, Is.False);
            Assert.That(gender.Heading, Is.EqualTo("Pick the article"));
            Assert.That(gender.Word, Is.EqualTo("ciudades"));
            Assert.That(gender.Translation, Is.EqualTo("city; town"));
            Assert.That(GenderScenarioViewModel.Articles.Select(a => a.Label), Is.EqualTo(new[] { "el", "la", "los", "las" }));
        });
    }

    [Test]
    public async Task Gender_Correct_KeepsFirstChoice()
    {
        var gender = Gender();

        gender.ChooseCommand.Execute(Article.Las);
        gender.ChooseCommand.Execute(Article.El);
        await gender.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(gender.Chosen, Is.EqualTo(Article.Las));
            Assert.That(gender.IsCorrect, Is.True);
            Assert.That(gender.Feedback, Is.EqualTo("Correct: las ciudades"));
            Assert.That(_completions, Is.EqualTo(new[] { true }));
        });
    }

    [Test]
    public async Task Gender_Wrong_ShowsExpected()
    {
        var gender = Gender();

        gender.ChooseCommand.Execute(Article.Los);
        await gender.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(gender.IsIncorrect, Is.True);
            Assert.That(gender.Feedback, Is.EqualTo("It's las ciudades"));
            Assert.That(_completions, Is.EqualTo(new[] { false }));
        });
    }

    [Test]
    public void Option_ToString_IsLabel()
    {
        Assert.That(new Option<int>(1, "one").ToString(), Is.EqualTo("one"));
    }
}
