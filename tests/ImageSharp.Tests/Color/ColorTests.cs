// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;

using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public partial class ColorTests
    {
        [Fact]
        public void WithAlpha()
        {
            Color c1 = Color.FromRgba(111, 222, 55, 255);
            Color c2 = c1.WithAlpha(0.5f);

            var expected = new Rgba32(111, 222, 55, 128);

            Assert.Equal(expected, (Rgba32)c2);
        }

        [Fact]
        public void Equality_WhenTrue()
        {
            Color c1 = new Rgba64(100, 2000, 3000, 40000);
            Color c2 = new Rgba64(100, 2000, 3000, 40000);

            Assert.True(c1.Equals(c2));
            Assert.True(c1 == c2);
            Assert.False(c1 != c2);
            Assert.True(c1.GetHashCode() == c2.GetHashCode());
        }

        [Fact]
        public void Equality_WhenFalse()
        {
            Color c1 = new Rgba64(100, 2000, 3000, 40000);
            Color c2 = new Rgba64(101, 2000, 3000, 40000);
            Color c3 = new Rgba64(100, 2000, 3000, 40001);

            Assert.False(c1.Equals(c2));
            Assert.False(c2.Equals(c3));
            Assert.False(c3.Equals(c1));

            Assert.False(c1 == c2);
            Assert.True(c1 != c2);

            Assert.False(c1.Equals(null));
        }

        [Fact]
        public void ToHex()
        {
            string expected = "ABCD1234";
            Color color = Color.FromHex(expected);
            string actual = color.ToHex();

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void WebSafePalette_IsCorrect()
        {
            Rgba32[] actualPalette = Color.WebSafePalette.ToArray().Select(c => (Rgba32)c).ToArray();
            Assert.Equal(ReferencePalette.WebSafeColors, actualPalette);
        }

        [Fact]
        public void WernerPalette_IsCorrect()
        {
            Rgba32[] actualPalette = Color.WernerPalette.ToArray().Select(c => (Rgba32)c).ToArray();
            Assert.Equal(ReferencePalette.WernerColors, actualPalette);
        }

        public class FromHex
        {
            [Fact]
            public void ShortHex()
            {
                Assert.Equal(new Rgb24(255, 255, 255), (Rgb24) Color.FromHex("#fff"));
                Assert.Equal(new Rgb24(255, 255, 255), (Rgb24) Color.FromHex("fff"));
                Assert.Equal(new Rgba32(0, 0, 0, 255), (Rgba32) Color.FromHex("000f"));
            }

            [Fact]
            public void LeadingPoundIsOptional()
            {
                Assert.Equal(new Rgb24(0, 128, 128), (Rgb24) Color.FromHex("#008080"));
                Assert.Equal(new Rgb24(0, 128, 128), (Rgb24) Color.FromHex("008080"));
            }

            [Fact]
            public void ThrowsOnEmpty()
            {
                Assert.Throws<ArgumentException>(() => Color.FromHex(""));
            }

            [Fact]
            public void ThrowsOnNull()
            {
                Assert.Throws<ArgumentNullException>(() => Color.FromHex(null));
            }
        }
    }
}
