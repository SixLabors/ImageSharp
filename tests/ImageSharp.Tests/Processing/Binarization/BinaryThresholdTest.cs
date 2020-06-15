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
            Assert.Equal(Color.White, p.UpperColor);
            Assert.Equal(Color.Black, p.LowerColor);
        }

        [Fact]
        public void BinaryThreshold_rect_CorrectProcessor()
        {
            this.operations.BinaryThreshold(.93f, this.rect);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>(this.rect);
            Assert.Equal(.93f, p.Threshold);
            Assert.Equal(Color.White, p.UpperColor);
            Assert.Equal(Color.Black, p.LowerColor);
        }

        [Fact]
        public void BinaryThreshold_CorrectProcessorWithUpperLower()
        {
            this.operations.BinaryThreshold(.23f, Color.HotPink, Color.Yellow);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>();
            Assert.Equal(.23f, p.Threshold);
            Assert.Equal(Color.HotPink, p.UpperColor);
            Assert.Equal(Color.Yellow, p.LowerColor);
        }

        [Fact]
        public void BinaryThreshold_rect_CorrectProcessorWithUpperLower()
        {
            this.operations.BinaryThreshold(.93f, Color.HotPink, Color.Yellow, this.rect);
            BinaryThresholdProcessor p = this.Verify<BinaryThresholdProcessor>(this.rect);
            Assert.Equal(.93f, p.Threshold);
            Assert.Equal(Color.HotPink, p.UpperColor);
            Assert.Equal(Color.Yellow, p.LowerColor);
        }
    }
}