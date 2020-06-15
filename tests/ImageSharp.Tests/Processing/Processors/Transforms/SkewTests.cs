// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms
{
    [GroupOutput("Transforms")]
    public class SkewTests
    {
        private const PixelTypes CommonPixelTypes = PixelTypes.Bgra32 | PixelTypes.Rgb24;

        public static readonly string[] ResamplerNames = new[]
                                                             {
                                                                 nameof(KnownResamplers.Bicubic),
                                                                 nameof(KnownResamplers.Box),
                                                                 nameof(KnownResamplers.CatmullRom),
                                                                 nameof(KnownResamplers.Hermite),
                                                                 nameof(KnownResamplers.Lanczos2),
                                                                 nameof(KnownResamplers.Lanczos3),
                                                                 nameof(KnownResamplers.Lanczos5),
                                                                 nameof(KnownResamplers.Lanczos8),
                                                                 nameof(KnownResamplers.MitchellNetravali),
                                                                 nameof(KnownResamplers.NearestNeighbor),
                                                                 nameof(KnownResamplers.Robidoux),
                                                                 nameof(KnownResamplers.RobidouxSharp),
                                                                 nameof(KnownResamplers.Spline),
                                                                 nameof(KnownResamplers.Triangle),
                                                                 nameof(KnownResamplers.Welch),
                                                             };

        public static readonly TheoryData<float, float> SkewValues = new TheoryData<float, float>
                                                                         {
                                                                             { 20, 10 },
                                                                             { -20, -10 }
                                                                         };

        private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.01f);

        [Theory]
        [WithTestPatternImages(nameof(SkewValues), 100, 50, CommonPixelTypes)]
        public void Skew_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider, float x, float y)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTest(ctx => ctx.Skew(x, y), $"{x}_{y}", ValidatorComparer);
        }

        [Theory]
        [WithFile(TestImages.Png.Ducky, nameof(ResamplerNames), PixelTypes.Rgba32)]
        public void Skew_WorksWithAllResamplers<TPixel>(TestImageProvider<TPixel> provider, string resamplerName)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            IResampler sampler = TestUtils.GetResampler(resamplerName);

            provider.RunValidatingProcessorTest(
                x => x.Skew(21, 32, sampler),
                resamplerName,
                comparer: ValidatorComparer,
                appendPixelTypeToFileName: false);
        }
    }
}