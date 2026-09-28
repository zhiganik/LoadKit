namespace LoadKit.Core.Reporting;

/// <param name="Kind">An <c>ErrorKind</c> name, e.g. <c>UnexpectedStatus</c> or <c>Timeout</c>.</param>
public sealed record ReportErrorKind(string Kind, int Count);
