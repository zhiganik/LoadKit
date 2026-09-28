using LoadKit.Cli.Rendering;
using LoadKit.Core.Auth;
using LoadKit.Core.Engine;
using LoadKit.Core.Scenarios;
using Spectre.Console;

namespace LoadKit.Cli.Commands;

/// <summary>Steps shared by <c>check</c> and <c>run</c>: remote URL confirmation and preflight.</summary>
internal static class ScenarioCommandSteps
{
    /// <returns>True when the URL is local, confirmed in advance (<c>--yes</c>) or confirmed at the prompt.</returns>
    public static bool ConfirmRemoteUrl(IAnsiConsole console, CompiledScenario scenario, string maskedBaseUrl, string plan, bool confirmedInAdvance)
    {
        if (LocalAddress.IsLocal(scenario.BaseUri) || confirmedInAdvance)
        {
            return true;
        }

        if (!ConsoleFactory.IsInteractiveTerminal)
        {
            console.MarkupLine($"[red]confirmation required:[/] {Markup.Escape(maskedBaseUrl)} is not localhost ({Markup.Escape(plan)})");
            console.MarkupLine("  get confirmation from the owner of the API and retry with --yes");
            return false;
        }

        if (console.Confirm($"Load {Markup.Escape(maskedBaseUrl)}? {Markup.Escape(plan)}", defaultValue: false))
        {
            return true;
        }

        console.MarkupLine("Cancelled: nothing was sent.");
        return false;
    }

    /// <summary>Checks baseUrl and acquires the first token. On failure prints the hint and disposes the provider.</summary>
    /// <param name="unauthenticatedHttpClient">Also used by token providers to refresh; keep it alive for the whole command.</param>
    /// <returns>Null provider with <c>Succeeded = false</c> means exit code 3.</returns>
    public static async Task<(bool Succeeded, IAuthProvider? AuthProvider)> RunPreflightAsync(
        IAnsiConsole console,
        CompiledScenario scenario,
        HttpClient unauthenticatedHttpClient,
        SecretMasker secretMasker)
    {
        var authProvider = new AuthProviderFactory(unauthenticatedHttpClient, TimeProvider.System, secretMasker).Create(scenario.Scenario);
        var preflight = await new PreflightChecker(unauthenticatedHttpClient, TimeProvider.System, secretMasker)
            .RunAsync(scenario, authProvider, CancellationToken.None);
        PreflightRenderer.Render(console, preflight);
        if (preflight.Succeeded)
        {
            return (true, authProvider);
        }

        authProvider?.Dispose();
        return (false, null);
    }
}
