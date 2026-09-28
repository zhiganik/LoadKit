using LoadKit.Core.Scenarios.Model;

namespace LoadKit.Core.Engine;

/// <summary>Effective run parameters after CLI overrides.</summary>
/// <param name="TotalRequests">Requests to send including warmup; null for a duration run.</param>
/// <param name="Duration">Run length including warmup; null for a request-count run.</param>
/// <param name="RunId">Value of the <c>loadrun</c> query parameter; null when runs are not tagged.</param>
public sealed record RunOptions(
    int Concurrency,
    int? TotalRequests,
    TimeSpan? Duration,
    int Warmup,
    TimeSpan RequestTimeout,
    string? RunId)
{
    public const string RunIdQueryParameter = "loadrun";

    public static RunOptions FromLoad(LoadOptions load, string? runId)
    {
        return new RunOptions(
            load.Concurrency,
            load.TotalRequests,
            load.DurationSec is { } durationSec ? TimeSpan.FromSeconds(durationSec) : null,
            load.Warmup,
            TimeSpan.FromMilliseconds(load.TimeoutMs),
            runId);
    }
}
