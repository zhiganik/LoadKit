using LoadKit.Core.Scenarios.Templates;

namespace LoadKit.Core.Scenarios;

/// <summary>A header or query parameter whose value may contain templates.</summary>
public sealed record CompiledNameValue(string Name, CompiledTemplate Value);
