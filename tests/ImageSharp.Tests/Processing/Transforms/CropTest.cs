// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class CropTest : BaseImageOperationsExtensionTest
    {
        [Theory]
        [InlineData(10, 10)]
        [InlineData(12, 123)]
        public void CropWidthHeightCropProcessorWithRectangleSet(int width, int height)
        {
            this.operations.Crop(width, height);
            CropProcessor processor = this.Verify<CropProcessor>();

            Assert.Equal(new Rectangle(0, 0, width, height), processor.CropRectangle);
        }

        [Theory]
        [InlineData(10, 10, 2, 6)]
        [InlineData(12, 123, 6, 2)]
        public void CropRectangleCropProcessorWithRectangleSet(int x, int y, int width, int height)
        {
            var cropRectangle = new Rectangle(x, y, width, height);
            this.operations.Crop(cropRectangle);
            CropProcessor processor = this.Verify<CropProcessor>();

            Assert.Equal(cropRectangle, processor.CropRectangle);
        }

        [Fact]
        public void CropRectangleWithInvalidBoundsThrowsException()
        {
            var cropRectangle = Rectangle.Inflate(this.SourceBounds(), 5, 5);
            Assert.Throws<ArgumentException>(() => this.operations.Crop(cropRectangle));
        }
    }
}