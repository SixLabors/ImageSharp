// <copyright file="GaussianBlurTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;
    using ImageSharp.PixelFormats;
    using Xunit;

    public class GaussianBlurTest
    {
        public static readonly TheoryData<int> GaussianBlurValues
        = new TheoryData<int>
        {
            3 ,
            5 ,
        };

        [Theory]
        [WithTestPatternImages(nameof(GaussianBlurValues), 320, 240, PixelTypes.StandardImageClass)]
        public void ImageShouldApplyGaussianBlurFilter<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.GaussianBlur(value)
                    .DebugSave(provider, value.ToString());
            }
        }

        [Theory]
        [WithTestPatternImages(nameof(GaussianBlurValues), 320, 240, PixelTypes.StandardImageClass)]
        public void ImageShouldApplyGaussianBlurFilterInBox<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (Image<TPixel> image = new Image<TPixel>(source))
            {
                Rectangle rect = new Rectangle(image.Width / 4, image.Height / 4, image.Width / 2, image.Height / 2);
                image.GaussianBlur(value, rect)
                    .DebugSave(provider, value.ToString());

                // lets draw identical shapes over the blured areas and ensure that it didn't change the outer area
                image.Fill(NamedColors<TPixel>.HotPink, rect);
                source.Fill(NamedColors<TPixel>.HotPink, rect);
                ImageComparer.CheckSimilarity(image, source);
            }
        }
    }
}