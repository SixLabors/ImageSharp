// <copyright file="BinaryThresholdTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Binarization
{
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing.Processors;
    using SixLabors.Primitives;
    using Xunit;

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