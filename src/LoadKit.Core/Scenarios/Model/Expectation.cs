namespace LoadKit.Core.Scenarios.Model;

public sealed record Expectation(IReadOnlyList<int> Status, int? MaxMs);
