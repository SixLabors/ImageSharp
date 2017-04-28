// <copyright file="PixelateTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;
    using ImageSharp.PixelFormats;
    using Xunit;

    public class PixelateTest 
    {
        public static readonly TheoryData<int> PixelateValues
        = new TheoryData<int>
        {
            4 ,
            8
        };

        [Theory]
        [WithTestPatternImages(nameof(PixelateValues), 320, 240, PixelTypes.StandardImageClass)]
        public void ImageShouldApplyPixelateFilter<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Pixelate(value)
                    .DebugSave(provider, new
                    {
                        size = value
                    });

                using (PixelAccessor<TPixel> pixels = image.Lock())
                {
                    for (int y = 0; y < pixels.Height; y += value)
                    {
                        for (int x = 0; x < pixels.Width; x += value)
                        {
                            TPixel source = pixels[x, y];
                            for (int pixY = y; pixY < y + value && pixY < pixels.Height; pixY++)
                            {
                                for (int pixX = x; pixX < x + value && pixX < pixels.Width; pixX++)
                                {
                                    Assert.Equal(source, pixels[pixX, pixY]);
                                }
                            }
                        }
                    }
                }
            }
        }

        [Theory]
        [WithTestPatternImages(nameof(PixelateValues), 320, 240, PixelTypes.StandardImageClass)]
        public void ImageShouldApplyPixelateFilterInBox<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (Image<TPixel> image = new Image<TPixel>(source))
            {
                Rectangle rect = new Rectangle(image.Width/4, image.Height / 4, image.Width / 2, image.Height / 2);

                image.Pixelate(value, rect)
                    .DebugSave(provider, new
                    {
                        size = value
                    });

                using (PixelAccessor<TPixel> pixels = image.Lock())
                using (PixelAccessor<TPixel> sourcePixels = source.Lock())
                {
                    for (int y = 0; y < pixels.Height; y++)
                    {
                        for (int x = 0; x < pixels.Width; x++)
                        {
                            var tx = x;
                            var ty = y;
                            TPixel sourceColor = sourcePixels[tx, ty];
                            if (rect.Contains(tx, ty))
                            {
                                var sourceX = tx - ((tx - rect.Left) % value) + (value / 2);
                                var sourceY = ty - ((ty - rect.Top) % value) + (value / 2);

                                sourceColor = pixels[sourceX, sourceY];
                            }
                            Assert.Equal(sourceColor, pixels[tx, ty]);
                        }
                    }
                }
            }
        }
    }
}