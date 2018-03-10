// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Effects
{
    using SixLabors.ImageSharp.Processing.Filters;
    using SixLabors.ImageSharp.Processing.Filters.Processors;

    public class OpacityTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Alpha_amount_AlphaProcessorDefaultsSet()
        {
            this.operations.Opacity(0.2f);
            OpacityProcessor<Rgba32> processor = this.Verify<OpacityProcessor<Rgba32>>();

            Assert.Equal(.2f, processor.Amount);
        }

        [Fact]
        public void Alpha_amount_rect_AlphaProcessorDefaultsSet()
        {
            this.operations.Opacity(0.6f, this.rect);
            OpacityProcessor<Rgba32> processor = this.Verify<OpacityProcessor<Rgba32>>(this.rect);

            Assert.Equal(.6f, processor.Amount);
        }
    }
}