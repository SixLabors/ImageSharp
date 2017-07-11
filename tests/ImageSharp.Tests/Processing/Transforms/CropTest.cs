// <copyright file="CropTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Transforms
{
    using System;
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing.Processors;
    using SixLabors.Primitives;
    using Xunit;

    public class CropTest : BaseImageOperationsExtensionTest
    {
        [Theory]
        [InlineData(10, 10)]
        [InlineData(12, 123)]
        public void Crop_Width_height_CropProcessorWithRectangleSet(int width, int height)
        {
            this.operations.Crop(width, height);
            var processor = this.Verify<CropProcessor<Rgba32>>();

            Assert.Equal(new Rectangle(0, 0, width, height), processor.CropRectangle);
        }

        [Theory]
        [InlineData(10, 10, 2, 6)]
        [InlineData(12, 123, 6, 2)]
        public void Crop_Rectangle_CropProcessorWithRectangleSet(int x, int y, int width, int height)
        {
            var rect = new Rectangle(x, y, width, height);
            this.operations.Crop(rect);
            var processor = this.Verify<CropProcessor<Rgba32>>();

            Assert.Equal(rect, processor.CropRectangle);
        }
    }
}