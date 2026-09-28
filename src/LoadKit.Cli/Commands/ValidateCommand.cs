using System.ComponentModel;
using LoadKit.Cli.Rendering;
using LoadKit.Core.Scenarios;
using Spectre.Console.Cli;

namespace LoadKit.Cli.Commands;

/// <summary><c>loadtest validate &lt;file&gt;</c>: checks format and variables, sends nothing. Exit 0 or 2.</summary>
internal sealed class ValidateCommand : AsyncCommand<ValidateCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<file>")]
        [Description("Path to the scenario JSON file.")]
        public string ScenarioFile { get; init; } = string.Empty;

        [CommandOption("--env-file <path>")]
        [Description("Path to a .env file. Default: .env next to the scenario, then in its parent folder.")]
        public string? EnvFile { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var result = await new ScenarioLoader().LoadAsync(settings.ScenarioFile, settings.EnvFile, CancellationToken.None);
        ValidationRenderer.Render(ConsoleFactory.Create(), settings.ScenarioFile, result);
        return result.IsValid ? ExitCodes.Success : ExitCodes.InvalidScenario;
    }
}
