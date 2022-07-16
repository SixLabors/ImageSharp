// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Filters
{
    [Trait("Category", "Processors")]
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
