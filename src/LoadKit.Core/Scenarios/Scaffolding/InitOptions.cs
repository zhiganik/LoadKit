namespace LoadKit.Core.Scenarios.Scaffolding;

/// <summary>Answers for <c>loadtest init</c>, from flags or interactive prompts.</summary>
/// <param name="ScenarioPath">For example <c>loadtests/scenarios/my-api.json</c>.</param>
/// <param name="AuthType"><see cref="ScenarioScaffolder.NoAuth"/> or an <c>auth.type</c>.</param>
/// <param name="Source">For <c>azureIdentity</c>; null means <c>azureCli</c>.</param>
/// <param name="Scope">Required for <c>azureIdentity</c> and <c>oauth2ClientCredentials</c>.</param>
/// <param name="Header">For <c>apiKey</c>; null means <see cref="ScenarioScaffolder.DefaultApiKeyHeader"/>.</param>
public sealed record InitOptions(
    string ScenarioPath,
    string BaseUrl,
    string AuthType,
    string? Source,
    string? Scope,
    string? Header);
