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
    [GroupOutput("Filters")]
    public class FilterTest
    {
        // Testing the generic FilterProcessor with more than one pixel type intentionally.
        // There is no need to do this with the specialized ones.
        [Theory]
        [WithTestPatternImages(48, 48, PixelTypes.Rgba32 | PixelTypes.Bgra32)]
        public void ApplyFilter<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            Matrix4x4 brightness = MatrixFilters.CreateBrightnessFilter(0.9F);
            Matrix4x4 hue = MatrixFilters.CreateHueFilter(180F);
            Matrix4x4 saturation = MatrixFilters.CreateSaturateFilter(1.5F);
            Matrix4x4 m = brightness * hue * saturation;

            provider.RunValidatingProcessorTest(x => x.Filter(m));
        }

        [Theory]
        [WithTestPatternImages(48, 48, PixelTypes.Rgba32)]
        public void ApplyFilterInBox<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (Image<TPixel> image = source.Clone())
            {
                var bounds = new Rectangle(image.Width / 4, image.Width / 4, image.Width / 2, image.Height / 2);

                Matrix4x4 brightness = MatrixFilters.CreateBrightnessFilter(0.9F);
                Matrix4x4 hue = MatrixFilters.CreateHueFilter(180F);
                Matrix4x4 saturation = MatrixFilters.CreateSaturateFilter(1.5F);
                image.Mutate(x => x.Filter(brightness * hue * saturation, bounds));
                image.DebugSave(provider);

                ImageComparer.Tolerant().VerifySimilarityIgnoreRegion(source, image, bounds);
            }
        }
    }
}