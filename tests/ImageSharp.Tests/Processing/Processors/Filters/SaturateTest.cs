// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Filters
{
    [GroupOutput("Filters")]
    public class SaturateTest
    {
        public static readonly TheoryData<float> SaturationValues
        = new TheoryData<float>
        {
            .5F,
           1.5F,
        };

        [Theory]
        [WithTestPatternImages(nameof(SaturationValues), 48, 48, PixelTypes.Rgba32)]
        public void ApplySaturationFilter<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTest(x => x.Saturate(value), value);
        }

        [Theory]
        [WithTestPatternImages(nameof(SaturationValues), 48, 48, PixelTypes.Rgba32)]
        public void ApplySaturationFilterInBox<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (Image<TPixel> image = source.Clone())
            {
                var bounds = new Rectangle(image.Width / 4, image.Height / 4, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.Saturate(value, bounds));
                image.DebugSave(provider, value);

                ImageComparer.Tolerant().VerifySimilarityIgnoreRegion(source, image, bounds);
            }
        }
    }
}