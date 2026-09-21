using System.Text.Json;

namespace Spanish.Core;

public class LearnLibrary
{
    public List<Noun> Nouns { get; set; } = new();

    private readonly LearnCache _history = new LearnCache();

    public static LearnLibrary LoadFromFile(string path)
    {
        using var file = File.OpenRead(path);
        return JsonSerializer.Deserialize<LearnLibrary>(file)!;
    }
    
    public void SaveToFile(string path)
    {
        using var file = new StreamWriter(File.OpenWrite(path));
        file.Write(JsonSerializer.Serialize(this));
    }

    public LearnUnit GetNextUnit()
    {
        var found = Nouns.OrderBy(n => n.LearnCoefficient).First();
        _history.Add(found);
        return found;
    }
    
    public LearnUnitScenario GetNextScenario(LearnUnit learnUnit)
    {
        return learnUnit.GetScenario();
    }
    
    public void Correct(Noun noun)
    {
        Nouns.First(n => n.BaseValue.Equals(noun.BaseValue, StringComparison.InvariantCultureIgnoreCase))
            .IncreaseCoefficient();
    }
    
    public void Incorrect(Noun noun)
    {
        Nouns.First(n => n.BaseValue.Equals(noun.BaseValue, StringComparison.InvariantCultureIgnoreCase))
            .DecreaseCoefficient();
    }
}