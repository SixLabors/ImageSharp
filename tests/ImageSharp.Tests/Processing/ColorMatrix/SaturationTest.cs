// <copyright file="SaturationTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.ColorMatrix
{
    using ImageSharp.PixelFormats;
    using SixLabors.Primitives;
    using Xunit;

    public class SaturationTest : FileTestBase
    {
        public static readonly TheoryData<int> SaturationValues
        = new TheoryData<int>
        {
            50 ,
           -50 ,
        };

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(SaturationValues), DefaultPixelType)]
        public void ImageShouldApplySaturationFilter<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Saturation(value)
                    .DebugSave(provider, value);
            }
        }

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(SaturationValues), DefaultPixelType)]
        public void ImageShouldApplySaturationFilterInBox<TPixel>(TestImageProvider<TPixel> provider, int value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (var image = new Image<TPixel>(source))
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Saturation(value, bounds)
                    .DebugSave(provider, value);

                ImageComparer.EnsureProcessorChangesAreConstrained(source, image, bounds);
            }
        }
    }
}