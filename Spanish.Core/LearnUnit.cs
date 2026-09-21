using System.Text.Json.Serialization;

namespace Spanish.Core;

public abstract class LearnUnit
{

    public string BaseValue { get; set; } = string.Empty;
    public int LearnCoefficient { get; set; }

    public void IncreaseCoefficient()
    {
        LearnCoefficient++;
    }
    
    public void DecreaseCoefficient()
    {
        LearnCoefficient--;
    }

    public abstract LearnUnitScenario GetScenario();
}

public class Noun: LearnUnit
{
    public Topic[] Topics { get; set; } = [];
    
    [JsonIgnore]
    public Gender Gender { get; set; }
    public string PluralValue { get; set; } = string.Empty;

    [JsonPropertyName("Gender")]
    public string GenderString { get => Gender.ToString(); set => Gender = (Gender)Enum.Parse(typeof(Gender), value, true); }

    public override LearnUnitScenario GetScenario()
    {
        throw new NotImplementedException();
    }
}

public class Verb: LearnUnit
{
    public Topic[] Topics { get; set; } = [];
    
    public static Dictionary<string, int> ConjugationsDefinitions = new Dictionary<string, int>
    {
        {"Yo", 0},
        {"Tú", 1},
        {"Él", 2},
        {"Ella", 2},
        {"Usted", 2},
        {"Nosotros", 3},
        {"Nosotras", 3},
        {"Ellos", 4},
        {"Ellas", 4},
        {"Ustedes", 4},
    };

    public required string[] PresentConjugations { get; set; }
    public required string[] PreteriteConjugations { get; set; }

    public string NonPersonalGerund { get; set; }
    public override LearnUnitScenario GetScenario()
    {
        throw new NotImplementedException();
    }
}


public enum Gender
{
    Masculine,
    Feminine
}

public enum NounScenarioTypes
{
    Card, // show english, flip to spanish
    Fill, //show english, fill in spanish and check
    Gender, // show spanish, fill in gender
    Plural //show spanish singular, fill in plural
}

public enum VerbScenarioTypes
{
    Present, //present tense
    Preterite, //simple past tense
    Gerund // present continuous
}

public class Topic : LearnUnit
{
    [JsonPropertyName("Topic")]
    public string Nme { get; set; } = string.Empty;

    public override LearnUnitScenario GetScenario()
    {
        throw new NotImplementedException();
    }
}