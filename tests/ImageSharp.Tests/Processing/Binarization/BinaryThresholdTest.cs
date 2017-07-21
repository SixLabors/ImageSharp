// <copyright file="BinaryThresholdTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Binarization
{
    using ImageSharp.PixelFormats;
    using SixLabors.Primitives;
    using Xunit;

    public class BinaryThresholdTest : FileTestBase
    {
        public static readonly TheoryData<float> BinaryThresholdValues
        = new TheoryData<float>
        {
            .25F,
            .75F
        };

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(BinaryThresholdValues), DefaultPixelType)]
        public void ImageShouldApplyBinaryThresholdFilter<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.BinaryThreshold(value)
                     .DebugSave(provider, value);
            }
        }

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(BinaryThresholdValues), DefaultPixelType)]
        public void ImageShouldApplyBinaryThresholdInBox<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : struct, IPixel<TPixel>
        {

            using (Image<TPixel> source = provider.GetImage())
            using (var image = new Image<TPixel>(source))
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.BinaryThreshold(value, bounds)
                     .DebugSave(provider, value);

                ImageComparer.EnsureProcessorChangesAreConstrained(source, image, bounds);
            }
        }
    }
}