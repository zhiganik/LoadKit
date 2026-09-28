using System.Text.Json;
using LoadKit.Core.Scenarios;
using LoadKit.Core.Scenarios.Templates;
using LoadKit.Core.Tests.TestSupport;

namespace LoadKit.Core.Tests.Scenarios.Templates;

public sealed class TemplateCompilerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 9, 24, 10, 15, 30, TimeSpan.Zero);

    private readonly TemplateCompiler _compiler = new(TemplateRegistry.CreateDefault());

    [Fact]
    public void Compile_TextWithoutTemplates_IsLiteral()
    {
        var template = CompileValid("/api/orders");

        Assert.True(template.IsLiteral);
        Assert.Equal("/api/orders", template.Render(CreateContext()));
    }

    [Fact]
    public void Compile_EmptyText_RendersEmpty()
    {
        var template = CompileValid(string.Empty);

        Assert.True(template.IsLiteral);
        Assert.Equal(string.Empty, template.Render(CreateContext()));
    }

    [Fact]
    public void Seq_IncrementsAcrossTemplatesSharingContext()
    {
        var first = CompileValid("a-{{seq}}");
        var second = CompileValid("{{seq}}");
        var context = CreateContext();

        Assert.Equal("a-1", first.Render(context));
        Assert.Equal("2", second.Render(context));
        Assert.Equal("a-3", first.Render(context));
    }

    [Fact]
    public void Now_RendersIso8601Utc()
    {
        var template = CompileValid("at {{now}}");

        Assert.Equal("at 2026-09-24T10:15:30Z", template.Render(CreateContext()));
    }

    [Fact]
    public void Guid_RendersParsableGuid()
    {
        var value = CompileValid("{{guid}}").Render(CreateContext());

        Assert.True(Guid.TryParse(value, out _));
    }

    [Fact]
    public void RandomInt_StaysWithinInclusiveBounds()
    {
        var template = CompileValid("{{randomInt:1:3}}");
        var context = CreateContext();
        var seen = new HashSet<string>();

        for (var iteration = 0; iteration < 300; iteration++)
        {
            seen.Add(template.Render(context));
        }

        Assert.Equal(["1", "2", "3"], seen.Order());
    }

    [Fact]
    public void RandomInt_AcceptsSameBoundsAndIntMaxValue()
    {
        Assert.Equal("5", CompileValid("{{randomInt:5:5}}").Render(CreateContext()));
        Assert.Equal("2147483647", CompileValid("{{randomInt:2147483647:2147483647}}").Render(CreateContext()));
    }

    [Fact]
    public void Compile_TrimsWhitespaceInsideBraces()
    {
        Assert.Equal("1", CompileValid("{{ seq }}").Render(CreateContext()));
    }

    [Fact]
    public void SingleNumericTemplate_IsMarkedNumeric()
    {
        Assert.True(CompileValid("{{seq}}").IsSingleNumericValue);
        Assert.True(CompileValid("{{randomInt:1:9}}").IsSingleNumericValue);
        Assert.False(CompileValid("{{guid}}").IsSingleNumericValue);
        Assert.False(CompileValid("n{{seq}}").IsSingleNumericValue);
    }

    [Theory]
    [InlineData("{{uuid}}", "use {{guid}}")]
    [InlineData("{{sequence}}", "use {{seq}}")]
    [InlineData("{{randint:1:2}}", "use {{randomInt:MIN:MAX}}")]
    [InlineData("{{GUID}}", "use {{guid}}")]
    public void UnknownTemplate_SuggestsKnownOne(string text, string expectedHint)
    {
        var error = Assert.Single(_compiler.Compile(text).Errors);

        Assert.Equal(ValidationCodes.UnknownTemplate, error.Code);
        Assert.Equal(expectedHint, error.Hint);
    }

    [Fact]
    public void UnknownTemplate_WithoutCloseMatch_ListsAvailable()
    {
        var error = Assert.Single(_compiler.Compile("{{somethingElse}}").Errors);

        Assert.Equal("unknown template {{somethingElse}}", error.Message);
        Assert.Contains("{{randomInt:MIN:MAX}}", error.Hint, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{{randomInt}}")]
    [InlineData("{{randomInt:1}}")]
    [InlineData("{{randomInt:a:b}}")]
    [InlineData("{{randomInt:9:1}}")]
    [InlineData("{{guid:1}}")]
    [InlineData("{{seq:x}}")]
    [InlineData("{{now:utc}}")]
    [InlineData("prefix {{guid")]
    public void InvalidTemplate_IsReported(string text)
    {
        var result = _compiler.Compile(text);

        Assert.Null(result.Template);
        Assert.Equal(ValidationCodes.InvalidTemplate, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Compile_CollectsAllErrorsInOneString()
    {
        var result = _compiler.Compile("{{uuid}}-{{randomInt:5:1}}");

        Assert.Equal([ValidationCodes.UnknownTemplate, ValidationCodes.InvalidTemplate], result.Errors.Select(error => error.Code));
    }

    [Fact]
    public void CompileJsonBody_NumericOnlyTemplateBecomesNumber_OthersStayInStrings()
    {
        using var body = JsonDocument.Parse("""
            { "id": "{{seq}}", "ref": "order-{{seq}}", "qty": 1, "tags": ["{{seq}}", "fixed"], "nested": { "n": "{{seq}}" } }
            """);

        var template = CompileBodyValid(body.RootElement);
        var rendered = template.Render(CreateContext());

        Assert.Equal("""{"id":1,"ref":"order-2","qty":1,"tags":[3,"fixed"],"nested":{"n":4}}""", rendered);
    }

    [Fact]
    public void CompileJsonBody_WithoutTemplates_IsLiteralCompactJson()
    {
        using var body = JsonDocument.Parse("""{ "name": "Иван <b>", "n": 1.5 }""");

        var template = CompileBodyValid(body.RootElement);

        Assert.True(template.IsLiteral);
        Assert.Equal("""{"name":"Иван <b>","n":1.5}""", template.Render(CreateContext()));
    }

    [Fact]
    public void CompileJsonBody_EscapesLiteralPartsAroundTemplates()
    {
        using var body = JsonDocument.Parse("""{ "text": "say \"hi\" {{seq}}" }""");

        var rendered = CompileBodyValid(body.RootElement).Render(CreateContext());

        Assert.Equal("""{"text":"say \"hi\" 1"}""", rendered);
        using var parsed = JsonDocument.Parse(rendered);
        Assert.Equal("say \"hi\" 1", parsed.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public void CompileJsonBody_ReportsTemplateErrors()
    {
        using var body = JsonDocument.Parse("""{ "id": "{{uuid}}" }""");

        var result = _compiler.CompileJsonBody(body.RootElement);

        Assert.Null(result.Template);
        Assert.Equal(ValidationCodes.UnknownTemplate, Assert.Single(result.Errors).Code);
    }

    private CompiledTemplate CompileValid(string text)
    {
        var result = _compiler.Compile(text);
        Assert.Empty(result.Errors);
        return result.Template!;
    }

    private CompiledTemplate CompileBodyValid(JsonElement body)
    {
        var result = _compiler.CompileJsonBody(body);
        Assert.Empty(result.Errors);
        return result.Template!;
    }

    private static TemplateContext CreateContext()
    {
        return new TemplateContext(new FixedTimeProvider(FixedNow), Random.Shared);
    }
}
