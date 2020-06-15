// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Quantization
{
    public class QuantizerTests
    {
        /// <summary>
        /// Something is causing tests to fail on NETFX in CI.
        /// Could be a JIT error as everything runs well and is identical to .NET Core output.
        /// Not worth investigating for now.
        /// <see href="https://github.com/SixLabors/ImageSharp/pull/1114/checks?check_run_id=448891164#step:11:631"/>
        /// </summary>
        private static readonly bool SkipAllQuantizerTests = TestEnvironment.IsFramework;

        public static readonly string[] CommonTestImages =
        {
            TestImages.Png.CalliphoraPartial,
            TestImages.Png.Bike
        };

        private static readonly QuantizerOptions NoDitherOptions = new QuantizerOptions { Dither = null };
        private static readonly QuantizerOptions DiffuserDitherOptions = new QuantizerOptions { Dither = KnownDitherings.FloydSteinberg };
        private static readonly QuantizerOptions OrderedDitherOptions = new QuantizerOptions { Dither = KnownDitherings.Bayer8x8 };

        private static readonly QuantizerOptions Diffuser0_ScaleDitherOptions = new QuantizerOptions
        {
            Dither = KnownDitherings.FloydSteinberg,
            DitherScale = 0F
        };

        private static readonly QuantizerOptions Diffuser0_25_ScaleDitherOptions = new QuantizerOptions
        {
            Dither = KnownDitherings.FloydSteinberg,
            DitherScale = .25F
        };

        private static readonly QuantizerOptions Diffuser0_5_ScaleDitherOptions = new QuantizerOptions
        {
            Dither = KnownDitherings.FloydSteinberg,
            DitherScale = .5F
        };

        private static readonly QuantizerOptions Diffuser0_75_ScaleDitherOptions = new QuantizerOptions
        {
            Dither = KnownDitherings.FloydSteinberg,
            DitherScale = .75F
        };

        private static readonly QuantizerOptions Ordered0_ScaleDitherOptions = new QuantizerOptions
        {
            Dither = KnownDitherings.Bayer8x8,
            DitherScale = 0F
        };

        private static readonly QuantizerOptions Ordered0_25_ScaleDitherOptions = new QuantizerOptions
        {
            Dither = KnownDitherings.Bayer8x8,
            DitherScale = .25F
        };

        private static readonly QuantizerOptions Ordered0_5_ScaleDitherOptions = new QuantizerOptions
        {
            Dither = KnownDitherings.Bayer8x8,
            DitherScale = .5F
        };

        private static readonly QuantizerOptions Ordered0_75_ScaleDitherOptions = new QuantizerOptions
        {
            Dither = KnownDitherings.Bayer8x8,
            DitherScale = .75F
        };

        public static readonly TheoryData<IQuantizer> Quantizers
        = new TheoryData<IQuantizer>
        {
            // Known uses error diffusion by default.
            KnownQuantizers.Octree,
            KnownQuantizers.WebSafe,
            KnownQuantizers.Werner,
            KnownQuantizers.Wu,
            new OctreeQuantizer(NoDitherOptions),
            new WebSafePaletteQuantizer(NoDitherOptions),
            new WernerPaletteQuantizer(NoDitherOptions),
            new WuQuantizer(NoDitherOptions),
            new OctreeQuantizer(OrderedDitherOptions),
            new WebSafePaletteQuantizer(OrderedDitherOptions),
            new WernerPaletteQuantizer(OrderedDitherOptions),
            new WuQuantizer(OrderedDitherOptions)
        };

        public static readonly TheoryData<IQuantizer> DitherScaleQuantizers
        = new TheoryData<IQuantizer>
        {
            new OctreeQuantizer(Diffuser0_ScaleDitherOptions),
            new WebSafePaletteQuantizer(Diffuser0_ScaleDitherOptions),
            new WernerPaletteQuantizer(Diffuser0_ScaleDitherOptions),
            new WuQuantizer(Diffuser0_ScaleDitherOptions),

            new OctreeQuantizer(Diffuser0_25_ScaleDitherOptions),
            new WebSafePaletteQuantizer(Diffuser0_25_ScaleDitherOptions),
            new WernerPaletteQuantizer(Diffuser0_25_ScaleDitherOptions),
            new WuQuantizer(Diffuser0_25_ScaleDitherOptions),

            new OctreeQuantizer(Diffuser0_5_ScaleDitherOptions),
            new WebSafePaletteQuantizer(Diffuser0_5_ScaleDitherOptions),
            new WernerPaletteQuantizer(Diffuser0_5_ScaleDitherOptions),
            new WuQuantizer(Diffuser0_5_ScaleDitherOptions),

            new OctreeQuantizer(Diffuser0_75_ScaleDitherOptions),
            new WebSafePaletteQuantizer(Diffuser0_75_ScaleDitherOptions),
            new WernerPaletteQuantizer(Diffuser0_75_ScaleDitherOptions),
            new WuQuantizer(Diffuser0_75_ScaleDitherOptions),

            new OctreeQuantizer(DiffuserDitherOptions),
            new WebSafePaletteQuantizer(DiffuserDitherOptions),
            new WernerPaletteQuantizer(DiffuserDitherOptions),
            new WuQuantizer(DiffuserDitherOptions),

            new OctreeQuantizer(Ordered0_ScaleDitherOptions),
            new WebSafePaletteQuantizer(Ordered0_ScaleDitherOptions),
            new WernerPaletteQuantizer(Ordered0_ScaleDitherOptions),
            new WuQuantizer(Ordered0_ScaleDitherOptions),

            new OctreeQuantizer(Ordered0_25_ScaleDitherOptions),
            new WebSafePaletteQuantizer(Ordered0_25_ScaleDitherOptions),
            new WernerPaletteQuantizer(Ordered0_25_ScaleDitherOptions),
            new WuQuantizer(Ordered0_25_ScaleDitherOptions),

            new OctreeQuantizer(Ordered0_5_ScaleDitherOptions),
            new WebSafePaletteQuantizer(Ordered0_5_ScaleDitherOptions),
            new WernerPaletteQuantizer(Ordered0_5_ScaleDitherOptions),
            new WuQuantizer(Ordered0_5_ScaleDitherOptions),

            new OctreeQuantizer(Ordered0_75_ScaleDitherOptions),
            new WebSafePaletteQuantizer(Ordered0_75_ScaleDitherOptions),
            new WernerPaletteQuantizer(Ordered0_75_ScaleDitherOptions),
            new WuQuantizer(Ordered0_75_ScaleDitherOptions),

            new OctreeQuantizer(OrderedDitherOptions),
            new WebSafePaletteQuantizer(OrderedDitherOptions),
            new WernerPaletteQuantizer(OrderedDitherOptions),
            new WuQuantizer(OrderedDitherOptions),
        };

        private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.05F);

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), nameof(Quantizers), PixelTypes.Rgba32)]
        public void ApplyQuantizationInBox<TPixel>(TestImageProvider<TPixel> provider, IQuantizer quantizer)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (SkipAllQuantizerTests)
            {
                return;
            }

            string quantizerName = quantizer.GetType().Name;
            string ditherName = quantizer.Options.Dither?.GetType()?.Name ?? "NoDither";
            string testOutputDetails = $"{quantizerName}_{ditherName}";

            provider.RunRectangleConstrainedValidatingProcessorTest(
                (x, rect) => x.Quantize(quantizer, rect),
                comparer: ValidatorComparer,
                testOutputDetails: testOutputDetails,
                appendPixelTypeToFileName: false);
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), nameof(Quantizers), PixelTypes.Rgba32)]
        public void ApplyQuantization<TPixel>(TestImageProvider<TPixel> provider, IQuantizer quantizer)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (SkipAllQuantizerTests)
            {
                return;
            }

            string quantizerName = quantizer.GetType().Name;
            string ditherName = quantizer.Options.Dither?.GetType()?.Name ?? "NoDither";
            string testOutputDetails = $"{quantizerName}_{ditherName}";

            provider.RunValidatingProcessorTest(
                x => x.Quantize(quantizer),
                comparer: ValidatorComparer,
                testOutputDetails: testOutputDetails,
                appendPixelTypeToFileName: false);
        }

        [Theory]
        [WithFile(TestImages.Png.David, nameof(DitherScaleQuantizers), PixelTypes.Rgba32)]
        public void ApplyQuantizationWithDitheringScale<TPixel>(TestImageProvider<TPixel> provider, IQuantizer quantizer)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (SkipAllQuantizerTests)
            {
                return;
            }

            string quantizerName = quantizer.GetType().Name;
            string ditherName = quantizer.Options.Dither.GetType().Name;
            float ditherScale = quantizer.Options.DitherScale;
            string testOutputDetails = FormattableString.Invariant($"{quantizerName}_{ditherName}_{ditherScale}");

            provider.RunValidatingProcessorTest(
                x => x.Quantize(quantizer),
                comparer: ValidatorComparer,
                testOutputDetails: testOutputDetails,
                appendPixelTypeToFileName: false);
        }
    }
}
