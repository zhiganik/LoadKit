using System.Globalization;
using System.Text;
using System.Text.Json;
using LoadKit.Core.Scenarios;
using LoadKit.Core.Scenarios.Model;

namespace LoadKit.Core.Auth.Providers;

/// <summary>
/// <c>login</c>: calls the API's own login endpoint and reads the token by <c>tokenPath</c>. Lifetime comes from
/// <c>expiresInPath</c> (seconds), otherwise from the JWT <c>exp</c> claim, otherwise it is unknown.
/// </summary>
public sealed class LoginAuthProvider : TokenAuthProviderBase
{
    private const int MaxErrorBodyLength = 200;

    private readonly LoginRequestDefinition _request;
    private readonly Uri _loginUri;
    private readonly JsonPathQuery _tokenPath;
    private readonly JsonPathQuery? _expiresInPath;
    private readonly HttpClient _tokenHttpClient;

    /// <param name="tokenHttpClient">A client without <see cref="AuthHandler"/>.</param>
    public LoginAuthProvider(
        LoginAuth options,
        string baseUrl,
        HttpClient tokenHttpClient,
        TimeProvider timeProvider,
        SecretMasker secretMasker)
        : base(options.Header, options.Format, timeProvider, secretMasker)
    {
        _request = options.Request;
        _loginUri = BuildUri(baseUrl, options.Request);
        _tokenPath = JsonPathQuery.Parse(options.TokenPath);
        _expiresInPath = options.ExpiresInPath is null ? null : JsonPathQuery.Parse(options.ExpiresInPath);
        _tokenHttpClient = tokenHttpClient;
    }

    protected override async Task<AccessTokenResult> AcquireTokenAsync(CancellationToken cancellationToken)
    {
        var requestDescription = $"login request {_request.Method} {_request.Path}";
        using var request = CreateRequest();
        using var response = await SendAsync(request, requestDescription, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new AuthException(
                $"{requestDescription} returned {(int)response.StatusCode}: {Truncate(body)}",
                "check the credentials in .env and auth.request (method, path, body)");
        }

        using var document = ParseJson(body, requestDescription);
        if (!_tokenPath.TryRead(document.RootElement, out var tokenElement) || tokenElement.ValueKind != JsonValueKind.String)
        {
            // The body is not shown: it may hold the token under a different name.
            throw new AuthException(
                $"auth.tokenPath {_tokenPath.Path} does not point to a string in the login response",
                $"the response has {DescribeShape(document.RootElement)}; fix auth.tokenPath");
        }

        var token = tokenElement.GetString()!;
        return new AccessTokenResult(token, ReadExpiry(document.RootElement) ?? JwtExpiry.TryRead(token));
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string requestDescription, CancellationToken cancellationToken)
    {
        try
        {
            return await _tokenHttpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new AuthException($"{requestDescription} failed: {exception.Message}", "check that the API is running and baseUrl is correct", exception);
        }
    }

    private HttpRequestMessage CreateRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Parse(_request.Method), _loginUri);
        var bodyText = _request.Body is { } body ? body.GetRawText() : _request.BodyRaw;
        if (bodyText is not null)
        {
            request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(bodyText));
            request.Content.Headers.TryAddWithoutValidation("Content-Type", _request.ContentType);
        }

        foreach (var (name, value) in _request.Headers)
        {
            if (!request.Headers.TryAddWithoutValidation(name, value))
            {
                request.Content?.Headers.Remove(name);
                request.Content?.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return request;
    }

    private DateTimeOffset? ReadExpiry(JsonElement root)
    {
        if (_expiresInPath is null)
        {
            return null;
        }

        if (!_expiresInPath.TryRead(root, out var expiresIn) || !TryGetSeconds(expiresIn, out var seconds))
        {
            throw new AuthException(
                $"auth.expiresInPath {_expiresInPath.Path} does not point to a number of seconds in the login response",
                $"the response has {DescribeShape(root)}; fix auth.expiresInPath or remove it to use the JWT exp claim");
        }

        return TimeProvider.GetUtcNow().AddSeconds(seconds);
    }

    internal static bool TryGetSeconds(JsonElement value, out double seconds)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            return value.TryGetDouble(out seconds);
        }

        seconds = 0;
        return value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
    }

    internal static JsonDocument ParseJson(string body, string requestDescription)
    {
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException exception)
        {
            throw new AuthException($"{requestDescription} did not return JSON", "check that the endpoint returns the token as JSON", exception);
        }
    }

    internal static string Truncate(string text)
    {
        var singleLine = text.ReplaceLineEndings(" ").Trim();
        return singleLine.Length <= MaxErrorBodyLength ? singleLine : string.Concat(singleLine.AsSpan(0, MaxErrorBodyLength), "…");
    }

    private static string DescribeShape(JsonElement root)
    {
        return root.ValueKind == JsonValueKind.Object
            ? $"properties: {string.Join(", ", root.EnumerateObject().Select(property => property.Name))}"
            : $"a JSON {root.ValueKind.ToString().ToLowerInvariant()} at the root";
    }

    private static Uri BuildUri(string baseUrl, LoginRequestDefinition request)
    {
        var url = new StringBuilder(baseUrl.TrimEnd('/')).Append(request.Path);
        var separator = request.Path.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        foreach (var (name, value) in request.Query)
        {
            url.Append(separator).Append(Uri.EscapeDataString(name)).Append('=').Append(Uri.EscapeDataString(value));
            separator = '&';
        }

        return new Uri(url.ToString(), UriKind.Absolute);
    }
}
