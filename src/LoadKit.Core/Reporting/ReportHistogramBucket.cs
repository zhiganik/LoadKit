namespace LoadKit.Core.Reporting;

/// <param name="ToMs">Exclusive upper bound; null for the last, open bucket.</param>
public sealed record ReportHistogramBucket(double FromMs, double? ToMs, int Count);
