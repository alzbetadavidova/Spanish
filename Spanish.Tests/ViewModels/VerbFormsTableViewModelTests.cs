using Spanish.Core;
using Spanish.ViewModels;

namespace Spanish.Tests.ViewModels;

public class VerbFormsTableViewModelTests
{
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
            Assert.That(table.HasConjugations, Is.True);
            Assert.That(table.Gerund, Is.EqualTo("hablando"));
            Assert.That(table.HasGerund, Is.True);
        });
    }

    [Test]
    public void Constructor_MissingPresent_ShowsDashes()
    {
        var verb = TestData.Hablar();
        verb.PresentConjugations = Verb.EmptyConjugations();

        var table = new VerbFormsTableViewModel(verb);

        Assert.Multiple(() =>
        {
            Assert.That(table.Rows.Select(r => r.Present), Is.All.EqualTo(VerbFormsTableViewModel.Missing));
            Assert.That(table.Rows[0].Preterite, Is.EqualTo("hablé"));
            Assert.That(table.HasConjugations, Is.True);
        });
    }

    [Test]
    public void Constructor_MissingPreterite_ShowsDashes()
    {
        var verb = TestData.Hablar();
        verb.PreteriteConjugations = Verb.EmptyConjugations();

        var table = new VerbFormsTableViewModel(verb);

        Assert.Multiple(() =>
        {
            Assert.That(table.Rows.Select(r => r.Preterite), Is.All.EqualTo(VerbFormsTableViewModel.Missing));
            Assert.That(table.HasConjugations, Is.True);
        });
    }

    [Test]
    public void Constructor_ShortArrayFromAHandEditedFile_ShowsDashesForTheRest()
    {
        var verb = TestData.Hablar();
        verb.PreteriteConjugations = [" hablé "];

        var table = new VerbFormsTableViewModel(verb);

        Assert.That(table.Rows.Select(r => r.Preterite),
            Is.EqualTo(new[] { "hablé", "—", "—", "—", "—" }));
    }

    [Test]
    public void Constructor_OnlyAGerund_HasNoConjugations()
    {
        var verb = new Verb { BaseValue = "hablar", NonPersonalGerund = " hablando " };

        var table = new VerbFormsTableViewModel(verb);

        Assert.Multiple(() =>
        {
            Assert.That(table.HasConjugations, Is.False);
            Assert.That(table.Gerund, Is.EqualTo("hablando"));
            Assert.That(table.HasGerund, Is.True);
        });
    }

    [Test]
    public void Constructor_NoGerund_HidesIt()
    {
        var verb = TestData.Hablar();
        verb.NonPersonalGerund = " ";

        var table = new VerbFormsTableViewModel(verb);

        Assert.Multiple(() =>
        {
            Assert.That(table.Gerund, Is.Empty);
            Assert.That(table.HasGerund, Is.False);
        });
    }

    [Test]
    public void Constructor_NullVerb_Throws()
    {
        Assert.That(() => new VerbFormsTableViewModel(null!), Throws.ArgumentNullException);
    }
}
