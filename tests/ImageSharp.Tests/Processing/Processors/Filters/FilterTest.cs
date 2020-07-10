// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Filters
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.Processing;

    [GroupOutput("Filters")]
    public class FilterTest
    {
        private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.0218f, 3);

        // Testing the generic FilterProcessor with more than one pixel type intentionally.
        // There is no need to do this with the specialized ones.
        [Theory]
        [WithTestPatternImages(48, 48, PixelTypes.Rgba32 | PixelTypes.Bgra32)]
        public void ApplyFilter<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ColorMatrix m = CreateCombinedTestFilterMatrix();

            provider.RunValidatingProcessorTest(x => x.Filter(m), comparer: ValidatorComparer);
        }

        [Theory]
        [WithTestPatternImages(48, 48, PixelTypes.Rgba32)]
        public void ApplyFilterInBox<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ColorMatrix m = CreateCombinedTestFilterMatrix();

            provider.RunRectangleConstrainedValidatingProcessorTest((x, b) => x.Filter(m, b), comparer: ValidatorComparer);
        }

        [Theory]
        [WithTestPatternImages(70, 120, PixelTypes.Rgba32)]
        public void FilterProcessor_WorksWithDiscoBuffers<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ColorMatrix m = CreateCombinedTestFilterMatrix();

            provider.RunBufferCapacityLimitProcessorTest(37, c => c.Filter(m));
        }

        private static ColorMatrix CreateCombinedTestFilterMatrix()
        {
            ColorMatrix brightness = KnownFilterMatrices.CreateBrightnessFilter(0.9F);
            ColorMatrix hue = KnownFilterMatrices.CreateHueFilter(180F);
            ColorMatrix saturation = KnownFilterMatrices.CreateSaturateFilter(1.5F);
            return brightness * hue * saturation;
        }
    }
}
