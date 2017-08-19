// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Effects
{
    public class BrightnessTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Brightness_amount_BrightnessProcessorDefaultsSet()
        {
            this.operations.Brightness(23);
            var processor = this.Verify<BrightnessProcessor<Rgba32>>();

            Assert.Equal(23, processor.Value);
        }

        [Fact]
        public void Brightness_amount_rect_BrightnessProcessorDefaultsSet()
        {
            this.operations.Brightness(23, this.rect);
            var processor = this.Verify<BrightnessProcessor<Rgba32>>(this.rect);

            Assert.Equal(23, processor.Value);
        }
    }
}