// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests;

public partial class ColorTests
{
    [Fact]
    public void WithAlpha()
    {
        Color c1 = Color.FromPixel(new Rgba32(111, 222, 55, 255));
        Color c2 = c1.WithAlpha(0.5f);

        Rgba32 expected = new(111, 222, 55, 128);

        Assert.Equal(expected, c2.ToPixel<Rgba32>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Equality_WhenTrue(bool highPrecision)
    {
        Color c1 = Color.FromPixel(new Rgba64(100, 2000, 3000, 40000));
        Color c2 = Color.FromPixel(new Rgba64(100, 2000, 3000, 40000));

        if (highPrecision)
        {
            c1 = Color.FromPixel(c1.ToPixel<RgbaDouble>());
            c2 = Color.FromPixel(c2.ToPixel<RgbaDouble>());
        }

        Assert.True(c1.Equals(c2));
        Assert.True(c1 == c2);
        Assert.False(c1 != c2);
        Assert.True(c1.GetHashCode() == c2.GetHashCode());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Equality_WhenFalse(bool highPrecision)
    {
        Color c1 = Color.FromPixel(new Rgba64(100, 2000, 3000, 40000));
        Color c2 = Color.FromPixel(new Rgba64(101, 2000, 3000, 40000));
        Color c3 = Color.FromPixel(new Rgba64(100, 2000, 3000, 40001));

        if (highPrecision)
        {
            c1 = Color.FromPixel(c1.ToPixel<RgbaDouble>());
            c2 = Color.FromPixel(c2.ToPixel<RgbaDouble>());
            c3 = Color.FromPixel(c3.ToPixel<RgbaDouble>());
        }

        Assert.False(c1.Equals(c2));
        Assert.False(c2.Equals(c3));
        Assert.False(c3.Equals(c1));

        Assert.False(c1 == c2);
        Assert.True(c1 != c2);

        Assert.False(c1.Equals(null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ToHex(bool highPrecision)
    {
        string expected = "ABCD1234";
        Color color = Color.ParseHex(expected);

        if (highPrecision)
        {
            color = Color.FromPixel(color.ToPixel<RgbaDouble>());
        }

        string actual = color.ToHex();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WebSafePalette_IsCorrect()
    {
        Rgba32[] actualPalette = Color.WebSafePalette.ToArray().Select(c => c.ToPixel<Rgba32>()).ToArray();

        for (int i = 0; i < ReferencePalette.WebSafeColors.Length; i++)
        {
            Assert.Equal(ReferencePalette.WebSafeColors[i].ToPixel<Rgba32>(), actualPalette[i]);
        }
    }

    [Fact]
    public void WernerPalette_IsCorrect()
    {
        Rgba32[] actualPalette = Color.WernerPalette.ToArray().Select(c => c.ToPixel<Rgba32>()).ToArray();

        for (int i = 0; i < ReferencePalette.WernerColors.Length; i++)
        {
            Assert.Equal(ReferencePalette.WernerColors[i].ToPixel<Rgba32>(), actualPalette[i]);
        }
    }

    public class FromHex
    {
        [Fact]
        public void ShortHex()
        {
            Assert.Equal(new Rgb24(255, 255, 255), Color.ParseHex("#fff").ToPixel<Rgb24>());
            Assert.Equal(new Rgb24(255, 255, 255), Color.ParseHex("fff").ToPixel<Rgb24>());
            Assert.Equal(new Rgba32(0, 0, 0, 255), Color.ParseHex("000f").ToPixel<Rgba32>());
        }

        [Fact]
        public void TryShortHex()
        {
            Assert.True(Color.TryParseHex("#fff", out Color actual));
            Assert.Equal(new Rgb24(255, 255, 255), actual.ToPixel<Rgb24>());

            Assert.True(Color.TryParseHex("fff", out actual));
            Assert.Equal(new Rgb24(255, 255, 255), actual.ToPixel<Rgb24>());

            Assert.True(Color.TryParseHex("000f", out actual));
            Assert.Equal(new Rgba32(0, 0, 0, 255), actual.ToPixel<Rgba32>());
        }

        [Fact]
        public void LeadingPoundIsOptional()
        {
            Assert.Equal(new Rgb24(0, 128, 128), Color.ParseHex("#008080").ToPixel<Rgb24>());
            Assert.Equal(new Rgb24(0, 128, 128), Color.ParseHex("008080").ToPixel<Rgb24>());
        }

        [Fact]
        public void ThrowsOnEmpty() => Assert.Throws<ArgumentException>(() => Color.ParseHex(string.Empty));

        [Fact]
        public void ThrowsOnInvalid() => Assert.Throws<ArgumentException>(() => Color.ParseHex("!"));

        [Fact]
        public void ThrowsOnNull() => Assert.Throws<ArgumentNullException>(() => Color.ParseHex(null));

        [Fact]
        public void FalseOnEmpty() => Assert.False(Color.TryParseHex(string.Empty, out Color _));

        [Fact]
        public void FalseOnInvalid() => Assert.False(Color.TryParseHex("!", out Color _));

        [Fact]
        public void FalseOnNull() => Assert.False(Color.TryParseHex(null, out Color _));
    }

    public class FromString
    {
        [Fact]
        public void ColorNames()
        {
            foreach (string name in ReferencePalette.ColorNames.Keys)
            {
                Rgba32 expected = ReferencePalette.ColorNames[name].ToPixel<Rgba32>();
                Assert.Equal(expected, Color.Parse(name).ToPixel<Rgba32>());
                Assert.Equal(expected, Color.Parse(name.ToLowerInvariant()).ToPixel<Rgba32>());
                Assert.Equal(expected, Color.Parse(expected.ToHex()).ToPixel<Rgba32>());
            }
        }

        [Fact]
        public void TryColorNames()
        {
            foreach (string name in ReferencePalette.ColorNames.Keys)
            {
                Rgba32 expected = ReferencePalette.ColorNames[name].ToPixel<Rgba32>();

                Assert.True(Color.TryParse(name, out Color actual));
                Assert.Equal(expected, actual.ToPixel<Rgba32>());

                Assert.True(Color.TryParse(name.ToLowerInvariant(), out actual));
                Assert.Equal(expected, actual.ToPixel<Rgba32>());

                Assert.True(Color.TryParse(expected.ToHex(), out actual));
                Assert.Equal(expected, actual.ToPixel<Rgba32>());
            }
        }

        [Fact]
        public void ThrowsOnEmpty() => Assert.Throws<ArgumentException>(() => Color.Parse(string.Empty));

        [Fact]
        public void ThrowsOnInvalid() => Assert.Throws<ArgumentException>(() => Color.Parse("!"));

        [Fact]
        public void ThrowsOnNull() => Assert.Throws<ArgumentNullException>(() => Color.Parse(null));

        [Fact]
        public void FalseOnEmpty() => Assert.False(Color.TryParse(string.Empty, out Color _));

        [Fact]
        public void FalseOnInvalid() => Assert.False(Color.TryParse("!", out Color _));

        [Fact]
        public void FalseOnNull() => Assert.False(Color.TryParse(null, out Color _));
    }
}
