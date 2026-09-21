using System.Text.Json;
using Spanish.Core;

namespace Spanish.Tests;

public class LearnLibraryTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test] 
    public void LoadFromFile_Success()
    {
        var actual = LearnLibrary.LoadFromFile("test.json");
        
        Assert.That(JsonSerializer.Serialize(actual), Is.EqualTo(JsonSerializer.Serialize(_library)));
    }

    private readonly LearnLibrary _library = new()
    {
        Nouns = new()
        {
            new()
            {
                Gender = Gender.Feminine,
                PluralValue = "ciudades",
                BaseValue = "ciudad"
            }
        }
    };
}
