using System.Globalization;
using LoadKit.Core.Engine;
using Spectre.Console;

namespace LoadKit.Cli.Rendering;

/// <summary>Prints one block per checked request: verdict, method, URL, status, time and the start of the body.</summary>
internal static class CheckRenderer
{
    private const int MaxBodyLineLength = 300;

    public static void Render(IAnsiConsole console, IReadOnlyList<RequestCheckResult> results)
    {
        var unexpectedCount = 0;
        foreach (var result in results)
        {
            if (!result.IsExpected)
            {
                unexpectedCount++;
            }

            RenderResult(console, result);
        }

        if (unexpectedCount == 0)
        {
            console.MarkupLine($"[green]All {results.Count} request(s) returned an expected status.[/]");
            return;
        }

        console.MarkupLine($"[red]{unexpectedCount} of {results.Count} request(s) did not return an expected status.[/] Fix them before running load.");
    }

    private static void RenderResult(IAnsiConsole console, RequestCheckResult result)
    {
        var verdict = result.IsExpected ? "[green]ok[/]" : "[red]UNEXPECTED[/]";
        var milliseconds = result.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture);
        var outcome = result.StatusCode > 0
            ? $"{result.StatusCode} in {milliseconds} ms"
            : $"no response ({result.Error}) after {milliseconds} ms";
        console.MarkupLine(
            $"{verdict} [bold]{Markup.Escape(result.Name)}[/] {result.Method} {Markup.Escape(result.Url)} → {Markup.Escape(outcome)}");

        var detail = result.ErrorMessage ?? (result.BodyStart.Length == 0 ? "(empty body)" : result.BodyStart);
        console.MarkupLine($"    {Markup.Escape(ToSingleLine(detail))}");
    }

    private static string ToSingleLine(string text)
    {
        var singleLine = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return singleLine.Length <= MaxBodyLineLength ? singleLine : string.Concat(singleLine.AsSpan(0, MaxBodyLineLength), "…");
    }
}
