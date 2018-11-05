// Copyright (c) Six Labors and contributors.
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
            var quantizer = new WuQuantizer(128);

            Assert.Equal(128, quantizer.MaxColors);
            Assert.Equal(KnownDiffusers.FloydSteinberg, quantizer.Diffuser);

            quantizer = new WuQuantizer(false);
            Assert.Equal(QuantizerConstants.MaxColors, quantizer.MaxColors);
            Assert.Null(quantizer.Diffuser);

            quantizer = new WuQuantizer(KnownDiffusers.Atkinson);
            Assert.Equal(QuantizerConstants.MaxColors, quantizer.MaxColors);
            Assert.Equal(KnownDiffusers.Atkinson, quantizer.Diffuser);

            quantizer = new WuQuantizer(KnownDiffusers.Atkinson, 128);
            Assert.Equal(128, quantizer.MaxColors);
            Assert.Equal(KnownDiffusers.Atkinson, quantizer.Diffuser);
        }

        [Fact]
        public void WuQuantizerCanCreateFrameQuantizer()
        {
            var quantizer = new WuQuantizer();
            IFrameQuantizer<Rgba32> frameQuantizer = quantizer.CreateFrameQuantizer<Rgba32>(Configuration.Default);

            Assert.NotNull(frameQuantizer);
            Assert.True(frameQuantizer.Dither);
            Assert.Equal(KnownDiffusers.FloydSteinberg, frameQuantizer.Diffuser);

            quantizer = new WuQuantizer(false);
            frameQuantizer = quantizer.CreateFrameQuantizer<Rgba32>(Configuration.Default);

            Assert.NotNull(frameQuantizer);
            Assert.False(frameQuantizer.Dither);
            Assert.Null(frameQuantizer.Diffuser);

            quantizer = new WuQuantizer(KnownDiffusers.Atkinson);
            frameQuantizer = quantizer.CreateFrameQuantizer<Rgba32>(Configuration.Default);
            Assert.NotNull(frameQuantizer);
            Assert.True(frameQuantizer.Dither);
            Assert.Equal(KnownDiffusers.Atkinson, frameQuantizer.Diffuser);
        }
    }
}
