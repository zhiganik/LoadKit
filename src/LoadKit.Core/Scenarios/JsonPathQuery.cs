using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace LoadKit.Core.Scenarios;

/// <summary>
/// The JSONPath subset used by <c>auth.tokenPath</c> / <c>auth.expiresInPath</c>: <c>$</c>, <c>.name</c>,
/// <c>['name']</c> and <c>[index]</c>, e.g. <c>$.data.accessToken</c> or <c>$.tokens[0]['access-token']</c>.
/// </summary>
public sealed class JsonPathQuery
{
    private readonly Segment[] _segments;

    private JsonPathQuery(string path, Segment[] segments)
    {
        Path = path;
        _segments = segments;
    }

    public string Path { get; }

    public static bool TryParse(string path, [NotNullWhen(true)] out JsonPathQuery? query, [NotNullWhen(false)] out string? error)
    {
        query = null;
        if (!path.StartsWith('$'))
        {
            error = "must start with '$', e.g. $.accessToken";
            return false;
        }

        var segments = new List<Segment>();
        var position = 1;
        while (position < path.Length)
        {
            if (!TryParseSegment(path, ref position, segments, out error))
            {
                return false;
            }
        }

        query = new JsonPathQuery(path, [.. segments]);
        error = null;
        return true;
    }

    public static JsonPathQuery Parse(string path)
    {
        return TryParse(path, out var query, out var error)
            ? query
            : throw new FormatException($"Invalid JSONPath '{path}': {error}");
    }

    public bool TryRead(JsonElement root, out JsonElement value)
    {
        value = root;
        foreach (var segment in _segments)
        {
            if (segment.PropertyName is { } propertyName)
            {
                if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(propertyName, out value))
                {
                    return false;
                }
            }
            else if (value.ValueKind != JsonValueKind.Array || segment.Index >= value.GetArrayLength())
            {
                return false;
            }
            else
            {
                value = value[segment.Index];
            }
        }

        return true;
    }

    private static bool TryParseSegment(string path, ref int position, List<Segment> segments, [NotNullWhen(false)] out string? error)
    {
        error = null;
        if (path[position] == '.')
        {
            var nameStart = position + 1;
            var nameEnd = path.IndexOfAny(['.', '['], nameStart);
            nameEnd = nameEnd < 0 ? path.Length : nameEnd;
            if (nameEnd == nameStart)
            {
                error = $"empty property name at position {position}";
                return false;
            }

            segments.Add(Segment.Property(path[nameStart..nameEnd]));
            position = nameEnd;
            return true;
        }

        if (path[position] == '[')
        {
            var closing = path.IndexOf(']', position);
            if (closing < 0)
            {
                error = $"missing ']' after position {position}";
                return false;
            }

            var content = path[(position + 1)..closing];
            position = closing + 1;
            if (content.Length >= 2 && content[0] == '\'' && content[^1] == '\'')
            {
                segments.Add(Segment.Property(content[1..^1]));
                return true;
            }

            if (int.TryParse(content, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
            {
                segments.Add(Segment.Element(index));
                return true;
            }

            error = $"'[{content}]' must be an index like [0] or a quoted name like ['name']";
            return false;
        }

        error = $"unexpected '{path[position]}' at position {position}; use .name, ['name'] or [index]";
        return false;
    }

    private readonly record struct Segment(string? PropertyName, int Index)
    {
        public static Segment Property(string name)
        {
            return new Segment(name, -1);
        }

        public static Segment Element(int index)
        {
            return new Segment(null, index);
        }
    }
}
