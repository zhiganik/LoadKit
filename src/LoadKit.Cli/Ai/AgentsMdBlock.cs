namespace LoadKit.Cli.Ai;

/// <summary>
/// The LoadKit block in a project's AGENTS.md, for AI tools without skill support. It lives between markers so that
/// running <c>ai install --agents-md</c> again replaces it instead of adding a second copy.
/// </summary>
internal static class AgentsMdBlock
{
    public const string StartMarker = "<!-- loadkit:start -->";
    public const string EndMarker = "<!-- loadkit:end -->";

    /// <param name="skillLink">Path to the installed SKILL.md as seen from AGENTS.md, with '/'.</param>
    public static string Build(string skillLink)
    {
        return $$"""
            {{StartMarker}}
            ## Load testing (LoadKit)

            For HTTP load tests use the `loadtest` CLI. Full instructions: [{{skillLink}}]({{skillLink}}).

            - Describe load as a JSON scenario in `loadtests/scenarios/` and run it with `loadtest`; never write code that generates load.
            - Secrets only as `${env:NAME}` with values in `loadtests/.env`; never put them into scenarios or the chat.
            - Before `check` or `run` against a non-localhost URL, get explicit confirmation from the user, then add `--yes`.
            {{EndMarker}}
            """;
    }

    /// <summary>Replaces an existing block, or appends one; content outside the markers is kept as is.</summary>
    public static string Merge(string existingContent, string block)
    {
        var start = existingContent.IndexOf(StartMarker, StringComparison.Ordinal);
        var end = start < 0 ? -1 : existingContent.IndexOf(EndMarker, start, StringComparison.Ordinal);
        if (start >= 0 && end >= 0)
        {
            return string.Concat(existingContent.AsSpan(0, start), block, existingContent.AsSpan(end + EndMarker.Length));
        }

        if (existingContent.Length == 0)
        {
            return block + "\n";
        }

        var separator = existingContent.EndsWith("\n\n", StringComparison.Ordinal) ? string.Empty
            : existingContent.EndsWith('\n') ? "\n"
            : "\n\n";
        return existingContent + separator + block + "\n";
    }
}
