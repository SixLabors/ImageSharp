// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

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
        private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.05f);
        
        private static IOrderedDither DefaultDitherer => KnownDitherers.BayerDither4x4;

        private static IErrorDiffuser DefaultErrorDiffuser => KnownDiffusers.Atkinson;

        /// <summary>
        /// The output is visually correct old 32bit runtime,
        /// but it is very different because of floating point inaccuracies.
        /// </summary>
        private static readonly bool SkipAllDitherTests =
            !TestEnvironment.Is64BitProcess && string.IsNullOrEmpty(TestEnvironment.NetCoreVersion);

        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, PixelTypes.Rgba32)]
        public void ApplyDiffusionFilterInBox<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            if (SkipAllDitherTests)
            {
                return;
            }
            
            provider.RunRectangleConstrainedValidatingProcessorTest(
                (x, rect) => x.Diffuse(DefaultErrorDiffuser, .5F, rect),
                comparer: ValidatorComparer);
        }

        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, PixelTypes.Rgba32)]
        public void ApplyDitherFilterInBox<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            if (SkipAllDitherTests)
            {
                return;
            }
            
            provider.RunRectangleConstrainedValidatingProcessorTest(
                (x, rect) => x.Dither(DefaultDitherer, rect),
                comparer: ValidatorComparer);
        }

        [Theory]
        [WithFile(TestImages.Png.Filter0, CommonNonDefaultPixelTypes)]
        public void DiffusionFilter_ShouldNotDependOnSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            if (SkipAllDitherTests)
            {
                return;
            }
            
            // Increased tolerance because of compatibility issues on .NET 4.6.2:
            var comparer = ImageComparer.TolerantPercentage(1f);
            provider.RunValidatingProcessorTest(x => x.Diffuse(DefaultErrorDiffuser, 0.5f), comparer: comparer);
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), nameof(ErrorDiffusers), PixelTypes.Rgba32)]
        public void DiffusionFilter_WorksWithAllErrorDiffusers<TPixel>(
            TestImageProvider<TPixel> provider,
            IErrorDiffuser diffuser)
            where TPixel : struct, IPixel<TPixel>
        {
            if (SkipAllDitherTests)
            {
                return;
            }
            
            provider.RunValidatingProcessorTest(
                x => x.Diffuse(diffuser, 0.5f),
                testOutputDetails: diffuser.GetType().Name,
                comparer: ValidatorComparer,
                appendPixelTypeToFileName: false);
        }

        [Theory]
        [WithFile(TestImages.Png.Filter0, CommonNonDefaultPixelTypes)]
        public void DitherFilter_ShouldNotDependOnSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            if (SkipAllDitherTests)
            {
                return;
            }
            
            provider.RunValidatingProcessorTest(
                x => x.Dither(DefaultDitherer),
                comparer: ValidatorComparer);
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), nameof(OrderedDitherers), PixelTypes.Rgba32)]
        public void DitherFilter_WorksWithAllDitherers<TPixel>(
            TestImageProvider<TPixel> provider,
            IOrderedDither ditherer)
            where TPixel : struct, IPixel<TPixel>
        {
            if (SkipAllDitherTests)
            {
                return;
            }
            
            provider.RunValidatingProcessorTest(
                x => x.Dither(ditherer),
                testOutputDetails: ditherer.GetType().Name,
                comparer: ValidatorComparer,
                appendPixelTypeToFileName: false);
        }
    }
}