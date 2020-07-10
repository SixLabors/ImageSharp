// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Filters;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Filters
{
    public class PolaroidTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Polaroid_amount_PolaroidProcessorDefaultsSet()
        {
            this.operations.Polaroid();
            var processor = this.Verify<PolaroidProcessor>();
            Assert.Equal(processor.GraphicsOptions, this.options);
        }

        [Fact]
        public void Polaroid_amount_rect_PolaroidProcessorDefaultsSet()
        {
            this.operations.Polaroid(this.rect);
            var processor = this.Verify<PolaroidProcessor>(this.rect);
            Assert.Equal(processor.GraphicsOptions, this.options);
        }
    }
}
