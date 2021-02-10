// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Binarization;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Binarization
{
    public class BinaryThresholdTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void BinaryThreshold_CorrectProcessor()
        {
            this.operations.BinaryThreshold(.23f);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>();
            Assert.Equal(.23f, p.Threshold);
            Assert.Equal(BinaryThresholdColorComponent.Luminance, p.ColorComponent);
            Assert.Equal(Color.White, p.UpperColor);
            Assert.Equal(Color.Black, p.LowerColor);
        }

        [Fact]
        public void BinaryThreshold_rect_CorrectProcessor()
        {
            this.operations.BinaryThreshold(.93f, this.rect);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>(this.rect);
            Assert.Equal(.93f, p.Threshold);
            Assert.Equal(BinaryThresholdColorComponent.Luminance, p.ColorComponent);
            Assert.Equal(Color.White, p.UpperColor);
            Assert.Equal(Color.Black, p.LowerColor);
        }

        [Fact]
        public void BinaryThreshold_CorrectProcessorWithUpperLower()
        {
            this.operations.BinaryThreshold(.23f, Color.HotPink, Color.Yellow);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>();
            Assert.Equal(.23f, p.Threshold);
            Assert.Equal(BinaryThresholdColorComponent.Luminance, p.ColorComponent);
            Assert.Equal(Color.HotPink, p.UpperColor);
            Assert.Equal(Color.Yellow, p.LowerColor);
        }

        [Fact]
        public void BinaryThreshold_rect_CorrectProcessorWithUpperLower()
        {
            this.operations.BinaryThreshold(.93f, Color.HotPink, Color.Yellow, this.rect);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>(this.rect);
            Assert.Equal(BinaryThresholdColorComponent.Luminance, p.ColorComponent);
            Assert.Equal(.93f, p.Threshold);
            Assert.Equal(Color.HotPink, p.UpperColor);
            Assert.Equal(Color.Yellow, p.LowerColor);
        }

        [Fact]
        public void BinarySaturationThreshold_CorrectProcessor()
        {
            this.operations.BinaryThreshold(.23f, BinaryThresholdColorComponent.Saturation);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>();
            Assert.Equal(.23f, p.Threshold);
            Assert.Equal(BinaryThresholdColorComponent.Saturation, p.ColorComponent);
            Assert.Equal(Color.White, p.UpperColor);
            Assert.Equal(Color.Black, p.LowerColor);
        }

        [Fact]
        public void BinarySaturationThreshold_rect_CorrectProcessor()
        {
            this.operations.BinaryThreshold(.93f, BinaryThresholdColorComponent.Saturation, this.rect);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>(this.rect);
            Assert.Equal(.93f, p.Threshold);
            Assert.Equal(BinaryThresholdColorComponent.Saturation, p.ColorComponent);
            Assert.Equal(Color.White, p.UpperColor);
            Assert.Equal(Color.Black, p.LowerColor);
        }

        [Fact]
        public void BinarySaturationThreshold_CorrectProcessorWithUpperLower()
        {
            this.operations.BinaryThreshold(.23f, Color.HotPink, Color.Yellow, BinaryThresholdColorComponent.Saturation);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>();
            Assert.Equal(.23f, p.Threshold);
            Assert.Equal(BinaryThresholdColorComponent.Saturation, p.ColorComponent);
            Assert.Equal(Color.HotPink, p.UpperColor);
            Assert.Equal(Color.Yellow, p.LowerColor);
        }

        [Fact]
        public void BinarySaturationThreshold_rect_CorrectProcessorWithUpperLower()
        {
            this.operations.BinaryThreshold(.93f, Color.HotPink, Color.Yellow, BinaryThresholdColorComponent.Saturation, this.rect);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>(this.rect);
            Assert.Equal(.93f, p.Threshold);
            Assert.Equal(BinaryThresholdColorComponent.Saturation, p.ColorComponent);
            Assert.Equal(Color.HotPink, p.UpperColor);
            Assert.Equal(Color.Yellow, p.LowerColor);
        }

        [Fact]
        public void BinaryColorfulness_L10Threshold_CorrectProcessor()
        {
            this.operations.BinaryThreshold(.23f, BinaryThresholdColorComponent.Colorfulness_L10);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>();
            Assert.Equal(.23f, p.Threshold);
            Assert.Equal(BinaryThresholdColorComponent.Colorfulness_L10, p.ColorComponent);
            Assert.Equal(Color.White, p.UpperColor);
            Assert.Equal(Color.Black, p.LowerColor);
        }

        [Fact]
        public void BinaryColorfulness_L10Threshold_rect_CorrectProcessor()
        {
            this.operations.BinaryThreshold(.93f, BinaryThresholdColorComponent.Colorfulness_L10, this.rect);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>(this.rect);
            Assert.Equal(.93f, p.Threshold);
            Assert.Equal(BinaryThresholdColorComponent.Colorfulness_L10, p.ColorComponent);
            Assert.Equal(Color.White, p.UpperColor);
            Assert.Equal(Color.Black, p.LowerColor);
        }

        [Fact]
        public void BinaryColorfulness_L10Threshold_CorrectProcessorWithUpperLower()
        {
            this.operations.BinaryThreshold(.23f, Color.HotPink, Color.Yellow, BinaryThresholdColorComponent.Colorfulness_L10);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>();
            Assert.Equal(.23f, p.Threshold);
            Assert.Equal(BinaryThresholdColorComponent.Colorfulness_L10, p.ColorComponent);
            Assert.Equal(Color.HotPink, p.UpperColor);
            Assert.Equal(Color.Yellow, p.LowerColor);
        }

        [Fact]
        public void BinaryColorfulness_L10Threshold_rect_CorrectProcessorWithUpperLower()
        {
            this.operations.BinaryThreshold(.93f, Color.HotPink, Color.Yellow, BinaryThresholdColorComponent.Colorfulness_L10, this.rect);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>(this.rect);
            Assert.Equal(.93f, p.Threshold);
            Assert.Equal(BinaryThresholdColorComponent.Colorfulness_L10, p.ColorComponent);
            Assert.Equal(Color.HotPink, p.UpperColor);
            Assert.Equal(Color.Yellow, p.LowerColor);
        }
    }
}
