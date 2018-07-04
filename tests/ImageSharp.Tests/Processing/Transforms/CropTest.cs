// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SixLabors.Primitives;
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
            CropProcessor<Rgba32> processor = this.Verify<CropProcessor<Rgba32>>();

            Assert.Equal(new Rectangle(0, 0, width, height), processor.CropRectangle);
        }

        [Theory]
        [InlineData(10, 10, 2, 6)]
        [InlineData(12, 123, 6, 2)]
        public void CropRectangleCropProcessorWithRectangleSet(int x, int y, int width, int height)
        {
            var cropRectangle = new Rectangle(x, y, width, height);
            this.operations.Crop(cropRectangle);
            CropProcessor<Rgba32> processor = this.Verify<CropProcessor<Rgba32>>();

            Assert.Equal(cropRectangle, processor.CropRectangle);
        }
    }
}