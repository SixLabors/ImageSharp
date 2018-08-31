// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class RotateTests : BaseImageOperationsExtensionTest
    {
        [Theory]
        [InlineData(85.6f)]
        [InlineData(21)]
        public void RotateDegreesFloatRotateProcessorWithAnglesSet(float angle)
        {
            this.operations.Rotate(angle);
            RotateProcessor<Rgba32> processor = this.Verify<RotateProcessor<Rgba32>>();

            Assert.Equal(angle, processor.Degrees);
        }

        [Theory]
        [InlineData(RotateMode.None, 0)]
        [InlineData(RotateMode.Rotate90, 90)]
        [InlineData(RotateMode.Rotate180, 180)]
        [InlineData(RotateMode.Rotate270, 270)]
        public void RotateRotateTypeRotateProcessorWithAnglesConvertedFromEnum(RotateMode angle, float expectedAngle)
        {
            this.operations.Rotate(angle); // is this api needed ???
            RotateProcessor<Rgba32> processor = this.Verify<RotateProcessor<Rgba32>>();

            Assert.Equal(expectedAngle, processor.Degrees);
        }
    }
}