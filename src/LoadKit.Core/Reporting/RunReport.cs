namespace LoadKit.Core.Reporting;

/// <summary>
/// The single report model: the console summary, <c>report.md</c> and <c>report.json</c> are built from it.
/// Serialized as <c>report.json</c>, it is a public contract described by <c>schemas/report.schema.json</c>;
/// changing it means bumping <see cref="CurrentSchemaVersion"/> and updating the schema. Contains no secrets.
/// </summary>
/// <param name="ThresholdsPassed">Null when the scenario has no thresholds.</param>
/// <param name="ApplicationInsights">Null when the run was not tagged with <c>loadrun</c>.</param>
public sealed record RunReport(
    int SchemaVersion,
    string ToolVersion,
    ReportScenario Scenario,
    ReportRun Run,
    ReportLoad Load,
    ReportRequestStatistics Overall,
    IReadOnlyList<ReportRequestStatistics> Requests,
    IReadOnlyList<ReportHistogramBucket> Histogram,
    IReadOnlyList<ReportThresholdCheck> Thresholds,
    bool? ThresholdsPassed,
    IReadOnlyList<ReportErrorSample> ErrorSamples,
    IReadOnlyList<ReportWarning> Warnings,
    ReportApplicationInsights? ApplicationInsights)
{
    public const int CurrentSchemaVersion = 1;
}
