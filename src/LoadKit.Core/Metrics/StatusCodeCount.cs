namespace LoadKit.Core.Metrics;

/// <param name="UnexpectedCount">How many of them were not in <c>expect.status</c>.</param>
public sealed record StatusCodeCount(int StatusCode, int Count, int UnexpectedCount);
