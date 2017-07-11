// <copyright file="PixelateTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Effects
{
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing.Processors;
    using SixLabors.Primitives;
    using Xunit;

    public class PixelateTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Pixelate_PixelateProcessorDefaultsSet()
        {
            this.operations.Pixelate();
            var processor = this.Verify<PixelateProcessor<Rgba32>>();

            Assert.Equal(4, processor.Size);
        }

        [Fact]
        public void Pixelate_Size_PixelateProcessorDefaultsSet()
        {
            this.operations.Pixelate(12);
            var processor = this.Verify<PixelateProcessor<Rgba32>>();

            Assert.Equal(12, processor.Size);
        }

        [Fact]
        public void Pixelate_Size_rect_PixelateProcessorDefaultsSet()
        {
            this.operations.Pixelate(23, this.rect);
            var processor = this.Verify<PixelateProcessor<Rgba32>>(this.rect);

            Assert.Equal(23, processor.Size);
        }
    }
}