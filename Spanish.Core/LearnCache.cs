namespace Spanish.Core;

public class LearnCache
{
    private static int Capacity = 20;

    private readonly List<LearnUnit> _history = new List<LearnUnit>();
    
    public bool Has(LearnUnit learnUnit) => _history.Any(l => l.BaseValue.Equals(learnUnit.BaseValue, StringComparison.OrdinalIgnoreCase));

    public void Add(LearnUnit learnUnit)
    {
        if (_history.Count >= Capacity)
        {
            _history.RemoveAt(0);
        }
        _history.Add(learnUnit);
    } 
}