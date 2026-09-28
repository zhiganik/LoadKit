using System.Text.Json;
using System.Text.Json.Nodes;

namespace LoadKit.Core.Scenarios.Validation;

internal static class ScenarioJsonParser
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static JsonObject? Parse(string json, ICollection<ValidationIssue> issues)
    {
        const string SyntaxHint = "fix the JSON syntax; comments and trailing commas are allowed";
        try
        {
            var node = JsonNode.Parse(json.TrimStart('﻿'), documentOptions: DocumentOptions);
            if (node is not JsonObject root)
            {
                issues.Add(ValidationIssue.Error(ValidationCodes.InvalidType, JsonPath.Root, "the scenario must be a JSON object", "the file must start with {"));
                return null;
            }

            // JsonObject materializes lazily; walk it now so duplicate keys fail here, not later.
            Materialize(root);
            return root;
        }
        catch (JsonException exception)
        {
            issues.Add(ValidationIssue.Error(ValidationCodes.InvalidJson, JsonPath.Root, "invalid JSON: " + exception.Message, SyntaxHint));
        }
        catch (ArgumentException exception)
        {
            issues.Add(ValidationIssue.Error(ValidationCodes.InvalidJson, JsonPath.Root, "invalid JSON: " + exception.Message, "remove the duplicate property"));
        }

        return null;
    }

    private static void Materialize(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var (_, value) in jsonObject)
                {
                    Materialize(value);
                }

                break;
            case JsonArray jsonArray:
                foreach (var item in jsonArray)
                {
                    Materialize(item);
                }

                break;
        }
    }
}
