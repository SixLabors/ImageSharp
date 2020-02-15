// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Quantization
{
    public class QuantizerTests
    {
        public static readonly string[] CommonTestImages =
        {
            TestImages.Png.CalliphoraPartial,
            TestImages.Png.Bike
        };

        public static readonly TheoryData<IQuantizer> Quantizers
        = new TheoryData<IQuantizer>
        {
            KnownQuantizers.Octree,
            KnownQuantizers.WebSafe,
            KnownQuantizers.Werner,
            KnownQuantizers.Wu,
            new OctreeQuantizer(false),
            new WebSafePaletteQuantizer(false),
            new WernerPaletteQuantizer(false),
            new WuQuantizer(false),
            new OctreeQuantizer(KnownDitherings.BayerDither8x8),
            new WebSafePaletteQuantizer(KnownDitherings.BayerDither8x8),
            new WernerPaletteQuantizer(KnownDitherings.BayerDither8x8),
            new WuQuantizer(KnownDitherings.BayerDither8x8)
        };

        private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.05f);

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), nameof(Quantizers), PixelTypes.Rgba32)]
        public void ApplyQuantizationInBox<TPixel>(TestImageProvider<TPixel> provider, IQuantizer quantizer)
            where TPixel : struct, IPixel<TPixel>
        {
            string quantizerName = quantizer.GetType().Name;
            string ditherName = quantizer.Dither?.GetType()?.Name ?? "noDither";
            string ditherType = quantizer.Dither?.DitherType.ToString() ?? string.Empty;
            string testOutputDetails = $"{quantizerName}_{ditherName}_{ditherType}";

            provider.RunRectangleConstrainedValidatingProcessorTest(
                (x, rect) => x.Quantize(quantizer, rect),
                comparer: ValidatorComparer,
                testOutputDetails: testOutputDetails,
                appendPixelTypeToFileName: false);
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), nameof(Quantizers), PixelTypes.Rgba32)]
        public void ApplyQuantization<TPixel>(TestImageProvider<TPixel> provider, IQuantizer quantizer)
            where TPixel : struct, IPixel<TPixel>
        {
            string quantizerName = quantizer.GetType().Name;
            string ditherName = quantizer.Dither?.GetType()?.Name ?? "noDither";
            string ditherType = quantizer.Dither?.DitherType.ToString() ?? string.Empty;
            string testOutputDetails = $"{quantizerName}_{ditherName}_{ditherType}";

            provider.RunValidatingProcessorTest(
                x => x.Quantize(quantizer),
                comparer: ValidatorComparer,
                testOutputDetails: testOutputDetails,
                appendPixelTypeToFileName: false);
        }
    }
}
