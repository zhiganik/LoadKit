namespace LoadKit.Core.Reporting;

/// <param name="Status">HTTP status; 0 when there was no response.</param>
/// <param name="Text">Start of the response body or the error message, masked.</param>
public sealed record ReportErrorSample(string Request, int Status, string Error, string Text);
