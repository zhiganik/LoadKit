namespace LoadKit.Core.Engine;

/// <summary>Picks a request index proportionally to its weight: prefix sums and binary search, no allocations.</summary>
public sealed class WeightedRequestPicker
{
    private readonly long[] _cumulativeWeights;
    private readonly long _totalWeight;

    public WeightedRequestPicker(IReadOnlyList<int> weights)
    {
        if (weights.Count == 0)
        {
            throw new ArgumentException("At least one weight is required.", nameof(weights));
        }

        _cumulativeWeights = new long[weights.Count];
        long runningTotal = 0;
        for (var index = 0; index < weights.Count; index++)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(weights[index], 1, nameof(weights));
            runningTotal += weights[index];
            _cumulativeWeights[index] = runningTotal;
        }

        _totalWeight = runningTotal;
    }

    /// <param name="random">Must be thread-safe when shared by workers, e.g. <see cref="Random.Shared"/>.</param>
    public int Pick(Random random)
    {
        if (_cumulativeWeights.Length == 1)
        {
            return 0;
        }

        var target = random.NextInt64(_totalWeight);
        var low = 0;
        var high = _cumulativeWeights.Length - 1;
        while (low < high)
        {
            var middle = (low + high) >>> 1;
            if (_cumulativeWeights[middle] > target)
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }

        return low;
    }
}
