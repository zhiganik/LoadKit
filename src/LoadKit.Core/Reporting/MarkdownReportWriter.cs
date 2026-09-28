using System.Globalization;
using System.Text;

namespace LoadKit.Core.Reporting;

/// <summary><c>report.md</c>: for people, pull requests and AI assistants. Plain words, no emoji, stable headings.</summary>
public static class MarkdownReportWriter
{
    private const int HistogramBarWidth = 30;

    public static string Write(RunReport report)
    {
        var markdown = new StringBuilder();
        WriteHeader(markdown, report);
        WriteSummary(markdown, report);
        WriteWarnings(markdown, report.Warnings);
        WriteThresholds(markdown, report.Thresholds);
        WriteRequests(markdown, report);
        WriteStatusCodes(markdown, report.Overall);
        WriteErrorKinds(markdown, report.Overall);
        WriteHistogram(markdown, report.Histogram);
        WriteErrorSamples(markdown, report.ErrorSamples);
        WriteApplicationInsights(markdown, report.ApplicationInsights);

        // The same bytes on every OS: reports are compared and committed to PRs.
        return markdown.ToString().ReplaceLineEndings("\n");
    }

    private static void WriteHeader(StringBuilder markdown, RunReport report)
    {
        markdown.Append("# Load test report: ").AppendLine(report.Scenario.Name).AppendLine();
        if (report.Scenario.Description is { } description)
        {
            markdown.AppendLine(description).AppendLine();
        }

        var load = report.Load;
        var volume = load.TotalRequests is { } total ? $"{total} requests" : $"{load.DurationSec} s";
        var warmup = load.Warmup > 0 ? $" (warmup {load.Warmup})" : string.Empty;
        markdown.AppendLine("| Parameter | Value |").AppendLine("|---|---|");
        Row(markdown, "Base URL", report.Scenario.BaseUrl);
        Row(markdown, "Auth", report.Scenario.AuthType ?? "none");
        Row(markdown, "Run id (`loadrun`)", report.Run.RunId ?? "not tagged");
        Row(markdown, "Started (UTC)", report.Run.StartedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        Row(markdown, "Load", $"concurrency {load.Concurrency}, {volume}{warmup}, timeout {load.TimeoutMs} ms");
        Row(markdown, "Duration", $"{Number(report.Run.MeasuredSeconds)} s measured, {Number(report.Run.ElapsedSeconds)} s total");
        Row(markdown, "Result", DescribeResult(report));
        Row(markdown, "LoadKit", report.ToolVersion);
        markdown.AppendLine();
    }

    private static string DescribeResult(RunReport report)
    {
        if (report.Run.Interrupted)
        {
            return "INTERRUPTED (partial results)";
        }

        return report.ThresholdsPassed switch
        {
            true => "thresholds PASSED",
            false => "thresholds FAILED",
            null => "no thresholds",
        };
    }

    private static void WriteSummary(StringBuilder markdown, RunReport report)
    {
        var overall = report.Overall;
        markdown.AppendLine("## Summary").AppendLine();
        markdown.Append($"- Requests: {overall.Count}, errors: {overall.Errors} ({Number(overall.ErrorRatePercent)}%), ");
        markdown.AppendLine($"throughput: {Number(overall.RequestsPerSecond)} requests/s");
        if (overall.LatencyMs is { } latency)
        {
            markdown.AppendLine($"- Latency: p50 {Number(latency.P50)} ms, p95 {Number(latency.P95)} ms, p99 {Number(latency.P99)} ms, max {Number(latency.Max)} ms");
        }
        else
        {
            markdown.AppendLine("- Latency: no responses received");
        }

        if (report.Run.TesterCpuPercent is { } cpu)
        {
            markdown.AppendLine($"- Load generator CPU: {Number(cpu)}%");
        }

        markdown.AppendLine();
    }

    private static void WriteWarnings(StringBuilder markdown, IReadOnlyList<ReportWarning> warnings)
    {
        if (warnings.Count == 0)
        {
            return;
        }

        markdown.AppendLine("## Warnings").AppendLine();
        foreach (var warning in warnings)
        {
            markdown.AppendLine($"- `{warning.Code}`: {warning.Message}");
        }

        markdown.AppendLine();
    }

    private static void WriteThresholds(StringBuilder markdown, IReadOnlyList<ReportThresholdCheck> thresholds)
    {
        if (thresholds.Count == 0)
        {
            return;
        }

        markdown.AppendLine("## Thresholds").AppendLine();
        markdown.AppendLine("| Threshold | Limit | Actual | Result |").AppendLine("|---|---:|---:|---|");
        foreach (var check in thresholds)
        {
            var actual = check.Actual is { } value ? Number(value) : "n/a";
            markdown.AppendLine($"| {check.Name} | {Number(check.Limit)} | {actual} | {(check.Passed ? "ok" : "FAIL")} |");
        }

        markdown.AppendLine();
    }

    private static void WriteRequests(StringBuilder markdown, RunReport report)
    {
        markdown.AppendLine("## Requests").AppendLine();
        markdown.AppendLine("Times in ms.").AppendLine();
        markdown.AppendLine("| Request | Method | Path | Count | Errors | Error % | RPS | Min | Mean | p50 | p95 | p99 | Max |");
        markdown.AppendLine("|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var request in report.Requests)
        {
            WriteRequestRow(markdown, request, request.Name);
        }

        WriteRequestRow(markdown, report.Overall, $"**{report.Overall.Name}**");
        markdown.AppendLine();
    }

    private static void WriteRequestRow(StringBuilder markdown, ReportRequestStatistics request, string name)
    {
        var latency = request.LatencyMs;
        markdown.Append($"| {Cell(name)} | {request.Method} | {Cell(request.Path is null ? string.Empty : $"`{request.Path}`")} ");
        markdown.Append($"| {request.Count} | {request.Errors} | {Number(request.ErrorRatePercent)} | {Number(request.RequestsPerSecond)} ");
        markdown.AppendLine(latency is null
            ? "| - | - | - | - | - | - |"
            : $"| {Number(latency.Min)} | {Number(latency.Mean)} | {Number(latency.P50)} | {Number(latency.P95)} | {Number(latency.P99)} | {Number(latency.Max)} |");
    }

    private static void WriteStatusCodes(StringBuilder markdown, ReportRequestStatistics overall)
    {
        if (overall.StatusCodes.Count == 0)
        {
            return;
        }

        markdown.AppendLine("## Status codes").AppendLine();
        markdown.AppendLine("| Status | Count | Unexpected |").AppendLine("|---|---:|---:|");
        foreach (var statusCode in overall.StatusCodes)
        {
            markdown.AppendLine($"| {statusCode.Status} | {statusCode.Count} | {statusCode.Unexpected} |");
        }

        markdown.AppendLine();
    }

    private static void WriteErrorKinds(StringBuilder markdown, ReportRequestStatistics overall)
    {
        if (overall.ErrorKinds.Count == 0)
        {
            return;
        }

        markdown.AppendLine("## Errors").AppendLine();
        markdown.AppendLine("| Error | Count |").AppendLine("|---|---:|");
        foreach (var errorKind in overall.ErrorKinds)
        {
            markdown.AppendLine($"| {errorKind.Kind} | {errorKind.Count} |");
        }

        markdown.AppendLine();
    }

    private static void WriteHistogram(StringBuilder markdown, IReadOnlyList<ReportHistogramBucket> histogram)
    {
        if (histogram.Count == 0)
        {
            return;
        }

        var total = histogram.Sum(bucket => bucket.Count);
        var largest = histogram.Max(bucket => bucket.Count);
        markdown.AppendLine("## Latency histogram").AppendLine();
        markdown.AppendLine("| Time, ms | Count | Share | |").AppendLine("|---|---:|---:|---|");
        foreach (var bucket in histogram)
        {
            var range = bucket.ToMs is { } to ? $"{Number(bucket.FromMs)}–{Number(to)}" : $"≥ {Number(bucket.FromMs)}";
            var bar = new string('█', largest == 0 ? 0 : (int)Math.Round(bucket.Count * (double)HistogramBarWidth / largest));
            markdown.AppendLine($"| {range} | {bucket.Count} | {Number(bucket.Count * 100.0 / total)}% | {bar} |");
        }

        markdown.AppendLine();
    }

    private static void WriteErrorSamples(StringBuilder markdown, IReadOnlyList<ReportErrorSample> samples)
    {
        if (samples.Count == 0)
        {
            return;
        }

        markdown.AppendLine("## Sample errors").AppendLine();
        foreach (var sample in samples)
        {
            var status = sample.Status > 0 ? sample.Status.ToString(CultureInfo.InvariantCulture) : "no response";
            markdown.AppendLine($"### {status}, {sample.Request} ({sample.Error})").AppendLine();
            markdown.AppendLine("````text").AppendLine(sample.Text.Length == 0 ? "(empty body)" : sample.Text).AppendLine("````").AppendLine();
        }
    }

    private static void WriteApplicationInsights(StringBuilder markdown, ReportApplicationInsights? applicationInsights)
    {
        markdown.AppendLine("## Application Insights").AppendLine();
        if (applicationInsights is null)
        {
            markdown.AppendLine("The run was not tagged (`tagRuns: false` or `--no-tag`), so it cannot be filtered by `loadrun`.");
            return;
        }

        markdown.AppendLine($"Requests of this run carry `loadrun={applicationInsights.RunId}` in the query string.").AppendLine();
        markdown.AppendLine("```kusto").AppendLine(applicationInsights.Kql).AppendLine("```");
    }

    private static void Row(StringBuilder markdown, string name, string value)
    {
        markdown.AppendLine($"| {name} | {Cell(value)} |");
    }

    private static string Cell(string text)
    {
        return text.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");
    }

    private static string Number(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
