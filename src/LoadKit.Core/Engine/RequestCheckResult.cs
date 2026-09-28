using LoadKit.Core.Metrics;

namespace LoadKit.Core.Engine;

/// <summary>Result of one <c>check</c> request. <see cref="Url"/>, <see cref="BodyStart"/> and <see cref="ErrorMessage"/> are masked.</summary>
/// <param name="StatusCode">0 when there was no response; see <paramref name="Error"/> and <paramref name="ErrorMessage"/>.</param>
/// <param name="IsExpected">The status is in <c>expect.status</c>.</param>
public sealed record RequestCheckResult(
    string Name,
    string Method,
    string Url,
    int StatusCode,
    TimeSpan Elapsed,
    bool IsExpected,
    string BodyStart,
    ErrorKind Error,
    string? ErrorMessage);
