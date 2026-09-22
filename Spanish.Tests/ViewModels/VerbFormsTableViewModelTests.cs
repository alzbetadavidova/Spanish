using Spanish.Core;
using Spanish.ViewModels;

namespace Spanish.Tests.ViewModels;

public class VerbFormsTableViewModelTests
{
    private const string Missing = VerbFormsTableViewModel.Missing;

    [Test]
    public void Constructor_ListsEveryPersonWithBothTensesAndTheGerund()
    {
        var table = new VerbFormsTableViewModel(TestData.Hablar());

        Assert.Multiple(() =>
        {
            Assert.That(table.Rows, Is.EqualTo(new[]
            {
                new VerbFormRow("yo", "hablo", "hablé"),
                new VerbFormRow("tú", "hablas", "hablaste"),
                new VerbFormRow("él / ella / usted", "habla", "habló"),
                new VerbFormRow("nosotros / nosotras", "hablamos", "hablamos"),
                new VerbFormRow("ellos / ellas / ustedes", "hablan", "hablaron")
            }));
            Assert.That(table.Gerund, Is.EqualTo("hablando"));
        });
    }

    [Test]
    public void Constructor_MissingTense_ShowsDashes()
    {
        var verb = TestData.Hablar();
        verb.PresentConjugations = Verb.EmptyConjugations();

        var table = new VerbFormsTableViewModel(verb);

        Assert.Multiple(() =>
        {
            Assert.That(table.Rows.Select(r => r.Present), Is.All.EqualTo(Missing));
            Assert.That(table.Rows[0].Preterite, Is.EqualTo("hablé"));
        });
    }

    [Test]
    public void Constructor_ShortArrayFromAHandEditedFile_ShowsDashesForTheRest()
    {
        var verb = TestData.Hablar();
        verb.PreteriteConjugations = [" hablé "];

        var table = new VerbFormsTableViewModel(verb);

        Assert.That(table.Rows.Select(r => r.Preterite), Is.EqualTo(new[] { "hablé", Missing, Missing, Missing, Missing }));
    }

    [Test]
    public void Constructor_OnlyAGerund_ShowsDashesForEveryPerson()
    {
        var verb = new Verb { BaseValue = "hablar", NonPersonalGerund = " hablando " };

        var table = new VerbFormsTableViewModel(verb);

        Assert.Multiple(() =>
        {
            Assert.That(table.Rows.Select(r => (r.Present, r.Preterite)), Is.All.EqualTo((Missing, Missing)));
            Assert.That(table.Gerund, Is.EqualTo("hablando"));
        });
    }

    [Test]
    public void Constructor_NoGerund_ShowsADash()
    {
        var verb = TestData.Hablar();
        verb.NonPersonalGerund = " ";

        Assert.That(new VerbFormsTableViewModel(verb).Gerund, Is.EqualTo(Missing));
    }

    [Test]
    public void Constructor_NullVerb_Throws()
    {
        Assert.That(() => new VerbFormsTableViewModel(null!), Throws.ArgumentNullException);
    }
}
