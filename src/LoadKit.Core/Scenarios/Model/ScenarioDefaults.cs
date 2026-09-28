namespace LoadKit.Core.Scenarios.Model;

/// <summary>Defaults for optional scenario fields. Keep in sync with SCENARIO_REFERENCE.md and the schema.</summary>
public static class ScenarioDefaults
{
    public const bool TagRuns = true;
    public const int Weight = 1;
    public const int Warmup = 0;
    public const int TimeoutMs = 30_000;
    public const string ContentType = "application/json";
    public const string AuthHeader = "Authorization";
    public const string AuthFormat = "Bearer {token}";
    public const string AzureIdentitySource = AzureIdentitySources.AzureCli;
}
