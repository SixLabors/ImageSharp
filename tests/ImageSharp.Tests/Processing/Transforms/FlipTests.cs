﻿// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    using SixLabors.ImageSharp.Processing.Transforms;
    using SixLabors.ImageSharp.Processing.Transforms.Processors;

    public class FlipTests : BaseImageOperationsExtensionTest
    {

        [Theory]
        [InlineData(FlipType.None)]
        [InlineData(FlipType.Horizontal)]
        [InlineData(FlipType.Vertical)]
        public void Flip_degreesFloat_RotateProcessorWithAnglesSetAndExpandTrue(FlipType flip)
        {
            this.operations.Flip(flip);
            FlipProcessor<Rgba32> flipProcessor = this.Verify<FlipProcessor<Rgba32>>();

            Assert.Equal(flip, flipProcessor.FlipType);
        }
    }
}
