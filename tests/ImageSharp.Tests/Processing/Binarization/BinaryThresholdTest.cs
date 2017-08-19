// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Binarization
{
    public class BinaryThresholdTest : BaseImageOperationsExtensionTest
    {

        [Fact]
        public void BinaryThreshold_CorrectProcessor()
        {
            this.operations.BinaryThreshold(.23f);
            var p = this.Verify<BinaryThresholdProcessor<Rgba32>>();
            Assert.Equal(.23f, p.Threshold);
        }

        [Fact]
        public void BinaryThreshold_rect_CorrectProcessor()
        {
            this.operations.BinaryThreshold(.93f, this.rect);
            var p = this.Verify<BinaryThresholdProcessor<Rgba32>>(this.rect);
            Assert.Equal(.93f, p.Threshold);
        }
    }
}