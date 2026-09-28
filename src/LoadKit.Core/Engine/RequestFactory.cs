using System.Runtime.CompilerServices;
using System.Text;
using LoadKit.Core.Auth;
using LoadKit.Core.Scenarios;
using LoadKit.Core.Scenarios.Templates;

namespace LoadKit.Core.Engine;

/// <summary>
/// Builds <see cref="HttpRequestMessage"/>s from compiled requests. Everything that does not change between
/// requests (literal URLs, escaped names, literal bodies, header placement) is prepared once in the constructor.
/// </summary>
public sealed class RequestFactory
{
    private const string ContentTypeHeader = "Content-Type";

    private readonly PreparedRequest[] _requests;
    private readonly TemplateContext _templateContext;

    /// <param name="runId">When set, <c>loadrun=&lt;runId&gt;</c> is appended to every URL.</param>
    public RequestFactory(CompiledScenario scenario, TemplateContext templateContext, string? runId)
    {
        _templateContext = templateContext;
        var baseUrl = scenario.Scenario.BaseUrl.TrimEnd('/');
        var runIdParameter = runId is null ? null : $"{RunOptions.RunIdQueryParameter}={Uri.EscapeDataString(runId)}";

        _requests = new PreparedRequest[scenario.Requests.Count];
        for (var index = 0; index < _requests.Length; index++)
        {
            _requests[index] = new PreparedRequest(scenario.Requests[index], baseUrl, runIdParameter, templateContext);
        }
    }

    public HttpRequestMessage Create(int requestIndex)
    {
        var prepared = _requests[requestIndex];
        var request = new HttpRequestMessage(prepared.Method, prepared.FixedUri ?? prepared.BuildUri(_templateContext));
        foreach (var header in prepared.RequestHeaders)
        {
            request.Headers.TryAddWithoutValidation(header.Name, header.Value.Render(_templateContext));
        }

        if (prepared.Body is not null)
        {
            request.Content = CreateContent(prepared);
        }

        if (prepared.SkipAuth)
        {
            request.Options.Set(AuthRequestOptions.SkipAuth, true);
        }

        return request;
    }

    private ByteArrayContent CreateContent(PreparedRequest prepared)
    {
        var bodyBytes = prepared.FixedBodyBytes ?? Encoding.UTF8.GetBytes(prepared.Body!.Render(_templateContext));
        var content = new ByteArrayContent(bodyBytes);
        if (prepared.BodyContentType is not null)
        {
            content.Headers.TryAddWithoutValidation(ContentTypeHeader, prepared.BodyContentType);
        }

        foreach (var header in prepared.ContentHeaders)
        {
            content.Headers.TryAddWithoutValidation(header.Name, header.Value.Render(_templateContext));
        }

        return content;
    }

    private sealed class PreparedRequest
    {
        private readonly string _baseUrl;
        private readonly CompiledTemplate _path;
        private readonly (string EscapedName, CompiledTemplate Value)[] _query;
        private readonly string? _runIdParameter;

        public PreparedRequest(CompiledRequest request, string baseUrl, string? runIdParameter, TemplateContext templateContext)
        {
            _baseUrl = baseUrl;
            _path = request.Path;
            _runIdParameter = runIdParameter;
            _query = [.. request.Query.Select(parameter => (Uri.EscapeDataString(parameter.Name), parameter.Value))];

            Method = HttpMethod.Parse(request.Definition.Method);
            SkipAuth = !request.Definition.UseAuth;
            Body = request.Body;
            if (request.Body is { IsLiteral: true } literalBody)
            {
                FixedBodyBytes = Encoding.UTF8.GetBytes(literalBody.Render(templateContext));
            }

            SplitHeaders(request.Headers, out var requestHeaders, out var contentHeaders);
            RequestHeaders = requestHeaders;
            ContentHeaders = contentHeaders;
            var hasExplicitContentType = contentHeaders.Exists(header =>
                string.Equals(header.Name, ContentTypeHeader, StringComparison.OrdinalIgnoreCase));
            BodyContentType = hasExplicitContentType ? null : request.BodyContentType;

            if (_path.IsLiteral && _query.All(parameter => parameter.Value.IsLiteral))
            {
                FixedUri = BuildUri(templateContext);
            }
        }

        public HttpMethod Method { get; }

        public bool SkipAuth { get; }

        public Uri? FixedUri { get; }

        public List<CompiledNameValue> RequestHeaders { get; }

        public List<CompiledNameValue> ContentHeaders { get; }

        public CompiledTemplate? Body { get; }

        public byte[]? FixedBodyBytes { get; }

        public string? BodyContentType { get; }

        public Uri BuildUri(TemplateContext templateContext)
        {
            var path = _path.Render(templateContext);
            var separator = path.Contains('?', StringComparison.Ordinal) ? '&' : '?';
            var url = new DefaultInterpolatedStringHandler(0, 2 + (_query.Length * 4) + 2);
            url.AppendLiteral(_baseUrl);
            url.AppendLiteral(path);
            foreach (var (escapedName, value) in _query)
            {
                url.AppendFormatted(separator);
                url.AppendLiteral(escapedName);
                url.AppendFormatted('=');
                url.AppendLiteral(Uri.EscapeDataString(value.Render(templateContext)));
                separator = '&';
            }

            if (_runIdParameter is not null)
            {
                url.AppendFormatted(separator);
                url.AppendLiteral(_runIdParameter);
            }

            return new Uri(url.ToStringAndClear(), UriKind.Absolute);
        }

        // Content-Type and similar headers belong to HttpContent; HttpRequestHeaders rejects them.
        private static void SplitHeaders(
            IReadOnlyList<CompiledNameValue> headers,
            out List<CompiledNameValue> requestHeaders,
            out List<CompiledNameValue> contentHeaders)
        {
            requestHeaders = [];
            contentHeaders = [];
            using var probe = new HttpRequestMessage();
            foreach (var header in headers)
            {
                var isRequestHeader = probe.Headers.TryAddWithoutValidation(header.Name, string.Empty);
                (isRequestHeader ? requestHeaders : contentHeaders).Add(header);
            }
        }
    }
}
