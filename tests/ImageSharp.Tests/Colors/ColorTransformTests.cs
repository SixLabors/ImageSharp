// <copyright file="ColorTransformTests.cs" company="James Jackson-South">
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
    public class ColorTransformTests
    {
        /// <summary>
        /// Orange backdrop
        /// </summary>
        private static readonly Rgba32 Backdrop = new Rgba32(204, 102, 0);

        /// <summary>
        /// Blue source
        /// </summary>
        private static readonly Rgba32 Source = new Rgba32(0, 102, 153);

        [Fact]
        public void Normal()
        {
            Rgba32 normal = Rgba32.Normal(Backdrop, Source);
            Assert.True(normal == Source);
        }

        [Fact]
        public void Multiply()
        {
            Assert.True(Rgba32.Multiply(Backdrop, Rgba32.Black) == Rgba32.Black);
            Assert.True(Rgba32.Multiply(Backdrop, Rgba32.White) == Backdrop);

            Rgba32 multiply = Rgba32.Multiply(Backdrop, Source);
            Assert.True(multiply == new Rgba32(0, 41, 0));
        }

        [Fact]
        public void Screen()
        {
            Assert.True(Rgba32.Screen(Backdrop, Rgba32.Black) == Backdrop);
            Assert.True(Rgba32.Screen(Backdrop, Rgba32.White) == Rgba32.White);

            Rgba32 screen = Rgba32.Screen(Backdrop, Source);
            Assert.True(screen == new Rgba32(204, 163, 153));
        }

        [Fact]
        public void HardLight()
        {
            Rgba32 hardLight = Rgba32.HardLight(Backdrop, Source);
            Assert.True(hardLight == new Rgba32(0, 82, 51));
        }

        [Fact]
        public void Overlay()
        {
            Rgba32 overlay = Rgba32.Overlay(Backdrop, Source);
            Assert.True(overlay == new Rgba32(153, 82, 0));
        }

        [Fact]
        public void Darken()
        {
            Rgba32 darken = Rgba32.Darken(Backdrop, Source);
            Assert.True(darken == new Rgba32(0, 102, 0));
        }

        [Fact]
        public void Lighten()
        {
            Rgba32 lighten = Rgba32.Lighten(Backdrop, Source);
            Assert.True(lighten == new Rgba32(204, 102, 153));
        }

        [Fact]
        public void SoftLight()
        {
            Rgba32 softLight = Rgba32.SoftLight(Backdrop, Source);
            Assert.True(softLight == new Rgba32(163, 90, 0));
        }

        [Fact]
        public void ColorDodge()
        {
            Rgba32 colorDodge = Rgba32.ColorDodge(Backdrop, Source);
            Assert.True(colorDodge == new Rgba32(204, 170, 0));
        }

        [Fact]
        public void ColorBurn()
        {
            Rgba32 colorBurn = Rgba32.ColorBurn(Backdrop, Source);
            Assert.True(colorBurn == new Rgba32(0, 0, 0));
        }

        [Fact]
        public void Difference()
        {
            Rgba32 difference = Rgba32.Difference(Backdrop, Source);
            Assert.True(difference == new Rgba32(204, 0, 153));
        }

        [Fact]
        public void Exclusion()
        {
            Rgba32 exclusion = Rgba32.Exclusion(Backdrop, Source);
            Assert.True(exclusion == new Rgba32(204, 122, 153));
        }
    }
}
