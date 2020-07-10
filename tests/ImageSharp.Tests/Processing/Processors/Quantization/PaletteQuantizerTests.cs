// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Quantization
{
    public class PaletteQuantizerTests
    {
        private static readonly Color[] Palette = new Color[] { Color.Red, Color.Green, Color.Blue };

        [Fact]
        public void PaletteQuantizerConstructor()
        {
            var expected = new QuantizerOptions { MaxColors = 128 };
            var quantizer = new PaletteQuantizer(Palette, expected);

            Assert.Equal(expected.MaxColors, quantizer.Options.MaxColors);
            Assert.Equal(QuantizerConstants.DefaultDither, quantizer.Options.Dither);

            expected = new QuantizerOptions { Dither = null };
            quantizer = new PaletteQuantizer(Palette, expected);
            Assert.Equal(QuantizerConstants.MaxColors, quantizer.Options.MaxColors);
            Assert.Null(quantizer.Options.Dither);

            expected = new QuantizerOptions { Dither = KnownDitherings.Atkinson };
            quantizer = new PaletteQuantizer(Palette, expected);
            Assert.Equal(QuantizerConstants.MaxColors, quantizer.Options.MaxColors);
            Assert.Equal(KnownDitherings.Atkinson, quantizer.Options.Dither);

            expected = new QuantizerOptions { Dither = KnownDitherings.Atkinson, MaxColors = 0 };
            quantizer = new PaletteQuantizer(Palette, expected);
            Assert.Equal(QuantizerConstants.MinColors, quantizer.Options.MaxColors);
            Assert.Equal(KnownDitherings.Atkinson, quantizer.Options.Dither);
        }

        [Fact]
        public void PaletteQuantizerCanCreateFrameQuantizer()
        {
            var quantizer = new PaletteQuantizer(Palette);
            IQuantizer<Rgba32> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(Configuration.Default);

            Assert.NotNull(frameQuantizer);
            Assert.NotNull(frameQuantizer.Options);
            Assert.Equal(QuantizerConstants.DefaultDither, frameQuantizer.Options.Dither);
            frameQuantizer.Dispose();

            quantizer = new PaletteQuantizer(Palette, new QuantizerOptions { Dither = null });
            frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(Configuration.Default);

            Assert.NotNull(frameQuantizer);
            Assert.Null(frameQuantizer.Options.Dither);
            frameQuantizer.Dispose();

            quantizer = new PaletteQuantizer(Palette, new QuantizerOptions { Dither = KnownDitherings.Atkinson });
            frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(Configuration.Default);
            Assert.NotNull(frameQuantizer);
            Assert.Equal(KnownDitherings.Atkinson, frameQuantizer.Options.Dither);
            frameQuantizer.Dispose();
        }

        [Fact]
        public void KnownQuantizersWebSafeTests()
        {
            IQuantizer quantizer = KnownQuantizers.WebSafe;
            Assert.Equal(QuantizerConstants.DefaultDither, quantizer.Options.Dither);
        }

        [Fact]
        public void KnownQuantizersWernerTests()
        {
            IQuantizer quantizer = KnownQuantizers.Werner;
            Assert.Equal(QuantizerConstants.DefaultDither, quantizer.Options.Dither);
        }
    }
}
