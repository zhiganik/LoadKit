namespace LoadKit.Core.Scenarios.Templates;

/// <summary><see cref="Template"/> is set only when <see cref="Errors"/> is empty.</summary>
public sealed record TemplateCompileResult(CompiledTemplate? Template, IReadOnlyList<TemplateError> Errors);
