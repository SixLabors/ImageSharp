// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Quantization
{
    public class WuQuantizerTests
    {
        [Fact]
        public void WuQuantizerConstructor()
        {
            var expected = new QuantizerOptions { MaxColors = 128 };
            var quantizer = new WuQuantizer(expected);

            Assert.Equal(expected.MaxColors, quantizer.Options.MaxColors);
            Assert.Equal(QuantizerConstants.DefaultDither, quantizer.Options.Dither);

            expected = new QuantizerOptions { Dither = null };
            quantizer = new WuQuantizer(expected);
            Assert.Equal(QuantizerConstants.MaxColors, quantizer.Options.MaxColors);
            Assert.Null(quantizer.Options.Dither);

            expected = new QuantizerOptions { Dither = KnownDitherings.Atkinson };
            quantizer = new WuQuantizer(expected);
            Assert.Equal(QuantizerConstants.MaxColors, quantizer.Options.MaxColors);
            Assert.Equal(KnownDitherings.Atkinson, quantizer.Options.Dither);

            expected = new QuantizerOptions { Dither = KnownDitherings.Atkinson, MaxColors = 0 };
            quantizer = new WuQuantizer(expected);
            Assert.Equal(QuantizerConstants.MinColors, quantizer.Options.MaxColors);
            Assert.Equal(KnownDitherings.Atkinson, quantizer.Options.Dither);
        }

        [Fact]
        public void WuQuantizerCanCreateFrameQuantizer()
        {
            var quantizer = new WuQuantizer();
            IQuantizer<Rgba32> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(Configuration.Default);

            Assert.NotNull(frameQuantizer);
            Assert.NotNull(frameQuantizer.Options);
            Assert.Equal(QuantizerConstants.DefaultDither, frameQuantizer.Options.Dither);
            frameQuantizer.Dispose();

            quantizer = new WuQuantizer(new QuantizerOptions { Dither = null });
            frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(Configuration.Default);

            Assert.NotNull(frameQuantizer);
            Assert.Null(frameQuantizer.Options.Dither);
            frameQuantizer.Dispose();

            quantizer = new WuQuantizer(new QuantizerOptions { Dither = KnownDitherings.Atkinson });
            frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(Configuration.Default);
            Assert.NotNull(frameQuantizer);
            Assert.Equal(KnownDitherings.Atkinson, frameQuantizer.Options.Dither);
            frameQuantizer.Dispose();
        }
    }
}
