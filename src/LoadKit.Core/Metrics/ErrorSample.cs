namespace LoadKit.Core.Metrics;

/// <summary>
/// An example of a failed request: the start of the response body, or the exception message when there was no
/// response. <see cref="Text"/> is already masked.
/// </summary>
public sealed record ErrorSample(string RequestName, int StatusCode, ErrorKind Error, string Text);
