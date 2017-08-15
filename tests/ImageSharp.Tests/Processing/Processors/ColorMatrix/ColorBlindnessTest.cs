// <copyright file="ColorBlindnessTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Processors.ColorMatrix
{
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;
    using SixLabors.Primitives;
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
        [WithFileCollection(nameof(DefaultFiles), nameof(ColorBlindnessFilters), DefaultPixelType)]
        public void ImageShouldApplyColorBlindnessFilter<TPixel>(TestImageProvider<TPixel> provider, ColorBlindness colorBlindness)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.ColorBlindness(colorBlindness));
                image.DebugSave(provider, colorBlindness.ToString());
            }
        }

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(ColorBlindnessFilters), DefaultPixelType)]
        public void ImageShouldApplyColorBlindnessFilterInBox<TPixel>(TestImageProvider<TPixel> provider, ColorBlindness colorBlindness)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (var image = source.Clone())
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.ColorBlindness(colorBlindness, bounds));
                image.DebugSave(provider, colorBlindness.ToString());

                PercentageImageComparer.EnsureProcessorChangesAreConstrained(source, image, bounds);
            }
        }
    }
}