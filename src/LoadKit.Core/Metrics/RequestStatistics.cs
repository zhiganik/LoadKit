namespace LoadKit.Core.Metrics;

/// <summary>Metrics for one scenario request or for all requests together.</summary>
/// <param name="Count">Measured requests (warmup excluded).</param>
/// <param name="Latency">Null when no request received a response.</param>
/// <param name="StatusCodes">Received status codes, ascending; requests without a response are in <paramref name="Errors"/>.</param>
/// <param name="Errors">Error counts by kind, only kinds that occurred.</param>
public sealed record RequestStatistics(
    string Name,
    int Count,
    int ErrorCount,
    double ErrorRatePercent,
    double RequestsPerSecond,
    LatencyStatistics? Latency,
    IReadOnlyList<StatusCodeCount> StatusCodes,
    IReadOnlyList<ErrorKindCount> Errors);
