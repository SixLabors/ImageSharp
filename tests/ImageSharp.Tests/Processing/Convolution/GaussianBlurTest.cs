// <copyright file="GaussianBlurTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Convolution
{
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing.Processors;
    using SixLabors.Primitives;
    using Xunit;

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