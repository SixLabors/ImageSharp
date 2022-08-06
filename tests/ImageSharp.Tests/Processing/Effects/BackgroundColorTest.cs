// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Overlays;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Effects
{
    [Trait("Category", "Processors")]
    public class BackgroundColorTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void BackgroundColor_amount_BackgroundColorProcessorDefaultsSet()
        {
            this.operations.BackgroundColor(Color.BlanchedAlmond);
            BackgroundColorProcessor processor = this.Verify<BackgroundColorProcessor>();

            Assert.Equal(this.options, processor.GraphicsOptions);
            Assert.Equal(Color.BlanchedAlmond, processor.Color);
        }

        [Fact]
        public void BackgroundColor_amount_rect_BackgroundColorProcessorDefaultsSet()
        {
            this.operations.BackgroundColor(Color.BlanchedAlmond, this.rect);
            BackgroundColorProcessor processor = this.Verify<BackgroundColorProcessor>(this.rect);

            Assert.Equal(this.options, processor.GraphicsOptions);
            Assert.Equal(Color.BlanchedAlmond, processor.Color);
        }

        [Fact]
        public void BackgroundColor_amount_options_BackgroundColorProcessorDefaultsSet()
        {
            this.operations.BackgroundColor(this.options, Color.BlanchedAlmond);
            BackgroundColorProcessor processor = this.Verify<BackgroundColorProcessor>();

            Assert.Equal(this.options, processor.GraphicsOptions);
            Assert.Equal(Color.BlanchedAlmond, processor.Color);
        }

        [Fact]
        public void BackgroundColor_amount_rect_options_BackgroundColorProcessorDefaultsSet()
        {
            this.operations.BackgroundColor(this.options, Color.BlanchedAlmond, this.rect);
            BackgroundColorProcessor processor = this.Verify<BackgroundColorProcessor>(this.rect);

            Assert.Equal(this.options, processor.GraphicsOptions);
            Assert.Equal(Color.BlanchedAlmond, processor.Color);
        }
    }
}
