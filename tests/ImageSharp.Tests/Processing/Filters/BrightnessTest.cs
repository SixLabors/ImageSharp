// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Effects
{
    public class BrightnessTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Brightness_amount_BrightnessProcessorDefaultsSet()
        {
            this.operations.Brightness(1.5F);
            BrightnessProcessor<Rgba32> processor = this.Verify<BrightnessProcessor<Rgba32>>();

            Assert.Equal(1.5F, processor.Amount);
        }

        [Fact]
        public void Brightness_amount_rect_BrightnessProcessorDefaultsSet()
        {
            this.operations.Brightness(1.5F, this.rect);
            BrightnessProcessor<Rgba32> processor = this.Verify<BrightnessProcessor<Rgba32>>(this.rect);

            Assert.Equal(1.5F, processor.Amount);
        }
    }
}