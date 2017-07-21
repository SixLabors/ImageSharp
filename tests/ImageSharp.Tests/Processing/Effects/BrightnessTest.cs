// <copyright file="BrightnessTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Effects
{
    using ImageSharp.PixelFormats;
    using SixLabors.Primitives;
    using Xunit;

    public class BrightnessTest : FileTestBase
    {
        public static readonly TheoryData<int> BrightnessValues
        = new TheoryData<int>
        {
            50,
           -50
        };

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(BrightnessValues), DefaultPixelType)]
        public void ImageShouldApplyBrightnessFilter<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Brightness(value)
                    .DebugSave(provider, value);
            }
        }

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(BrightnessValues), DefaultPixelType)]
        public void ImageShouldApplyBrightnessFilterInBox<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (var image = new Image<TPixel>(source))
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Brightness(value, bounds)
                    .DebugSave(provider, value);

                ImageComparer.EnsureProcessorChangesAreConstrained(source, image, bounds); ;
            }
        }
    }
}