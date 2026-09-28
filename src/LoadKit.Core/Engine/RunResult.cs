using LoadKit.Core.Metrics;

namespace LoadKit.Core.Engine;

/// <summary>Everything a run produced; reports and the console summary are built from it.</summary>
/// <param name="Elapsed">Whole run including warmup.</param>
/// <param name="Interrupted">The run was cancelled (Ctrl+C); metrics cover only the collected data.</param>
/// <param name="Results">Raw measured results.</param>
/// <param name="ErrorSamples">Up to 5 samples per status code and error kind, masked.</param>
/// <param name="TesterCpuPercent">Average CPU of this process over the run, as a share of all cores; null if unknown.</param>
public sealed record RunResult(
    string ScenarioName,
    RunOptions Options,
    DateTimeOffset StartedAt,
    TimeSpan Elapsed,
    bool Interrupted,
    RunStatistics Statistics,
    IReadOnlyList<ThresholdCheck> ThresholdChecks,
    IReadOnlyList<ErrorSample> ErrorSamples,
    IReadOnlyList<RequestResult> Results,
    double? TesterCpuPercent)
{
    public bool ThresholdsPassed => ThresholdEvaluator.AllPassed(ThresholdChecks);
}
