// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Effects;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Effects
{
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