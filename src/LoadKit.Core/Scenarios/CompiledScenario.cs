using LoadKit.Core.Scenarios.Model;

namespace LoadKit.Core.Scenarios;

/// <summary>A valid scenario with templates parsed, ready for the engine.</summary>
public sealed record CompiledScenario(Scenario Scenario, Uri BaseUri, IReadOnlyList<CompiledRequest> Requests)
{
    public override string ToString()
    {
        return Scenario.ToString();
    }
}
