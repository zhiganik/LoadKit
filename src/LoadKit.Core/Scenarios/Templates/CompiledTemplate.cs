using System.Runtime.CompilerServices;

namespace LoadKit.Core.Scenarios.Templates;

/// <summary>
/// A string split into literal and generated segments at load time, so rendering on the hot path
/// only concatenates.
/// </summary>
public sealed class CompiledTemplate
{
    private readonly TemplateSegment[] _segments;

    public CompiledTemplate(IEnumerable<TemplateSegment> segments)
    {
        _segments = [.. segments];
    }

    public IReadOnlyList<TemplateSegment> Segments => _segments;

    /// <summary>True when the value never changes; <see cref="Render"/> then returns the same string every time.</summary>
    public bool IsLiteral => _segments.Length == 0 || (_segments.Length == 1 && _segments[0].IsLiteral);

    /// <summary>True for a value like <c>"{{seq}}"</c> that consists of one numeric template only.</summary>
    public bool IsSingleNumericValue => _segments.Length == 1 && !_segments[0].IsLiteral && _segments[0].IsNumeric;

    public static CompiledTemplate FromLiteral(string text)
    {
        return new CompiledTemplate([TemplateSegment.FromLiteral(text)]);
    }

    public string Render(TemplateContext context)
    {
        if (_segments.Length == 0)
        {
            return string.Empty;
        }

        if (_segments.Length == 1)
        {
            return RenderSegment(_segments[0], context);
        }

        var builder = new DefaultInterpolatedStringHandler(0, _segments.Length);
        foreach (var segment in _segments)
        {
            builder.AppendLiteral(RenderSegment(segment, context));
        }

        return builder.ToStringAndClear();
    }

    private static string RenderSegment(TemplateSegment segment, TemplateContext context)
    {
        return segment.ValueFactory is null ? segment.Literal! : segment.ValueFactory(context);
    }
}
