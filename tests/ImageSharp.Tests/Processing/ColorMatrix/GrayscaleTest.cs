// <copyright file="GrayscaleTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.ColorMatrix
{
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;
    using SixLabors.Primitives;
    using Xunit;

    public class GrayscaleTest : FileTestBase
    {
        public static readonly TheoryData<GrayscaleMode> GrayscaleModeTypes
            = new TheoryData<GrayscaleMode>
            {
                GrayscaleMode.Bt601,
                GrayscaleMode.Bt709
            };

        /// <summary>
        /// Use test patterns over loaded images to save decode time.
        /// </summary>
        [Theory]
        [WithTestPatternImages(nameof(GrayscaleModeTypes), 50, 50, DefaultPixelType)]
        public void ImageShouldApplyGrayscaleFilterAll<TPixel>(TestImageProvider<TPixel> provider, GrayscaleMode value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Grayscale(value));
                byte[] data = new byte[3];
                for (int i = 0; i < image.Pixels.Length; i++)
                {
                    image.Pixels[i].ToXyzBytes(data, 0);
                    Assert.Equal(data[0], data[1]);
                    Assert.Equal(data[1], data[2]);
                }

                image.DebugSave(provider, value.ToString());
            }
        }

        [Theory]
        [WithTestPatternImages(nameof(GrayscaleModeTypes), 50, 50, DefaultPixelType)]
        public void ImageShouldApplyGrayscaleFilterInBox<TPixel>(TestImageProvider<TPixel> provider, GrayscaleMode value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (var image = new Image<TPixel>(source))
            {
                var bounds = new Rectangle(image.Width / 4, image.Height / 4, image.Width / 2, image.Height / 2);
                image.Mutate(x => x.Grayscale(value, bounds));
                image.DebugSave(provider, value.ToString());

                ImageComparer.EnsureProcessorChangesAreConstrained(source, image, bounds);
            }
        }
    }
}