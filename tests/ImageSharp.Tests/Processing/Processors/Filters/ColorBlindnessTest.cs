// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Filters
{
    using SixLabors.ImageSharp.Processing;
    using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

    [GroupOutput("Filters")]
    public class ColorBlindnessTest
    {
        private readonly ImageComparer imageComparer = ImageComparer.Tolerant(0.03F);

        public static readonly TheoryData<ColorBlindnessMode> ColorBlindnessFilters
        = new TheoryData<ColorBlindnessMode>
        {
            ColorBlindnessMode.Achromatomaly,
            ColorBlindnessMode.Achromatopsia,
            ColorBlindnessMode.Deuteranomaly,
            ColorBlindnessMode.Deuteranopia,
            ColorBlindnessMode.Protanomaly,
            ColorBlindnessMode.Protanopia,
            ColorBlindnessMode.Tritanomaly,
            ColorBlindnessMode.Tritanopia
        };

        [Theory]
        [WithTestPatternImages(nameof(ColorBlindnessFilters), 48, 48, PixelTypes.Rgba32)]
        public void ApplyColorBlindnessFilter<TPixel>(TestImageProvider<TPixel> provider, ColorBlindnessMode colorBlindness)
            where TPixel : unmanaged, IPixel<TPixel> => provider.RunValidatingProcessorTest(x => x.ColorBlindness(colorBlindness), colorBlindness.ToString(), this.imageComparer);
    }
}