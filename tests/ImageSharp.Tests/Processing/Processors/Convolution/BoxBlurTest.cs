// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Convolution
{
    public class BoxBlurTest : FileTestBase
    {
        public static readonly TheoryData<int> BoxBlurValues
        = new TheoryData<int>
        {
            3,
            5
        };

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(BoxBlurValues), DefaultPixelType)]
        public void ImageShouldApplyBoxBlurFilter<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.BoxBlur(value));
                image.DebugSave(provider, value);
            }
        }

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(BoxBlurValues), DefaultPixelType)]
        public void ImageShouldApplyBoxBlurFilterInBox<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (var image = source.Clone())
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.BoxBlur(value, bounds));
                image.DebugSave(provider, value);

                ImageComparer.Tolerant().VerifySimilarityIgnoreRegion(source, image, bounds);
            }
        }
    }
}