using LoadKit.Cli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("loadtest");

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
});

return await app.RunAsync(args);
