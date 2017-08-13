// <copyright file="AlphaTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Processors.Effects
{
    using ImageSharp.PixelFormats;
    using SixLabors.Primitives;
    using Xunit;

    public class AlphaTest : FileTestBase
    {
        public static readonly TheoryData<float> AlphaValues
        = new TheoryData<float>
        {
            20/100F,
            80/100F
        };

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(AlphaValues), DefaultPixelType)]
        public void ImageShouldApplyAlphaFilter<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Alpha(value));
                image.DebugSave(provider, value, Extensions.Png);
            }
        }

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(AlphaValues), DefaultPixelType)]
        public void ImageShouldApplyAlphaFilterInBox<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (var image = source.Clone())
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.Alpha(value, bounds));
                image.DebugSave(provider, value, Extensions.Png);

                ImageComparer.EnsureProcessorChangesAreConstrained(source, image, bounds);
            }
        }
    }
}