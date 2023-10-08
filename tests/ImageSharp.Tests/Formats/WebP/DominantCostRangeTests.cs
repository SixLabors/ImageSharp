// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Webp.Lossless;

namespace SixLabors.ImageSharp.Tests.Formats.Webp;

[Trait("Format", "Webp")]
public class DominantCostRangeTests
{
    [Fact]
    public void DominantCost_Constructor()
    {
        DominantCostRange dominantCostRange = new();
        Assert.Equal(0, dominantCostRange.LiteralMax);
        Assert.Equal(double.MaxValue, dominantCostRange.LiteralMin);
        Assert.Equal(0, dominantCostRange.RedMax);
        Assert.Equal(double.MaxValue, dominantCostRange.RedMin);
        Assert.Equal(0, dominantCostRange.BlueMax);
        Assert.Equal(double.MaxValue, dominantCostRange.BlueMin);
    }

    [Fact]
    public void UpdateDominantCostRange_Works()
    {
        // arrange
        DominantCostRange dominantCostRange = new();
        using Vp8LHistogram histogram = Vp8LHistogram.Create(Configuration.Default.MemoryAllocator, 10);
        histogram.LiteralCost = 1.0d;
        histogram.RedCost = 2.0d;
        histogram.BlueCost = 3.0d;

        // act
        dominantCostRange.UpdateDominantCostRange(histogram);

        // assert
        Assert.Equal(1.0d, dominantCostRange.LiteralMax);
        Assert.Equal(1.0d, dominantCostRange.LiteralMin);
        Assert.Equal(2.0d, dominantCostRange.RedMax);
        Assert.Equal(2.0d, dominantCostRange.RedMin);
        Assert.Equal(3.0d, dominantCostRange.BlueMax);
        Assert.Equal(3.0d, dominantCostRange.BlueMin);
    }

    [Theory]
    [InlineData(3, 19)]
    [InlineData(4, 34)]
    public void GetHistoBinIndex_Works(int partitions, int expectedIndex)
    {
        // arrange
        DominantCostRange dominantCostRange = new()
        {
            BlueMax = 253.4625,
            BlueMin = 109.0,
            LiteralMax = 285.0,
            LiteralMin = 133.0,
            RedMax = 191.0,
            RedMin = 109.0
        };
        using Vp8LHistogram histogram = Vp8LHistogram.Create(Configuration.Default.MemoryAllocator, 6);
        histogram.LiteralCost = 247.0d;
        histogram.RedCost = 112.0d;
        histogram.BlueCost = 202.0d;
        histogram.BitCost = 733.0d;

        dominantCostRange.UpdateDominantCostRange(histogram);

        // act
        int binIndex = dominantCostRange.GetHistoBinIndex(histogram, partitions);

        // assert
        Assert.Equal(expectedIndex, binIndex);
    }
}
