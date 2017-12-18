// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Filters
{
    public class SaturateTest : FileTestBase
    {
        public static readonly TheoryData<float> SaturationValues
        = new TheoryData<float>
        {
            .5f,
           1.5F,
        };

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(SaturationValues), DefaultPixelType)]
        public void ImageShouldApplySaturationFilter<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Saturate(value));
                image.DebugSave(provider, value);
            }
        }

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(SaturationValues), DefaultPixelType)]
        public void ImageShouldApplySaturationFilterInBox<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (Image<TPixel> image = source.Clone())
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.Saturate(value, bounds));
                image.DebugSave(provider, value);

                ImageComparer.Tolerant().VerifySimilarityIgnoreRegion(source, image, bounds);
            }
        }
    }
}