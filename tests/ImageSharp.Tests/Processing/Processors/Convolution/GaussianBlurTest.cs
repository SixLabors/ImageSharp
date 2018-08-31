// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.Primitives;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Convolution
{
    public class GaussianBlurTest : FileTestBase
    {
        public static readonly TheoryData<int> GaussianBlurValues = new TheoryData<int> { 3, 5 };

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(GaussianBlurValues), DefaultPixelType)]
        public void ImageShouldApplyGaussianBlurFilter<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.GaussianBlur(value));
                image.DebugSave(provider, value);
            }
        }

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(GaussianBlurValues), DefaultPixelType)]
        public void ImageShouldApplyGaussianBlurFilterInBox<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (Image<TPixel> image = source.Clone())
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.GaussianBlur(value, bounds));
                image.DebugSave(provider, value);

                ImageComparer.Tolerant().VerifySimilarityIgnoreRegion(source, image, bounds);
            }
        }
    }
}