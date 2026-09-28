using LoadKit.Core.Scenarios.Model;

namespace LoadKit.Core.Metrics;

/// <summary>Compares overall percentiles and the error rate with <c>thresholds</c>. A value equal to the limit passes.</summary>
public static class ThresholdEvaluator
{
    public static IReadOnlyList<ThresholdCheck> Evaluate(Thresholds? thresholds, RequestStatistics overall)
    {
        if (thresholds is null)
        {
            return [];
        }

        var checks = new List<ThresholdCheck>();
        AddLatencyCheck(checks, "p50Ms", thresholds.P50Ms, overall.Latency?.P50);
        AddLatencyCheck(checks, "p95Ms", thresholds.P95Ms, overall.Latency?.P95);
        AddLatencyCheck(checks, "p99Ms", thresholds.P99Ms, overall.Latency?.P99);
        if (thresholds.ErrorRatePercent is { } errorRateLimit)
        {
            checks.Add(Check("errorRatePercent", errorRateLimit, overall.ErrorRatePercent));
        }

        return checks;
    }

    public static bool AllPassed(IReadOnlyList<ThresholdCheck> checks)
    {
        foreach (var check in checks)
        {
            if (!check.Passed)
            {
                return false;
            }
        }

        return true;
    }

    private static void AddLatencyCheck(List<ThresholdCheck> checks, string name, int? limitMs, TimeSpan? actual)
    {
        if (limitMs is { } limit)
        {
            checks.Add(Check(name, limit, actual?.TotalMilliseconds));
        }
    }

    private static ThresholdCheck Check(string name, double limit, double? actual)
    {
        return new ThresholdCheck(name, limit, actual, actual is { } value && value <= limit);
    }
}
