using System.Diagnostics;
using LoadKit.Core.Metrics;

namespace LoadKit.Core.Tests.Metrics;

public sealed class RunStatisticsCalculatorTests
{
    private static readonly string[] RequestNames = ["fast", "slow"];

    [Fact]
    public void Overall_IsComputedFromRawData_NotAveragedOverRequests()
    {
        // 9 fast responses of 1 ms and 1 slow of 100 ms: averaging per-request p50 would give 50.5 ms.
        var results = new List<RequestResult>();
        for (var index = 0; index < 9; index++)
        {
            results.Add(Ok(0, milliseconds: 1));
        }

        results.Add(Ok(1, milliseconds: 100));

        var statistics = RunStatisticsCalculator.Calculate(results, RequestNames, TimeSpan.FromSeconds(2));

        Assert.Equal(1, statistics.Overall.Latency!.P50.TotalMilliseconds, precision: 3);
        Assert.Equal(100, statistics.Overall.Latency.P99.TotalMilliseconds, precision: 3);
        Assert.Equal(100, statistics.Requests[1].Latency!.P50.TotalMilliseconds, precision: 3);
        Assert.Equal(10, statistics.Overall.Count);
        Assert.Equal(5, statistics.Overall.RequestsPerSecond, precision: 6);
        Assert.Equal(RunStatisticsCalculator.OverallName, statistics.Overall.Name);
    }

    [Fact]
    public void RequestsWithoutResponse_CountAsErrors_ButNotInLatency()
    {
        RequestResult[] results =
        [
            Ok(0, milliseconds: 10),
            new(0, 0, MillisecondsToTicks(30_000), ErrorKind.Timeout),
            new(0, 0, MillisecondsToTicks(1), ErrorKind.Connection),
        ];

        var fast = RunStatisticsCalculator.Calculate(results, RequestNames, TimeSpan.FromSeconds(1)).Requests[0];

        Assert.Equal(3, fast.Count);
        Assert.Equal(2, fast.ErrorCount);
        Assert.Equal(200.0 / 3, fast.ErrorRatePercent, precision: 6);
        Assert.Equal(10, fast.Latency!.Max.TotalMilliseconds, precision: 3);
        Assert.Equal([new ErrorKindCount(ErrorKind.Timeout, 1), new ErrorKindCount(ErrorKind.Connection, 1)], fast.Errors);
        Assert.Equal([new StatusCodeCount(200, 1, 0)], fast.StatusCodes);
    }

    [Fact]
    public void StatusCodes_SeparateUnexpectedFromExpected()
    {
        RequestResult[] results =
        [
            new(0, 500, MillisecondsToTicks(1), ErrorKind.None),
            new(1, 500, MillisecondsToTicks(1), ErrorKind.UnexpectedStatus),
            new(1, 200, MillisecondsToTicks(9), ErrorKind.SlowResponse),
        ];

        var statistics = RunStatisticsCalculator.Calculate(results, RequestNames, TimeSpan.FromSeconds(1));

        Assert.Equal([new StatusCodeCount(200, 1, 0), new StatusCodeCount(500, 2, 1)], statistics.Overall.StatusCodes);
        Assert.Equal(2, statistics.Overall.ErrorCount);
        Assert.Equal(0, statistics.Requests[0].ErrorCount);
    }

    [Fact]
    public void RequestWithoutResults_HasNoLatency()
    {
        var statistics = RunStatisticsCalculator.Calculate([Ok(0, milliseconds: 1)], RequestNames, TimeSpan.Zero);

        Assert.Equal(0, statistics.Requests[1].Count);
        Assert.Null(statistics.Requests[1].Latency);
        Assert.Equal(0, statistics.Requests[1].ErrorRatePercent);
        Assert.Equal(0, statistics.Overall.RequestsPerSecond);
    }

    private static RequestResult Ok(int requestIndex, int milliseconds)
    {
        return new RequestResult(requestIndex, 200, MillisecondsToTicks(milliseconds), ErrorKind.None);
    }

    private static long MillisecondsToTicks(int milliseconds)
    {
        return milliseconds * Stopwatch.Frequency / 1000;
    }
}
