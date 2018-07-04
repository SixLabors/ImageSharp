// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Convolution;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Convolution
{
    public class GaussianBlurTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void GaussianBlur_GaussianBlurProcessorDefaultsSet()
        {
            this.operations.GaussianBlur();
            var processor = this.Verify<GaussianBlurProcessor<Rgba32>>();

            Assert.Equal(3f, processor.Sigma);
        }

        [Fact]
        public void GaussianBlur_amount_GaussianBlurProcessorDefaultsSet()
        {
            this.operations.GaussianBlur(0.2f);
            var processor = this.Verify<GaussianBlurProcessor<Rgba32>>();

            Assert.Equal(.2f, processor.Sigma);
        }

        [Fact]
        public void GaussianBlur_amount_rect_GaussianBlurProcessorDefaultsSet()
        {
            this.operations.GaussianBlur(0.6f, this.rect);
            var processor = this.Verify<GaussianBlurProcessor<Rgba32>>(this.rect);

            Assert.Equal(.6f, processor.Sigma);
        }
    }
}