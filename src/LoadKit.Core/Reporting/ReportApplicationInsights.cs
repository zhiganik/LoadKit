namespace LoadKit.Core.Reporting;

/// <param name="Kql">A ready query for the <c>requests</c> table filtered by <c>loadrun</c>.</param>
public sealed record ReportApplicationInsights(string RunId, string Kql);
