// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Quantization
{
    public class PaletteQuantizerTests
    {
        private static readonly Color[] Rgb = new Color[] { Color.Red, Color.Green, Color.Blue };

        [Fact]
        public void PaletteQuantizerConstructor()
        {
            var quantizer = new PaletteQuantizer(Rgb);

            Assert.Equal(Rgb, quantizer.Palette);
            Assert.Equal(KnownDitherers.FloydSteinberg, quantizer.Dither);

            quantizer = new PaletteQuantizer(Rgb, false);
            Assert.Equal(Rgb, quantizer.Palette);
            Assert.Null(quantizer.Dither);

            quantizer = new PaletteQuantizer(Rgb, KnownDitherers.Atkinson);
            Assert.Equal(Rgb, quantizer.Palette);
            Assert.Equal(KnownDitherers.Atkinson, quantizer.Dither);
        }

        [Fact]
        public void PaletteQuantizerCanCreateFrameQuantizer()
        {
            var quantizer = new PaletteQuantizer(Rgb);
            IFrameQuantizer<Rgba32> frameQuantizer = quantizer.CreateFrameQuantizer<Rgba32>(Configuration.Default);

            Assert.NotNull(frameQuantizer);
            Assert.True(frameQuantizer.DoDither);
            Assert.Equal(KnownDitherers.FloydSteinberg, frameQuantizer.Dither);

            quantizer = new PaletteQuantizer(Rgb, false);
            frameQuantizer = quantizer.CreateFrameQuantizer<Rgba32>(Configuration.Default);

            Assert.NotNull(frameQuantizer);
            Assert.False(frameQuantizer.DoDither);
            Assert.Null(frameQuantizer.Dither);

            quantizer = new PaletteQuantizer(Rgb, KnownDitherers.Atkinson);
            frameQuantizer = quantizer.CreateFrameQuantizer<Rgba32>(Configuration.Default);
            Assert.NotNull(frameQuantizer);
            Assert.True(frameQuantizer.DoDither);
            Assert.Equal(KnownDitherers.Atkinson, frameQuantizer.Dither);
        }

        [Fact]
        public void KnownQuantizersWebSafeTests()
        {
            IQuantizer quantizer = KnownQuantizers.WebSafe;
            Assert.Equal(KnownDitherers.FloydSteinberg, quantizer.Dither);
        }

        [Fact]
        public void KnownQuantizersWernerTests()
        {
            IQuantizer quantizer = KnownQuantizers.Werner;
            Assert.Equal(KnownDitherers.FloydSteinberg, quantizer.Dither);
        }
    }
}
