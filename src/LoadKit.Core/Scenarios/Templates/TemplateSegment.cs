namespace LoadKit.Core.Scenarios.Templates;

/// <summary>Either literal text or a generated value.</summary>
public readonly record struct TemplateSegment(string? Literal, TemplateValueFactory? ValueFactory, bool IsNumeric)
{
    public bool IsLiteral => ValueFactory is null;

    public static TemplateSegment FromLiteral(string literal)
    {
        return new TemplateSegment(literal, null, false);
    }

    public static TemplateSegment FromGenerator(TemplateValueFactory valueFactory, bool isNumeric)
    {
        return new TemplateSegment(null, valueFactory, isNumeric);
    }
}
