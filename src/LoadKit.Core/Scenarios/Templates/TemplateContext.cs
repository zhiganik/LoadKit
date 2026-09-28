namespace LoadKit.Core.Scenarios.Templates;

/// <summary>
/// Shared state for template generators during one run. <paramref name="random"/> must be thread-safe
/// (for example <see cref="Random.Shared"/>) because workers render requests concurrently.
/// </summary>
public sealed class TemplateContext(TimeProvider timeProvider, Random random)
{
    private long _sequence;

    public TimeProvider TimeProvider => timeProvider;

    public Random Random => random;

    /// <summary>Next value of <c>{{seq}}</c>: 1, 2, 3, ... shared by the whole scenario.</summary>
    public long NextSequence()
    {
        return Interlocked.Increment(ref _sequence);
    }
}
