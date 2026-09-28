namespace LoadKit.Core.Reporting;

/// <param name="RunId">The <c>loadrun</c> query value; null when runs are not tagged.</param>
/// <param name="ElapsedSeconds">The whole run including warmup.</param>
/// <param name="MeasuredSeconds">From the first measured request to the end; the base for RPS.</param>
/// <param name="TesterCpuPercent">Average CPU of the load generator process across all cores.</param>
public sealed record ReportRun(
    string? RunId,
    DateTimeOffset StartedAt,
    double ElapsedSeconds,
    double MeasuredSeconds,
    bool Interrupted,
    double? TesterCpuPercent);
