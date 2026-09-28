namespace LoadKit.Core.Engine;

/// <summary>Preflight outcome; a failure means exit code 3. Checks stop at the first failure.</summary>
public sealed record PreflightResult(IReadOnlyList<PreflightStep> Steps)
{
    public bool Succeeded => Steps.All(step => step.Succeeded);
}
