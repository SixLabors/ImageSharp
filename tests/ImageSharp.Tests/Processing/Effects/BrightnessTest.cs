// <copyright file="BrightnessTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Effects
{
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing.Processors;
    using SixLabors.Primitives;
    using Xunit;

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