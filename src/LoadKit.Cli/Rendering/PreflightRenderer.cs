using LoadKit.Core.Engine;
using Spectre.Console;

namespace LoadKit.Cli.Rendering;

/// <summary>Prints preflight steps: "ok baseUrl: ...", warnings, or "preflight failed" with a hint.</summary>
internal static class PreflightRenderer
{
    public static void Render(IAnsiConsole console, PreflightResult result)
    {
        foreach (var step in result.Steps)
        {
            if (!step.Succeeded)
            {
                console.MarkupLine($"[red]preflight failed[/] [bold]{step.Name}[/]: {Markup.Escape(step.Message)}");
                if (step.Hint is not null)
                {
                    console.MarkupLine($"  [grey]hint:[/] {Markup.Escape(step.Hint)}");
                }

                continue;
            }

            console.MarkupLine($"[green]ok[/] {step.Name}: {Markup.Escape(step.Message)}");
            if (step.Warning is not null)
            {
                console.MarkupLine($"  [yellow]warning:[/] {Markup.Escape(step.Warning)}");
            }
        }
    }
}
