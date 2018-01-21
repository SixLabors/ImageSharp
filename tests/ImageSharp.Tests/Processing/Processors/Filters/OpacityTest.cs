// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Effects
{
    [GroupOutput("Filters")]
    public class OpacityTest
    {
        public static readonly TheoryData<float> AlphaValues
        = new TheoryData<float>
        {
            20/100F,
            80/100F
        };

        [Theory]
        [WithTestPatternImages(nameof(AlphaValues), 48, 48, PixelTypes.Rgba32)]
        public void ApplyAlphaFilter<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTest(x => x.Opacity(value), value);
        }

        [Theory]
        [WithTestPatternImages(nameof(AlphaValues), 48, 48, PixelTypes.Rgba32)]
        public void ApplyAlphaFilterInBox<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (Image<TPixel> image = source.Clone())
            {
                var bounds = new Rectangle(image.Width / 4, image.Height / 4, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.Opacity(value, bounds));
                image.DebugSave(provider, value);

                ImageComparer.Tolerant().VerifySimilarityIgnoreRegion(source, image, bounds);
            }
        }
    }
}