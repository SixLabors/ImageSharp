// <copyright file="GlowTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Overlays
{
    using System;
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing.Processors;
    using SixLabors.Primitives;
    using Xunit;

    public class GlowTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Glow_GlowProcessorWithDefaultValues()
        {
            this.operations.Glow();
            var p = this.Verify<GlowProcessor<Rgba32>>();

            Assert.Equal(GraphicsOptions.Default, p.GraphicsOptions);
            Assert.Equal(Rgba32.Black, p.GlowColor);
            Assert.Equal(ValueSize.PercentageOfWidth(.5f), p.Radius);
        }

        [Fact]
        public void Glow_Color_GlowProcessorWithDefaultValues()
        {
            this.operations.Glow(Rgba32.Aquamarine);
            var p = this.Verify<GlowProcessor<Rgba32>>();

            Assert.Equal(GraphicsOptions.Default, p.GraphicsOptions);
            Assert.Equal(Rgba32.Aquamarine, p.GlowColor);
            Assert.Equal(ValueSize.PercentageOfWidth(.5f), p.Radius);
        }

        [Fact]
        public void Glow_Radux_GlowProcessorWithDefaultValues()
        {
            this.operations.Glow(3.5f);
            var p = this.Verify<GlowProcessor<Rgba32>>();

            Assert.Equal(GraphicsOptions.Default, p.GraphicsOptions);
            Assert.Equal(Rgba32.Black, p.GlowColor);
            Assert.Equal(ValueSize.Absolute(3.5f), p.Radius);
        }

        [Fact]
        public void Glow_Rect_GlowProcessorWithDefaultValues()
        {
            var rect = new Rectangle(12, 123, 43, 65);
            this.operations.Glow(rect);
            var p = this.Verify<GlowProcessor<Rgba32>>(rect);

            Assert.Equal(GraphicsOptions.Default, p.GraphicsOptions);
            Assert.Equal(Rgba32.Black, p.GlowColor);
            Assert.Equal(ValueSize.PercentageOfWidth(.5f), p.Radius);
        }
    }
}