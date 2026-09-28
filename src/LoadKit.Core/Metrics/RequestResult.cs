namespace LoadKit.Core.Metrics;

/// <summary>
/// Outcome of one measured request.
/// </summary>
/// <param name="RequestIndex">Index of the entry in the scenario <c>requests[]</c>.</param>
/// <param name="StatusCode">HTTP status code; <c>0</c> when no response was received.</param>
/// <param name="ElapsedTicks">
/// <see cref="System.Diagnostics.Stopwatch"/> ticks from sending until the body was fully read.
/// </param>
/// <param name="Error">Error classification; <see cref="ErrorKind.None"/> for success.</param>
public readonly record struct RequestResult(int RequestIndex, int StatusCode, long ElapsedTicks, ErrorKind Error)
{
    public bool HasResponse => StatusCode > 0;

    public bool IsError => Error != ErrorKind.None;
}
