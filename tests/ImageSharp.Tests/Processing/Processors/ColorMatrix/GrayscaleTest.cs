﻿// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Filters
{
    public class GrayscaleTest : FileTestBase
    {
        public static readonly TheoryData<GrayscaleMode> GrayscaleModeTypes
            = new TheoryData<GrayscaleMode>
            {
                GrayscaleMode.Bt601,
                GrayscaleMode.Bt709
            };

        /// <summary>
        /// Use test patterns over loaded images to save decode time.
        /// </summary>
        [Theory]
        [WithTestPatternImages(nameof(GrayscaleModeTypes), 50, 50, DefaultPixelType)]
        public void ImageShouldApplyGrayscaleFilterAll<TPixel>(TestImageProvider<TPixel> provider, GrayscaleMode value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Grayscale(value));
                var rgb = default(Rgb24);
                System.Span<TPixel> span = image.Frames.RootFrame.GetPixelSpan();
                for (int i = 0; i < span.Length; i++)
                {
                    span[i].ToRgb24(ref rgb);
                    Assert.Equal(rgb.R, rgb.B);
                    Assert.Equal(rgb.B, rgb.G);
                }

                image.DebugSave(provider, value.ToString());
            }
        }

        [Theory]
        [WithTestPatternImages(nameof(GrayscaleModeTypes), 50, 50, DefaultPixelType)]
        public void ImageShouldApplyGrayscaleFilterInBox<TPixel>(TestImageProvider<TPixel> provider, GrayscaleMode value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (Image<TPixel> image = source.Clone())
            {
                var bounds = new Rectangle(image.Width / 4, image.Height / 4, image.Width / 2, image.Height / 2);
                image.Mutate(x => x.Grayscale(value, bounds));
                image.DebugSave(provider, value.ToString());

                ImageComparer.Tolerant().VerifySimilarityIgnoreRegion(source, image, bounds);
            }
        }
    }
}