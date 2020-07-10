// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms
{
    public class PadTest
    {
        public static readonly string[] CommonTestImages =
        {
            TestImages.Png.CalliphoraPartial, TestImages.Png.Bike
        };

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32)]
        public void ImageShouldPad<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Pad(image.Width + 50, image.Height + 50));
                image.DebugSave(provider);

                // Check pixels are empty
                for (int y = 0; y < 25; y++)
                {
                    for (int x = 0; x < 25; x++)
                    {
                        Assert.Equal(default, image[x, y]);
                    }
                }
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32)]
        public void ImageShouldPadWithBackgroundColor<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var color = Color.Red;
            TPixel expected = color.ToPixel<TPixel>();
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Pad(image.Width + 50, image.Height + 50, color));
                image.DebugSave(provider);

                // Check pixels are filled
                for (int y = 0; y < 25; y++)
                {
                    for (int x = 0; x < 25; x++)
                    {
                        Assert.Equal(expected, image[x, y]);
                    }
                }
            }
        }
    }
}
