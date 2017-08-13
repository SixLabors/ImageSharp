// <copyright file="HueTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Processors.ColorMatrix
{
    using ImageSharp.PixelFormats;
    using SixLabors.Primitives;
    using Xunit;

    public class HueTest : FileTestBase
    {
        public static readonly TheoryData<int> HueValues
        = new TheoryData<int>
        {
            180,
           -180
        };

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(HueValues), DefaultPixelType)]
        public void ImageShouldApplyHueFilter<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Hue(value));
                image.DebugSave(provider, value, Extensions.Bmp);
            }
        }

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(HueValues), DefaultPixelType)]
        public void ImageShouldApplyHueFilterInBox<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (var image = source.Clone())
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.Hue(value, bounds));
                image.DebugSave(provider, value, Extensions.Bmp);

                ImageComparer.EnsureProcessorChangesAreConstrained(source, image, bounds);
            }
        }
    }
}