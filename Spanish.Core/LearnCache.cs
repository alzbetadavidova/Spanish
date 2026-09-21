namespace Spanish.Core;

/// <summary>Remembers the most recently practiced exercises so they are not repeated immediately.</summary>
public class LearnCache(int capacity)
{
    private readonly List<string> _history = [];

    public int Capacity { get; } = capacity > 0
        ? capacity
        : throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

    /// <summary>Whether <paramref name="key"/> is among the <paramref name="depth"/> most recent entries.</summary>
    public bool Has(string key, int depth)
    {
        var start = Math.Max(0, _history.Count - depth);
        for (var i = start; i < _history.Count; i++)
        {
            if (_history[i] == key)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Makes <paramref name="key"/> the most recent entry, dropping an earlier occurrence of it.</summary>
    public void Touch(string key)
    {
        _history.Remove(key);
        Add(key);
    }

    public void Add(string key)
    {
        if (_history.Count >= Capacity)
        {
            _history.RemoveAt(0);
        }
        _history.Add(key);
    }
}
