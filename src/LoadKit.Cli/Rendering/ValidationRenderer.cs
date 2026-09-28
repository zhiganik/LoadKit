using LoadKit.Core.Scenarios;
using Spectre.Console;

namespace LoadKit.Cli.Rendering;

/// <summary>Prints validation issues: severity, JSON path, code, message and hint.</summary>
internal static class ValidationRenderer
{
    public static void Render(IAnsiConsole console, string scenarioFilePath, ScenarioLoadResult result)
    {
        RenderIssues(console, result.Issues);

        var escapedPath = Markup.Escape(scenarioFilePath);
        if (result.Scenario is { } scenario)
        {
            console.MarkupLine(
                $"[green]Valid:[/] {escapedPath} ({scenario.Requests.Count} request(s), {result.WarningCount} warning(s))");
        }
        else
        {
            console.MarkupLine($"[red]Invalid:[/] {escapedPath} ({result.ErrorCount} error(s), {result.WarningCount} warning(s))");
        }
    }

    public static void RenderIssues(IAnsiConsole console, IReadOnlyList<ValidationIssue> issues)
    {
        foreach (var issue in issues)
        {
            RenderIssue(console, issue);
        }
    }

    private static void RenderIssue(IAnsiConsole console, ValidationIssue issue)
    {
        var (label, color) = issue.Severity switch
        {
            ValidationSeverity.Error => ("error", "red"),
            ValidationSeverity.Warning => ("warning", "yellow"),
            _ => ("info", "blue"),
        };

        console.MarkupLine($"[{color}]{label}[/] [bold]{Markup.Escape(issue.Path)}[/] ({issue.Code})");
        console.MarkupLine($"  {Markup.Escape(issue.Message)}");
        if (issue.Hint is not null)
        {
            console.MarkupLine($"  [grey]hint:[/] {Markup.Escape(issue.Hint)}");
        }
    }
}
