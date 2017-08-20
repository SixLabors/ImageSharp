// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Effects
{
    public class ContrastTest : FileTestBase
    {
        public static readonly TheoryData<int> ContrastValues
        = new TheoryData<int>
        {
            50,
           -50
        };

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(ContrastValues), DefaultPixelType)]
        public void ImageShouldApplyContrastFilter<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Contrast(value));
                image.DebugSave(provider, value);
            }
        }

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(ContrastValues), DefaultPixelType)]
        public void ImageShouldApplyContrastFilterInBox<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (var image = source.Clone())
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.Contrast(value, bounds));
                image.DebugSave(provider, value);

                ImageComparer.Tolerant().VerifySimilarityIgnoreRegion(source, image, bounds);
            }
        }
    }
}