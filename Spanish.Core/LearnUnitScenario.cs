namespace Spanish.Core;

public abstract class LearnUnitScenario
{
    
}

public class NounScenario : LearnUnitScenario
{
    public NounScenarioTypes Type { get; set; }
    public Noun Noun { get; set; }
}

public class VerbScenario : LearnUnitScenario
{
    public VerbScenarioTypes Type { get; set; }
    public Verb Noun { get; set; }
}