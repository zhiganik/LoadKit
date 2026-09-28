namespace LoadKit.Core.Scenarios.Scaffolding;

/// <param name="EnvFilePath">Relative path of the <c>.env</c> to fill.</param>
/// <param name="EmptyVariables">Variables in <c>.env</c> that still have no value (names only).</param>
public sealed record InitResult(
    IReadOnlyList<InitFileChange> Changes,
    string ScenarioPath,
    string EnvFilePath,
    string ReportsPath,
    IReadOnlyList<string> EmptyVariables);
