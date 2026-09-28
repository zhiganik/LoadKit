using System.Diagnostics;

namespace LoadKit.IntegrationTests;

/// <summary>Runs the built <c>loadtest</c> CLI as a real process, the way users and AI agents do.</summary>
internal static class CliRunner
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <param name="workingDirectory">Null: the test process's directory.</param>
    public static async Task<CliResult> RunAsync(IEnumerable<string> arguments, CancellationToken cancellationToken, string? workingDirectory = null)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory ?? string.Empty,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "loadtest.dll"));
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start loadtest.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        var standardOutput = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var standardError = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);
        return new CliResult(process.ExitCode, await standardOutput, await standardError);
    }
}
