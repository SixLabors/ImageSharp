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
            Assert.Equal(KnownDitherings.FloydSteinberg, quantizer.Dither);

            quantizer = new WuQuantizer(false);
            Assert.Equal(QuantizerConstants.MaxColors, quantizer.MaxColors);
            Assert.Null(quantizer.Dither);

            quantizer = new WuQuantizer(KnownDitherings.Atkinson);
            Assert.Equal(QuantizerConstants.MaxColors, quantizer.MaxColors);
            Assert.Equal(KnownDitherings.Atkinson, quantizer.Dither);

            quantizer = new WuQuantizer(KnownDitherings.Atkinson, 128);
            Assert.Equal(128, quantizer.MaxColors);
            Assert.Equal(KnownDitherings.Atkinson, quantizer.Dither);
        }

        [Fact]
        public void WuQuantizerCanCreateFrameQuantizer()
        {
            var quantizer = new WuQuantizer();
            IFrameQuantizer<Rgba32> frameQuantizer = quantizer.CreateFrameQuantizer<Rgba32>(Configuration.Default);

            Assert.NotNull(frameQuantizer);
            Assert.True(frameQuantizer.DoDither);
            Assert.Equal(KnownDitherings.FloydSteinberg, frameQuantizer.Dither);
            frameQuantizer.Dispose();

            quantizer = new WuQuantizer(false);
            frameQuantizer = quantizer.CreateFrameQuantizer<Rgba32>(Configuration.Default);

            Assert.NotNull(frameQuantizer);
            Assert.False(frameQuantizer.DoDither);
            Assert.Null(frameQuantizer.Dither);
            frameQuantizer.Dispose();

            quantizer = new WuQuantizer(KnownDitherings.Atkinson);
            frameQuantizer = quantizer.CreateFrameQuantizer<Rgba32>(Configuration.Default);
            Assert.NotNull(frameQuantizer);
            Assert.True(frameQuantizer.DoDither);
            Assert.Equal(KnownDitherings.Atkinson, frameQuantizer.Dither);
            frameQuantizer.Dispose();
        }
    }
}
