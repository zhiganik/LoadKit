using LoadKit.Cli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("loadtest");

    config.AddCommand<ValidateCommand>("validate")
        .WithDescription("Check a scenario file and its variables without sending requests.")
        .WithExample("validate", "loadtests/scenarios/my-api.json");
});

return await app.RunAsync(args);
