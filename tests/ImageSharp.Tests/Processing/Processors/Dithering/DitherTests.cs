// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
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

        public static readonly TheoryData<IDither, string> ErrorDiffusers
            = new TheoryData<IDither, string>
            {
                { KnownDitherings.Atkinson, nameof(KnownDitherings.Atkinson) },
                { KnownDitherings.Burks, nameof(KnownDitherings.Burks) },
                { KnownDitherings.FloydSteinberg, nameof(KnownDitherings.FloydSteinberg) },
                { KnownDitherings.JarvisJudiceNinke, nameof(KnownDitherings.JarvisJudiceNinke) },
                { KnownDitherings.Sierra2, nameof(KnownDitherings.Sierra2) },
                { KnownDitherings.Sierra3, nameof(KnownDitherings.Sierra3) },
                { KnownDitherings.SierraLite, nameof(KnownDitherings.SierraLite) },
                { KnownDitherings.StevensonArce, nameof(KnownDitherings.StevensonArce) },
                { KnownDitherings.Stucki, nameof(KnownDitherings.Stucki) },
            };

        public static readonly TheoryData<IDither, string> OrderedDitherers
            = new TheoryData<IDither, string>
            {
                { KnownDitherings.Bayer2x2, nameof(KnownDitherings.Bayer2x2) },
                { KnownDitherings.Bayer4x4, nameof(KnownDitherings.Bayer4x4) },
                { KnownDitherings.Bayer8x8, nameof(KnownDitherings.Bayer8x8) },
                { KnownDitherings.Ordered3x3, nameof(KnownDitherings.Ordered3x3) }
            };

        private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.05f);

        private static IDither DefaultDitherer => KnownDitherings.Bayer4x4;

        private static IDither DefaultErrorDiffuser => KnownDitherings.Atkinson;

        /// <summary>
        /// The output is visually correct old 32bit runtime,
        /// but it is very different because of floating point inaccuracies.
        /// </summary>
        private static readonly bool SkipAllDitherTests =
            !TestEnvironment.Is64BitProcess && string.IsNullOrEmpty(TestEnvironment.NetCoreVersion);

        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, PixelTypes.Rgba32)]
        public void ApplyDiffusionFilterInBox<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (SkipAllDitherTests)
            {
                return;
            }

            provider.RunRectangleConstrainedValidatingProcessorTest(
                (x, rect) => x.Dither(DefaultErrorDiffuser, rect),
                comparer: ValidatorComparer);
        }

        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, PixelTypes.Rgba32)]
        public void ApplyDitherFilterInBox<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
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
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (SkipAllDitherTests)
            {
                return;
            }

            // Increased tolerance because of compatibility issues on .NET 4.6.2:
            var comparer = ImageComparer.TolerantPercentage(1f);
            provider.RunValidatingProcessorTest(x => x.Dither(DefaultErrorDiffuser), comparer: comparer);
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), nameof(ErrorDiffusers), PixelTypes.Rgba32)]
        public void DiffusionFilter_WorksWithAllErrorDiffusers<TPixel>(
            TestImageProvider<TPixel> provider,
            IDither diffuser,
            string name)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (SkipAllDitherTests)
            {
                return;
            }

            provider.RunValidatingProcessorTest(
                x => x.Dither(diffuser),
                testOutputDetails: name,
                comparer: ValidatorComparer,
                appendPixelTypeToFileName: false);
        }

        [Theory]
        [WithFile(TestImages.Png.Filter0, CommonNonDefaultPixelTypes)]
        public void DitherFilter_ShouldNotDependOnSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
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
            IDither ditherer,
            string name)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (SkipAllDitherTests)
            {
                return;
            }

            provider.RunValidatingProcessorTest(
                x => x.Dither(ditherer),
                testOutputDetails: name,
                comparer: ValidatorComparer,
                appendPixelTypeToFileName: false);
        }

        [Theory]
        [WithFile(TestImages.Png.Bike, PixelTypes.Rgba32, nameof(OrderedDither.Ordered3x3))]
        [WithFile(TestImages.Png.Bike, PixelTypes.Rgba32, nameof(ErrorDither.FloydSteinberg))]
        public void CommonDitherers_WorkWithDiscoBuffers<TPixel>(
            TestImageProvider<TPixel> provider,
            string name)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            IDither dither = TestUtils.GetDither(name);
            if (SkipAllDitherTests)
            {
                return;
            }

            provider.RunBufferCapacityLimitProcessorTest(
                41,
                c => c.Dither(dither),
                name,
                ImageComparer.TolerantPercentage(0.001f));
        }
    }
}
