// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
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
            BinaryThresholdProcessor<Rgba32> p = this.Verify<BinaryThresholdProcessor<Rgba32>>();
            Assert.Equal(.23f, p.Threshold);
            Assert.Equal(NamedColors<Rgba32>.White, p.UpperColor);
            Assert.Equal(NamedColors<Rgba32>.Black, p.LowerColor);
        }

        [Fact]
        public void BinaryThreshold_rect_CorrectProcessor()
        {
            this.operations.BinaryThreshold(.93f, this.rect);
            BinaryThresholdProcessor<Rgba32> p = this.Verify<BinaryThresholdProcessor<Rgba32>>(this.rect);
            Assert.Equal(.93f, p.Threshold);
            Assert.Equal(NamedColors<Rgba32>.White, p.UpperColor);
            Assert.Equal(NamedColors<Rgba32>.Black, p.LowerColor);
        }

        [Fact]
        public void BinaryThreshold_CorrectProcessorWithUpperLower()
        {
            this.operations.BinaryThreshold(.23f, NamedColors<Rgba32>.HotPink, NamedColors<Rgba32>.Yellow);
            BinaryThresholdProcessor<Rgba32> p = this.Verify<BinaryThresholdProcessor<Rgba32>>();
            Assert.Equal(.23f, p.Threshold);
            Assert.Equal(NamedColors<Rgba32>.HotPink, p.UpperColor);
            Assert.Equal(NamedColors<Rgba32>.Yellow, p.LowerColor);
        }

        [Fact]
        public void BinaryThreshold_rect_CorrectProcessorWithUpperLower()
        {
            this.operations.BinaryThreshold(.93f, NamedColors<Rgba32>.HotPink, NamedColors<Rgba32>.Yellow, this.rect);
            BinaryThresholdProcessor<Rgba32> p = this.Verify<BinaryThresholdProcessor<Rgba32>>(this.rect);
            Assert.Equal(.93f, p.Threshold);
            Assert.Equal(NamedColors<Rgba32>.HotPink, p.UpperColor);
            Assert.Equal(NamedColors<Rgba32>.Yellow, p.LowerColor);
        }
    }
}