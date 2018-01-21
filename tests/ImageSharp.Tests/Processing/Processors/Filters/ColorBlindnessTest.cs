// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Filters
{
    [GroupOutput("Filters")]
    public class ColorBlindnessTest
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
        [WithTestPatternImages(nameof(ColorBlindnessFilters), 48, 48, PixelTypes.Rgba32)]
        public void ApplyColorBlindnessFilter<TPixel>(TestImageProvider<TPixel> provider, ColorBlindness colorBlindness)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTest(x => x.ColorBlindness(colorBlindness), colorBlindness.ToString());
        }

        [Theory]
        [WithTestPatternImages(nameof(ColorBlindnessFilters), 48, 48, PixelTypes.Rgba32)]
        public void ApplyColorBlindnessFilterInBox<TPixel>(TestImageProvider<TPixel> provider, ColorBlindness colorBlindness)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (Image<TPixel> image = source.Clone())
            {
                var bounds = new Rectangle(image.Width / 4, image.Width / 4, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.ColorBlindness(colorBlindness, bounds));
                image.DebugSave(provider, colorBlindness.ToString());

                ImageComparer.Tolerant().VerifySimilarityIgnoreRegion(source, image, bounds);
            }
        }
    }
}