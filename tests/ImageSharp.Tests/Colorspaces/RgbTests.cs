// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Tests the <see cref="Rgb"/> struct.
    /// </summary>
    public class RgbTests
    {
        [Fact]
        public void RgbConstructorAssignsFields()
        {
            const float r = .75F;
            const float g = .64F;
            const float b = .87F;
            var rgb = new Rgb(r, g, b);

            Assert.Equal(r, rgb.R);
            Assert.Equal(g, rgb.G);
            Assert.Equal(b, rgb.B);
        }

        [Fact]
        public void RgbEquality()
        {
            var x = default(Rgb);
            var y = new Rgb(Vector3.One);

            Assert.True(default(Rgb) == default(Rgb));
            Assert.False(default(Rgb) != default(Rgb));
            Assert.Equal(default(Rgb), default(Rgb));
            Assert.Equal(new Rgb(1, 0, 1), new Rgb(1, 0, 1));
            Assert.Equal(new Rgb(Vector3.One), new Rgb(Vector3.One));
            Assert.False(x.Equals(y));
            Assert.False(x.Equals((object)y));
            Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
        }

        [Fact]
        public void RgbAndRgb24Operators()
        {
            const byte r = 64;
            const byte g = 128;
            const byte b = 255;

            Rgb24 rgb24 = new Rgb(r / 255F, g / 255F, b / 255F);
            Rgb rgb2 = rgb24;

            Assert.Equal(r, rgb24.R);
            Assert.Equal(g, rgb24.G);
            Assert.Equal(b, rgb24.B);

            Assert.Equal(r / 255F, rgb2.R);
            Assert.Equal(g / 255F, rgb2.G);
            Assert.Equal(b / 255F, rgb2.B);
        }

        [Fact]
        public void RgbAndRgba32Operators()
        {
            const byte r = 64;
            const byte g = 128;
            const byte b = 255;

            Rgba32 rgba32 = new Rgb(r / 255F, g / 255F, b / 255F);
            Rgb rgb2 = rgba32;

            Assert.Equal(r, rgba32.R);
            Assert.Equal(g, rgba32.G);
            Assert.Equal(b, rgba32.B);

            Assert.Equal(r / 255F, rgb2.R);
            Assert.Equal(g / 255F, rgb2.G);
            Assert.Equal(b / 255F, rgb2.B);
        }
    }
}
