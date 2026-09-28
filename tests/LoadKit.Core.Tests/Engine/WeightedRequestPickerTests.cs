using LoadKit.Core.Engine;
using LoadKit.Core.Tests.TestSupport;

namespace LoadKit.Core.Tests.Engine;

public sealed class WeightedRequestPickerTests
{
    [Fact]
    public void Pick_MapsRandomValuesToWeightRanges()
    {
        // Weights 1, 3, 6 → ranges [0], [1..3], [4..9].
        var picker = new WeightedRequestPicker([1, 3, 6]);
        var random = new SequenceRandom(0, 1, 3, 4, 9);

        var picks = Enumerable.Range(0, 5).Select(_ => picker.Pick(random)).ToArray();

        Assert.Equal([0, 1, 1, 2, 2], picks);
    }

    [Fact]
    public void Pick_FollowsWeightsStatistically()
    {
        var picker = new WeightedRequestPicker([70, 25, 5]);
        var random = new Random(12345);
        var counts = new int[3];

        for (var draw = 0; draw < 100_000; draw++)
        {
            counts[picker.Pick(random)]++;
        }

        Assert.InRange(counts[0], 69_000, 71_000);
        Assert.InRange(counts[1], 24_000, 26_000);
        Assert.InRange(counts[2], 4_500, 5_500);
    }

    [Fact]
    public void SingleRequest_IsAlwaysPicked()
    {
        var picker = new WeightedRequestPicker([5]);

        Assert.Equal(0, picker.Pick(new SequenceRandom()));
    }

    [Fact]
    public void Constructor_RejectsInvalidWeights()
    {
        Assert.Throws<ArgumentException>(() => new WeightedRequestPicker([]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WeightedRequestPicker([1, 0]));
    }
}
