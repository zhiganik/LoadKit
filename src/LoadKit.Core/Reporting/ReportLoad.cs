namespace LoadKit.Core.Reporting;

/// <summary>Effective load parameters, after CLI overrides. Exactly one of the two volume fields is set.</summary>
public sealed record ReportLoad(int Concurrency, int? TotalRequests, int? DurationSec, int Warmup, int TimeoutMs);
