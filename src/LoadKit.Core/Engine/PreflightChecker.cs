using System.Globalization;
using LoadKit.Core.Auth;
using LoadKit.Core.Metrics;
using LoadKit.Core.Scenarios;

namespace LoadKit.Core.Engine;

/// <summary>
/// Before any load: is <c>baseUrl</c> reachable (any HTTP response counts), then acquire the first token.
/// Used by both <c>check</c> and <c>run</c>.
/// </summary>
/// <param name="unauthenticatedHttpClient">From <see cref="HttpPipelineFactory.CreateUnauthenticated"/>.</param>
public sealed class PreflightChecker(HttpClient unauthenticatedHttpClient, TimeProvider timeProvider, SecretMasker secretMasker)
{
    public const string BaseUrlStep = "baseUrl";
    public const string AuthStep = "auth";

    public async Task<PreflightResult> RunAsync(CompiledScenario scenario, IAuthProvider? authProvider, CancellationToken cancellationToken)
    {
        var steps = new List<PreflightStep> { await CheckBaseUrlAsync(scenario, cancellationToken) };
        if (steps[0].Succeeded && authProvider is not null)
        {
            steps.Add(await InitializeAuthAsync(scenario, authProvider, cancellationToken));
        }

        return new PreflightResult(steps);
    }

    private async Task<PreflightStep> CheckBaseUrlAsync(CompiledScenario scenario, CancellationToken cancellationToken)
    {
        var baseUrl = secretMasker.MaskText(scenario.Scenario.BaseUrl);
        try
        {
            using var response = await unauthenticatedHttpClient.GetAsync(scenario.BaseUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return new PreflightStep(BaseUrlStep, true, $"{baseUrl} is reachable (HTTP {(int)response.StatusCode})", null, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var seconds = HttpPipelineFactory.UnauthenticatedRequestTimeout.TotalSeconds.ToString(CultureInfo.InvariantCulture);
            return Failed(BaseUrlStep, $"{baseUrl} did not respond within {seconds} s", "check that the API is running and baseUrl is correct");
        }
        catch (HttpRequestException exception)
        {
            var hint = TransportErrorClassifier.Classify(exception) == ErrorKind.Tls
                ? "TLS failed; for a local https API run 'dotnet dev-certs https --trust', or use its http address"
                : "check that the API is running and baseUrl (host and port) is correct";
            return Failed(BaseUrlStep, $"{baseUrl} is not reachable: {exception.Message}", hint);
        }
    }

    private async Task<PreflightStep> InitializeAuthAsync(CompiledScenario scenario, IAuthProvider authProvider, CancellationToken cancellationToken)
    {
        var authType = scenario.Scenario.Auth!.Type;
        try
        {
            await authProvider.InitializeAsync(cancellationToken);
        }
        catch (AuthException exception)
        {
            return Failed(AuthStep, $"{authType}: {exception.Message}", exception.Hint);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Failed(AuthStep, $"{authType}: {exception.Message}", "check the auth section of the scenario");
        }

        if (authProvider is not TokenAuthProviderBase tokenProvider)
        {
            return new PreflightStep(AuthStep, true, $"{authType}: credentials ready", null, null);
        }

        if (tokenProvider.ExpiresAt is not { } expiresAt)
        {
            return new PreflightStep(
                AuthStep,
                true,
                $"{authType}: token acquired",
                null,
                "token lifetime unknown (no expiry in the response and no JWT exp): it is treated as non-expiring");
        }

        var lifetime = expiresAt - timeProvider.GetUtcNow();
        return new PreflightStep(AuthStep, true, $"{authType}: token acquired, expires in {FormatLifetime(lifetime)}", null, null);
    }

    private PreflightStep Failed(string name, string message, string? hint)
    {
        return new PreflightStep(name, false, secretMasker.MaskText(message), hint is null ? null : secretMasker.MaskText(hint), null);
    }

    private static string FormatLifetime(TimeSpan lifetime)
    {
        return lifetime.TotalMinutes >= 2
            ? $"{Math.Round(lifetime.TotalMinutes).ToString(CultureInfo.InvariantCulture)} min"
            : $"{Math.Round(lifetime.TotalSeconds).ToString(CultureInfo.InvariantCulture)} s";
    }
}
