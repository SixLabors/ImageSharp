// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Overlays;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Overlays
{
    public class GlowTest : BaseImageOperationsExtensionTest
    {
        private static readonly GraphicsOptionsComparer graphicsOptionsComparer = new GraphicsOptionsComparer();

        [Fact]
        public void Glow_GlowProcessorWithDefaultValues()
        {
            this.operations.Glow();
            GlowProcessor p = this.Verify<GlowProcessor>();

            Assert.Equal(new GraphicsOptions(), p.GraphicsOptions, graphicsOptionsComparer);
            Assert.Equal(Color.Black, p.GlowColor);
            Assert.Equal(ValueSize.PercentageOfWidth(.5f), p.Radius);
        }

        [Fact]
        public void Glow_Color_GlowProcessorWithDefaultValues()
        {
            this.operations.Glow(Rgba32.Aquamarine);
            GlowProcessor p = this.Verify<GlowProcessor>();

            Assert.Equal(new GraphicsOptions(), p.GraphicsOptions, graphicsOptionsComparer);
            Assert.Equal(Color.Aquamarine, p.GlowColor);
            Assert.Equal(ValueSize.PercentageOfWidth(.5f), p.Radius);
        }

        [Fact]
        public void Glow_Radux_GlowProcessorWithDefaultValues()
        {
            this.operations.Glow(3.5f);
            GlowProcessor p = this.Verify<GlowProcessor>();

            Assert.Equal(new GraphicsOptions(), p.GraphicsOptions, graphicsOptionsComparer);
            Assert.Equal(Color.Black, p.GlowColor);
            Assert.Equal(ValueSize.Absolute(3.5f), p.Radius);
        }

        [Fact]
        public void Glow_Rect_GlowProcessorWithDefaultValues()
        {
            var rect = new Rectangle(12, 123, 43, 65);
            this.operations.Glow(rect);
            GlowProcessor p = this.Verify<GlowProcessor>(rect);

            Assert.Equal(new GraphicsOptions(), p.GraphicsOptions, graphicsOptionsComparer);
            Assert.Equal(Color.Black, p.GlowColor);
            Assert.Equal(ValueSize.PercentageOfWidth(.5f), p.Radius);
        }
    }
}
