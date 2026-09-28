using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using LoadKit.Core.Scenarios.Validation;

namespace LoadKit.Core.Scenarios.Templates;

/// <summary>
/// Parses <c>{{name:arg1:arg2}}</c> templates once, at load time, into <see cref="CompiledTemplate"/>.
/// </summary>
public sealed class TemplateCompiler(TemplateRegistry registry)
{
    private const string TemplateOpen = "{{";
    private const string TemplateClose = "}}";
    private const char ArgumentSeparator = ':';
    private const string SlotMarkerSuffix = "@@";

    private static readonly JsonSerializerOptions BodySerializerOptions = new()
    {
        // Keep non-ASCII text readable and the slot markers below unescaped.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly Dictionary<string, string> CommonMistakes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["uuid"] = "guid",
        ["random"] = "randomInt",
        ["rand"] = "randomInt",
        ["int"] = "randomInt",
        ["sequence"] = "seq",
        ["counter"] = "seq",
        ["timestamp"] = "now",
        ["date"] = "now",
        ["datetime"] = "now",
    };

    public TemplateCompileResult Compile(string text)
    {
        var segments = new List<TemplateSegment>();
        var errors = new List<TemplateError>();
        var literal = new StringBuilder();
        var position = 0;

        while (position < text.Length)
        {
            var openIndex = text.IndexOf(TemplateOpen, position, StringComparison.Ordinal);
            if (openIndex < 0)
            {
                literal.Append(text, position, text.Length - position);
                break;
            }

            literal.Append(text, position, openIndex - position);
            var closeIndex = text.IndexOf(TemplateClose, openIndex + TemplateOpen.Length, StringComparison.Ordinal);
            if (closeIndex < 0)
            {
                errors.Add(new TemplateError(
                    ValidationCodes.InvalidTemplate,
                    "unclosed '{{' in \"" + text + "\"",
                    "close the template with '}}', for example {{guid}}"));
                break;
            }

            var expression = text[(openIndex + TemplateOpen.Length)..closeIndex];
            if (TryCompileExpression(expression, errors, out var segment))
            {
                FlushLiteral(literal, segments);
                segments.Add(segment);
            }

            position = closeIndex + TemplateClose.Length;
        }

        FlushLiteral(literal, segments);
        return errors.Count > 0
            ? new TemplateCompileResult(null, errors)
            : new TemplateCompileResult(new CompiledTemplate(segments), errors);
    }

    /// <summary>
    /// Compiles a JSON body into one template over its JSON text. A string value that is only a numeric template
    /// (<c>"{{seq}}"</c>) becomes a JSON number; other templates are inserted inside their strings.
    /// </summary>
    public TemplateCompileResult CompileJsonBody(JsonElement body)
    {
        var rawBody = body.GetRawText();
        var slotMarkerPrefix = CreateSlotMarkerPrefix(rawBody);
        var slots = new List<BodySlot>();
        var errors = new List<TemplateError>();

        var bodyNode = JsonNode.Parse(rawBody);
        ReplaceTemplatesWithMarkers(bodyNode, slotMarkerPrefix, slots, errors);
        if (errors.Count > 0)
        {
            return new TemplateCompileResult(null, errors);
        }

        var bodyJsonWithMarkers = bodyNode?.ToJsonString(BodySerializerOptions) ?? "null";
        var segments = SplitOnMarkers(bodyJsonWithMarkers, slotMarkerPrefix, slots);
        return new TemplateCompileResult(new CompiledTemplate(segments), errors);
    }

    private bool TryCompileExpression(string expression, List<TemplateError> errors, out TemplateSegment segment)
    {
        segment = default;
        var parts = expression.Split(ArgumentSeparator);
        var name = parts[0].Trim();
        var arguments = parts[1..];

        if (!registry.TryGet(name, out var generator))
        {
            errors.Add(new TemplateError(
                ValidationCodes.UnknownTemplate,
                "unknown template {{" + expression.Trim() + "}}",
                SuggestTemplate(name)));
            return false;
        }

        if (!generator.TryBind(arguments, out var valueFactory, out var bindError))
        {
            errors.Add(new TemplateError(
                ValidationCodes.InvalidTemplate,
                "invalid template {{" + expression.Trim() + "}}: " + bindError,
                "use " + generator.Usage));
            return false;
        }

        segment = TemplateSegment.FromGenerator(valueFactory, generator.IsNumeric);
        return true;
    }

    private string SuggestTemplate(string unknownName)
    {
        if (CommonMistakes.TryGetValue(unknownName, out var knownName) && registry.TryGet(knownName, out var mistaken))
        {
            return "use " + mistaken.Usage;
        }

        var closest = registry.Generators
            .Select(generator => (Generator: generator, Distance: TextDistance.Levenshtein(unknownName.ToLowerInvariant(), generator.Name.ToLowerInvariant())))
            .Where(candidate => candidate.Distance <= 2)
            .OrderBy(candidate => candidate.Distance)
            .Select(candidate => candidate.Generator)
            .FirstOrDefault();
        if (closest is not null)
        {
            return "use " + closest.Usage;
        }

        return "available templates: " + string.Join(", ", registry.Generators.Select(generator => generator.Usage));
    }

    private void ReplaceTemplatesWithMarkers(JsonNode? node, string slotMarkerPrefix, List<BodySlot> slots, List<TemplateError> errors)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var propertyName in jsonObject.Select(property => property.Key).ToList())
                {
                    var child = jsonObject[propertyName];
                    if (TryReplaceString(child, slotMarkerPrefix, slots, errors, out var replacement))
                    {
                        jsonObject[propertyName] = replacement;
                    }
                    else
                    {
                        ReplaceTemplatesWithMarkers(child, slotMarkerPrefix, slots, errors);
                    }
                }

                break;
            case JsonArray jsonArray:
                for (var index = 0; index < jsonArray.Count; index++)
                {
                    var child = jsonArray[index];
                    if (TryReplaceString(child, slotMarkerPrefix, slots, errors, out var replacement))
                    {
                        jsonArray[index] = replacement;
                    }
                    else
                    {
                        ReplaceTemplatesWithMarkers(child, slotMarkerPrefix, slots, errors);
                    }
                }

                break;
        }
    }

    private bool TryReplaceString(JsonNode? node, string slotMarkerPrefix, List<BodySlot> slots, List<TemplateError> errors, out JsonNode? replacement)
    {
        replacement = null;
        if (node is not JsonValue value || value.GetValueKind() != JsonValueKind.String)
        {
            return false;
        }

        var result = Compile(value.GetValue<string>());
        if (result.Template is null)
        {
            errors.AddRange(result.Errors);
            return false;
        }

        if (result.Template.IsLiteral)
        {
            return false;
        }

        if (result.Template.IsSingleNumericValue)
        {
            replacement = JsonValue.Create(AddSlot(slotMarkerPrefix, slots, result.Template.Segments[0], isUnquotedNumber: true));
            return true;
        }

        var textWithMarkers = new StringBuilder();
        foreach (var segment in result.Template.Segments)
        {
            textWithMarkers.Append(segment.IsLiteral ? segment.Literal : AddSlot(slotMarkerPrefix, slots, segment, isUnquotedNumber: false));
        }

        replacement = JsonValue.Create(textWithMarkers.ToString());
        return true;
    }

    private static string AddSlot(string slotMarkerPrefix, List<BodySlot> slots, TemplateSegment segment, bool isUnquotedNumber)
    {
        slots.Add(new BodySlot(segment, isUnquotedNumber));
        return slotMarkerPrefix + (slots.Count - 1).ToString(CultureInfo.InvariantCulture) + SlotMarkerSuffix;
    }

    private static List<TemplateSegment> SplitOnMarkers(string json, string slotMarkerPrefix, List<BodySlot> slots)
    {
        var segments = new List<TemplateSegment>();
        var literal = new StringBuilder();
        var position = 0;

        while (true)
        {
            var markerIndex = json.IndexOf(slotMarkerPrefix, position, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                literal.Append(json, position, json.Length - position);
                break;
            }

            literal.Append(json, position, markerIndex - position);
            var slotNumberStart = markerIndex + slotMarkerPrefix.Length;
            var suffixIndex = json.IndexOf(SlotMarkerSuffix, slotNumberStart, StringComparison.Ordinal);
            var slotNumber = int.Parse(json.AsSpan(slotNumberStart, suffixIndex - slotNumberStart), CultureInfo.InvariantCulture);
            var slot = slots[slotNumber];
            position = suffixIndex + SlotMarkerSuffix.Length;

            if (slot.IsUnquotedNumber)
            {
                // Drop the quotes around the marker so the value is written as a JSON number.
                literal.Length -= 1;
                position += 1;
            }

            FlushLiteral(literal, segments);
            segments.Add(slot.Segment);
        }

        FlushLiteral(literal, segments);
        return segments;
    }

    private static string CreateSlotMarkerPrefix(string rawBody)
    {
        while (true)
        {
            var prefix = "@@LK" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6)) + ArgumentSeparator;
            if (!rawBody.Contains(prefix, StringComparison.Ordinal))
            {
                return prefix;
            }
        }
    }

    private static void FlushLiteral(StringBuilder literal, List<TemplateSegment> segments)
    {
        if (literal.Length > 0)
        {
            segments.Add(TemplateSegment.FromLiteral(literal.ToString()));
            literal.Clear();
        }
    }

    private readonly record struct BodySlot(TemplateSegment Segment, bool IsUnquotedNumber);
}
