namespace LoadKit.Core.Reporting;

/// <summary>Response times in milliseconds, rounded to 0.001.</summary>
public sealed record ReportLatency(double Min, double Mean, double P50, double P95, double P99, double Max);
