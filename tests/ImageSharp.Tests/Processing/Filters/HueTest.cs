// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Filters;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Filters
{
    public class HueTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Hue_amount_HueProcessorDefaultsSet()
        {
            this.operations.Hue(34f);
            var processor = this.Verify<HueProcessor>();

            Assert.Equal(34f, processor.Degrees);
        }

        [Fact]
        public void Hue_amount_rect_HueProcessorDefaultsSet()
        {
            this.operations.Hue(5f, this.rect);
            var processor = this.Verify<HueProcessor>(this.rect);

            Assert.Equal(5f, processor.Degrees);
        }
    }
}