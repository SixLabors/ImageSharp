// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Overlays;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Effects
{
    public class BackgroundColorTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void BackgroundColor_amount_BackgroundColorProcessorDefaultsSet()
        {
            this.operations.BackgroundColor(Rgba32.BlanchedAlmond);
            var processor = this.Verify<BackgroundColorProcessor<Rgba32>>();

            Assert.Equal(GraphicsOptions.Default, processor.GraphicsOptions);
            Assert.Equal(Rgba32.BlanchedAlmond, processor.Color);
        }

        [Fact]
        public void BackgroundColor_amount_rect_BackgroundColorProcessorDefaultsSet()
        {
            this.operations.BackgroundColor(Rgba32.BlanchedAlmond, this.rect);
            var processor = this.Verify<BackgroundColorProcessor<Rgba32>>(this.rect);

            Assert.Equal(GraphicsOptions.Default, processor.GraphicsOptions);
            Assert.Equal(Rgba32.BlanchedAlmond, processor.Color);
        }

        [Fact]
        public void BackgroundColor_amount_options_BackgroundColorProcessorDefaultsSet()
        {
            this.operations.BackgroundColor(this.options, Rgba32.BlanchedAlmond);
            var processor = this.Verify<BackgroundColorProcessor<Rgba32>>();

            Assert.Equal(this.options, processor.GraphicsOptions);
            Assert.Equal(Rgba32.BlanchedAlmond, processor.Color);
        }

        [Fact]
        public void BackgroundColor_amount_rect_options_BackgroundColorProcessorDefaultsSet()
        {
            this.operations.BackgroundColor(this.options, Rgba32.BlanchedAlmond, this.rect);
            var processor = this.Verify<BackgroundColorProcessor<Rgba32>>(this.rect);

            Assert.Equal(this.options, processor.GraphicsOptions);
            Assert.Equal(Rgba32.BlanchedAlmond, processor.Color);
        }
    }
}