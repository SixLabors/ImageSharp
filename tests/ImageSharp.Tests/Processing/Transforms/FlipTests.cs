// <copyright file="FlipTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Transforms
{
    using System;
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;
    using ImageSharp.Processing.Processors;
    using Xunit;

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
