using System.Globalization;
using System.Text;
using LoadKit.Core.Reporting;
using Spectre.Console;

namespace LoadKit.Cli.Rendering;

/// <summary>
/// Console summary after a run, built from the same <see cref="RunReport"/> as report.md and report.json.
/// Redirected output uses Markdown tables so scripts and AI agents can read it.
/// </summary>
internal static class RunSummaryRenderer
{
    private const int MaxSampleLength = 300;

    public static void Render(IAnsiConsole console, RunReport report)
    {
        var border = console.Profile.Capabilities.Interactive ? TableBorder.Rounded : TableBorder.Markdown;

        console.WriteLine();
        RenderHeadline(console, report);
        console.Write(CreateRequestTable(report, border));
        RenderStatusCodes(console, report.Overall, border);
        RenderErrorKinds(console, report.Overall, border);
        RenderSamples(console, report.ErrorSamples);
        RenderThresholds(console, report.Thresholds, border);
        RenderWarnings(console, report.Warnings);
        RenderOutcome(console, report);
    }

    private static void RenderHeadline(IAnsiConsole console, RunReport report)
    {
        console.MarkupLine(
            $"[bold]{Markup.Escape(report.Scenario.Name)}[/]: {report.Overall.Count} measured requests in "
            + $"{FormatNumber(report.Run.MeasuredSeconds)} s, {FormatNumber(report.Overall.RequestsPerSecond)} rps");
    }

    private static Table CreateRequestTable(RunReport report, TableBorder border)
    {
        var table = new Table().Border(border);
        table.AddColumn("Request");
        foreach (var column in new[] { "Count", "Errors", "Error %", "RPS", "Min ms", "Mean ms", "p50 ms", "p95 ms", "p99 ms", "Max ms" })
        {
            table.AddColumn(new TableColumn(column).RightAligned());
        }

        foreach (var request in report.Requests)
        {
            table.AddRow(CreateRow(request, isOverall: false));
        }

        table.AddRow(CreateRow(report.Overall, isOverall: true));
        return table;
    }

    private static string[] CreateRow(ReportRequestStatistics request, bool isOverall)
    {
        var latency = request.LatencyMs;
        var name = Markup.Escape(request.Name);
        return
        [
            isOverall ? $"[bold]{name}[/]" : name,
            request.Count.ToString(CultureInfo.InvariantCulture),
            request.Errors > 0 ? $"[red]{request.Errors}[/]" : "0",
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

    private static void RenderStatusCodes(IAnsiConsole console, ReportRequestStatistics overall, TableBorder border)
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
                statusCode.Status.ToString(CultureInfo.InvariantCulture),
                statusCode.Count.ToString(CultureInfo.InvariantCulture),
                statusCode.Unexpected > 0 ? $"[red]{statusCode.Unexpected}[/]" : "0");
        }

        console.Write(table);
    }

    private static void RenderErrorKinds(IAnsiConsole console, ReportRequestStatistics overall, TableBorder border)
    {
        if (overall.ErrorKinds.Count == 0)
        {
            return;
        }

        var table = new Table().Border(border).Title("Errors");
        table.AddColumn("Error");
        table.AddColumn(new TableColumn("Count").RightAligned());
        foreach (var errorKind in overall.ErrorKinds)
        {
            table.AddRow(errorKind.Kind, errorKind.Count.ToString(CultureInfo.InvariantCulture));
        }

        console.Write(table);
    }

    private static void RenderSamples(IAnsiConsole console, IReadOnlyList<ReportErrorSample> samples)
    {
        if (samples.Count == 0)
        {
            return;
        }

        console.MarkupLine("[bold]Sample errors[/]");
        foreach (var sample in samples)
        {
            var status = sample.Status > 0 ? sample.Status.ToString(CultureInfo.InvariantCulture) : "no response";
            console.MarkupLine(
                $"  [red]{status}[/] {Markup.Escape(sample.Request)} ({sample.Error}): {Markup.Escape(ToSingleLine(sample.Text))}");
        }
    }

    private static void RenderThresholds(IAnsiConsole console, IReadOnlyList<ReportThresholdCheck> checks, TableBorder border)
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

    private static void RenderWarnings(IAnsiConsole console, IReadOnlyList<ReportWarning> warnings)
    {
        foreach (var warning in warnings)
        {
            console.MarkupLine($"[yellow]warning[/] ({warning.Code}): {Markup.Escape(warning.Message)}");
        }
    }

    private static void RenderOutcome(IAnsiConsole console, RunReport report)
    {
        if (report.Run.Interrupted)
        {
            console.MarkupLine("[yellow]Interrupted:[/] the run was cancelled; results cover only the requests completed so far.");
            return;
        }

        switch (report.ThresholdsPassed)
        {
            case true:
                console.MarkupLine("[green]Thresholds passed.[/]");
                break;
            case false:
                var failed = report.Thresholds.Where(check => !check.Passed).Select(check => check.Name);
                console.MarkupLine($"[red]Thresholds failed:[/] {string.Join(", ", failed)}");
                break;
        }
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
                return builder.Append("...").ToString().Trim();
            }
        }

        return builder.Length == 0 ? "(empty body)" : builder.ToString().Trim();
    }

    private static string FormatMilliseconds(double? milliseconds)
    {
        return milliseconds is { } value ? FormatNumber(value) : "-";
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
