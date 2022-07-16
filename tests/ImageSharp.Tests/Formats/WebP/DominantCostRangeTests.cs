// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Webp.Lossless;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Webp
{
    [Trait("Format", "Webp")]
    public class DominantCostRangeTests
    {
        [Fact]
        public void DominantCost_Constructor()
        {
            var dominantCostRange = new DominantCostRange();
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
            var dominantCostRange = new DominantCostRange();
            var histogram = new Vp8LHistogram(10)
            {
                LiteralCost = 1.0d,
                RedCost = 2.0d,
                BlueCost = 3.0d
            };

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
            var dominantCostRange = new DominantCostRange()
            {
                BlueMax = 253.4625,
                BlueMin = 109.0,
                LiteralMax = 285.0,
                LiteralMin = 133.0,
                RedMax = 191.0,
                RedMin = 109.0
            };
            var histogram = new Vp8LHistogram(6)
            {
                LiteralCost = 247.0d,
                RedCost = 112.0d,
                BlueCost = 202.0d,
                BitCost = 733.0d
            };
            dominantCostRange.UpdateDominantCostRange(histogram);

            // act
            int binIndex = dominantCostRange.GetHistoBinIndex(histogram, partitions);

            // assert
            Assert.Equal(expectedIndex, binIndex);
        }
    }
}
