// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Binarization
{
    using SixLabors.ImageSharp.Processing;

    public class BinaryThresholdTest
    {
        public static readonly TheoryData<float> BinaryThresholdValues
            = new TheoryData<float>
        {
            .25F,
            .75F
        };

        public static readonly string[] CommonTestImages =
            {
                TestImages.Png.CalliphoraPartial, TestImages.Png.Bike
            };

        public const PixelTypes TestPixelTypes = PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.Rgb24;

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), nameof(BinaryThresholdValues), PixelTypes.Rgba32)]
        public void ImageShouldApplyBinaryThresholdFilter<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.BinaryThreshold(value));
                image.DebugSave(provider, value);
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), nameof(BinaryThresholdValues), PixelTypes.Rgba32)]
        public void ImageShouldApplyBinaryThresholdInBox<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (var image = source.Clone())
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.BinaryThreshold(value, bounds));
                image.DebugSave(provider, value);

                ImageComparer.Tolerant().VerifySimilarityIgnoreRegion(source, image, bounds);
            }
        }
    }
}
