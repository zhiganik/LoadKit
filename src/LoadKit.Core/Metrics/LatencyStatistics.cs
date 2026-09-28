namespace LoadKit.Core.Metrics;

/// <summary>Response time distribution of the requests that received a response.</summary>
public sealed record LatencyStatistics(
    TimeSpan Min,
    TimeSpan Mean,
    TimeSpan P50,
    TimeSpan P95,
    TimeSpan P99,
    TimeSpan Max);
