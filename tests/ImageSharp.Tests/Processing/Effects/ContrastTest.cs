// <copyright file="ContrastTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Effects
{
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing.Processors;
    using SixLabors.Primitives;
    using Xunit;

    public class ContrastTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Contrast_amount_ContrastProcessorDefaultsSet()
        {
            this.operations.Contrast(23);
            var processor = this.Verify<ContrastProcessor<Rgba32>>();

            Assert.Equal(23, processor.Value);
        }

        [Fact]
        public void Contrast_amount_rect_ContrastProcessorDefaultsSet()
        {
            this.operations.Contrast(23, this.rect);
            var processor = this.Verify<ContrastProcessor<Rgba32>>(this.rect);

            Assert.Equal(23, processor.Value);
        }
    }
}