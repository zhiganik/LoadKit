using System.Globalization;
using LoadKit.Core.Auth;
using LoadKit.Core.Engine;
using LoadKit.Core.Metrics;
using LoadKit.Core.Scenarios;
using LoadKit.Core.Scenarios.Model;

namespace LoadKit.Core.Reporting;

/// <summary>Turns a <see cref="RunResult"/> into a <see cref="RunReport"/>: units in ms, masked texts, warnings, KQL.</summary>
public static class RunReportBuilder
{
    public const double TesterCpuWarningPercent = 85;
    public const int FewRequestsWarningCount = 100;

    /// <param name="authProvider">The provider used by the run, for token refresh statistics; null without auth.</param>
    public static RunReport Build(
        CompiledScenario scenario,
        RunResult result,
        string toolVersion,
        SecretMasker secretMasker,
        IAuthProvider? authProvider)
    {
        var definition = scenario.Scenario;
        var statistics = result.Statistics;
        var requests = new List<ReportRequestStatistics>(statistics.Requests.Count);
        for (var index = 0; index < statistics.Requests.Count; index++)
        {
            var request = scenario.Requests[index].Definition;
            requests.Add(ToReport(statistics.Requests[index], request.Method, secretMasker.MaskText(request.Path)));
        }

        var runId = result.Options.RunId;
        return new RunReport(
            RunReport.CurrentSchemaVersion,
            toolVersion,
            new ReportScenario(definition.Name, definition.Description, secretMasker.MaskText(definition.BaseUrl), definition.Auth?.Type),
            new ReportRun(
                runId,
                result.StartedAt,
                Round(result.Elapsed.TotalSeconds),
                Round(statistics.MeasuredDuration.TotalSeconds),
                result.Interrupted,
                result.TesterCpuPercent is { } cpu ? Math.Round(cpu, 1) : null),
            ToReport(result.Options, definition.Load),
            ToReport(statistics.Overall, method: null, path: null),
            requests,
            [.. HistogramBuilder.Build(result.Results).Select(bucket =>
                new ReportHistogramBucket(bucket.From.TotalMilliseconds, bucket.To?.TotalMilliseconds, bucket.Count))],
            [.. result.ThresholdChecks.Select(check =>
                new ReportThresholdCheck(check.Name, check.Limit, check.Actual is { } actual ? Round(actual) : null, check.Passed))],
            result.ThresholdChecks.Count == 0 ? null : result.ThresholdsPassed,
            [.. result.ErrorSamples.Select(sample =>
                new ReportErrorSample(sample.RequestName, sample.StatusCode, sample.Error.ToString(), sample.Text))],
            BuildWarnings(result, definition.Auth, authProvider),
            runId is null ? null : new ReportApplicationInsights(runId, ApplicationInsightsQuery.Build(runId, result.StartedAt, result.Elapsed)));
    }

    private static ReportLoad ToReport(RunOptions options, LoadOptions load)
    {
        return new ReportLoad(
            options.Concurrency,
            options.TotalRequests,
            options.Duration is { } duration ? (int)duration.TotalSeconds : null,
            options.Warmup,
            load.TimeoutMs);
    }

    private static ReportRequestStatistics ToReport(RequestStatistics statistics, string? method, string? path)
    {
        var latency = statistics.Latency is { } value
            ? new ReportLatency(Ms(value.Min), Ms(value.Mean), Ms(value.P50), Ms(value.P95), Ms(value.P99), Ms(value.Max))
            : null;
        return new ReportRequestStatistics(
            statistics.Name,
            method,
            path,
            statistics.Count,
            statistics.Count - statistics.ErrorCount,
            statistics.ErrorCount,
            Round(statistics.ErrorRatePercent),
            Round(statistics.RequestsPerSecond),
            latency,
            [.. statistics.StatusCodes.Select(code => new ReportStatusCode(code.StatusCode, code.Count, code.UnexpectedCount))],
            [.. statistics.Errors.Select(error => new ReportErrorKind(error.Error.ToString(), error.Count))]);
    }

    private static List<ReportWarning> BuildWarnings(RunResult result, AuthOptions? auth, IAuthProvider? authProvider)
    {
        var warnings = new List<ReportWarning>();
        var overall = result.Statistics.Overall;
        if (result.Interrupted)
        {
            var planned = result.Options.TotalRequests is { } total ? $" of {total - result.Options.Warmup} planned" : string.Empty;
            warnings.Add(new ReportWarning(
                ReportWarningCodes.Interrupted,
                $"the run was interrupted (Ctrl+C); results cover {overall.Count}{planned} measured requests"));
        }

        var unexpectedUnauthorized = overall.StatusCodes.FirstOrDefault(code => code.StatusCode == 401)?.UnexpectedCount ?? 0;
        if (unexpectedUnauthorized > 0)
        {
            warnings.Add(new ReportWarning(ReportWarningCodes.UnauthorizedResponses, DescribeUnauthorized(unexpectedUnauthorized, auth)));
        }

        if (authProvider is TokenAuthProviderBase { RefreshFailureCount: > 0 } tokenProvider)
        {
            warnings.Add(new ReportWarning(
                ReportWarningCodes.TokenRefreshFailed,
                $"background token refresh failed {tokenProvider.RefreshFailureCount} time(s); last error: {tokenProvider.LastRefreshError}"));
        }

        if (result.TesterCpuPercent is > TesterCpuWarningPercent and var cpu)
        {
            warnings.Add(new ReportWarning(
                ReportWarningCodes.TesterCpu,
                $"the load generator used {cpu.ToString("0", CultureInfo.InvariantCulture)}% CPU: it may be the bottleneck; "
                + "lower concurrency or run it on another machine"));
        }

        if (overall.Count < FewRequestsWarningCount)
        {
            warnings.Add(new ReportWarning(
                ReportWarningCodes.FewRequests,
                $"only {overall.Count} measured requests: p99 is close to the maximum; use at least {FewRequestsWarningCount} for stable percentiles"));
        }

        return warnings;
    }

    private static string DescribeUnauthorized(int count, AuthOptions? auth)
    {
        var what = $"{count} responses were an unexpected 401 Unauthorized";
        return auth switch
        {
            null => $"{what}, and the scenario has no auth: add an auth section",
            BearerAuth or ApiKeyAuth => $"{what}: the token or key may be expired or wrong; update the variable in .env and rerun check",
            _ => $"{what}: tokens were rejected; check the scope and the account's access to the API, then rerun check",
        };
    }

    private static double Ms(TimeSpan duration)
    {
        return Round(duration.TotalMilliseconds);
    }

    private static double Round(double value)
    {
        return Math.Round(value, 3);
    }
}
