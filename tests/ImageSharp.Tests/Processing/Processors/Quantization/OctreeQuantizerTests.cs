// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Quantization
{
    public class OctreeQuantizerTests
    {
        [Fact]
        public void OctreeQuantizerConstructor()
        {
            var quantizer = new OctreeQuantizer(128);

            Assert.Equal(128, quantizer.MaxColors);
            Assert.Equal(KnownDitherings.FloydSteinberg, quantizer.Dither);

            quantizer = new OctreeQuantizer(false);
            Assert.Equal(QuantizerConstants.MaxColors, quantizer.MaxColors);
            Assert.Null(quantizer.Dither);

            quantizer = new OctreeQuantizer(KnownDitherings.Atkinson);
            Assert.Equal(QuantizerConstants.MaxColors, quantizer.MaxColors);
            Assert.Equal(KnownDitherings.Atkinson, quantizer.Dither);

            quantizer = new OctreeQuantizer(KnownDitherings.Atkinson, 128);
            Assert.Equal(128, quantizer.MaxColors);
            Assert.Equal(KnownDitherings.Atkinson, quantizer.Dither);
        }

        [Fact]
        public void OctreeQuantizerCanCreateFrameQuantizer()
        {
            var quantizer = new OctreeQuantizer();
            IFrameQuantizer<Rgba32> frameQuantizer = quantizer.CreateFrameQuantizer<Rgba32>(Configuration.Default);

            Assert.NotNull(frameQuantizer);
            Assert.True(frameQuantizer.DoDither);
            Assert.Equal(KnownDitherings.FloydSteinberg, frameQuantizer.Dither);
            frameQuantizer.Dispose();

            quantizer = new OctreeQuantizer(false);
            frameQuantizer = quantizer.CreateFrameQuantizer<Rgba32>(Configuration.Default);

            Assert.NotNull(frameQuantizer);
            Assert.False(frameQuantizer.DoDither);
            Assert.Null(frameQuantizer.Dither);
            frameQuantizer.Dispose();

            quantizer = new OctreeQuantizer(KnownDitherings.Atkinson);
            frameQuantizer = quantizer.CreateFrameQuantizer<Rgba32>(Configuration.Default);
            Assert.NotNull(frameQuantizer);
            Assert.True(frameQuantizer.DoDither);
            Assert.Equal(KnownDitherings.Atkinson, frameQuantizer.Dither);
            frameQuantizer.Dispose();
        }
    }
}
