using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LoadKit.Core.Metrics;

/// <summary>
/// Builds per-request and overall metrics from raw results. Latency uses only requests that received a
/// response: a timeout or refused connection has no response time.
/// </summary>
public static class RunStatisticsCalculator
{
    public const string OverallName = "total";

    /// <param name="requestNames">Scenario request names; <see cref="RequestResult.RequestIndex"/> indexes this list.</param>
    public static RunStatistics Calculate(
        IReadOnlyList<RequestResult> results,
        IReadOnlyList<string> requestNames,
        TimeSpan measuredDuration)
    {
        var resultsByRequest = new List<RequestResult>[requestNames.Count];
        for (var requestIndex = 0; requestIndex < requestNames.Count; requestIndex++)
        {
            resultsByRequest[requestIndex] = [];
        }

        foreach (var result in results)
        {
            resultsByRequest[result.RequestIndex].Add(result);
        }

        var requestStatistics = new List<RequestStatistics>(requestNames.Count);
        for (var requestIndex = 0; requestIndex < requestNames.Count; requestIndex++)
        {
            requestStatistics.Add(Calculate(requestNames[requestIndex], resultsByRequest[requestIndex], measuredDuration));
        }

        return new RunStatistics(Calculate(OverallName, results, measuredDuration), requestStatistics, measuredDuration);
    }

    private static RequestStatistics Calculate(string name, IReadOnlyList<RequestResult> results, TimeSpan measuredDuration)
    {
        var errorCount = 0;
        var elapsedTicks = new List<long>(results.Count);
        var statusCodes = new SortedDictionary<int, (int Count, int UnexpectedCount)>();
        var errors = new SortedDictionary<ErrorKind, int>();
        foreach (var result in results)
        {
            if (result.IsError)
            {
                errorCount++;
                errors[result.Error] = errors.GetValueOrDefault(result.Error) + 1;
            }

            if (result.HasResponse)
            {
                elapsedTicks.Add(result.ElapsedTicks);
                var (count, unexpectedCount) = statusCodes.GetValueOrDefault(result.StatusCode);
                var unexpected = result.Error == ErrorKind.UnexpectedStatus ? 1 : 0;
                statusCodes[result.StatusCode] = (count + 1, unexpectedCount + unexpected);
            }
        }

        return new RequestStatistics(
            name,
            results.Count,
            errorCount,
            results.Count == 0 ? 0 : errorCount * 100.0 / results.Count,
            measuredDuration > TimeSpan.Zero ? results.Count / measuredDuration.TotalSeconds : 0,
            CalculateLatency(elapsedTicks),
            [.. statusCodes.Select(pair => new StatusCodeCount(pair.Key, pair.Value.Count, pair.Value.UnexpectedCount))],
            [.. errors.Select(pair => new ErrorKindCount(pair.Key, pair.Value))]);
    }

    private static LatencyStatistics? CalculateLatency(List<long> elapsedTicks)
    {
        if (elapsedTicks.Count == 0)
        {
            return null;
        }

        elapsedTicks.Sort();
        var sortedTicks = CollectionsMarshal.AsSpan(elapsedTicks);
        var totalTicks = 0.0;
        foreach (var ticks in sortedTicks)
        {
            totalTicks += ticks;
        }

        return new LatencyStatistics(
            ToTimeSpan(sortedTicks[0]),
            ToTimeSpan((long)(totalTicks / sortedTicks.Length)),
            ToTimeSpan(PercentileCalculator.ValueAt(sortedTicks, 50)),
            ToTimeSpan(PercentileCalculator.ValueAt(sortedTicks, 95)),
            ToTimeSpan(PercentileCalculator.ValueAt(sortedTicks, 99)),
            ToTimeSpan(sortedTicks[^1]));
    }

    private static TimeSpan ToTimeSpan(long stopwatchTicks)
    {
        return Stopwatch.GetElapsedTime(0, stopwatchTicks);
    }
}
