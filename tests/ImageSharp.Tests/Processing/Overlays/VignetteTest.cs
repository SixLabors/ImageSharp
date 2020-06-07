// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Overlays;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Overlays
{
    public class VignetteTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Vignette_VignetteProcessorWithDefaultValues()
        {
            this.operations.Vignette();
            VignetteProcessor p = this.Verify<VignetteProcessor>();

            Assert.Equal(this.options, p.GraphicsOptions);
            Assert.Equal(Color.Black, p.VignetteColor);
            Assert.Equal(ValueSize.PercentageOfWidth(.5f), p.RadiusX);
            Assert.Equal(ValueSize.PercentageOfHeight(.5f), p.RadiusY);
        }

        [Fact]
        public void Vignette_Color_VignetteProcessorWithDefaultValues()
        {
            this.operations.Vignette(Color.Aquamarine);
            VignetteProcessor p = this.Verify<VignetteProcessor>();

            Assert.Equal(this.options, p.GraphicsOptions);
            Assert.Equal(Color.Aquamarine, p.VignetteColor);
            Assert.Equal(ValueSize.PercentageOfWidth(.5f), p.RadiusX);
            Assert.Equal(ValueSize.PercentageOfHeight(.5f), p.RadiusY);
        }

        [Fact]
        public void Vignette_Radux_VignetteProcessorWithDefaultValues()
        {
            this.operations.Vignette(3.5f, 12123f);
            VignetteProcessor p = this.Verify<VignetteProcessor>();

            Assert.Equal(this.options, p.GraphicsOptions);
            Assert.Equal(Color.Black, p.VignetteColor);
            Assert.Equal(ValueSize.Absolute(3.5f), p.RadiusX);
            Assert.Equal(ValueSize.Absolute(12123f), p.RadiusY);
        }

        [Fact]
        public void Vignette_Rect_VignetteProcessorWithDefaultValues()
        {
            var rect = new Rectangle(12, 123, 43, 65);
            this.operations.Vignette(rect);
            VignetteProcessor p = this.Verify<VignetteProcessor>(rect);

            Assert.Equal(this.options, p.GraphicsOptions);
            Assert.Equal(Color.Black, p.VignetteColor);
            Assert.Equal(ValueSize.PercentageOfWidth(.5f), p.RadiusX);
            Assert.Equal(ValueSize.PercentageOfHeight(.5f), p.RadiusY);
        }
    }
}
