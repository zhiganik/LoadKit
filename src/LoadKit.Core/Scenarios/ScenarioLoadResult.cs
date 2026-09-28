namespace LoadKit.Core.Scenarios;

/// <summary>
/// <see cref="Scenario"/> is set when there are no errors; <see cref="Issues"/> may still hold warnings and info.
/// </summary>
public sealed record ScenarioLoadResult(CompiledScenario? Scenario, IReadOnlyList<ValidationIssue> Issues)
{
    public bool IsValid => Scenario is not null;

    public int ErrorCount => Issues.Count(issue => issue.Severity == ValidationSeverity.Error);

    public int WarningCount => Issues.Count(issue => issue.Severity == ValidationSeverity.Warning);
}
