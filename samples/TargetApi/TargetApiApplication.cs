using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;

namespace TargetApi;

/// <summary>
/// Builds the sample API. Integration tests call <see cref="Create"/> with <c>--urls=http://127.0.0.1:0</c>
/// to run it on real Kestrel with a random free port.
/// </summary>
public static class TargetApiApplication
{
    public const string DependencyHttpClientName = "dependency";

    public static WebApplication Create(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.Configure<TargetApiOptions>(builder.Configuration.GetSection(TargetApiOptions.SectionName));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<TokenStore>();
        builder.Services.AddSingleton<CredentialChecker>();
        builder.Services.AddHttpClient(DependencyHttpClientName);

        var app = builder.Build();
        MapLoadEndpoints(app);
        MapAuthEndpoints(app);
        return app;
    }

    private static void MapLoadEndpoints(WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

        app.MapGet("/api/fast", () => Results.Ok(new { result = "fast" }));

        app.MapGet("/api/slow", async (int? delayMs, IOptions<TargetApiOptions> options, CancellationToken cancellationToken) =>
        {
            var effectiveDelayMs = delayMs ?? options.Value.SlowDelayMs;
            await Task.Delay(TimeSpan.FromMilliseconds(effectiveDelayMs), cancellationToken);
            return Results.Ok(new { result = "slow", delayMs = effectiveDelayMs });
        });

        app.MapGet("/api/fail", (double? probability, IOptions<TargetApiOptions> options) =>
        {
            var failProbability = probability ?? options.Value.FailProbability;
            return Random.Shared.NextDouble() < failProbability
                ? Results.Problem("Simulated failure.", statusCode: StatusCodes.Status500InternalServerError)
                : Results.Ok(new { result = "ok" });
        });

        app.MapGet("/api/dep", async (IHttpClientFactory httpClientFactory, IServer server, CancellationToken cancellationToken) =>
        {
            var dependencyUrl = new Uri(new Uri(GetOwnAddress(server)), "/fake-dependency");
            var httpClient = httpClientFactory.CreateClient(DependencyHttpClientName);
            using var response = await httpClient.GetAsync(dependencyUrl, cancellationToken);
            return response.IsSuccessStatusCode
                ? Results.Ok(new { result = "dep", dependencyStatus = (int)response.StatusCode })
                : Results.Problem("Dependency failed.", statusCode: StatusCodes.Status502BadGateway);
        });

        // Stands in for an external service called by /api/dep, so Application Insights shows a dependency.
        app.MapGet("/fake-dependency", async (IOptions<TargetApiOptions> options, CancellationToken cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(options.Value.DependencyDelayMs), cancellationToken);
            return Results.Ok(new { result = "dependency" });
        });
    }

    private static void MapAuthEndpoints(WebApplication app)
    {
        app.MapGet("/secure", (HttpRequest request, CredentialChecker credentialChecker) =>
            credentialChecker.IsAuthorized(request)
                ? Results.Ok(new { result = "secure" })
                : Results.Unauthorized());

        app.MapPost("/auth/login", (LoginRequest login, TokenStore tokenStore, IOptions<TargetApiOptions> options) =>
        {
            var credentialsMatch = string.Equals(login.Email, options.Value.LoginEmail, StringComparison.OrdinalIgnoreCase)
                && string.Equals(login.Password, options.Value.LoginPassword, StringComparison.Ordinal);
            if (!credentialsMatch)
            {
                return Results.Unauthorized();
            }

            var issuedToken = tokenStore.Issue();
            return Results.Ok(new LoginResponse(issuedToken.Token, issuedToken.ExpiresInSeconds));
        });

        app.MapPost("/oauth2/token", async (HttpRequest request, TokenStore tokenStore, IOptions<TargetApiOptions> options) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new OAuthErrorResponse("invalid_request"));
            }

            // Read the form directly: form parameter binding would add antiforgery metadata to the endpoint.
            var form = await request.ReadFormAsync();
            if (!string.Equals(form["grant_type"], "client_credentials", StringComparison.Ordinal))
            {
                return Results.BadRequest(new OAuthErrorResponse("unsupported_grant_type"));
            }

            var clientMatches = string.Equals(form["client_id"], options.Value.ClientId, StringComparison.Ordinal)
                && string.Equals(form["client_secret"], options.Value.ClientSecret, StringComparison.Ordinal);
            if (!clientMatches)
            {
                return Results.Json(new OAuthErrorResponse("invalid_client"), statusCode: StatusCodes.Status401Unauthorized);
            }

            var issuedToken = tokenStore.Issue();
            return Results.Ok(new OAuthTokenResponse(issuedToken.Token, "Bearer", issuedToken.ExpiresInSeconds));
        });
    }

    private static string GetOwnAddress(IServer server)
    {
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses
            ?? throw new InvalidOperationException("Server addresses are not available.");
        return addresses.First();
    }
}
