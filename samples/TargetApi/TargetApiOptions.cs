namespace TargetApi;

/// <summary>
/// Behavior knobs and dev-only credentials of the sample API. Bound from the <c>TargetApi</c> section.
/// </summary>
public sealed class TargetApiOptions
{
    public const string SectionName = "TargetApi";

    public int SlowDelayMs { get; set; } = 200;

    public double FailProbability { get; set; } = 0.2;

    public int DependencyDelayMs { get; set; } = 50;

    public int TokenLifetimeSeconds { get; set; } = 10;

    public string DevToken { get; set; } = "dev-token";

    public string ApiKey { get; set; } = "dev-api-key";

    public string LoginEmail { get; set; } = "loadtest@example.com";

    public string LoginPassword { get; set; } = "dev-password";

    public string ClientId { get; set; } = "loadkit-client";

    public string ClientSecret { get; set; } = "dev-client-secret";
}
