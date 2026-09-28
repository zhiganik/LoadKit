namespace LoadKit.Core.Reporting;

/// <param name="BaseUrl">Masked.</param>
/// <param name="AuthType">The <c>auth.type</c>, null without auth.</param>
public sealed record ReportScenario(string Name, string? Description, string BaseUrl, string? AuthType);
