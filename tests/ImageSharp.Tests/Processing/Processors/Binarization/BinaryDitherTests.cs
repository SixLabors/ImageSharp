﻿// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using SixLabors.Primitives;
using Xunit;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Binarization
{
    using SixLabors.ImageSharp.Processing;
    using SixLabors.ImageSharp.Processing.Binarization;
    using SixLabors.ImageSharp.Processing.Dithering.ErrorDiffusion;
    using SixLabors.ImageSharp.Processing.Dithering.Ordered;

    public class BinaryDitherTests : FileTestBase
    {
        public static readonly string[] CommonTestImages =
            {
                TestImages.Png.CalliphoraPartial, TestImages.Png.Bike
            };

        public static readonly TheoryData<string, IOrderedDither> OrderedDitherers = new TheoryData<string, IOrderedDither>
        {
            { "Bayer8x8", Ditherers.BayerDither8x8 },
            { "Bayer4x4", Ditherers.BayerDither4x4 },
            { "Ordered3x3", Ditherers.OrderedDither3x3 },
            { "Bayer2x2", Ditherers.BayerDither2x2 }
        };

        public static readonly TheoryData<string, IErrorDiffuser> ErrorDiffusers = new TheoryData<string, IErrorDiffuser>
        {
            { "Atkinson", Diffusers.Atkinson },
            { "Burks", Diffusers.Burks },
            { "FloydSteinberg", Diffusers.FloydSteinberg },
            { "JarvisJudiceNinke", Diffusers.JarvisJudiceNinke },
            { "Sierra2", Diffusers.Sierra2 },
            { "Sierra3", Diffusers.Sierra3 },
            { "SierraLite", Diffusers.SierraLite },
            { "StevensonArce", Diffusers.StevensonArce },
            { "Stucki", Diffusers.Stucki },
        };


        private static IOrderedDither DefaultDitherer => Dithering.Ditherers.BayerDither4x4;

        private static IErrorDiffuser DefaultErrorDiffuser => Diffusers.Atkinson;

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), nameof(OrderedDitherers), DefaultPixelType)]
        [WithTestPatternImages(nameof(OrderedDitherers), 100, 100, DefaultPixelType)]
        public void BinaryDitherFilter_WorksWithAllDitherers<TPixel>(TestImageProvider<TPixel> provider, string name, IOrderedDither ditherer)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.BinaryDither(ditherer));
                image.DebugSave(provider, name);
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), nameof(ErrorDiffusers), DefaultPixelType)]
        [WithTestPatternImages(nameof(ErrorDiffusers), 100, 100, DefaultPixelType)]
        public void DiffusionFilter_WorksWithAllErrorDiffusers<TPixel>(TestImageProvider<TPixel> provider, string name, IErrorDiffuser diffuser)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.BinaryDiffuse(diffuser, .5F));
                image.DebugSave(provider, name);
            }
        }

        [Theory]
        [WithFile(TestImages.Png.Bike, CommonNonDefaultPixelTypes)]
        public void BinaryDitherFilter_ShouldNotDependOnSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.BinaryDither(DefaultDitherer));
                image.DebugSave(provider);
            }
        }

        [Theory]
        [WithFile(TestImages.Png.Bike, CommonNonDefaultPixelTypes)]
        public void DiffusionFilter_ShouldNotDependOnSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.BinaryDiffuse(DefaultErrorDiffuser, 0.5f));
                image.DebugSave(provider);
            }
        }

        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, DefaultPixelType)]
        public void ApplyDitherFilterInBox<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (Image<TPixel> image = source.Clone())
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.BinaryDither(DefaultDitherer, bounds));
                image.DebugSave(provider);

                ImageComparer.Tolerant().VerifySimilarityIgnoreRegion(source, image, bounds);
            }
        }

        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, DefaultPixelType)]
        public void ApplyDiffusionFilterInBox<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (Image<TPixel> image = source.Clone())
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.BinaryDiffuse(DefaultErrorDiffuser, .5F, bounds));
                image.DebugSave(provider);

                ImageComparer.Tolerant().VerifySimilarityIgnoreRegion(source, image, bounds);
            }
        }
    }
}