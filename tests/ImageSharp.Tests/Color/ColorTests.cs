// Copyright (c) Six Labors.
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
            var c1 = Color.FromRgba(111, 222, 55, 255);
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
            var color = Color.ParseHex(expected);
            string actual = color.ToHex();

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void WebSafePalette_IsCorrect()
        {
            Rgba32[] actualPalette = Color.WebSafePalette.ToArray().Select(c => (Rgba32)c).ToArray();

            for (int i = 0; i < ReferencePalette.WebSafeColors.Length; i++)
            {
                Assert.Equal((Rgba32)ReferencePalette.WebSafeColors[i], actualPalette[i]);
            }
        }

        [Fact]
        public void WernerPalette_IsCorrect()
        {
            Rgba32[] actualPalette = Color.WernerPalette.ToArray().Select(c => (Rgba32)c).ToArray();

            for (int i = 0; i < ReferencePalette.WernerColors.Length; i++)
            {
                Assert.Equal((Rgba32)ReferencePalette.WernerColors[i], actualPalette[i]);
            }
        }

        public class FromHex
        {
            [Fact]
            public void ShortHex()
            {
                Assert.Equal(new Rgb24(255, 255, 255), (Rgb24)Color.ParseHex("#fff"));
                Assert.Equal(new Rgb24(255, 255, 255), (Rgb24)Color.ParseHex("fff"));
                Assert.Equal(new Rgba32(0, 0, 0, 255), (Rgba32)Color.ParseHex("000f"));
            }

            [Fact]
            public void TryShortHex()
            {
                Assert.True(Color.TryParseHex("#fff", out Color actual));
                Assert.Equal(new Rgb24(255, 255, 255), (Rgb24)actual);

                Assert.True(Color.TryParseHex("fff", out actual));
                Assert.Equal(new Rgb24(255, 255, 255), (Rgb24)actual);

                Assert.True(Color.TryParseHex("000f", out actual));
                Assert.Equal(new Rgba32(0, 0, 0, 255), (Rgba32)actual);
            }

            [Fact]
            public void LeadingPoundIsOptional()
            {
                Assert.Equal(new Rgb24(0, 128, 128), (Rgb24)Color.ParseHex("#008080"));
                Assert.Equal(new Rgb24(0, 128, 128), (Rgb24)Color.ParseHex("008080"));
            }

            [Fact]
            public void ThrowsOnEmpty()
            {
                Assert.Throws<ArgumentException>(() => Color.ParseHex(string.Empty));
            }

            [Fact]
            public void ThrowsOnInvalid()
            {
                Assert.Throws<ArgumentException>(() => Color.ParseHex("!"));
            }

            [Fact]
            public void ThrowsOnNull()
            {
                Assert.Throws<ArgumentNullException>(() => Color.ParseHex(null));
            }

            [Fact]
            public void FalseOnEmpty()
            {
                Assert.False(Color.TryParseHex(string.Empty, out Color _));
            }

            [Fact]
            public void FalseOnInvalid()
            {
                Assert.False(Color.TryParseHex("!", out Color _));
            }

            [Fact]
            public void FalseOnNull()
            {
                Assert.False(Color.TryParseHex(null, out Color _));
            }
        }

        public class FromString
        {
            [Fact]
            public void ColorNames()
            {
                foreach (string name in ReferencePalette.ColorNames.Keys)
                {
                    Rgba32 expected = ReferencePalette.ColorNames[name];
                    Assert.Equal(expected, (Rgba32)Color.Parse(name));
                    Assert.Equal(expected, (Rgba32)Color.Parse(name.ToLowerInvariant()));
                    Assert.Equal(expected, (Rgba32)Color.Parse(expected.ToHex()));
                }
            }

            [Fact]
            public void TryColorNames()
            {
                foreach (string name in ReferencePalette.ColorNames.Keys)
                {
                    Rgba32 expected = ReferencePalette.ColorNames[name];

                    Assert.True(Color.TryParse(name, out Color actual));
                    Assert.Equal(expected, (Rgba32)actual);

                    Assert.True(Color.TryParse(name.ToLowerInvariant(), out actual));
                    Assert.Equal(expected, (Rgba32)actual);

                    Assert.True(Color.TryParse(expected.ToHex(), out actual));
                    Assert.Equal(expected, (Rgba32)actual);
                }
            }

            [Fact]
            public void ThrowsOnEmpty()
            {
                Assert.Throws<ArgumentException>(() => Color.Parse(string.Empty));
            }

            [Fact]
            public void ThrowsOnInvalid()
            {
                Assert.Throws<ArgumentException>(() => Color.Parse("!"));
            }

            [Fact]
            public void ThrowsOnNull()
            {
                Assert.Throws<ArgumentNullException>(() => Color.Parse(null));
            }

            [Fact]
            public void FalseOnEmpty()
            {
                Assert.False(Color.TryParse(string.Empty, out Color _));
            }

            [Fact]
            public void FalseOnInvalid()
            {
                Assert.False(Color.TryParse("!", out Color _));
            }

            [Fact]
            public void FalseOnNull()
            {
                Assert.False(Color.TryParse(null, out Color _));
            }
        }
    }
}
