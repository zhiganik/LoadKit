namespace LoadKit.Core.Scenarios.Scaffolding;

/// <param name="Path">Relative to the working directory, with '/'.</param>
/// <param name="Change"><c>created</c>, <c>updated</c> or <c>unchanged</c>.</param>
public sealed record InitFileChange(string Path, string Change);
