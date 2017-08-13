// <copyright file="VignetteTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Overlays
{
    using System;
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing.Processors;
    using ImageSharp.Tests.TestUtilities;
    using SixLabors.Primitives;
    using Xunit;

    public class VignetteTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Vignette_VignetteProcessorWithDefaultValues()
        {
            this.operations.Vignette();
            var p = this.Verify<VignetteProcessor<Rgba32>>();

            Assert.Equal(GraphicsOptions.Default, p.GraphicsOptions);
            Assert.Equal(Rgba32.Black, p.VignetteColor);
            Assert.Equal(ValueSize.PercentageOfWidth(.5f), p.RadiusX);
            Assert.Equal(ValueSize.PercentageOfHeight(.5f), p.RadiusY);
        }

        [Fact]
        public void Vignette_Color_VignetteProcessorWithDefaultValues()
        {
            this.operations.Vignette(Rgba32.Aquamarine);
            var p = this.Verify<VignetteProcessor<Rgba32>>();

            Assert.Equal(GraphicsOptions.Default, p.GraphicsOptions);
            Assert.Equal(Rgba32.Aquamarine, p.VignetteColor);
            Assert.Equal(ValueSize.PercentageOfWidth(.5f), p.RadiusX);
            Assert.Equal(ValueSize.PercentageOfHeight(.5f), p.RadiusY);
        }

        [Fact]
        public void Vignette_Radux_VignetteProcessorWithDefaultValues()
        {
            this.operations.Vignette(3.5f, 12123f);
            var p = this.Verify<VignetteProcessor<Rgba32>>();

            Assert.Equal(GraphicsOptions.Default, p.GraphicsOptions);
            Assert.Equal(Rgba32.Black, p.VignetteColor);
            Assert.Equal(ValueSize.Absolute(3.5f), p.RadiusX);
            Assert.Equal(ValueSize.Absolute(12123f), p.RadiusY);
        }

        [Fact]
        public void Vignette_Rect_VignetteProcessorWithDefaultValues()
        {
            var rect = new Rectangle(12, 123, 43, 65);
            this.operations.Vignette(rect);
            var p = this.Verify<VignetteProcessor<Rgba32>>(rect);

            Assert.Equal(GraphicsOptions.Default, p.GraphicsOptions);
            Assert.Equal(Rgba32.Black, p.VignetteColor);
            Assert.Equal(ValueSize.PercentageOfWidth(.5f), p.RadiusX);
            Assert.Equal(ValueSize.PercentageOfHeight(.5f), p.RadiusY);
        }
    }
}