using System.Collections.Frozen;
using System.Text.Json;
using LoadKit.Core.Scenarios.Model;

namespace LoadKit.Core.Auth;

/// <summary>
/// Hides secrets in text that leaves the process: console, reports, logs. Masks known secret values
/// (also URL-encoded) and the values of sensitive headers.
/// </summary>
public sealed class SecretMasker
{
    public const string Mask = "***";

    /// <summary>Shorter values are not masked: replacing them everywhere would garble unrelated text.</summary>
    public const int MinimumSecretLength = 4;

    private static readonly FrozenSet<string> DefaultSensitiveHeaders = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "Authorization",
        "Proxy-Authorization",
        "Cookie",
        "Set-Cookie",
        "x-functions-key",
        "api-key",
        "x-api-key");

    private readonly HashSet<string> _secrets = new(StringComparer.Ordinal);
    private readonly Lock _secretsLock = new();
    private readonly HashSet<string> _sensitiveHeaders;
    private volatile string[] _secretsLongestFirst = [];

    public SecretMasker(IEnumerable<string> secretValues, IEnumerable<string> sensitiveHeaderNames)
    {
        foreach (var secret in secretValues)
        {
            AddSecret(secret);
        }

        _sensitiveHeaders = new HashSet<string>(DefaultSensitiveHeaders, StringComparer.OrdinalIgnoreCase);
        _sensitiveHeaders.UnionWith(sensitiveHeaderNames);
    }

    /// <summary>
    /// Registers a secret that appears at runtime, such as an acquired access token. Not for the hot path:
    /// called once per token acquisition.
    /// </summary>
    public void AddSecret(string secret)
    {
        if (secret.Length < MinimumSecretLength)
        {
            return;
        }

        lock (_secretsLock)
        {
            var added = _secrets.Add(secret) | _secrets.Add(Uri.EscapeDataString(secret));
            if (added)
            {
                _secretsLongestFirst = [.. _secrets.OrderByDescending(value => value.Length)];
            }
        }
    }

    /// <summary>A masker with only the default sensitive headers; secrets can still be added at runtime.</summary>
    public static SecretMasker CreateEmpty()
    {
        return new SecretMasker([], []);
    }

    /// <summary>
    /// Secrets of a loaded scenario: every value in <c>auth</c> that can carry a credential, and the values of
    /// sensitive headers in <c>headers</c> / <c>requests[].headers</c>.
    /// </summary>
    public static SecretMasker FromScenario(Scenario scenario)
    {
        var secrets = new List<string>();
        var sensitiveHeaders = new List<string>();
        CollectAuthSecrets(scenario.Auth, secrets, sensitiveHeaders);

        var masker = new SecretMasker(secrets, sensitiveHeaders);
        CollectSensitiveHeaderValues(masker, scenario.Headers, secrets);
        foreach (var request in scenario.Requests)
        {
            CollectSensitiveHeaderValues(masker, request.Headers, secrets);
        }

        return new SecretMasker(secrets, sensitiveHeaders);
    }

    public bool IsSensitiveHeader(string headerName)
    {
        return _sensitiveHeaders.Contains(headerName);
    }

    public string MaskHeaderValue(string headerName, string headerValue)
    {
        return IsSensitiveHeader(headerName) ? Mask : MaskText(headerValue);
    }

    public string MaskText(string text)
    {
        foreach (var secret in _secretsLongestFirst)
        {
            text = text.Replace(secret, Mask, StringComparison.Ordinal);
        }

        return text;
    }

    private static void CollectAuthSecrets(AuthOptions? auth, List<string> secrets, List<string> sensitiveHeaders)
    {
        switch (auth)
        {
            case BearerAuth bearer:
                secrets.Add(bearer.Token);
                sensitiveHeaders.Add(bearer.Header);
                break;
            case ApiKeyAuth apiKey:
                secrets.Add(apiKey.Value);
                if (apiKey.Header is not null)
                {
                    sensitiveHeaders.Add(apiKey.Header);
                }

                break;
            case OAuth2ClientCredentialsAuth oauth2:
                secrets.Add(oauth2.ClientSecret);
                break;
            case LoginAuth login:
                sensitiveHeaders.Add(login.Header);
                CollectLoginRequestSecrets(login.Request, secrets);
                break;
        }
    }

    // Login bodies hold user names and passwords; every string value is treated as a secret.
    private static void CollectLoginRequestSecrets(LoginRequestDefinition request, List<string> secrets)
    {
        secrets.AddRange(request.Headers.Values);
        secrets.AddRange(request.Query.Values);
        if (request.BodyRaw is not null)
        {
            secrets.Add(request.BodyRaw);
        }

        if (request.Body is { } body)
        {
            CollectJsonStrings(body, secrets);
        }
    }

    private static void CollectJsonStrings(JsonElement element, List<string> strings)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                strings.Add(element.GetString()!);
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    CollectJsonStrings(property.Value, strings);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectJsonStrings(item, strings);
                }

                break;
        }
    }

    private static void CollectSensitiveHeaderValues(SecretMasker masker, IReadOnlyDictionary<string, string> headers, List<string> secrets)
    {
        foreach (var (name, value) in headers)
        {
            if (masker.IsSensitiveHeader(name))
            {
                secrets.Add(value);
            }
        }
    }
}
