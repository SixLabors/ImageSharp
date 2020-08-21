// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class GraphicsOptionsTests
    {
        private static readonly GraphicsOptionsComparer GraphicsOptionsComparer = new GraphicsOptionsComparer();
        private readonly GraphicsOptions newGraphicsOptions = new GraphicsOptions();
        private readonly GraphicsOptions cloneGraphicsOptions = new GraphicsOptions().DeepClone();

        [Fact]
        public void CloneGraphicsOptionsIsNotNull() => Assert.True(this.cloneGraphicsOptions != null);

        [Fact]
        public void DefaultGraphicsOptionsAntialias()
        {
            Assert.True(this.newGraphicsOptions.Antialias);
            Assert.True(this.cloneGraphicsOptions.Antialias);
        }

        [Fact]
        public void DefaultGraphicsOptionsAntialiasSuppixelDepth()
        {
            const int Expected = 16;
            Assert.Equal(Expected, this.newGraphicsOptions.AntialiasSubpixelDepth);
            Assert.Equal(Expected, this.cloneGraphicsOptions.AntialiasSubpixelDepth);
        }

        [Fact]
        public void DefaultGraphicsOptionsBlendPercentage()
        {
            const float Expected = 1F;
            Assert.Equal(Expected, this.newGraphicsOptions.BlendPercentage);
            Assert.Equal(Expected, this.cloneGraphicsOptions.BlendPercentage);
        }

        [Fact]
        public void DefaultGraphicsOptionsColorBlendingMode()
        {
            const PixelColorBlendingMode Expected = PixelColorBlendingMode.Normal;
            Assert.Equal(Expected, this.newGraphicsOptions.ColorBlendingMode);
            Assert.Equal(Expected, this.cloneGraphicsOptions.ColorBlendingMode);
        }

        [Fact]
        public void DefaultGraphicsOptionsAlphaCompositionMode()
        {
            const PixelAlphaCompositionMode Expected = PixelAlphaCompositionMode.SrcOver;
            Assert.Equal(Expected, this.newGraphicsOptions.AlphaCompositionMode);
            Assert.Equal(Expected, this.cloneGraphicsOptions.AlphaCompositionMode);
        }

        [Fact]
        public void NonDefaultClone()
        {
            var expected = new GraphicsOptions
            {
                AlphaCompositionMode = PixelAlphaCompositionMode.DestAtop,
                Antialias = false,
                AntialiasSubpixelDepth = 23,
                BlendPercentage = .25F,
                ColorBlendingMode = PixelColorBlendingMode.HardLight,
            };

            GraphicsOptions actual = expected.DeepClone();

            Assert.Equal(expected, actual, GraphicsOptionsComparer);
        }

        [Fact]
        public void CloneIsDeep()
        {
            var expected = new GraphicsOptions();
            GraphicsOptions actual = expected.DeepClone();

            actual.AlphaCompositionMode = PixelAlphaCompositionMode.DestAtop;
            actual.Antialias = false;
            actual.AntialiasSubpixelDepth = 23;
            actual.BlendPercentage = .25F;
            actual.ColorBlendingMode = PixelColorBlendingMode.HardLight;

            Assert.NotEqual(expected, actual, GraphicsOptionsComparer);
        }
    }
}
