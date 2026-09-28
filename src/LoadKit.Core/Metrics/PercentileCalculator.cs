namespace LoadKit.Core.Metrics;

/// <summary>
/// Nearest-rank percentiles. The rank is computed in integers, <c>rank = (P × N + 99) / 100</c>:
/// in <c>double</c>, <c>0.07 × 100</c> is <c>7.000000000000001</c> and <c>ceil</c> would pick one element too far.
/// </summary>
public static class PercentileCalculator
{
    /// <summary>One-based rank of the <paramref name="percentile"/>-th percentile among <paramref name="count"/> values.</summary>
    public static int Rank(int percentile, int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(percentile, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percentile, 100);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        return (int)((((long)percentile * count) + 99) / 100);
    }

    /// <param name="sortedValues">Values sorted in ascending order; must not be empty.</param>
    public static long ValueAt(ReadOnlySpan<long> sortedValues, int percentile)
    {
        if (sortedValues.IsEmpty)
        {
            throw new ArgumentException("A percentile of an empty set is undefined.", nameof(sortedValues));
        }

        return sortedValues[Rank(percentile, sortedValues.Length) - 1];
    }
}
