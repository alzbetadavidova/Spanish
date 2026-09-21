namespace Spanish.Core;

public interface IClock
{
    DateTime Now { get; }
}

public interface IRandomSource
{
    /// <summary>Returns a value in [0, maxExclusive).</summary>
    int Next(int maxExclusive);
}

public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;
}

public sealed class SystemRandomSource : IRandomSource
{
    public int Next(int maxExclusive) => Random.Shared.Next(maxExclusive);
}
