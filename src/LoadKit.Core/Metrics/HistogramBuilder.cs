using System.Diagnostics;

namespace LoadKit.Core.Metrics;

/// <summary>
/// Response time distribution over fixed, readable bounds (1-2-5 steps from 1 ms to 30 s). Fixed bounds make two
/// reports comparable. Empty buckets before the first and after the last response are dropped.
/// </summary>
public static class HistogramBuilder
{
    private static readonly double[] UpperBoundsMs = [1, 2, 5, 10, 20, 50, 100, 200, 500, 1_000, 2_000, 5_000, 10_000, 30_000];

    public static IReadOnlyList<HistogramBucket> Build(IReadOnlyList<RequestResult> results)
    {
        var counts = new int[UpperBoundsMs.Length + 1];
        foreach (var result in results)
        {
            if (result.HasResponse)
            {
                counts[BucketIndex(Stopwatch.GetElapsedTime(0, result.ElapsedTicks).TotalMilliseconds)]++;
            }
        }

        var first = Array.FindIndex(counts, count => count > 0);
        if (first < 0)
        {
            return [];
        }

        var last = Array.FindLastIndex(counts, count => count > 0);
        var buckets = new List<HistogramBucket>(last - first + 1);
        for (var index = first; index <= last; index++)
        {
            var from = index == 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(UpperBoundsMs[index - 1]);
            TimeSpan? to = index < UpperBoundsMs.Length ? TimeSpan.FromMilliseconds(UpperBoundsMs[index]) : null;
            buckets.Add(new HistogramBucket(from, to, counts[index]));
        }

        return buckets;
    }

    private static int BucketIndex(double milliseconds)
    {
        for (var index = 0; index < UpperBoundsMs.Length; index++)
        {
            if (milliseconds < UpperBoundsMs[index])
            {
                return index;
            }
        }

        return UpperBoundsMs.Length;
    }
}
