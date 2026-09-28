using LoadKit.Core.Metrics;
using static LoadKit.Core.Tests.TestSupport.TestRunResults;

namespace LoadKit.Core.Tests.Metrics;

public sealed class HistogramBuilderTests
{
    [Fact]
    public void Buckets_UseFixedBounds_AndTrimEmptyEdges()
    {
        RequestResult[] results =
        [
            Response(0, 200, 3),
            Response(0, 200, 4.9),
            Response(0, 200, 5),
            Response(0, 200, 30),
        ];

        var buckets = HistogramBuilder.Build(results);

        Assert.Equal(
            [(2.0, (double?)5.0, 2), (5.0, 10.0, 1), (10.0, 20.0, 0), (20.0, 50.0, 1)],
            buckets.Select(bucket => (bucket.From.TotalMilliseconds, bucket.To?.TotalMilliseconds, bucket.Count)));
    }

    [Fact]
    public void VerySlowResponses_GoToTheOpenBucket()
    {
        var bucket = Assert.Single(HistogramBuilder.Build([Response(0, 200, 45_000)]));

        Assert.Equal((30_000.0, (TimeSpan?)null), (bucket.From.TotalMilliseconds, bucket.To));
    }

    [Fact]
    public void RequestsWithoutResponse_AreNotCounted()
    {
        Assert.Empty(HistogramBuilder.Build([new RequestResult(0, 0, 100, ErrorKind.Timeout)]));
    }
}
