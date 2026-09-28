using LoadKit.Core.Scenarios.Model;
using LoadKit.Core.Scenarios.Templates;

namespace LoadKit.Core.Scenarios;

/// <summary>
/// A request ready for the engine. <see cref="Headers"/> already include scenario-level headers
/// (request headers win). <see cref="Body"/> is null when the request has no body.
/// </summary>
public sealed record CompiledRequest(
    RequestDefinition Definition,
    CompiledTemplate Path,
    IReadOnlyList<CompiledNameValue> Headers,
    IReadOnlyList<CompiledNameValue> Query,
    CompiledTemplate? Body,
    string? BodyContentType);
