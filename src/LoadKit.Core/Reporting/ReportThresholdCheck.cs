namespace LoadKit.Core.Reporting;

/// <param name="Name">Scenario field name: <c>p50Ms</c>, <c>p95Ms</c>, <c>p99Ms</c> or <c>errorRatePercent</c>.</param>
/// <param name="Actual">Null when it could not be measured (no responses); the check then fails.</param>
public sealed record ReportThresholdCheck(string Name, double Limit, double? Actual, bool Passed);
