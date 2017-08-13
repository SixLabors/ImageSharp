// <copyright file="HueTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.ColorMatrix
{
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing.Processors;
    using SixLabors.Primitives;
    using Xunit;

    public class HueTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Hue_amount_HueProcessorDefaultsSet()
        {
            this.operations.Hue(34f);
            var processor = this.Verify<HueProcessor<Rgba32>>();

            Assert.Equal(34f, processor.Angle);
        }

        [Fact]
        public void Hue_amount_rect_HueProcessorDefaultsSet()
        {
            this.operations.Hue(5f, this.rect);
            var processor = this.Verify<HueProcessor<Rgba32>>(this.rect);

            Assert.Equal(5f, processor.Angle);
        }
    }
}