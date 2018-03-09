// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Transforms;
using SixLabors.ImageSharp.Processing.Transforms.Processors;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
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
        public void RotateDegreesFloatRotateProcessorWithAnglesSet(RotateType angle, FlipType flip, float expectedAngle)
        {
            this.operations.RotateFlip(angle, flip);
            RotateProcessor<Rgba32> rotateProcessor = this.Verify<RotateProcessor<Rgba32>>(0);
            FlipProcessor<Rgba32> flipProcessor = this.Verify<FlipProcessor<Rgba32>>(1);

            Assert.Equal(expectedAngle, rotateProcessor.Degrees);
            Assert.Equal(flip, flipProcessor.FlipType);
        }
    }
}