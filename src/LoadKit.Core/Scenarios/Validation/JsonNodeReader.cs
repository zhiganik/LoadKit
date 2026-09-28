using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LoadKit.Core.Scenarios.Validation;

/// <summary>Tolerant accessors: they return false/null when a field is missing or has the wrong type.</summary>
internal static class JsonNodeReader
{
    public static bool IsString(JsonNode? node, [NotNullWhen(true)] out string? value)
    {
        if (node is JsonValue jsonValue && jsonValue.GetValueKind() == JsonValueKind.String)
        {
            value = jsonValue.GetValue<string>();
            return true;
        }

        value = null;
        return false;
    }

    public static bool IsInteger(JsonNode? node, out int value)
    {
        value = 0;
        return node is JsonValue jsonValue
            && jsonValue.GetValueKind() == JsonValueKind.Number
            && jsonValue.TryGetValue(out value);
    }

    public static bool IsNumber(JsonNode? node, out double value)
    {
        value = 0;
        return node is JsonValue jsonValue
            && jsonValue.GetValueKind() == JsonValueKind.Number
            && jsonValue.TryGetValue(out value);
    }

    public static bool IsBoolean(JsonNode? node, out bool value)
    {
        value = false;
        if (node is not JsonValue jsonValue)
        {
            return false;
        }

        var kind = jsonValue.GetValueKind();
        value = kind == JsonValueKind.True;
        return kind is JsonValueKind.True or JsonValueKind.False;
    }

    public static bool TryGetString(JsonObject? jsonObject, string name, [NotNullWhen(true)] out string? value)
    {
        value = null;
        return jsonObject is not null && IsString(jsonObject[name], out value);
    }

    public static bool TryGetInteger(JsonObject? jsonObject, string name, out int value)
    {
        value = 0;
        return jsonObject is not null && IsInteger(jsonObject[name], out value);
    }

    public static bool TryGetNumber(JsonObject? jsonObject, string name, out double value)
    {
        value = 0;
        return jsonObject is not null && IsNumber(jsonObject[name], out value);
    }

    public static bool TryGetBoolean(JsonObject? jsonObject, string name, out bool value)
    {
        value = false;
        return jsonObject is not null && IsBoolean(jsonObject[name], out value);
    }

    public static JsonObject? GetObject(JsonObject? jsonObject, string name)
    {
        return jsonObject?[name] as JsonObject;
    }

    public static JsonArray? GetArray(JsonObject? jsonObject, string name)
    {
        return jsonObject?[name] as JsonArray;
    }

    public static bool Has(JsonObject? jsonObject, string name)
    {
        return jsonObject is not null && jsonObject.ContainsKey(name);
    }
}
