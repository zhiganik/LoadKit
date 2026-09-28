using System.Globalization;
using System.Text;
using LoadKit.Core.Engine;
using LoadKit.Core.Metrics;
using Spectre.Console;

namespace LoadKit.Cli.Rendering;

/// <summary>
/// Console summary after a run: per-request and overall latency, status codes, errors, samples, thresholds.
/// Redirected output uses Markdown tables so scripts and AI agents can read it.
/// </summary>
internal static class RunSummaryRenderer
{
    private const int MaxSampleLength = 300;

    public static void Render(IAnsiConsole console, RunResult result, bool usesAuth)
    {
        var border = console.Profile.Capabilities.Interactive ? TableBorder.Rounded : TableBorder.Markdown;
        var statistics = result.Statistics;

        console.WriteLine();
        RenderHeadline(console, result);
        console.Write(CreateRequestTable(statistics, border));
        RenderStatusCodes(console, statistics.Overall, border);
        RenderErrors(console, statistics.Overall, border);
        RenderSamples(console, result.ErrorSamples);
        RenderUnauthorizedHint(console, statistics.Overall, usesAuth);
        RenderThresholds(console, result.ThresholdChecks, border);
        RenderOutcome(console, result);
    }

    private static void RenderHeadline(IAnsiConsole console, RunResult result)
    {
        var overall = result.Statistics.Overall;
        var seconds = FormatNumber(result.Statistics.MeasuredDuration.TotalSeconds);
        console.MarkupLine(
            $"[bold]{Markup.Escape(result.ScenarioName)}[/]: {overall.Count} measured requests in {seconds} s, "
            + $"{FormatNumber(overall.RequestsPerSecond)} rps");
    }

    private static Table CreateRequestTable(RunStatistics statistics, TableBorder border)
    {
        var table = new Table().Border(border);
        table.AddColumn("Request");
        foreach (var column in new[] { "Count", "Errors", "Error %", "RPS", "Min ms", "Mean ms", "p50 ms", "p95 ms", "p99 ms", "Max ms" })
        {
            table.AddColumn(new TableColumn(column).RightAligned());
        }

        foreach (var request in statistics.Requests)
        {
            table.AddRow(CreateRow(request, isOverall: false));
        }

        table.AddRow(CreateRow(statistics.Overall, isOverall: true));
        return table;
    }

    private static string[] CreateRow(RequestStatistics request, bool isOverall)
    {
        var latency = request.Latency;
        var name = Markup.Escape(request.Name);
        return
        [
            isOverall ? $"[bold]{name}[/]" : name,
            request.Count.ToString(CultureInfo.InvariantCulture),
            request.ErrorCount > 0 ? $"[red]{request.ErrorCount}[/]" : "0",
            FormatNumber(request.ErrorRatePercent),
            FormatNumber(request.RequestsPerSecond),
            FormatMilliseconds(latency?.Min),
            FormatMilliseconds(latency?.Mean),
            FormatMilliseconds(latency?.P50),
            FormatMilliseconds(latency?.P95),
            FormatMilliseconds(latency?.P99),
            FormatMilliseconds(latency?.Max),
        ];
    }

    private static void RenderStatusCodes(IAnsiConsole console, RequestStatistics overall, TableBorder border)
    {
        if (overall.StatusCodes.Count == 0)
        {
            return;
        }

        var table = new Table().Border(border).Title("Status codes");
        table.AddColumn("Status");
        table.AddColumn(new TableColumn("Count").RightAligned());
        table.AddColumn(new TableColumn("Unexpected").RightAligned());
        foreach (var statusCode in overall.StatusCodes)
        {
            table.AddRow(
                statusCode.StatusCode.ToString(CultureInfo.InvariantCulture),
                statusCode.Count.ToString(CultureInfo.InvariantCulture),
                statusCode.UnexpectedCount > 0 ? $"[red]{statusCode.UnexpectedCount}[/]" : "0");
        }

        console.Write(table);
    }

    private static void RenderErrors(IAnsiConsole console, RequestStatistics overall, TableBorder border)
    {
        if (overall.Errors.Count == 0)
        {
            return;
        }

        var table = new Table().Border(border).Title("Errors");
        table.AddColumn("Error");
        table.AddColumn(new TableColumn("Count").RightAligned());
        foreach (var error in overall.Errors)
        {
            table.AddRow(error.Error.ToString(), error.Count.ToString(CultureInfo.InvariantCulture));
        }

        console.Write(table);
    }

    private static void RenderSamples(IAnsiConsole console, IReadOnlyList<ErrorSample> samples)
    {
        if (samples.Count == 0)
        {
            return;
        }

        console.MarkupLine("[bold]Sample errors[/]");
        foreach (var sample in samples)
        {
            var status = sample.StatusCode > 0 ? sample.StatusCode.ToString(CultureInfo.InvariantCulture) : "no response";
            console.MarkupLine(
                $"  [red]{status}[/] {Markup.Escape(sample.RequestName)} ({sample.Error}): {Markup.Escape(ToSingleLine(sample.Text))}");
        }
    }

    private static void RenderUnauthorizedHint(IAnsiConsole console, RequestStatistics overall, bool usesAuth)
    {
        foreach (var statusCode in overall.StatusCodes)
        {
            if (usesAuth && statusCode.StatusCode == 401 && statusCode.UnexpectedCount > 0)
            {
                console.MarkupLine("[yellow]hint:[/] unexpected 401 responses: the token or key may be expired or wrong; update the variable in .env");
                return;
            }
        }
    }

    private static void RenderThresholds(IAnsiConsole console, IReadOnlyList<ThresholdCheck> checks, TableBorder border)
    {
        if (checks.Count == 0)
        {
            return;
        }

        var table = new Table().Border(border).Title("Thresholds");
        table.AddColumn("Threshold");
        table.AddColumn(new TableColumn("Limit").RightAligned());
        table.AddColumn(new TableColumn("Actual").RightAligned());
        table.AddColumn("Result");
        foreach (var check in checks)
        {
            table.AddRow(
                check.Name,
                FormatNumber(check.Limit),
                check.Actual is { } actual ? FormatNumber(actual) : "n/a",
                check.Passed ? "[green]ok[/]" : "[red]FAIL[/]");
        }

        console.Write(table);
    }

    private static void RenderOutcome(IAnsiConsole console, RunResult result)
    {
        if (result.Interrupted)
        {
            console.MarkupLine("[yellow]Interrupted:[/] the run was cancelled; results cover only the requests completed so far.");
            return;
        }

        if (result.ThresholdChecks.Count == 0)
        {
            return;
        }

        if (result.ThresholdsPassed)
        {
            console.MarkupLine("[green]Thresholds passed.[/]");
            return;
        }

        var failed = new List<string>();
        foreach (var check in result.ThresholdChecks)
        {
            if (!check.Passed)
            {
                failed.Add(check.Name);
            }
        }

        console.MarkupLine($"[red]Thresholds failed:[/] {string.Join(", ", failed)}");
    }

    private static string ToSingleLine(string text)
    {
        var builder = new StringBuilder(Math.Min(text.Length, MaxSampleLength));
        var previousWasWhitespace = false;
        foreach (var character in text)
        {
            var isWhitespace = char.IsWhiteSpace(character);
            if (!(isWhitespace && previousWasWhitespace))
            {
                builder.Append(isWhitespace ? ' ' : character);
            }

            previousWasWhitespace = isWhitespace;
            if (builder.Length >= MaxSampleLength)
            {
                return builder.Append('…').ToString().Trim();
            }
        }

        return builder.Length == 0 ? "(empty body)" : builder.ToString().Trim();
    }

    private static string FormatMilliseconds(TimeSpan? value)
    {
        return value is { } duration ? FormatNumber(duration.TotalMilliseconds) : "-";
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
