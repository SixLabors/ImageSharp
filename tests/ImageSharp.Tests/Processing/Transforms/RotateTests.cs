// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class RotateTests : BaseImageOperationsExtensionTest
    {

        [Theory]
        [InlineData(85.6f)]
        [InlineData(21)]
        public void Rotate_degreesFloat_RotateProcessorWithAnglesSetAndExpandTrue(float angle)
        {
            this.operations.Rotate(angle);
            var processor = this.Verify<RotateProcessor<Rgba32>>();

            Assert.Equal(angle, processor.Angle);
            Assert.True(processor.Expand);
        }


        [Theory]
        [InlineData(RotateType.None, 0)]
        [InlineData(RotateType.Rotate90, 90)]
        [InlineData(RotateType.Rotate180, 180)]
        [InlineData(RotateType.Rotate270, 270)]
        public void Rotate_RotateType_RotateProcessorWithAnglesConvertedFromEnumAndExpandTrue(RotateType angle, float expectedangle)
        {
            this.operations.Rotate(angle); // is this api needed ???
            var processor = this.Verify<RotateProcessor<Rgba32>>();

            Assert.Equal(expectedangle, processor.Angle);
            Assert.False(processor.Expand);
        }


        [Theory]
        [InlineData(85.6f, false)]
        [InlineData(21, true)]
        [InlineData(21, false)]
        public void Rotate_degreesFloat_expand_RotateProcessorWithAnglesSetAndExpandSet(float angle, bool expand)
        {
            this.operations.Rotate(angle, expand);
            var processor = this.Verify<RotateProcessor<Rgba32>>();

            Assert.Equal(angle, processor.Angle);
            Assert.Equal(expand, processor.Expand);
        }
    }
}