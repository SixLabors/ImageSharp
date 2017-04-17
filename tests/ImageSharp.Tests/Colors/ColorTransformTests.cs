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
        private static readonly Color Backdrop = new Color(204, 102, 0);

        /// <summary>
        /// Blue source
        /// </summary>
        private static readonly Color Source = new Color(0, 102, 153);

        [Fact]
        public void Normal()
        {
            Color normal = Color.Normal(Backdrop, Source);
            Assert.True(normal == Source);
        }

        [Fact]
        public void Multiply()
        {
            Assert.True(Color.Multiply(Backdrop, Color.Black) == Color.Black);
            Assert.True(Color.Multiply(Backdrop, Color.White) == Backdrop);

            Color multiply = Color.Multiply(Backdrop, Source);
            Assert.True(multiply == new Color(0, 41, 0));
        }

        [Fact]
        public void Screen()
        {
            Assert.True(Color.Screen(Backdrop, Color.Black) == Backdrop);
            Assert.True(Color.Screen(Backdrop, Color.White) == Color.White);

            Color screen = Color.Screen(Backdrop, Source);
            Assert.True(screen == new Color(204, 163, 153));
        }

        [Fact]
        public void HardLight()
        {
            Color hardLight = Color.HardLight(Backdrop, Source);
            Assert.True(hardLight == new Color(0, 82, 51));
        }

        [Fact]
        public void Overlay()
        {
            Color overlay = Color.Overlay(Backdrop, Source);
            Assert.True(overlay == new Color(153, 82, 0));
        }

        [Fact]
        public void Darken()
        {
            Color darken = Color.Darken(Backdrop, Source);
            Assert.True(darken == new Color(0, 102, 0));
        }

        [Fact]
        public void Lighten()
        {
            Color lighten = Color.Lighten(Backdrop, Source);
            Assert.True(lighten == new Color(204, 102, 153));
        }

        [Fact]
        public void SoftLight()
        {
            Color softLight = Color.SoftLight(Backdrop, Source);
            Assert.True(softLight == new Color(163, 90, 0));
        }

        [Fact]
        public void ColorDodge()
        {
            Color colorDodge = Color.ColorDodge(Backdrop, Source);
            Assert.True(colorDodge == new Color(204, 170, 0));
        }

        [Fact]
        public void ColorBurn()
        {
            Color colorBurn = Color.ColorBurn(Backdrop, Source);
            Assert.True(colorBurn == new Color(0, 0, 0));
        }

        [Fact]
        public void Difference()
        {
            Color difference = Color.Difference(Backdrop, Source);
            Assert.True(difference == new Color(204, 0, 153));
        }

        [Fact]
        public void Exclusion()
        {
            Color exclusion = Color.Exclusion(Backdrop, Source);
            Assert.True(exclusion == new Color(204, 122, 153));
        }
    }
}
