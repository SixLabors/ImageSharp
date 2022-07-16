// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Binarization;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Binarization
{
    [Trait("Category", "Processors")]
    public class BinaryThresholdTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void BinaryThreshold_CorrectProcessor()
        {
            this.operations.BinaryThreshold(.23f);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>();
            Assert.Equal(.23f, p.Threshold);
            Assert.Equal(BinaryThresholdMode.Luminance, p.Mode);
            Assert.Equal(Color.White, p.UpperColor);
            Assert.Equal(Color.Black, p.LowerColor);
        }

        [Fact]
        public void BinaryThreshold_rect_CorrectProcessor()
        {
            this.operations.BinaryThreshold(.93f, this.rect);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>(this.rect);
            Assert.Equal(.93f, p.Threshold);
            Assert.Equal(BinaryThresholdMode.Luminance, p.Mode);
            Assert.Equal(Color.White, p.UpperColor);
            Assert.Equal(Color.Black, p.LowerColor);
        }

        [Fact]
        public void BinaryThreshold_CorrectProcessorWithUpperLower()
        {
            this.operations.BinaryThreshold(.23f, Color.HotPink, Color.Yellow);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>();
            Assert.Equal(.23f, p.Threshold);
            Assert.Equal(BinaryThresholdMode.Luminance, p.Mode);
            Assert.Equal(Color.HotPink, p.UpperColor);
            Assert.Equal(Color.Yellow, p.LowerColor);
        }

        [Fact]
        public void BinaryThreshold_rect_CorrectProcessorWithUpperLower()
        {
            this.operations.BinaryThreshold(.93f, Color.HotPink, Color.Yellow, this.rect);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>(this.rect);
            Assert.Equal(BinaryThresholdMode.Luminance, p.Mode);
            Assert.Equal(.93f, p.Threshold);
            Assert.Equal(Color.HotPink, p.UpperColor);
            Assert.Equal(Color.Yellow, p.LowerColor);
        }

        [Fact]
        public void BinarySaturationThreshold_CorrectProcessor()
        {
            this.operations.BinaryThreshold(.23f, BinaryThresholdMode.Saturation);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>();
            Assert.Equal(.23f, p.Threshold);
            Assert.Equal(BinaryThresholdMode.Saturation, p.Mode);
            Assert.Equal(Color.White, p.UpperColor);
            Assert.Equal(Color.Black, p.LowerColor);
        }

        [Fact]
        public void BinarySaturationThreshold_rect_CorrectProcessor()
        {
            this.operations.BinaryThreshold(.93f, BinaryThresholdMode.Saturation, this.rect);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>(this.rect);
            Assert.Equal(.93f, p.Threshold);
            Assert.Equal(BinaryThresholdMode.Saturation, p.Mode);
            Assert.Equal(Color.White, p.UpperColor);
            Assert.Equal(Color.Black, p.LowerColor);
        }

        [Fact]
        public void BinarySaturationThreshold_CorrectProcessorWithUpperLower()
        {
            this.operations.BinaryThreshold(.23f, Color.HotPink, Color.Yellow, BinaryThresholdMode.Saturation);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>();
            Assert.Equal(.23f, p.Threshold);
            Assert.Equal(BinaryThresholdMode.Saturation, p.Mode);
            Assert.Equal(Color.HotPink, p.UpperColor);
            Assert.Equal(Color.Yellow, p.LowerColor);
        }

        [Fact]
        public void BinarySaturationThreshold_rect_CorrectProcessorWithUpperLower()
        {
            this.operations.BinaryThreshold(.93f, Color.HotPink, Color.Yellow, BinaryThresholdMode.Saturation, this.rect);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>(this.rect);
            Assert.Equal(.93f, p.Threshold);
            Assert.Equal(BinaryThresholdMode.Saturation, p.Mode);
            Assert.Equal(Color.HotPink, p.UpperColor);
            Assert.Equal(Color.Yellow, p.LowerColor);
        }

        [Fact]
        public void BinaryMaxChromaThreshold_CorrectProcessor()
        {
            this.operations.BinaryThreshold(.23f, BinaryThresholdMode.MaxChroma);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>();
            Assert.Equal(.23f, p.Threshold);
            Assert.Equal(BinaryThresholdMode.MaxChroma, p.Mode);
            Assert.Equal(Color.White, p.UpperColor);
            Assert.Equal(Color.Black, p.LowerColor);
        }

        [Fact]
        public void BinaryMaxChromaThreshold_rect_CorrectProcessor()
        {
            this.operations.BinaryThreshold(.93f, BinaryThresholdMode.MaxChroma, this.rect);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>(this.rect);
            Assert.Equal(.93f, p.Threshold);
            Assert.Equal(BinaryThresholdMode.MaxChroma, p.Mode);
            Assert.Equal(Color.White, p.UpperColor);
            Assert.Equal(Color.Black, p.LowerColor);
        }

        [Fact]
        public void BinaryMaxChromaThreshold_CorrectProcessorWithUpperLower()
        {
            this.operations.BinaryThreshold(.23f, Color.HotPink, Color.Yellow, BinaryThresholdMode.MaxChroma);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>();
            Assert.Equal(.23f, p.Threshold);
            Assert.Equal(BinaryThresholdMode.MaxChroma, p.Mode);
            Assert.Equal(Color.HotPink, p.UpperColor);
            Assert.Equal(Color.Yellow, p.LowerColor);
        }

        [Fact]
        public void BinaryMaxChromaThreshold_rect_CorrectProcessorWithUpperLower()
        {
            this.operations.BinaryThreshold(.93f, Color.HotPink, Color.Yellow, BinaryThresholdMode.MaxChroma, this.rect);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>(this.rect);
            Assert.Equal(.93f, p.Threshold);
            Assert.Equal(BinaryThresholdMode.MaxChroma, p.Mode);
            Assert.Equal(Color.HotPink, p.UpperColor);
            Assert.Equal(Color.Yellow, p.LowerColor);
        }
    }
}
