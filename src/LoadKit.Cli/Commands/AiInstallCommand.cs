using System.ComponentModel;
using LoadKit.Cli.Ai;
using LoadKit.Cli.Rendering;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LoadKit.Cli.Commands;

/// <summary>
/// <c>loadtest ai install</c>: writes the embedded <c>loadtest</c> skill for AI assistants (ADR-003).
/// Default: <c>./.claude/skills/loadtest/</c>; <c>--global</c>: <c>~/.claude/skills/loadtest/</c>; <c>--dir</c>: any skills folder.
/// </summary>
internal sealed class AiInstallCommand : Command<AiInstallCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--global")]
        [Description("Install into ~/.claude/skills/loadtest/ for all projects.")]
        public bool Global { get; init; }

        [CommandOption("--dir <path>")]
        [Description("Install into <path>/loadtest/, e.g. the skills folder of another AI tool.")]
        public string? Directory { get; init; }

        [CommandOption("--agents-md")]
        [Description("Also add a LoadKit block to ./AGENTS.md (for tools without skill support).")]
        public bool AgentsMd { get; init; }

        public override ValidationResult Validate()
        {
            return Global && Directory is not null
                ? ValidationResult.Error("use either --global or --dir, not both")
                : ValidationResult.Success();
        }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var console = ConsoleFactory.Create();
        var installer = new SkillInstaller(System.IO.Directory.GetCurrentDirectory(), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        var skillDirectory = settings.Directory is { } directory ? installer.CustomSkillDirectory(directory)
            : settings.Global ? installer.GlobalSkillDirectory
            : installer.ProjectSkillDirectory;

        var changes = installer.Install(skillDirectory, SkillAssets.ReadAll()).ToList();
        if (settings.AgentsMd)
        {
            changes.Add(installer.UpdateAgentsMd(skillDirectory));
        }

        foreach (var change in changes)
        {
            console.MarkupLine($"{change.Change,-9} {Markup.Escape(change.Path)}");
        }

        console.WriteLine();
        console.MarkupLine($"Installed the loadtest skill {SkillVersion.ToolMajorMinor()} into {Markup.Escape(installer.Display(skillDirectory))}.");
        if (!settings.Global && settings.Directory is null)
        {
            console.MarkupLine("In Claude Code: ask for a load test, or run /loadtest <task>. Commit .claude/skills/loadtest/ to share it with the team.");
        }

        return ExitCodes.Success;
    }
}
