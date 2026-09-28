using System.Globalization;

namespace LoadKit.Core.Scenarios.Validation;

/// <summary>Builds user-facing JSON paths: <c>requests[0].headers['X Custom']</c>. The root is <c>$</c>.</summary>
internal static class JsonPath
{
    public const string Root = "$";

    public static string Child(string parentPath, string name)
    {
        var isSimpleName = name.Length > 0 && name.All(IsSimpleNameCharacter);
        if (!isSimpleName)
        {
            return ParentOrRoot(parentPath) + "['" + name.Replace("'", "\\'", StringComparison.Ordinal) + "']";
        }

        return parentPath is "" or Root ? name : parentPath + "." + name;
    }

    public static string Index(string parentPath, int index)
    {
        return ParentOrRoot(parentPath) + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
    }

    private static string ParentOrRoot(string parentPath)
    {
        return parentPath.Length == 0 ? Root : parentPath;
    }

    private static bool IsSimpleNameCharacter(char character)
    {
        return char.IsAsciiLetterOrDigit(character) || character is '_' or '$' or '-';
    }
}
