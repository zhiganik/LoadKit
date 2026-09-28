namespace LoadKit.Core.Reporting;

/// <summary>Metrics of one scenario request, or of all of them (<see cref="Method"/> and <see cref="Path"/> null).</summary>
/// <param name="Path">The scenario path (templates not rendered), masked.</param>
/// <param name="LatencyMs">Null when no request received a response.</param>
public sealed record ReportRequestStatistics(
    string Name,
    string? Method,
    string? Path,
    int Count,
    int Successful,
    int Errors,
    double ErrorRatePercent,
    double RequestsPerSecond,
    ReportLatency? LatencyMs,
    IReadOnlyList<ReportStatusCode> StatusCodes,
    IReadOnlyList<ReportErrorKind> ErrorKinds);
