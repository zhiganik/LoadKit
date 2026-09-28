namespace LoadKit.Core.Metrics;

/// <param name="Overall">All requests together, computed from raw data (not averaged over requests).</param>
/// <param name="Requests">One entry per scenario request, in scenario order.</param>
/// <param name="MeasuredDuration">From the first measured request to the end of the run.</param>
public sealed record RunStatistics(
    RequestStatistics Overall,
    IReadOnlyList<RequestStatistics> Requests,
    TimeSpan MeasuredDuration);
