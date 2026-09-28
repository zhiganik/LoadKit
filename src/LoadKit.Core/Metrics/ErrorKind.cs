namespace LoadKit.Core.Metrics;

/// <summary>Why a measured request counts as an error. <see cref="None"/> means success.</summary>
public enum ErrorKind
{
    None,

    /// <summary>The status code is not in <c>expect.status</c>.</summary>
    UnexpectedStatus,

    /// <summary>The status is expected, but the response took longer than <c>expect.maxMs</c>.</summary>
    SlowResponse,

    /// <summary>No complete response within <c>load.timeoutMs</c>.</summary>
    Timeout,

    Connection,
    Tls,
    Other,
}
