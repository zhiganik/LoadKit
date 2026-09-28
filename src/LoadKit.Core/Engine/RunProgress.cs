namespace LoadKit.Core.Engine;

/// <summary>Live run state, reported every 250 ms.</summary>
/// <param name="CompletedRequests">Finished requests including warmup.</param>
/// <param name="Errors">Errors among measured requests.</param>
/// <param name="RequestsPerSecond">Throughput since the previous report.</param>
public readonly record struct RunProgress(
    long CompletedRequests,
    long Errors,
    TimeSpan Elapsed,
    double RequestsPerSecond,
    int? PlannedRequests,
    TimeSpan? PlannedDuration);
