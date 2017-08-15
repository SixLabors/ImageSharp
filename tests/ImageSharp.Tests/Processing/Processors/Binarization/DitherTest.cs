// <copyright file="DitherTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Processors.Binarization
{
    using ImageSharp.Dithering;
    using ImageSharp.Dithering.Ordered;
    using ImageSharp.PixelFormats;
    using ImageSharp.Tests.TestUtilities.ImageComparison;

    using SixLabors.Primitives;
    using Xunit;

    public class DitherTest : FileTestBase
    {
        public static readonly TheoryData<string, IOrderedDither> Ditherers = new TheoryData<string, IOrderedDither>
        {
            { "Ordered", new Ordered() },
            { "Bayer", new Bayer() }
        };

        public static readonly TheoryData<string, IErrorDiffuser> ErrorDiffusers = new TheoryData<string, IErrorDiffuser>
        {
            { "Atkinson", new Atkinson() },
            { "Burks", new Burks() },
            { "FloydSteinberg", new FloydSteinberg() },
            { "JarvisJudiceNinke", new JarvisJudiceNinke() },
            { "Sierra2", new Sierra2() },
            { "Sierra3", new Sierra3() },
            { "SierraLite", new SierraLite() },
            { "Stucki", new Stucki() },
        };

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(Ditherers), DefaultPixelType)]
        public void ImageShouldApplyDitherFilter<TPixel>(TestImageProvider<TPixel> provider, string name, IOrderedDither ditherer)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Dither(ditherer));
                image.DebugSave(provider, name);
            }
        }

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(Ditherers), DefaultPixelType)]
        public void ImageShouldApplyDitherFilterInBox<TPixel>(TestImageProvider<TPixel> provider, string name, IOrderedDither ditherer)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (var image = source.Clone())
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.Dither(ditherer, bounds));
                image.DebugSave(provider, name);

                ImageComparer.Tolerant().EnsureProcessorChangesAreConstrained(source, image, bounds);
            }
        }

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(ErrorDiffusers), DefaultPixelType)]
        public void ImageShouldApplyDiffusionFilter<TPixel>(TestImageProvider<TPixel> provider, string name, IErrorDiffuser diffuser)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Dither(diffuser, .5F));
                image.DebugSave(provider, name);
            }
        }

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(ErrorDiffusers), DefaultPixelType)]
        public void ImageShouldApplyDiffusionFilterInBox<TPixel>(TestImageProvider<TPixel> provider, string name, IErrorDiffuser diffuser)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (var image = source.Clone())
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.Dither(diffuser, .5F, bounds));
                image.DebugSave(provider, name);

                ImageComparer.Tolerant().EnsureProcessorChangesAreConstrained(source, image, bounds);
            }
        }
    }
}