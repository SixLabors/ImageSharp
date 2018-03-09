// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    using SixLabors.ImageSharp.Processing.Transforms;
    using SixLabors.ImageSharp.Processing.Transforms.Processors;

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
        [InlineData(RotateType.None, 0)]
        [InlineData(RotateType.Rotate90, 90)]
        [InlineData(RotateType.Rotate180, 180)]
        [InlineData(RotateType.Rotate270, 270)]
        public void RotateRotateTypeRotateProcessorWithAnglesConvertedFromEnum(RotateType angle, float expectedangle)
        {
            this.operations.Rotate(angle); // is this api needed ???
            RotateProcessor<Rgba32> processor = this.Verify<RotateProcessor<Rgba32>>();

            Assert.Equal(expectedangle, processor.Degrees);
        }
    }
}