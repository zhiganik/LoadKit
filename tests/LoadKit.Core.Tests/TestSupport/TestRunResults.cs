using System.Diagnostics;
using LoadKit.Core.Engine;
using LoadKit.Core.Metrics;
using LoadKit.Core.Scenarios;

namespace LoadKit.Core.Tests.TestSupport;

/// <summary>Builds <see cref="RunResult"/>s from known request results, as the engine would.</summary>
internal static class TestRunResults
{
    public static readonly DateTimeOffset StartedAt = new(2026, 9, 28, 10, 15, 30, TimeSpan.Zero);

    public static RequestResult Response(int requestIndex, int statusCode, double milliseconds, ErrorKind error = ErrorKind.None)
    {
        return new RequestResult(requestIndex, statusCode, (long)(milliseconds * Stopwatch.Frequency / 1000), error);
    }

    public static RunResult Create(
        CompiledScenario scenario,
        IReadOnlyList<RequestResult> results,
        string? runId = "20260928-101530-abcd",
        bool interrupted = false,
        double? testerCpuPercent = 20,
        IReadOnlyList<ErrorSample>? errorSamples = null)
    {
        var requestNames = scenario.Requests.Select(request => request.Definition.Name).ToArray();
        var statistics = RunStatisticsCalculator.Calculate(results, requestNames, TimeSpan.FromSeconds(10));
        return new RunResult(
            scenario.Scenario.Name,
            RunOptions.FromLoad(scenario.Scenario.Load, runId),
            StartedAt,
            TimeSpan.FromSeconds(12),
            interrupted,
            statistics,
            ThresholdEvaluator.Evaluate(scenario.Scenario.Thresholds, statistics.Overall),
            errorSamples ?? [],
            results,
            testerCpuPercent);
    }
}
