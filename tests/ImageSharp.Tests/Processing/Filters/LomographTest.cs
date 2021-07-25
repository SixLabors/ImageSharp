// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Filters
{
    [Trait("Category", "Processors")]
    public class LomographTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Lomograph_amount_LomographProcessorDefaultsSet()
        {
            this.operations.Lomograph();
            var processor = this.Verify<LomographProcessor>();
            Assert.Equal(processor.GraphicsOptions, this.options);
        }

        [Fact]
        public void Lomograph_amount_rect_LomographProcessorDefaultsSet()
        {
            this.operations.Lomograph(this.rect);
            var processor = this.Verify<LomographProcessor>(this.rect);
            Assert.Equal(processor.GraphicsOptions, this.options);
        }
    }
}
