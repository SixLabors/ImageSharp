// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class RotateFlipTests : BaseImageOperationsExtensionTest
    {
        [Theory]
        [InlineData(RotateMode.None, FlipMode.None, 0)]
        [InlineData(RotateMode.Rotate90, FlipMode.None, 90)]
        [InlineData(RotateMode.Rotate180, FlipMode.None, 180)]
        [InlineData(RotateMode.Rotate270, FlipMode.None, 270)]
        [InlineData(RotateMode.None, FlipMode.Horizontal, 0)]
        [InlineData(RotateMode.Rotate90, FlipMode.Horizontal, 90)]
        [InlineData(RotateMode.Rotate180, FlipMode.Horizontal, 180)]
        [InlineData(RotateMode.Rotate270, FlipMode.Horizontal, 270)]
        [InlineData(RotateMode.None, FlipMode.Vertical, 0)]
        [InlineData(RotateMode.Rotate90, FlipMode.Vertical, 90)]
        [InlineData(RotateMode.Rotate180, FlipMode.Vertical, 180)]
        [InlineData(RotateMode.Rotate270, FlipMode.Vertical, 270)]
        public void RotateDegreesFloatRotateProcessorWithAnglesSet(RotateMode angle, FlipMode flip, float expectedAngle)
        {
            this.operations.RotateFlip(angle, flip);
            RotateProcessor<Rgba32> rotateProcessor = this.Verify<RotateProcessor<Rgba32>>(0);
            FlipProcessor<Rgba32> flipProcessor = this.Verify<FlipProcessor<Rgba32>>(1);

            Assert.Equal(expectedAngle, rotateProcessor.Degrees);
            Assert.Equal(flip, flipProcessor.FlipMode);
        }
    }
}