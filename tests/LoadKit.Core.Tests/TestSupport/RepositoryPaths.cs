namespace LoadKit.Core.Tests.TestSupport;

internal static class RepositoryPaths
{
    private const string SolutionFileName = "LoadKit.slnx";

    public static string Root { get; } = FindRoot();

    public static string Combine(params string[] relativeParts)
    {
        return Path.Combine([Root, .. relativeParts]);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException($"{SolutionFileName} not found above {AppContext.BaseDirectory}.");
    }
}
