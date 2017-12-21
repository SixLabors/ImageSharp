// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Filters
{
    public class HueTest : FileTestBase
    {
        public static readonly TheoryData<int> HueValues
        = new TheoryData<int>
        {
            180,
           -180
        };

        [Theory]
        [WithTestPatternImages(nameof(HueValues), 100, 100, DefaultPixelType)]
        public void ImageShouldApplyHueFilter<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Hue(value));
                image.DebugSave(provider, value);
            }
        }

        [Theory]
        [WithTestPatternImages(nameof(HueValues), 100, 100, DefaultPixelType)]
        public void ImageShouldApplyHueFilterInBox<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (Image<TPixel> image = source.Clone())
            {
                var bounds = new Rectangle(image.Width / 4, image.Height / 4, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.Hue(value, bounds));
                image.DebugSave(provider, value);

                ImageComparer.Tolerant().VerifySimilarityIgnoreRegion(source, image, bounds);
            }
        }
    }
}