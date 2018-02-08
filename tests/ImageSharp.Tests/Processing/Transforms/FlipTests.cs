// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class FlipTests : BaseImageOperationsExtensionTest
    {

        [Theory]
        [InlineData(FlipType.None)]
        [InlineData(FlipType.Horizontal)]
        [InlineData(FlipType.Vertical)]
        public void Flip_degreesFloat_RotateProcessorWithAnglesSetAndExpandTrue(FlipType flip)
        {
            this.operations.Flip(flip);
            var flipProcessor = this.Verify<FlipProcessor<Rgba32>>();

            Assert.Equal(flip, flipProcessor.FlipType);
        }
    }
}
