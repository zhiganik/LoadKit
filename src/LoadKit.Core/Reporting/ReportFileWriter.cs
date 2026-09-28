using System.Globalization;
using System.Text;

namespace LoadKit.Core.Reporting;

/// <summary>Writes <c>report.md</c> and <c>report.json</c> into <c>&lt;out&gt;/&lt;yyyyMMdd-HHmmss&gt;-&lt;scenario-name&gt;/</c>.</summary>
public static class ReportFileWriter
{
    public const string MarkdownFileName = "report.md";
    public const string JsonFileName = "report.json";

    /// <returns>The created report folder.</returns>
    public static async Task<string> WriteAsync(RunReport report, string outDirectory, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(outDirectory, FolderName(report));
        Directory.CreateDirectory(folder);
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        await File.WriteAllTextAsync(Path.Combine(folder, MarkdownFileName), MarkdownReportWriter.Write(report), encoding, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(folder, JsonFileName), JsonReportWriter.Write(report), encoding, cancellationToken);
        return folder;
    }

    /// <summary>UTC start time, then the scenario name reduced to letters, digits, '-' and '_'.</summary>
    public static string FolderName(RunReport report)
    {
        var timestamp = report.Run.StartedAt.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var name = new StringBuilder(report.Scenario.Name.Length);
        foreach (var character in report.Scenario.Name)
        {
            name.Append(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' ? character : '-');
        }

        var safeName = name.ToString().Trim('-');
        return safeName.Length == 0 ? timestamp : $"{timestamp}-{safeName}";
    }
}
