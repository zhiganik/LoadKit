namespace LoadKit.Cli.Ai;

internal sealed record FileChange(string Path, string Change)
{
    public const string Created = "created";
    public const string Updated = "updated";
    public const string Unchanged = "unchanged";
}
