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
        private static readonly Color[] Rgb = new Color[] { Rgba32.Red, Rgba32.Green, Rgba32.Blue };

        [Fact]
        public void PaletteQuantizerConstructor()
        {
            var quantizer = new PaletteQuantizer(Rgb);

            Assert.Equal(Rgb, quantizer.Palette);
            Assert.Equal(KnownDiffusers.FloydSteinberg, quantizer.Diffuser);

            quantizer = new PaletteQuantizer(Rgb, false);
            Assert.Equal(Rgb, quantizer.Palette);
            Assert.Null(quantizer.Diffuser);

            quantizer = new PaletteQuantizer(Rgb, KnownDiffusers.Atkinson);
            Assert.Equal(Rgb, quantizer.Palette);
            Assert.Equal(KnownDiffusers.Atkinson, quantizer.Diffuser);
        }

        [Fact]
        public void PaletteQuantizerCanCreateFrameQuantizer()
        {
            var quantizer = new PaletteQuantizer(Rgb);
            IFrameQuantizer<Rgba32> frameQuantizer = quantizer.CreateFrameQuantizer<Rgba32>(Configuration.Default);

            Assert.NotNull(frameQuantizer);
            Assert.True(frameQuantizer.Dither);
            Assert.Equal(KnownDiffusers.FloydSteinberg, frameQuantizer.Diffuser);

            quantizer = new PaletteQuantizer(Rgb, false);
            frameQuantizer = quantizer.CreateFrameQuantizer<Rgba32>(Configuration.Default);

            Assert.NotNull(frameQuantizer);
            Assert.False(frameQuantizer.Dither);
            Assert.Null(frameQuantizer.Diffuser);

            quantizer = new PaletteQuantizer(Rgb, KnownDiffusers.Atkinson);
            frameQuantizer = quantizer.CreateFrameQuantizer<Rgba32>(Configuration.Default);
            Assert.NotNull(frameQuantizer);
            Assert.True(frameQuantizer.Dither);
            Assert.Equal(KnownDiffusers.Atkinson, frameQuantizer.Diffuser);
        }

        [Fact]
        public void KnownQuantizersWebSafeTests()
        {
            IQuantizer quantizer = KnownQuantizers.WebSafe;
            Assert.Equal(KnownDiffusers.FloydSteinberg, quantizer.Diffuser);
        }

        [Fact]
        public void KnownQuantizersWernerTests()
        {
            IQuantizer quantizer = KnownQuantizers.Werner;
            Assert.Equal(KnownDiffusers.FloydSteinberg, quantizer.Diffuser);
        }
    }
}
