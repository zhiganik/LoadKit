using System.ComponentModel;
using LoadKit.Cli.Rendering;
using LoadKit.Core.Scenarios;
using LoadKit.Core.Scenarios.Model;
using LoadKit.Core.Scenarios.Scaffolding;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LoadKit.Cli.Commands;

/// <summary>
/// <c>loadtest init &lt;file&gt;</c>: scenario, schema, <c>.env</c> with empty variables, <c>.gitignore</c> entries.
/// In a terminal it asks for what the flags did not give; otherwise flags only. Exit 0, or 2 on invalid input.
/// </summary>
internal sealed class InitCommand : AsyncCommand<InitCommand.Settings>
{
    private static readonly (string AuthType, string Label)[] AuthLabels =
    [
        (ScenarioScaffolder.NoAuth, "No auth"),
        (AuthTypeNames.Bearer, "I copy a token by hand (Swagger, Postman, DevTools)"),
        (AuthTypeNames.ApiKey, "An API key in a header (e.g. an Azure Functions key)"),
        (AuthTypeNames.AzureIdentity, "Entra ID, and I am logged in with az login"),
        (AuthTypeNames.OAuth2ClientCredentials, "A service client with a client secret (OAuth2)"),
        (AuthTypeNames.Login, "The API has its own login endpoint"),
    ];

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<file>")]
        [Description("Scenario to create, e.g. loadtests/scenarios/my-api.json.")]
        public string ScenarioFile { get; init; } = string.Empty;

        [CommandOption("--base-url <url>")]
        [Description("API address.")]
        public string? BaseUrl { get; init; }

        [CommandOption("--auth <type>")]
        [Description("none, bearer, apiKey, azureIdentity, oauth2ClientCredentials, login.")]
        public string? Auth { get; init; }

        [CommandOption("--source <source>")]
        [Description("For azureIdentity: azureCli (default) or default.")]
        public string? Source { get; init; }

        [CommandOption("--scope <scope>")]
        [Description("For azureIdentity and oauth2ClientCredentials, e.g. api://my-api/.default.")]
        public string? Scope { get; init; }

        [CommandOption("--header <name>")]
        [Description("For apiKey (default x-functions-key).")]
        public string? Header { get; init; }

        [CommandOption("--no-interactive")]
        [Description("Do not ask questions (automatic without a terminal).")]
        public bool NoInteractive { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var console = ConsoleFactory.Create();
        var interactive = ConsoleFactory.IsInteractiveTerminal && !settings.NoInteractive;
        var options = interactive ? Ask(console, settings) : FromFlags(settings);
        if (options is null)
        {
            console.MarkupLine("[red]error[/] [bold]--base-url[/] (required-field)");
            console.MarkupLine("  --base-url is required without a terminal");
            console.MarkupLine("  [grey]hint:[/] loadtest init <file> --base-url http://localhost:5000 --auth none");
            return ExitCodes.InvalidScenario;
        }

        var initializer = new WorkspaceInitializer(Directory.GetCurrentDirectory());
        IReadOnlyList<ValidationIssue> issues = [.. ScenarioScaffolder.Validate(options), .. initializer.CheckTarget(options)];
        if (issues.Count > 0)
        {
            ValidationRenderer.RenderIssues(console, issues);
            return ExitCodes.InvalidScenario;
        }

        var result = await initializer.InitializeAsync(options, ToolInfo.ReadScenarioSchema(), CancellationToken.None);
        Render(console, result);
        return ExitCodes.Success;
    }

    private static InitOptions? FromFlags(Settings settings)
    {
        return settings.BaseUrl is null
            ? null
            : new InitOptions(settings.ScenarioFile, settings.BaseUrl, settings.Auth ?? ScenarioScaffolder.NoAuth, settings.Source, settings.Scope, settings.Header);
    }

    private static InitOptions Ask(IAnsiConsole console, Settings settings)
    {
        var baseUrl = settings.BaseUrl ?? console.Prompt(new TextPrompt<string>("API address (baseUrl):")
            .Validate(value => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme.StartsWith("http", StringComparison.Ordinal)
                ? ValidationResult.Success()
                : ValidationResult.Error("enter an http(s) URL, e.g. http://localhost:5000")));

        var authType = settings.Auth ?? console.Prompt(new SelectionPrompt<(string AuthType, string Label)>()
            .Title("How is the API protected?")
            .UseConverter(choice => Markup.Escape(choice.Label))
            .AddChoices(AuthLabels)).AuthType;

        var scope = settings.Scope;
        if (scope is null && authType is AuthTypeNames.AzureIdentity or AuthTypeNames.OAuth2ClientCredentials)
        {
            scope = console.Prompt(new TextPrompt<string>("Scope (for your own API: api://<application-id-uri>/.default):"));
        }

        var header = settings.Header;
        if (header is null && authType == AuthTypeNames.ApiKey)
        {
            header = console.Prompt(new TextPrompt<string>("Header name for the key:").DefaultValue(ScenarioScaffolder.DefaultApiKeyHeader));
        }

        return new InitOptions(settings.ScenarioFile, baseUrl, authType, settings.Source, scope, header);
    }

    private static void Render(IAnsiConsole console, InitResult result)
    {
        foreach (var change in result.Changes)
        {
            console.MarkupLine($"{change.Change,-9} {Markup.Escape(change.Path)}");
        }

        console.WriteLine();
        console.MarkupLine("[bold]Next steps[/]");
        var step = 1;
        if (result.EmptyVariables.Count > 0)
        {
            console.MarkupLine($"  {step++}. Fill in {Markup.Escape(string.Join(", ", result.EmptyVariables))} in {Markup.Escape(result.EnvFilePath)} (never in the scenario).");
        }

        console.MarkupLine($"  {step++}. Replace the placeholder request in {Markup.Escape(result.ScenarioPath)} with your endpoints.");
        console.MarkupLine($"  {step++}. loadtest validate {Markup.Escape(result.ScenarioPath)}");
        console.MarkupLine($"  {step++}. loadtest check {Markup.Escape(result.ScenarioPath)}");
        console.MarkupLine($"  {step}. loadtest run {Markup.Escape(result.ScenarioPath)} --out {Markup.Escape(result.ReportsPath)}");
    }
}
