using System.Text.Json.Nodes;
using LoadKit.Core.Scenarios.Model;

namespace LoadKit.Core.Scenarios.Validation;

/// <summary>
/// Reports secrets written directly in the scenario. Runs on raw values, before <c>${env:}</c> substitution;
/// afterwards a secret from <c>.env</c> would look the same as one typed into the file.
/// </summary>
internal sealed class SecretLiteralValidator(ICollection<ValidationIssue> issues)
{
    private const string Hint = "store the value in the .env file (for example loadtests/.env) and reference it as \"${env:NAME}\"";

    private static readonly string[] SecretPropertyNameParts = ["password", "secret", "pwd"];

    public void Validate(JsonObject root)
    {
        CheckAuthorizationHeader(JsonNodeReader.GetObject(root, "headers"), "headers");

        if (JsonNodeReader.GetArray(root, "requests") is { } requests)
        {
            for (var index = 0; index < requests.Count; index++)
            {
                var requestPath = JsonPath.Index("requests", index);
                CheckAuthorizationHeader(JsonNodeReader.GetObject(requests[index] as JsonObject, "headers"), JsonPath.Child(requestPath, "headers"));
            }
        }

        var auth = JsonNodeReader.GetObject(root, "auth");
        if (!JsonNodeReader.TryGetString(auth, "type", out var authType))
        {
            return;
        }

        switch (authType)
        {
            case AuthTypeNames.Bearer:
                CheckLiteral(auth, "token", "auth.token");
                break;
            case AuthTypeNames.ApiKey:
                CheckLiteral(auth, "value", "auth.value");
                break;
            case AuthTypeNames.OAuth2ClientCredentials:
                CheckLiteral(auth, "clientSecret", "auth.clientSecret");
                break;
            case AuthTypeNames.Login:
                var loginRequest = JsonNodeReader.GetObject(auth, "request");
                CheckAuthorizationHeader(JsonNodeReader.GetObject(loginRequest, "headers"), "auth.request.headers");
                CheckSecretProperties(loginRequest?["body"], "auth.request.body");
                break;
        }
    }

    private void CheckLiteral(JsonObject? jsonObject, string name, string path)
    {
        if (JsonNodeReader.TryGetString(jsonObject, name, out var value) && !EnvReferences.ContainsReference(value))
        {
            AddIssue(path);
        }
    }

    private void CheckAuthorizationHeader(JsonObject? headers, string headersPath)
    {
        if (headers is null)
        {
            return;
        }

        foreach (var (name, value) in headers)
        {
            if (string.Equals(name, "Authorization", StringComparison.OrdinalIgnoreCase)
                && JsonNodeReader.IsString(value, out var headerValue)
                && !EnvReferences.ContainsReference(headerValue))
            {
                AddIssue(JsonPath.Child(headersPath, name));
            }
        }
    }

    private void CheckSecretProperties(JsonNode? node, string path)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var (name, value) in jsonObject)
                {
                    var childPath = JsonPath.Child(path, name);
                    if (IsSecretPropertyName(name) && JsonNodeReader.IsString(value, out var text))
                    {
                        if (!EnvReferences.ContainsReference(text))
                        {
                            AddIssue(childPath);
                        }
                    }
                    else
                    {
                        CheckSecretProperties(value, childPath);
                    }
                }

                break;
            case JsonArray jsonArray:
                for (var index = 0; index < jsonArray.Count; index++)
                {
                    CheckSecretProperties(jsonArray[index], JsonPath.Index(path, index));
                }

                break;
        }
    }

    private static bool IsSecretPropertyName(string name)
    {
        foreach (var part in SecretPropertyNameParts)
        {
            if (name.Contains(part, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void AddIssue(string path)
    {
        issues.Add(ValidationIssue.Error(ValidationCodes.SecretLiteral, path, $"secret-like value found in {path}", Hint));
    }
}
