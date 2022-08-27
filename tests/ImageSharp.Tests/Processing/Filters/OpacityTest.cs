// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Filters
{
    [Trait("Category", "Processors")]
    public class OpacityTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Alpha_amount_AlphaProcessorDefaultsSet()
        {
            this.operations.Opacity(0.2f);
            OpacityProcessor processor = this.Verify<OpacityProcessor>();

            Assert.Equal(.2f, processor.Amount);
        }

        [Fact]
        public void Alpha_amount_rect_AlphaProcessorDefaultsSet()
        {
            this.operations.Opacity(0.6f, this.rect);
            OpacityProcessor processor = this.Verify<OpacityProcessor>(this.rect);

            Assert.Equal(.6f, processor.Amount);
        }
    }
}
