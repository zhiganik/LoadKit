using LoadKit.Core.Scenarios.Model;
using LoadKit.Core.Scenarios.Templates;

namespace LoadKit.Core.Scenarios;

/// <summary>Compiles templates of a validated scenario so the hot path does no parsing.</summary>
public sealed class ScenarioCompiler(TemplateCompiler templateCompiler)
{
    public CompiledScenario Compile(Scenario scenario)
    {
        var requests = new List<CompiledRequest>(scenario.Requests.Count);
        foreach (var request in scenario.Requests)
        {
            requests.Add(CompileRequest(scenario, request));
        }

        return new CompiledScenario(scenario, new Uri(scenario.BaseUrl, UriKind.Absolute), requests);
    }

    private CompiledRequest CompileRequest(Scenario scenario, RequestDefinition request)
    {
        var mergedHeaders = new Dictionary<string, string>(scenario.Headers, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in request.Headers)
        {
            mergedHeaders[name] = value;
        }

        CompiledTemplate? body = null;
        string? bodyContentType = null;
        if (request.Body is { } jsonBody)
        {
            body = RequireTemplate(templateCompiler.CompileJsonBody(jsonBody), request.Name);
            bodyContentType = request.ContentType;
        }
        else if (request.BodyRaw is { } rawBody)
        {
            body = CompileText(rawBody, request.Name);
            bodyContentType = request.ContentType;
        }

        return new CompiledRequest(
            request,
            CompileText(request.Path, request.Name),
            CompileNameValues(mergedHeaders, request.Name),
            CompileNameValues(request.Query, request.Name),
            body,
            bodyContentType);
    }

    private List<CompiledNameValue> CompileNameValues(IReadOnlyDictionary<string, string> values, string requestName)
    {
        var compiled = new List<CompiledNameValue>(values.Count);
        foreach (var (name, value) in values)
        {
            compiled.Add(new CompiledNameValue(name, CompileText(value, requestName)));
        }

        return compiled;
    }

    private CompiledTemplate CompileText(string text, string requestName)
    {
        return RequireTemplate(templateCompiler.Compile(text), requestName);
    }

    private static CompiledTemplate RequireTemplate(TemplateCompileResult result, string requestName)
    {
        return result.Template
            ?? throw new InvalidOperationException(
                $"Template errors in validated request '{requestName}': {string.Join("; ", result.Errors.Select(error => error.Message))}");
    }
}
