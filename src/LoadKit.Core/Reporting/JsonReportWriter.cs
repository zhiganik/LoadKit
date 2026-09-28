using System.Text.Encodings.Web;
using System.Text.Json;

namespace LoadKit.Core.Reporting;

/// <summary><c>report.json</c>: camelCase, indented, nulls written explicitly. Shape: <c>schemas/report.schema.json</c>.</summary>
public static class JsonReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,

        // The report is a file, never embedded in HTML: keep quotes and non-ASCII text readable.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Write(RunReport report)
    {
        return JsonSerializer.Serialize(report, Options) + "\n";
    }

    public static RunReport? Read(string json)
    {
        return JsonSerializer.Deserialize<RunReport>(json, Options);
    }
}
