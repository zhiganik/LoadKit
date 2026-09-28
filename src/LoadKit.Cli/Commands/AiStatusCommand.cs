using LoadKit.Cli.Ai;
using LoadKit.Cli.Rendering;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LoadKit.Cli.Commands;

/// <summary><c>loadtest ai status</c>: where the skill is installed and whether its version matches the tool. Exit 0.</summary>
internal sealed class AiStatusCommand : Command
{
    public override int Execute(CommandContext context)
    {
        var console = ConsoleFactory.Create();
        var installer = new SkillInstaller(Directory.GetCurrentDirectory(), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        console.MarkupLine($"loadtest {ToolInfo.Version} (skill version {SkillVersion.ToolMajorMinor()})");
        RenderLocation(console, installer, "project", installer.ProjectSkillDirectory);
        RenderLocation(console, installer, "global", installer.GlobalSkillDirectory);
        console.MarkupLine(installer.AgentsMdHasBlock()
            ? "AGENTS.md: has the LoadKit block"
            : "AGENTS.md: no LoadKit block (add with loadtest ai install --agents-md)");
        return ExitCodes.Success;
    }

    private static void RenderLocation(IAnsiConsole console, SkillInstaller installer, string label, string skillDirectory)
    {
        var location = Markup.Escape(installer.Display(skillDirectory));
        var installed = installer.Inspect(skillDirectory);
        if (installed is null)
        {
            console.MarkupLine($"{label}: not installed ({location})");
            return;
        }

        var version = installed.Version?.ToString() ?? "unknown";
        var command = label == "global" ? "loadtest ai install --global" : "loadtest ai install";
        if (installed.IsOutdated)
        {
            console.MarkupLine($"{label}: [yellow]{version}, older than the tool[/] ({location}); update with {command}");
        }
        else if (installed.IsNewerThanTool)
        {
            console.MarkupLine($"{label}: [yellow]{version}, newer than the tool[/] ({location}); update loadtest (dotnet tool update -g LoadKit)");
        }
        else
        {
            console.MarkupLine($"{label}: [green]{version}, up to date[/] ({location})");
        }
    }
}
