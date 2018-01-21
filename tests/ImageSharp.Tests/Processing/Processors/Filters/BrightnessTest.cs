// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Effects
{
    [GroupOutput("Filters")]
    public class BrightnessTest
    {
        public static readonly TheoryData<float> BrightnessValues
        = new TheoryData<float>
        {
            .5F,
           1.5F
        };

        [Theory]
        [WithTestPatternImages(nameof(BrightnessValues), 48, 48, PixelTypes.Rgba32)]
        public void ApplyBrightnessFilter<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTest(ctx => ctx.Brightness(value), value);
        }

        [Theory]
        [WithTestPatternImages(nameof(BrightnessValues), 48, 48, PixelTypes.Rgba32)]
        public void ApplyBrightnessFilterInBox<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (Image<TPixel> image = source.Clone())
            {
                var bounds = new Rectangle(image.Width / 4, image.Width / 4, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.Brightness(value, bounds));
                image.DebugSave(provider, value);

                ImageComparer.Tolerant().VerifySimilarityIgnoreRegion(source, image, bounds);
            }
        }
    }
}