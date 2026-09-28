namespace LoadKit.Core.Scenarios.Model;

public sealed record Thresholds(int? P50Ms, int? P95Ms, int? P99Ms, double? ErrorRatePercent);
