// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Convolution;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Convolution
{
    public class BoxBlurTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void BoxBlur_BoxBlurProcessorDefaultsSet()
        {
            this.operations.BoxBlur();
            var processor = this.Verify<BoxBlurProcessor>();

            Assert.Equal(7, processor.Radius);
        }

        [Fact]
        public void BoxBlur_amount_BoxBlurProcessorDefaultsSet()
        {
            this.operations.BoxBlur(34);
            var processor = this.Verify<BoxBlurProcessor>();

            Assert.Equal(34, processor.Radius);
        }

        [Fact]
        public void BoxBlur_amount_rect_BoxBlurProcessorDefaultsSet()
        {
            this.operations.BoxBlur(5, this.rect);
            var processor = this.Verify<BoxBlurProcessor>(this.rect);

            Assert.Equal(5, processor.Radius);
        }
    }
}