namespace LoadKit.Core.Reporting;

/// <param name="Unexpected">How many were not in <c>expect.status</c>.</param>
public sealed record ReportStatusCode(int Status, int Count, int Unexpected);
