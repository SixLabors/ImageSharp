// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    [Trait("Category", "Processors")]
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
            RotateProcessor rotateProcessor = this.Verify<RotateProcessor>(0);
            FlipProcessor flipProcessor = this.Verify<FlipProcessor>(1);

            Assert.Equal(expectedAngle, rotateProcessor.Degrees);
            Assert.Equal(flip, flipProcessor.FlipMode);
        }
    }
}
