namespace LoadKit.Core.Engine;

/// <summary>One preflight check. Texts are masked.</summary>
/// <param name="Name"><c>baseUrl</c> or <c>auth</c>.</param>
/// <param name="Hint">What to check when <paramref name="Succeeded"/> is false.</param>
/// <param name="Warning">A problem that does not stop the run, e.g. an unknown token lifetime.</param>
public sealed record PreflightStep(string Name, bool Succeeded, string Message, string? Hint, string? Warning);
