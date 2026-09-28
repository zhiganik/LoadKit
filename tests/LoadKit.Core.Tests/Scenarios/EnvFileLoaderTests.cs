using LoadKit.Core.Scenarios;
using LoadKit.Core.Tests.TestSupport;

namespace LoadKit.Core.Tests.Scenarios;

public sealed class EnvFileLoaderTests
{
    [Fact]
    public void Parse_ReadsKeyValueLines()
    {
        var variables = EnvFileLoader.Parse("API_TOKEN=abc\nFUNC_KEY=xyz\n");

        Assert.Equal("abc", variables["API_TOKEN"]);
        Assert.Equal("xyz", variables["FUNC_KEY"]);
    }

    [Fact]
    public void Parse_SkipsCommentsBlankAndInvalidLines()
    {
        var variables = EnvFileLoader.Parse("# comment\n\n   \nNO_SEPARATOR\n=novalue\nKEY=value\n");

        Assert.Equal(["KEY"], variables.Keys);
    }

    [Theory]
    [InlineData("KEY=\"quoted value\"", "quoted value")]
    [InlineData("KEY='single'", "single")]
    [InlineData("KEY=\"unbalanced'", "\"unbalanced'")]
    [InlineData("export KEY=exported", "exported")]
    [InlineData("  KEY =  spaced  ", "spaced")]
    [InlineData("KEY=a=b=c", "a=b=c")]
    [InlineData("KEY=", "")]
    public void Parse_NormalizesValues(string line, string expectedValue)
    {
        var variables = EnvFileLoader.Parse(line);

        Assert.Equal(expectedValue, variables["KEY"]);
    }

    [Fact]
    public void Parse_HandlesBomAndCrLf()
    {
        var variables = EnvFileLoader.Parse("﻿FIRST=1\r\nSECOND=2\r\n");

        Assert.Equal("1", variables["FIRST"]);
        Assert.Equal("2", variables["SECOND"]);
    }

    [Fact]
    public void Parse_LastValueWins()
    {
        var variables = EnvFileLoader.Parse("KEY=first\nKEY=second");

        Assert.Equal("second", variables["KEY"]);
    }

    [Fact]
    public void FindDefault_PrefersFileNextToScenario_ThenParentFolder()
    {
        using var directory = new TemporaryDirectory();
        var scenariosDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "scenarios")).FullName;
        var scenarioPath = Path.Combine(scenariosDirectory, "api.json");
        var parentEnvPath = Path.Combine(directory.Path, ".env");
        var besideEnvPath = Path.Combine(scenariosDirectory, ".env");

        Assert.Null(EnvFileLoader.FindDefault(scenarioPath));

        File.WriteAllText(parentEnvPath, "A=1");
        Assert.Equal(parentEnvPath, EnvFileLoader.FindDefault(scenarioPath));

        File.WriteAllText(besideEnvPath, "A=2");
        Assert.Equal(besideEnvPath, EnvFileLoader.FindDefault(scenarioPath));
    }
}
