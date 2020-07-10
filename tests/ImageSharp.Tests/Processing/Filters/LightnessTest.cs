// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Effects
{
    public class LightnessTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Lightness_amount_LightnessProcessorDefaultsSet()
        {
            this.operations.Lightness(.5F);
            LightnessProcessor processor = this.Verify<LightnessProcessor>();

            Assert.Equal(.5F, processor.Amount);
        }

        [Fact]
        public void Lightness_amount_rect_LightnessProcessorDefaultsSet()
        {
            this.operations.Lightness(.5F, this.rect);
            LightnessProcessor processor = this.Verify<LightnessProcessor>(this.rect);

            Assert.Equal(.5F, processor.Amount);
        }
    }
}
