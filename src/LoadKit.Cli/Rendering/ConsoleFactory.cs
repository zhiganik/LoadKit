using Spectre.Console;

namespace LoadKit.Cli.Rendering;

/// <summary>
/// Interactive terminal: the default Spectre console. Redirected output (CI, scripts, AI agents):
/// plain text without ANSI codes and without wrapping, so it can be read and matched reliably.
/// </summary>
internal static class ConsoleFactory
{
    private const int RedirectedOutputWidth = 1000;

    /// <summary>A person can answer prompts and watch live progress. False for CI, scripts and AI agents.</summary>
    public static bool IsInteractiveTerminal => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    public static IAnsiConsole Create()
    {
        if (!Console.IsOutputRedirected)
        {
            return AnsiConsole.Console;
        }

        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
        });
        console.Profile.Width = RedirectedOutputWidth;
        return console;
    }
}
