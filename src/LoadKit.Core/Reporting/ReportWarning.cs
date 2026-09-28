namespace LoadKit.Core.Reporting;

/// <param name="Code">One of <see cref="ReportWarningCodes"/>.</param>
public sealed record ReportWarning(string Code, string Message);
