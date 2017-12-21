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
    public class FilterTest : FileTestBase
    {
        [Theory]
        [WithTestPatternImages(100, 100, DefaultPixelType)]
        public void ImageShouldApplyFilter<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                Matrix4x4 brightness = MatrixFilters.CreateBrightnessFilter(0.9F);
                Matrix4x4 hue = MatrixFilters.CreateHueFilter(180F);
                Matrix4x4 saturation = MatrixFilters.CreateSaturateFilter(1.5F);
                image.Mutate(x => x.Filter(brightness * hue * saturation));
                image.DebugSave(provider);
            }
        }

        [Theory]
        [WithTestPatternImages(100, 100, DefaultPixelType)]
        public void ImageShouldApplyFilterInBox<TPixel>(TestImageProvider<TPixel> provider)
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