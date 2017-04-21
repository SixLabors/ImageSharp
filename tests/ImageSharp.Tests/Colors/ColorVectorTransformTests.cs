// <copyright file="ColorVectorTransformTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Colors
{
    using ImageSharp.PixelFormats;
    using Xunit;

    /// <summary>
    /// Tests the color transform algorithms. Test results match the output of CSS equivalents.
    /// <see href="https://jsfiddle.net/jamessouth/L1v8r6kh/"/>
    /// </summary>
    public class ColorVectorTransformTests
    {
        private static readonly ApproximateFloatComparer FloatComparer = new ApproximateFloatComparer(0.01F);

        /// <summary>
        /// Orange backdrop
        /// </summary>
        private static readonly RgbaVector Backdrop = new RgbaVector(204, 102, 0);

        /// <summary>
        /// Blue source
        /// </summary>
        private static readonly RgbaVector Source = new RgbaVector(0, 102, 153);

        [Fact]
        public void Normal()
        {
            RgbaVector normal = RgbaVector.Normal(Backdrop, Source);
            Assert.True(normal == Source);
        }

        [Fact]
        public void Multiply()
        {
            Assert.Equal(RgbaVector.Multiply(Backdrop, RgbaVector.Black).ToVector4(), Rgba32.Black.ToVector4(), FloatComparer);
            Assert.Equal(RgbaVector.Multiply(Backdrop, RgbaVector.White).ToVector4(), Backdrop.ToVector4(), FloatComparer);

            RgbaVector multiply = RgbaVector.Multiply(Backdrop, Source);
            Assert.Equal(multiply.ToVector4(), new RgbaVector(0, 41, 0).ToVector4(), FloatComparer);
        }

        [Fact]
        public void Screen()
        {
            Assert.Equal(RgbaVector.Screen(Backdrop, RgbaVector.Black).ToVector4(), Backdrop.ToVector4(), FloatComparer);
            Assert.Equal(RgbaVector.Screen(Backdrop, RgbaVector.White).ToVector4(), RgbaVector.White.ToVector4(), FloatComparer);

            RgbaVector screen = RgbaVector.Screen(Backdrop, Source);
            Assert.Equal(screen.ToVector4(), new RgbaVector(204, 163, 153).ToVector4(), FloatComparer);
        }

        [Fact]
        public void HardLight()
        {
            RgbaVector hardLight = RgbaVector.HardLight(Backdrop, Source);
            Assert.Equal(hardLight.ToVector4(), new RgbaVector(0, 82, 51).ToVector4(), FloatComparer);
        }

        [Fact]
        public void Overlay()
        {
            RgbaVector overlay = RgbaVector.Overlay(Backdrop, Source);
            Assert.Equal(overlay.ToVector4(), new RgbaVector(153, 82, 0).ToVector4(), FloatComparer);
        }

        [Fact]
        public void Darken()
        {
            RgbaVector darken = RgbaVector.Darken(Backdrop, Source);
            Assert.Equal(darken.ToVector4(), new RgbaVector(0, 102, 0).ToVector4(), FloatComparer);
        }

        [Fact]
        public void Lighten()
        {
            RgbaVector lighten = RgbaVector.Lighten(Backdrop, Source);
            Assert.Equal(lighten.ToVector4(), new RgbaVector(204, 102, 153).ToVector4(), FloatComparer);
        }

        [Fact]
        public void SoftLight()
        {
            RgbaVector softLight = RgbaVector.SoftLight(Backdrop, Source);
            Assert.Equal(softLight.ToVector4(), new RgbaVector(163, 90, 0).ToVector4(), FloatComparer);
        }

        [Fact]
        public void ColorDodge()
        {
            RgbaVector colorDodge = RgbaVector.ColorDodge(Backdrop, Source);
            Assert.Equal(colorDodge.ToVector4(), new RgbaVector(204, 170, 0).ToVector4(), FloatComparer);
        }

        [Fact]
        public void ColorBurn()
        {
            RgbaVector colorBurn = RgbaVector.ColorBurn(Backdrop, Source);
            Assert.Equal(colorBurn.ToVector4(), new RgbaVector(0, 0, 0).ToVector4(), FloatComparer);
        }

        [Fact]
        public void Difference()
        {
            RgbaVector difference = RgbaVector.Difference(Backdrop, Source);
            Assert.Equal(difference.ToVector4(), new RgbaVector(204, 0, 153).ToVector4(), FloatComparer);
        }

        [Fact]
        public void Exclusion()
        {
            RgbaVector exclusion = RgbaVector.Exclusion(Backdrop, Source);
            Assert.Equal(exclusion.ToVector4(), new RgbaVector(204, 122, 153).ToVector4(), FloatComparer);
        }
    }
}
