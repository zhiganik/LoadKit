using System.ComponentModel;
using LoadKit.Cli.Rendering;
using LoadKit.Core.Auth;
using LoadKit.Core.Engine;
using LoadKit.Core.Reporting;
using LoadKit.Core.Scenarios;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LoadKit.Cli.Commands;

/// <summary>
/// <c>loadtest run &lt;file&gt;</c>: validate, confirm a remote URL, acquire credentials, run the load, print the summary, write reports with --out.
/// Exit codes: 0 ok, 1 thresholds failed, 2 invalid scenario, 3 preflight failed, 4 confirmation required, 130 interrupted.
/// </summary>
internal sealed class RunCommand : AsyncCommand<RunCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<file>")]
        [Description("Path to the scenario JSON file.")]
        public string ScenarioFile { get; init; } = string.Empty;

        [CommandOption("--env-file <path>")]
        [Description("Path to a .env file. Default: .env next to the scenario, then in its parent folder.")]
        public string? EnvFile { get; init; }

        [CommandOption("--concurrency <n>")]
        [Description("Override load.concurrency.")]
        public int? Concurrency { get; init; }

        [CommandOption("--total <n>")]
        [Description("Override load.totalRequests (replaces durationSec).")]
        public int? TotalRequests { get; init; }

        [CommandOption("--duration <sec>")]
        [Description("Override load.durationSec (replaces totalRequests).")]
        public int? DurationSec { get; init; }

        [CommandOption("--out <dir>")]
        [Description("Folder for report.md and report.json (a subfolder per run).")]
        public string? OutDirectory { get; init; }

        [CommandOption("--no-tag")]
        [Description("Do not add loadrun=<id> to the query string.")]
        public bool NoTag { get; init; }

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

        var scenario = compiledScenario.Scenario;
        var overrides = new LoadOptionsOverrides(settings.Concurrency, settings.TotalRequests, settings.DurationSec);
        var overrideIssues = overrides.Validate(scenario.Load);
        ValidationRenderer.RenderIssues(console, [.. loadResult.Issues.Where(IsWorthShowing), .. overrideIssues]);
        if (overrideIssues.Any(issue => issue.Severity == ValidationSeverity.Error))
        {
            return ExitCodes.InvalidScenario;
        }

        var runId = scenario.TagRuns && !settings.NoTag ? RunIdGenerator.Create(TimeProvider.System, Random.Shared) : null;
        var options = RunOptions.FromLoad(overrides.ApplyTo(scenario.Load), runId);
        var secretMasker = SecretMasker.FromScenario(scenario);
        var maskedBaseUrl = secretMasker.MaskText(scenario.BaseUrl);

        if (!ScenarioCommandSteps.ConfirmRemoteUrl(console, compiledScenario, maskedBaseUrl, RunPlanText.Describe(options), settings.Yes))
        {
            return ExitCodes.ConfirmationRequired;
        }

        // Token providers refresh through this client during the run, so it lives until the end.
        using var unauthenticatedHttpClient = HttpPipelineFactory.CreateUnauthenticated();
        var (preflightSucceeded, authProvider) = await ScenarioCommandSteps.RunPreflightAsync(
            console, compiledScenario, unauthenticatedHttpClient, secretMasker);
        if (!preflightSucceeded)
        {
            return ExitCodes.PreflightFailed;
        }

        using (authProvider)
        {
            using var httpClient = HttpPipelineFactory.Create(options.Concurrency, authProvider);
            var runner = new LoadRunner(httpClient, TimeProvider.System, Random.Shared, secretMasker);
            var tag = runId is null ? string.Empty : $", {RunOptions.RunIdQueryParameter}={runId}";
            console.MarkupLine(
                $"Running [bold]{Markup.Escape(scenario.Name)}[/] against {Markup.Escape(maskedBaseUrl)}: {RunPlanText.Describe(options)}{tag}");

            var result = await RunUntilDoneOrCancelledAsync(console, runner, compiledScenario, options);
            var report = RunReportBuilder.Build(compiledScenario, result, ToolInfo.Version, secretMasker, authProvider);
            RunSummaryRenderer.Render(console, report);
            if (settings.OutDirectory is { } outDirectory)
            {
                var reportFolder = await ReportFileWriter.WriteAsync(report, outDirectory, CancellationToken.None);
                var markdownPath = Path.GetRelativePath(Directory.GetCurrentDirectory(), Path.Combine(reportFolder, ReportFileWriter.MarkdownFileName));
                console.MarkupLine($"Report: {Markup.Escape(markdownPath)} (and {ReportFileWriter.JsonFileName} next to it)");
            }

            if (result.Interrupted)
            {
                return ExitCodes.Interrupted;
            }

            return result.ThresholdsPassed ? ExitCodes.Success : ExitCodes.ThresholdsFailed;
        }
    }

    private static bool IsWorthShowing(ValidationIssue issue)
    {
        // remote-url info is replaced by the confirmation step.
        return issue.Severity != ValidationSeverity.Info;
    }

    /// <summary>First Ctrl+C stops the run and keeps the collected data; a second one terminates the process.</summary>
    private static async Task<RunResult> RunUntilDoneOrCancelledAsync(
        IAnsiConsole console,
        LoadRunner runner,
        CompiledScenario scenario,
        RunOptions options)
    {
        using var interruption = new CancellationTokenSource();
        ConsoleCancelEventHandler onCancelKeyPress = (_, eventArgs) =>
        {
            if (!interruption.IsCancellationRequested)
            {
                eventArgs.Cancel = true;
                interruption.Cancel();
            }
        };

        Console.CancelKeyPress += onCancelKeyPress;
        try
        {
            if (!console.Profile.Capabilities.Interactive)
            {
                return await runner.RunAsync(scenario, options, progress: null, interruption.Token);
            }

            return await console.Progress()
                .AutoClear(true)
                .Columns(new ProgressBarColumn(), new PercentageColumn(), new ElapsedTimeColumn(), new TaskDescriptionColumn())
                .StartAsync(async progressContext =>
                {
                    var task = progressContext.AddTask("starting", maxValue: 1);
                    return await runner.RunAsync(scenario, options, new ProgressTaskReporter(task), interruption.Token);
                });
        }
        finally
        {
            Console.CancelKeyPress -= onCancelKeyPress;
        }
    }
}
