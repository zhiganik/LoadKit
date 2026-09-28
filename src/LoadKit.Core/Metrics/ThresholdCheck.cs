namespace LoadKit.Core.Metrics;

/// <param name="Name">Threshold field name as in the scenario: <c>p95Ms</c>, <c>errorRatePercent</c>, ...</param>
/// <param name="Actual">Measured value; null when it cannot be measured (no responses), which fails the check.</param>
public sealed record ThresholdCheck(string Name, double Limit, double? Actual, bool Passed);
