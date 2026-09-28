using LoadKit.Core.Metrics;
using LoadKit.Core.Scenarios.Model;

namespace LoadKit.Core.Tests.Metrics;

public sealed class ThresholdEvaluatorTests
{
    private static readonly LatencyStatistics Latency = new(
        Min: TimeSpan.FromMilliseconds(1),
        Mean: TimeSpan.FromMilliseconds(50),
        P50: TimeSpan.FromMilliseconds(40),
        P95: TimeSpan.FromMilliseconds(200),
        P99: TimeSpan.FromMilliseconds(400),
        Max: TimeSpan.FromMilliseconds(900));

    [Fact]
    public void NoThresholds_GiveNoChecks()
    {
        var checks = ThresholdEvaluator.Evaluate(null, Overall(Latency, errorRatePercent: 50));

        Assert.Empty(checks);
        Assert.True(ThresholdEvaluator.AllPassed(checks));
    }

    [Fact]
    public void ValueEqualToLimit_Passes()
    {
        var checks = ThresholdEvaluator.Evaluate(new Thresholds(40, 200, 400, 1), Overall(Latency, errorRatePercent: 1));

        Assert.Equal(["p50Ms", "p95Ms", "p99Ms", "errorRatePercent"], checks.Select(check => check.Name));
        Assert.True(ThresholdEvaluator.AllPassed(checks));
    }

    [Fact]
    public void ExceededLimits_Fail()
    {
        var checks = ThresholdEvaluator.Evaluate(new Thresholds(null, 199, null, 0), Overall(Latency, errorRatePercent: 0.1));

        Assert.Equal([("p95Ms", false, 200.0), ("errorRatePercent", false, 0.1)], checks.Select(check => (check.Name, check.Passed, check.Actual!.Value)));
        Assert.False(ThresholdEvaluator.AllPassed(checks));
    }

    [Fact]
    public void LatencyThreshold_WithoutResponses_Fails()
    {
        var check = Assert.Single(ThresholdEvaluator.Evaluate(new Thresholds(null, 500, null, null), Overall(null, errorRatePercent: 100)));

        Assert.Null(check.Actual);
        Assert.False(check.Passed);
    }

    private static RequestStatistics Overall(LatencyStatistics? latency, double errorRatePercent)
    {
        return new RequestStatistics("total", 100, (int)errorRatePercent, errorRatePercent, 10, latency, [], []);
    }
}
