// <copyright file="GaussianBlurTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Convolution
{
    using ImageSharp.PixelFormats;
    using SixLabors.Primitives;
    using Xunit;

    public class GaussianBlurTest : FileTestBase
    {
        public static readonly TheoryData<int> GaussianBlurValues
        = new TheoryData<int>
        {
            3,
            5
        };

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(GaussianBlurValues), DefaultPixelType)]
        public void ImageShouldApplyGaussianBlurFilter<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.GaussianBlur(value));
                image.DebugSave(provider, value, Extensions.Bmp);
            }
        }

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(GaussianBlurValues), DefaultPixelType)]
        public void ImageShouldApplyGaussianBlurFilterInBox<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (var image = new Image<TPixel>(source))
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.GaussianBlur(value, bounds));
                image.DebugSave(provider, value, Extensions.Bmp);

                ImageComparer.EnsureProcessorChangesAreConstrained(source, image, bounds);
            }
        }
    }
}