// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.ColorMatrix
{
    public class SaturationTest : BaseImageOperationsExtensionTest
    {

        [Fact]
        public void Saturation_amount_SaturationProcessorDefaultsSet()
        {
            this.operations.Saturation(34);
            var processor = this.Verify<SaturationProcessor<Rgba32>>();

            Assert.Equal(34, processor.Amount);
        }

        [Fact]
        public void Saturation_amount_rect_SaturationProcessorDefaultsSet()
        {
            this.operations.Saturation(5, this.rect);
            var processor = this.Verify<SaturationProcessor<Rgba32>>(this.rect);

            Assert.Equal(5, processor.Amount);
        }
    }
}