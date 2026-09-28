using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("loadtest");
    config.PropagateExceptions();
});

return await app.RunAsync(args);
