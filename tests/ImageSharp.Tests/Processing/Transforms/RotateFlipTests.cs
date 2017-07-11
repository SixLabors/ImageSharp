// <copyright file="RotateFlipTests.cs" company="James Jackson-South">
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

    public class RotateFlipTests : BaseImageOperationsExtensionTest
    {

        [Theory]
        [InlineData(RotateType.None, FlipType.None, 0)]
        [InlineData(RotateType.Rotate90, FlipType.None, 90)]
        [InlineData(RotateType.Rotate180, FlipType.None, 180)]
        [InlineData(RotateType.Rotate270, FlipType.None, 270)]
        [InlineData(RotateType.None, FlipType.Horizontal, 0)]
        [InlineData(RotateType.Rotate90, FlipType.Horizontal, 90)]
        [InlineData(RotateType.Rotate180, FlipType.Horizontal, 180)]
        [InlineData(RotateType.Rotate270, FlipType.Horizontal, 270)]
        [InlineData(RotateType.None, FlipType.Vertical, 0)]
        [InlineData(RotateType.Rotate90, FlipType.Vertical, 90)]
        [InlineData(RotateType.Rotate180, FlipType.Vertical, 180)]
        [InlineData(RotateType.Rotate270, FlipType.Vertical, 270)]
        public void Rotate_degreesFloat_RotateProcessorWithAnglesSetAndExpandTrue(RotateType angle, FlipType flip, float expectedAngle)
        {
            this.operations.RotateFlip(angle, flip);
            var rotateProcessor = this.Verify<RotateProcessor<Rgba32>>(0);
            var flipProcessor = this.Verify<FlipProcessor<Rgba32>>(1);

            Assert.Equal(expectedAngle, rotateProcessor.Angle);
            Assert.False(rotateProcessor.Expand);
            Assert.Equal(flip, flipProcessor.FlipType);
        }
    }
}