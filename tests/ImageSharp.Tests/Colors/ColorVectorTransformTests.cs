// <copyright file="ColorVectorTransformTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Colors
{
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
        private static readonly ColorVector Backdrop = new ColorVector(204, 102, 0);

        /// <summary>
        /// Blue source
        /// </summary>
        private static readonly ColorVector Source = new ColorVector(0, 102, 153);

        [Fact]
        public void Normal()
        {
            ColorVector normal = ColorVector.Normal(Backdrop, Source);
            Assert.True(normal == Source);
        }

        [Fact]
        public void Multiply()
        {
            Assert.Equal(ColorVector.Multiply(Backdrop, ColorVector.Black).ToVector4(), Backdrop.ToVector4(), FloatComparer);
            Assert.Equal(ColorVector.Multiply(Backdrop, ColorVector.White).ToVector4(), ColorVector.White.ToVector4(), FloatComparer);

            ColorVector multiply = ColorVector.Multiply(Backdrop, Source);
            Assert.Equal(multiply.ToVector4(), new ColorVector(0, 41, 0).ToVector4(), FloatComparer);
        }

        [Fact]
        public void Screen()
        {
            Assert.Equal(ColorVector.Screen(Backdrop, ColorVector.Black).ToVector4(), Backdrop.ToVector4(), FloatComparer);
            Assert.Equal(ColorVector.Screen(Backdrop, ColorVector.White).ToVector4(), ColorVector.White.ToVector4(), FloatComparer);

            ColorVector screen = ColorVector.Screen(Backdrop, Source);
            Assert.Equal(screen.ToVector4(), new ColorVector(204, 163, 153).ToVector4(), FloatComparer);
        }

        [Fact]
        public void HardLight()
        {
            ColorVector hardLight = ColorVector.HardLight(Backdrop, Source);
            Assert.Equal(hardLight.ToVector4(), new ColorVector(0, 82, 51).ToVector4(), FloatComparer);
        }

        [Fact]
        public void Overlay()
        {
            ColorVector overlay = ColorVector.Overlay(Backdrop, Source);
            Assert.Equal(overlay.ToVector4(), new ColorVector(153, 82, 0).ToVector4(), FloatComparer);
        }

        [Fact]
        public void Darken()
        {
            ColorVector darken = ColorVector.Darken(Backdrop, Source);
            Assert.Equal(darken.ToVector4(), new ColorVector(0, 102, 0).ToVector4(), FloatComparer);
        }

        [Fact]
        public void Lighten()
        {
            ColorVector lighten = ColorVector.Lighten(Backdrop, Source);
            Assert.Equal(lighten.ToVector4(), new ColorVector(204, 102, 153).ToVector4(), FloatComparer);
        }

        [Fact]
        public void SoftLight()
        {
            ColorVector softLight = ColorVector.SoftLight(Backdrop, Source);
            Assert.Equal(softLight.ToVector4(), new ColorVector(163, 90, 0).ToVector4(), FloatComparer);
        }

        [Fact]
        public void ColorDodge()
        {
            ColorVector colorDodge = ColorVector.ColorDodge(Backdrop, Source);
            Assert.Equal(colorDodge.ToVector4(), new ColorVector(204, 170, 0).ToVector4(), FloatComparer);
        }

        [Fact]
        public void ColorBurn()
        {
            ColorVector colorBurn = ColorVector.ColorBurn(Backdrop, Source);
            Assert.Equal(colorBurn.ToVector4(), new ColorVector(0, 0, 0).ToVector4(), FloatComparer);
        }

        [Fact]
        public void Difference()
        {
            ColorVector difference = ColorVector.Difference(Backdrop, Source);
            Assert.Equal(difference.ToVector4(), new ColorVector(204, 0, 153).ToVector4(), FloatComparer);
        }

        [Fact]
        public void Exclusion()
        {
            ColorVector exclusion = ColorVector.Exclusion(Backdrop, Source);
            Assert.Equal(exclusion.ToVector4(), new ColorVector(204, 122, 153).ToVector4(), FloatComparer);
        }
    }
}
