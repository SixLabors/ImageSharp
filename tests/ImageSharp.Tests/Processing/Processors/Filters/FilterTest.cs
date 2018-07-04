// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Filters
{
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
            where TPixel : struct, IPixel<TPixel>
        {
            Matrix4x4 m = CreateCombinedTestFilterMatrix();

            provider.RunValidatingProcessorTest(x => x.Filter(m), comparer: ValidatorComparer);
        }

        [Theory]
        [WithTestPatternImages(48, 48, PixelTypes.Rgba32)]
        public void ApplyFilterInBox<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            Matrix4x4 m = CreateCombinedTestFilterMatrix();

            provider.RunRectangleConstrainedValidatingProcessorTest((x, b) => x.Filter(m, b), comparer: ValidatorComparer);
        }

        private static Matrix4x4 CreateCombinedTestFilterMatrix()
        {
            Matrix4x4 brightness = KnownFilterMatrices.CreateBrightnessFilter(0.9F);
            Matrix4x4 hue = KnownFilterMatrices.CreateHueFilter(180F);
            Matrix4x4 saturation = KnownFilterMatrices.CreateSaturateFilter(1.5F);
            Matrix4x4 m = brightness * hue * saturation;
            return m;
        }

    }
}