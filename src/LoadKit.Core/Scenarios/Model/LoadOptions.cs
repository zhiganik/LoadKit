namespace LoadKit.Core.Scenarios.Model;

/// <summary>Closed-model load parameters. Exactly one of <see cref="TotalRequests"/> and <see cref="DurationSec"/> is set.</summary>
public sealed record LoadOptions(
    int Concurrency,
    int? TotalRequests,
    int? DurationSec,
    int Warmup,
    int TimeoutMs);
