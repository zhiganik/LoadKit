using System.ComponentModel;
using LoadKit.Cli.Rendering;
using LoadKit.Core.Auth;
using LoadKit.Core.Engine;
using LoadKit.Core.Scenarios;
using Spectre.Console.Cli;

namespace LoadKit.Cli.Commands;

/// <summary>
/// <c>loadtest check &lt;file&gt;</c>: validate, preflight, then one real request per <c>requests[]</c> entry.
/// Exit codes: 0 (also when some statuses are unexpected: they are shown), 2 invalid scenario, 3 preflight failed,
/// 4 confirmation required.
/// </summary>
internal sealed class CheckCommand : AsyncCommand<CheckCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<file>")]
        [Description("Path to the scenario JSON file.")]
        public string ScenarioFile { get; init; } = string.Empty;

        [CommandOption("--env-file <path>")]
        [Description("Path to a .env file. Default: .env next to the scenario, then in its parent folder.")]
        public string? EnvFile { get; init; }

        [CommandOption("--yes")]
        [Description("Confirm a non-localhost URL in advance.")]
        public bool Yes { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var console = ConsoleFactory.Create();
        var loadResult = await new ScenarioLoader().LoadAsync(settings.ScenarioFile, settings.EnvFile, CancellationToken.None);
        if (loadResult.Scenario is not { } compiledScenario)
        {
            ValidationRenderer.Render(console, settings.ScenarioFile, loadResult);
            return ExitCodes.InvalidScenario;
        }

        ValidationRenderer.RenderIssues(console, [.. loadResult.Issues.Where(issue => issue.Severity != ValidationSeverity.Info)]);
        var scenario = compiledScenario.Scenario;
        var secretMasker = SecretMasker.FromScenario(scenario);
        var maskedBaseUrl = secretMasker.MaskText(scenario.BaseUrl);
        var plan = $"one request for each of {compiledScenario.Requests.Count} request(s)";
        if (!ScenarioCommandSteps.ConfirmRemoteUrl(console, compiledScenario, maskedBaseUrl, plan, settings.Yes))
        {
            return ExitCodes.ConfirmationRequired;
        }

        using var unauthenticatedHttpClient = HttpPipelineFactory.CreateUnauthenticated();
        var (preflightSucceeded, authProvider) = await ScenarioCommandSteps.RunPreflightAsync(
            console, compiledScenario, unauthenticatedHttpClient, secretMasker);
        if (!preflightSucceeded)
        {
            return ExitCodes.PreflightFailed;
        }

        using (authProvider)
        {
            using var httpClient = HttpPipelineFactory.Create(concurrency: 1, authProvider);
            var checker = new ScenarioChecker(httpClient, TimeProvider.System, Random.Shared, secretMasker);
            var results = await checker.CheckAsync(compiledScenario, CancellationToken.None);
            CheckRenderer.Render(console, results);
        }

        return ExitCodes.Success;
    }
}
