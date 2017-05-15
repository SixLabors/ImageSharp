// <copyright file="GrayscaleTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using Xunit;
    using ImageSharp.Processing;

    using ImageSharp.PixelFormats;

    public class GrayscaleTest : FileTestBase
    {
        /// <summary>
        /// Use test patterns over loaded images to save decode time.
        /// </summary>
        [Theory]
        [WithTestPatternImages(50, 50, PixelTypes.StandardImageClass, GrayscaleMode.Bt709)]
        [WithTestPatternImages(50, 50, PixelTypes.StandardImageClass, GrayscaleMode.Bt601)]
        public void ImageShouldApplyGrayscaleFilterAll<TPixel>(TestImageProvider<TPixel> provider, GrayscaleMode value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Grayscale(value);
                byte[] data = new byte[3];
                foreach (TPixel p in image.Pixels)
                {
                    p.ToXyzBytes(data, 0);
                    Assert.Equal(data[0], data[1]);
                    Assert.Equal(data[1], data[2]);
                }

                image.DebugSave(provider, value.ToString());
            }
        }

        [Theory]
        [WithTestPatternImages(50, 50, PixelTypes.StandardImageClass, GrayscaleMode.Bt709)]
        [WithTestPatternImages(50, 50, PixelTypes.StandardImageClass, GrayscaleMode.Bt601)]
        public void ImageShouldApplyGrayscaleFilterInBox<TPixel>(TestImageProvider<TPixel> provider, GrayscaleMode value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (Image<TPixel> image = new Image<TPixel>(source))
            {
                Rectangle rect = new Rectangle(image.Width / 4, image.Height / 4, image.Width / 2, image.Height / 2);
                image.Grayscale(rect, value)
                     .DebugSave(provider, value.ToString());

                // Let's draw identical shapes over the greyed areas and ensure that it didn't change the outer area
                image.Fill(NamedColors<TPixel>.HotPink, rect);
                source.Fill(NamedColors<TPixel>.HotPink, rect);
                ImageComparer.CheckSimilarity(image, source);
            }
        }
    }
}