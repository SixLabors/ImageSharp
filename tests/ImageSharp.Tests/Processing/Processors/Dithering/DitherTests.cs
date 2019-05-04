// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.Primitives;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Binarization
{
    public class DitherTests
    {
        public const PixelTypes CommonNonDefaultPixelTypes =
            PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.Rgb24 | PixelTypes.RgbaVector;
        
        public static readonly string[] CommonTestImages = { TestImages.Png.CalliphoraPartial, TestImages.Png.Bike };

        public static readonly TheoryData<IErrorDiffuser> ErrorDiffusers = new TheoryData<IErrorDiffuser>
                                                                               {
                                                                                   KnownDiffusers.Atkinson,
                                                                                   KnownDiffusers.Burks,
                                                                                   KnownDiffusers.FloydSteinberg,
                                                                                   KnownDiffusers.JarvisJudiceNinke,
                                                                                   KnownDiffusers.Sierra2,
                                                                                   KnownDiffusers.Sierra3,
                                                                                   KnownDiffusers.SierraLite,
                                                                                   KnownDiffusers.StevensonArce,
                                                                                   KnownDiffusers.Stucki,
                                                                               };

        public static readonly TheoryData<IOrderedDither> OrderedDitherers = new TheoryData<IOrderedDither>
                                                                                 {
                                                                                     KnownDitherers.BayerDither8x8,
                                                                                     KnownDitherers.BayerDither4x4,
                                                                                     KnownDitherers.OrderedDither3x3,
                                                                                     KnownDitherers.BayerDither2x2
                                                                                 };
        
        private static IOrderedDither DefaultDitherer => KnownDitherers.BayerDither4x4;

        private static IErrorDiffuser DefaultErrorDiffuser => KnownDiffusers.Atkinson;

        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, PixelTypes.Rgba32)]
        public void ApplyDiffusionFilterInBox<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (Image<TPixel> image = source.Clone())
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.Diffuse(DefaultErrorDiffuser, .5F, bounds));
                image.DebugSave(provider);

                ImageComparer.Tolerant().VerifySimilarityIgnoreRegion(source, image, bounds);
            }
        }

        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, PixelTypes.Rgba32)]
        public void ApplyDitherFilterInBox<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (Image<TPixel> image = source.Clone())
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.Dither(DefaultDitherer, bounds));
                image.DebugSave(provider);

                ImageComparer.Tolerant().VerifySimilarityIgnoreRegion(source, image, bounds);
            }
        }

        [Theory]
        [WithFile(TestImages.Png.Filter0, CommonNonDefaultPixelTypes)]
            public void DiffusionFilter_ShouldNotDependOnSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Diffuse(DefaultErrorDiffuser, 0.5f));
                image.DebugSave(provider);
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), nameof(ErrorDiffusers), PixelTypes.Rgba32)]
        public void DiffusionFilter_WorksWithAllErrorDiffusers<TPixel>(
            TestImageProvider<TPixel> provider,
            IErrorDiffuser diffuser)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Diffuse(diffuser, .5F));
                image.DebugSave(provider, diffuser.GetType().Name);
            }
        }

        [Theory]
        [WithFile(TestImages.Png.Filter0, CommonNonDefaultPixelTypes)]
        public void DitherFilter_ShouldNotDependOnSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Dither(DefaultDitherer));
                image.DebugSave(provider);
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), nameof(OrderedDitherers), PixelTypes.Rgba32)]
        public void DitherFilter_WorksWithAllDitherers<TPixel>(
            TestImageProvider<TPixel> provider,
            IOrderedDither ditherer)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Dither(ditherer));
                image.DebugSave(provider, ditherer.GetType().Name);
            }
        }
    }
}