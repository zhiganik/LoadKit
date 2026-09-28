using LoadKit.Cli.Ai;
using Spectre.Console;

namespace LoadKit.Cli.Rendering;

/// <summary>
/// <c>validate</c> warns when an installed skill is older than the tool: an agent following it may use outdated rules.
/// A warning only; the exit code does not change.
/// </summary>
internal static class SkillVersionWarning
{
    public static void RenderIfOutdated(IAnsiConsole console)
    {
        var installer = new SkillInstaller(Directory.GetCurrentDirectory(), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        Render(console, installer, installer.ProjectSkillDirectory, "loadtest ai install");
        Render(console, installer, installer.GlobalSkillDirectory, "loadtest ai install --global");
    }

    private static void Render(IAnsiConsole console, SkillInstaller installer, string skillDirectory, string updateCommand)
    {
        if (installer.Inspect(skillDirectory) is { IsOutdated: true } installed)
        {
            console.MarkupLine(
                $"[yellow]warning[/] (skill-outdated): the loadtest skill in {Markup.Escape(installer.Display(installed.Directory))} "
                + $"is {installed.Version?.ToString() ?? "unversioned"}, the tool is {SkillVersion.ToolMajorMinor()}; update with {updateCommand}");
        }
    }
}
