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
            Assert.Equal(KnownDiffusers.FloydSteinberg, quantizer.Diffuser);

            quantizer = new OctreeQuantizer(false);
            Assert.Equal(QuantizerConstants.MaxColors, quantizer.MaxColors);
            Assert.Null(quantizer.Diffuser);

            quantizer = new OctreeQuantizer(KnownDiffusers.Atkinson);
            Assert.Equal(QuantizerConstants.MaxColors, quantizer.MaxColors);
            Assert.Equal(KnownDiffusers.Atkinson, quantizer.Diffuser);

            quantizer = new OctreeQuantizer(KnownDiffusers.Atkinson, 128);
            Assert.Equal(128, quantizer.MaxColors);
            Assert.Equal(KnownDiffusers.Atkinson, quantizer.Diffuser);
        }

        [Fact]
        public void OctreeQuantizerCanCreateFrameQuantizer()
        {
            var quantizer = new OctreeQuantizer();
            IFrameQuantizer<Rgba32> frameQuantizer = quantizer.CreateFrameQuantizer<Rgba32>(Configuration.Default);

            Assert.NotNull(frameQuantizer);
            Assert.True(frameQuantizer.Dither);
            Assert.Equal(KnownDiffusers.FloydSteinberg, frameQuantizer.Diffuser);

            quantizer = new OctreeQuantizer(false);
            frameQuantizer = quantizer.CreateFrameQuantizer<Rgba32>(Configuration.Default);

            Assert.NotNull(frameQuantizer);
            Assert.False(frameQuantizer.Dither);
            Assert.Null(frameQuantizer.Diffuser);

            quantizer = new OctreeQuantizer(KnownDiffusers.Atkinson);
            frameQuantizer = quantizer.CreateFrameQuantizer<Rgba32>(Configuration.Default);
            Assert.NotNull(frameQuantizer);
            Assert.True(frameQuantizer.Dither);
            Assert.Equal(KnownDiffusers.Atkinson, frameQuantizer.Diffuser);
        }
    }
}
