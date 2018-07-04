// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    using SixLabors.ImageSharp.Processing;
    using SixLabors.ImageSharp.Processing.Processors.Transforms;

    public class FlipTests : BaseImageOperationsExtensionTest
    {

        [Theory]
        [InlineData(FlipMode.None)]
        [InlineData(FlipMode.Horizontal)]
        [InlineData(FlipMode.Vertical)]
        public void Flip_degreesFloat_RotateProcessorWithAnglesSetAndExpandTrue(FlipMode flip)
        {
            this.operations.Flip(flip);
            FlipProcessor<Rgba32> flipProcessor = this.Verify<FlipProcessor<Rgba32>>();

            Assert.Equal(flip, flipProcessor.FlipMode);
        }
    }
}
