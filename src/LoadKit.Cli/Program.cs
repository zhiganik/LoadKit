using LoadKit.Cli;
using LoadKit.Cli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("loadtest");
    config.SetApplicationVersion(ToolInfo.Version);

    config.AddCommand<InitCommand>("init")
        .WithDescription("Create a scenario, a .env template and .gitignore entries.")
        .WithExample("init", "loadtests/scenarios/my-api.json", "--base-url", "http://localhost:5000", "--auth", "bearer");

    config.AddCommand<ValidateCommand>("validate")
        .WithDescription("Check a scenario file and its variables without sending requests.")
        .WithExample("validate", "loadtests/scenarios/my-api.json");

    config.AddCommand<CheckCommand>("check")
        .WithDescription("Preflight plus one real request per scenario request: status, time and the start of the body.")
        .WithExample("check", "loadtests/scenarios/my-api.json");

    config.AddCommand<RunCommand>("run")
        .WithDescription("Run the load described by a scenario and print percentiles, status codes and errors.")
        .WithExample("run", "loadtests/scenarios/my-api.json")
        .WithExample("run", "loadtests/scenarios/my-api.json", "--concurrency", "20", "--duration", "60");

    config.AddBranch("ai", ai =>
    {
        ai.SetDescription("Install the loadtest skill for AI assistants and check its version.");
        ai.AddCommand<AiInstallCommand>("install")
            .WithDescription("Install the loadtest skill (default: ./.claude/skills/loadtest/).")
            .WithExample("ai", "install")
            .WithExample("ai", "install", "--global");
        ai.AddCommand<AiStatusCommand>("status")
            .WithDescription("Show where the skill is installed and whether its version matches the tool.");
    });
});

return await app.RunAsync(args);
