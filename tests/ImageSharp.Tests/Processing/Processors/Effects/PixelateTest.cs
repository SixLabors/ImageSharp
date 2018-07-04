// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Effects
{
    public class PixelateTest : FileTestBase
    {
        public static readonly TheoryData<int> PixelateValues
        = new TheoryData<int>
        {
            4 ,
            8
        };

        [Theory]
        [WithTestPatternImages(nameof(PixelateValues), 320, 240, PixelTypes.Rgba32)]
        public void ImageShouldApplyPixelateFilter<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Pixelate(value));
                image.DebugSave(provider, value);

                // Test the neigbouring pixels
                for (int y = 0; y < image.Height; y += value)
                {
                    for (int x = 0; x < image.Width; x += value)
                    {
                        TPixel source = image[x, y];
                        for (int pixY = y; pixY < y + value && pixY < image.Height; pixY++)
                        {
                            for (int pixX = x; pixX < x + value && pixX < image.Width; pixX++)
                            {
                                Assert.Equal(source, image[pixX, pixY]);
                            }
                        }
                    }
                }
            }
        }

        [Theory]
        [WithTestPatternImages(nameof(PixelateValues), 320, 240, PixelTypes.Rgba32)]
        public void ImageShouldApplyPixelateFilterInBox<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (var image = source.Clone())
            {
                var bounds = new Rectangle(image.Width / 4, image.Height / 4, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.Pixelate(value, bounds));
                image.DebugSave(provider, value);

                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        int tx = x;
                        int ty = y;
                        TPixel sourceColor = source[tx, ty];
                        if (bounds.Contains(tx, ty))
                        {
                            int sourceX = tx - ((tx - bounds.Left) % value) + (value / 2);
                            int sourceY = ty - ((ty - bounds.Top) % value) + (value / 2);

                            sourceColor = image[sourceX, sourceY];
                        }
                        Assert.Equal(sourceColor, image[tx, ty]);
                    }
                }

                ImageComparer.Tolerant().VerifySimilarityIgnoreRegion(source, image, bounds);
            }
        }
    }
}