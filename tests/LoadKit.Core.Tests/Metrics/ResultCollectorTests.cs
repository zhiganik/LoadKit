using LoadKit.Core.Metrics;

namespace LoadKit.Core.Tests.Metrics;

public sealed class ResultCollectorTests
{
    [Fact]
    public void RequestCountMode_SkipsSlotsNotWritten()
    {
        var collector = ResultCollector.ForRequestCount(4);
        collector.Record(workerIndex: 0, measuredIndex: 3, new RequestResult(0, 200, 30, ErrorKind.None));
        collector.Record(workerIndex: 1, measuredIndex: 1, new RequestResult(0, 200, 10, ErrorKind.None));

        Assert.Equal([10L, 30L], collector.ToArray().Select(result => result.ElapsedTicks));
    }

    [Fact]
    public void DurationMode_MergesWorkerLists()
    {
        var collector = ResultCollector.ForDuration(workerCount: 2);
        collector.Record(workerIndex: 1, measuredIndex: 0, new RequestResult(0, 200, 1, ErrorKind.None));
        collector.Record(workerIndex: 0, measuredIndex: 0, new RequestResult(0, 200, 2, ErrorKind.None));
        collector.Record(workerIndex: 1, measuredIndex: 0, new RequestResult(0, 200, 3, ErrorKind.None));

        Assert.Equal([1L, 2L, 3L], collector.ToArray().Select(result => result.ElapsedTicks).Order());
    }

    [Fact]
    public void ErrorSamples_KeepAtMostTheLimitPerGroup()
    {
        var collector = new ErrorSampleCollector(maxSamplesPerGroup: 2);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (collector.TryReserve(500, ErrorKind.UnexpectedStatus, out var slot))
            {
                collector.Store(slot, new ErrorSample("fail", 500, ErrorKind.UnexpectedStatus, $"body {attempt}"));
            }
        }

        Assert.True(collector.TryReserve(0, ErrorKind.Timeout, out var timeoutSlot));
        collector.Store(timeoutSlot, new ErrorSample("slow", 0, ErrorKind.Timeout, "timeout"));

        Assert.Equal(["timeout", "body 0", "body 1"], collector.ToList().Select(sample => sample.Text));
    }
}
