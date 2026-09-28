namespace LoadKit.Core.Metrics;

/// <summary>Responses with <see cref="From"/> ≤ duration &lt; <see cref="To"/>; <see cref="To"/> is null for the last, open bucket.</summary>
public sealed record HistogramBucket(TimeSpan From, TimeSpan? To, int Count);
