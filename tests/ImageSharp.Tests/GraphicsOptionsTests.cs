// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class GraphicsOptionsTests
    {
        private readonly GraphicsOptions newGraphicsOptions = new GraphicsOptions();
        private readonly GraphicsOptions defaultGraphicsOptions = GraphicsOptions.Default;
        private readonly GraphicsOptions cloneGraphicsOptions = GraphicsOptions.Default.Clone();

        [Fact]
        public void DefaultGraphicsOptionsIsNotNull() => Assert.True(this.defaultGraphicsOptions != null);

        [Fact]
        public void DefaultGraphicsOptionsAntialias()
        {
            Assert.True(this.newGraphicsOptions.Antialias);
            Assert.True(this.defaultGraphicsOptions.Antialias);
            Assert.True(this.cloneGraphicsOptions.Antialias);
        }

        [Fact]
        public void DefaultGraphicsOptionsAntialiasSuppixelDepth()
        {
            const int Expected = 16;
            Assert.Equal(Expected, this.newGraphicsOptions.AntialiasSubpixelDepth);
            Assert.Equal(Expected, this.defaultGraphicsOptions.AntialiasSubpixelDepth);
            Assert.Equal(Expected, this.cloneGraphicsOptions.AntialiasSubpixelDepth);
        }

        [Fact]
        public void DefaultGraphicsOptionsBlendPercentage()
        {
            const float Expected = 1F;
            Assert.Equal(Expected, this.newGraphicsOptions.BlendPercentage);
            Assert.Equal(Expected, this.defaultGraphicsOptions.BlendPercentage);
            Assert.Equal(Expected, this.cloneGraphicsOptions.BlendPercentage);
        }

        [Fact]
        public void DefaultGraphicsOptionsColorBlendingMode()
        {
            const PixelColorBlendingMode Expected = PixelColorBlendingMode.Normal;
            Assert.Equal(Expected, this.newGraphicsOptions.ColorBlendingMode);
            Assert.Equal(Expected, this.defaultGraphicsOptions.ColorBlendingMode);
            Assert.Equal(Expected, this.cloneGraphicsOptions.ColorBlendingMode);
        }

        [Fact]
        public void DefaultGraphicsOptionsAlphaCompositionMode()
        {
            const PixelAlphaCompositionMode Expected = PixelAlphaCompositionMode.SrcOver;
            Assert.Equal(Expected, this.newGraphicsOptions.AlphaCompositionMode);
            Assert.Equal(Expected, this.defaultGraphicsOptions.AlphaCompositionMode);
            Assert.Equal(Expected, this.cloneGraphicsOptions.AlphaCompositionMode);
        }

        [Fact]
        public void IsOpaqueColor()
        {
            Assert.True(new GraphicsOptions().IsOpaqueColorWithoutBlending(Rgba32.Red));
            Assert.False(new GraphicsOptions { BlendPercentage = .5F }.IsOpaqueColorWithoutBlending(Rgba32.Red));
            Assert.False(new GraphicsOptions().IsOpaqueColorWithoutBlending(Rgba32.Transparent));
            Assert.False(new GraphicsOptions { ColorBlendingMode = PixelColorBlendingMode.Lighten, BlendPercentage = 1F }.IsOpaqueColorWithoutBlending(Rgba32.Red));
            Assert.False(new GraphicsOptions { ColorBlendingMode = PixelColorBlendingMode.Normal, AlphaCompositionMode = PixelAlphaCompositionMode.DestOver, BlendPercentage = 1f }.IsOpaqueColorWithoutBlending(Rgba32.Red));
        }
    }
}
