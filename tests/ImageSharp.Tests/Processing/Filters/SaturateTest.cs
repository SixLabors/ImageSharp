// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Filters
{
    public class SaturateTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Saturation_amount_SaturationProcessorDefaultsSet()
        {
            this.operations.Saturate(34);
            SaturateProcessor processor = this.Verify<SaturateProcessor>();

            Assert.Equal(34, processor.Amount);
        }

        [Fact]
        public void Saturation_amount_rect_SaturationProcessorDefaultsSet()
        {
            this.operations.Saturate(5, this.rect);
            SaturateProcessor processor = this.Verify<SaturateProcessor>(this.rect);

            Assert.Equal(5, processor.Amount);
        }
    }
}