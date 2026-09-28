using System.Reflection;

namespace LoadKit.Cli;

/// <summary>Tool version and embedded assets.</summary>
internal static class ToolInfo
{
    private const string ScenarioSchemaResource = "LoadKit.Assets.scenario.schema.json";

    /// <summary>The <c>Version</c> from Directory.Build.props, without the source revision suffix.</summary>
    public static string Version { get; } = ReadVersion();

    public static string ReadScenarioSchema()
    {
        using var stream = typeof(ToolInfo).Assembly.GetManifestResourceStream(ScenarioSchemaResource)
            ?? throw new InvalidOperationException($"Embedded resource {ScenarioSchemaResource} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string ReadVersion()
    {
        var informationalVersion = typeof(ToolInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "0.0.0";
        var metadataStart = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        return metadataStart < 0 ? informationalVersion : informationalVersion[..metadataStart];
    }
}
