using LoadKit.Core.Metrics;

namespace LoadKit.Core.Tests.Metrics;

public sealed class PercentileCalculatorTests
{
    [Fact]
    public void TwoThousandValues_UseNearestRank()
    {
        var sortedValues = Enumerable.Range(1, 2000).Select(value => (long)value).ToArray();

        Assert.Equal(1000, PercentileCalculator.ValueAt(sortedValues, 50));
        Assert.Equal(1900, PercentileCalculator.ValueAt(sortedValues, 95));
        Assert.Equal(1980, PercentileCalculator.ValueAt(sortedValues, 99));
        Assert.Equal(2000, PercentileCalculator.ValueAt(sortedValues, 100));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(95)]
    [InlineData(99)]
    [InlineData(100)]
    public void SingleValue_IsEveryPercentile(int percentile)
    {
        Assert.Equal(42, PercentileCalculator.ValueAt([42L], percentile));
    }

    [Fact]
    public void IdenticalValues_GiveThatValue()
    {
        var sortedValues = Enumerable.Repeat(7L, 333).ToArray();

        Assert.Equal(7, PercentileCalculator.ValueAt(sortedValues, 50));
        Assert.Equal(7, PercentileCalculator.ValueAt(sortedValues, 99));
    }

    [Fact]
    public void EmptySet_Throws()
    {
        Assert.Throws<ArgumentException>(() => PercentileCalculator.ValueAt([], 95));
    }

    [Fact]
    public void Rank_IsExact_WhereDoubleArithmeticIsOffByOne()
    {
        // 0.07 × 100 is 7.000000000000001 in double, so ceil gives 8.
        Assert.Equal(8, (int)Math.Ceiling(7 / 100.0 * 100));

        Assert.Equal(7, PercentileCalculator.Rank(7, 100));
        Assert.Equal(7, PercentileCalculator.ValueAt(Enumerable.Range(1, 100).Select(value => (long)value).ToArray(), 7));
    }

    [Fact]
    public void Rank_MatchesExactCeiling_ForAllPercentilesAndCounts()
    {
        for (var count = 1; count <= 3000; count++)
        {
            for (var percentile = 1; percentile <= 100; percentile++)
            {
                var expectedRank = (int)Math.Ceiling(percentile * (decimal)count / 100m);
                Assert.Equal(expectedRank, PercentileCalculator.Rank(percentile, count));
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Rank_RejectsPercentileOutOfRange(int percentile)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PercentileCalculator.Rank(percentile, 10));
    }
}
