using Spanish.Core;

namespace Spanish.Tests;

public class StressedASuggesterTests
{
    [TestCase("agua", true)] // a-gua: second to last syllable
    [TestCase("hacha", true)] // silent h
    [TestCase("hambre", true)]
    [TestCase("aula", true)] // au is one syllable
    [TestCase("águila", true)] // written accent on the a
    [TestCase("área", true)]
    [TestCase("  Alma ", true)] // trimmed, any case
    [TestCase("a", true)] // one syllable ending in a vowel
    [TestCase("al", true)] // one syllable ending in a consonant
    [TestCase("almas", true)] // ends in s: second to last syllable
    [TestCase("arman", true)] // ends in n: second to last syllable
    [TestCase("amiga", false)] // a-mi-ga: stress on mi
    [TestCase("ahora", false)] // a-ho-ra
    [TestCase("aorta", false)] // a-or-ta: strong vowels are separate syllables
    [TestCase("aldea", false)] // al-de-a: also at the end
    [TestCase("azul", false)] // ends in l: stress on the last syllable
    [TestCase("amígdala", false)] // written accent elsewhere
    [TestCase("casa", false)] // does not start with a
    [TestCase("h", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void StartsWithStressedA(string? word, bool expected)
    {
        Assert.That(StressedASuggester.StartsWithStressedA(word), Is.EqualTo(expected));
    }
}

public class VerbFormSuggesterTests
{
    [Test]
    public void Suggest_Ar()
    {
        var forms = VerbFormSuggester.Suggest("hablar")!;

        Assert.Multiple(() =>
        {
            Assert.That(forms.Present, Is.EqualTo(new[] { "hablo", "hablas", "habla", "hablamos", "hablan" }));
            Assert.That(forms.Preterite, Is.EqualTo(new[] { "hablé", "hablaste", "habló", "hablamos", "hablaron" }));
            Assert.That(forms.Gerund, Is.EqualTo("hablando"));
        });
    }

    // The spelling changes keep the sound regular.
    [TestCase("buscar", "busqué")]
    [TestCase("llegar", "llegué")]
    [TestCase("cruzar", "crucé")]
    [TestCase("  TOMAR ", "tomé")]
    public void Suggest_ArPreteriteYo(string infinitive, string expected)
    {
        Assert.That(VerbFormSuggester.Suggest(infinitive)!.Preterite[0], Is.EqualTo(expected));
    }

    [Test]
    public void Suggest_Er()
    {
        var forms = VerbFormSuggester.Suggest("comer")!;

        Assert.Multiple(() =>
        {
            Assert.That(forms.Present, Is.EqualTo(new[] { "como", "comes", "come", "comemos", "comen" }));
            Assert.That(forms.Preterite, Is.EqualTo(new[] { "comí", "comiste", "comió", "comimos", "comieron" }));
            Assert.That(forms.Gerund, Is.EqualTo("comiendo"));
        });
    }

    [TestCase("vivir", "vivimos")]
    [TestCase("oír", "oimos")] // stem o: irregular, but still conjugated as -ir
    public void Suggest_Ir(string infinitive, string nosotros)
    {
        var forms = VerbFormSuggester.Suggest(infinitive)!;

        Assert.Multiple(() =>
        {
            Assert.That(forms.Present[3], Is.EqualTo(nosotros));
            Assert.That(forms.Preterite[2], Does.EndWith("ió"));
            Assert.That(forms.Gerund, Does.EndWith("iendo"));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("a")]
    [TestCase("casa")]
    public void Suggest_NotAnInfinitive_ReturnsNull(string? word)
    {
        Assert.That(VerbFormSuggester.Suggest(word), Is.Null);
    }
}
