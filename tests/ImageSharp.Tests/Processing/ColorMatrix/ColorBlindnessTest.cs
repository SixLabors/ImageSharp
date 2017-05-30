// <copyright file="ColorBlindnessTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.ColorMatrix
{
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;

    using Xunit;

    public class ColorBlindnessTest : FileTestBase
    {
        public static readonly TheoryData<ColorBlindness> ColorBlindnessFilters
        = new TheoryData<ColorBlindness>
        {
            ColorBlindness.Achromatomaly,
            ColorBlindness.Achromatopsia,
            ColorBlindness.Deuteranomaly,
            ColorBlindness.Deuteranopia,
            ColorBlindness.Protanomaly,
            ColorBlindness.Protanopia,
            ColorBlindness.Tritanomaly,
            ColorBlindness.Tritanopia
        };

        [Theory]
        [WithFileCollection(nameof(AllBmpFiles), nameof(ColorBlindnessFilters), StandardPixelType)]
        public void ImageShouldApplyColorBlindnessFilter<TPixel>(TestImageProvider<TPixel> provider, ColorBlindness colorBlindness)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.ColorBlindness(colorBlindness)
                    .DebugSave(provider, colorBlindness.ToString(), Extensions.Bmp);
            }
        }

        [Theory]
        [WithFileCollection(nameof(AllBmpFiles), nameof(ColorBlindnessFilters), StandardPixelType)]
        public void ImageShouldApplyColorBlindnessFilterInBox<TPixel>(TestImageProvider<TPixel> provider, ColorBlindness colorBlindness)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (var image = new Image<TPixel>(source))
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.ColorBlindness(colorBlindness, bounds)
                    .DebugSave(provider, colorBlindness.ToString(), Extensions.Bmp);

                // Draw identical shapes over the bounded and compare to ensure changes are constrained.
                image.Fill(NamedColors<TPixel>.HotPink, bounds);
                source.Fill(NamedColors<TPixel>.HotPink, bounds);
                ImageComparer.CheckSimilarity(image, source);
            }
        }
    }
}