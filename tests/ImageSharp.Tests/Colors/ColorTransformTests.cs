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
        private static readonly Color32 Backdrop = new Color32(204, 102, 0);

        /// <summary>
        /// Blue source
        /// </summary>
        private static readonly Color32 Source = new Color32(0, 102, 153);

        [Fact]
        public void Normal()
        {
            Color32 normal = Color32.Normal(Backdrop, Source);
            Assert.True(normal == Source);
        }

        [Fact]
        public void Multiply()
        {
            Assert.True(Color32.Multiply(Backdrop, Color32.Black) == Color32.Black);
            Assert.True(Color32.Multiply(Backdrop, Color32.White) == Backdrop);

            Color32 multiply = Color32.Multiply(Backdrop, Source);
            Assert.True(multiply == new Color32(0, 41, 0));
        }

        [Fact]
        public void Screen()
        {
            Assert.True(Color32.Screen(Backdrop, Color32.Black) == Backdrop);
            Assert.True(Color32.Screen(Backdrop, Color32.White) == Color32.White);

            Color32 screen = Color32.Screen(Backdrop, Source);
            Assert.True(screen == new Color32(204, 163, 153));
        }

        [Fact]
        public void HardLight()
        {
            Color32 hardLight = Color32.HardLight(Backdrop, Source);
            Assert.True(hardLight == new Color32(0, 82, 51));
        }

        [Fact]
        public void Overlay()
        {
            Color32 overlay = Color32.Overlay(Backdrop, Source);
            Assert.True(overlay == new Color32(153, 82, 0));
        }

        [Fact]
        public void Darken()
        {
            Color32 darken = Color32.Darken(Backdrop, Source);
            Assert.True(darken == new Color32(0, 102, 0));
        }

        [Fact]
        public void Lighten()
        {
            Color32 lighten = Color32.Lighten(Backdrop, Source);
            Assert.True(lighten == new Color32(204, 102, 153));
        }

        [Fact]
        public void SoftLight()
        {
            Color32 softLight = Color32.SoftLight(Backdrop, Source);
            Assert.True(softLight == new Color32(163, 90, 0));
        }

        [Fact]
        public void ColorDodge()
        {
            Color32 colorDodge = Color32.ColorDodge(Backdrop, Source);
            Assert.True(colorDodge == new Color32(204, 170, 0));
        }

        [Fact]
        public void ColorBurn()
        {
            Color32 colorBurn = Color32.ColorBurn(Backdrop, Source);
            Assert.True(colorBurn == new Color32(0, 0, 0));
        }

        [Fact]
        public void Difference()
        {
            Color32 difference = Color32.Difference(Backdrop, Source);
            Assert.True(difference == new Color32(204, 0, 153));
        }

        [Fact]
        public void Exclusion()
        {
            Color32 exclusion = Color32.Exclusion(Backdrop, Source);
            Assert.True(exclusion == new Color32(204, 122, 153));
        }
    }
}
