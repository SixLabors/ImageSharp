// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    [Trait("Category", "PixelFormats")]
    public class Rgb24Tests
    {
        public static readonly TheoryData<byte, byte, byte> ColorData =
            new TheoryData<byte, byte, byte>
            {
                { 1, 2, 3 },
                { 4, 5, 6 },
                { 0, 255, 42 }
            };

        [Theory]
        [MemberData(nameof(ColorData))]
        public void Constructor(byte r, byte g, byte b)
        {
            var p = new Rgb24(r, g, b);

            Assert.Equal(r, p.R);
            Assert.Equal(g, p.G);
            Assert.Equal(b, p.B);
        }

        [Fact]
        public unsafe void ByteLayoutIsSequentialRgb()
        {
            var color = new Rgb24(1, 2, 3);
            byte* ptr = (byte*)&color;

            Assert.Equal(1, ptr[0]);
            Assert.Equal(2, ptr[1]);
            Assert.Equal(3, ptr[2]);
        }

        [Theory]
        [MemberData(nameof(ColorData))]
        public void Equals_WhenTrue(byte r, byte g, byte b)
        {
            var x = new Rgb24(r, g, b);
            var y = new Rgb24(r, g, b);

            Assert.True(x.Equals(y));
            Assert.True(x.Equals((object)y));
            Assert.Equal(x.GetHashCode(), y.GetHashCode());
        }

        [Theory]
        [InlineData(1, 2, 3, 1, 2, 4)]
        [InlineData(0, 255, 0, 0, 244, 0)]
        [InlineData(1, 255, 0, 0, 255, 0)]
        public void Equals_WhenFalse(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
        {
            var a = new Rgb24(r1, g1, b1);
            var b = new Rgb24(r2, g2, b2);

            Assert.False(a.Equals(b));
            Assert.False(a.Equals((object)b));
        }

        [Fact]
        public void FromRgba32()
        {
            var rgb = default(Rgb24);
            rgb.FromRgba32(new Rgba32(1, 2, 3, 4));

            Assert.Equal(1, rgb.R);
            Assert.Equal(2, rgb.G);
            Assert.Equal(3, rgb.B);
        }

        private static Vector4 Vec(byte r, byte g, byte b, byte a = 255) => new Vector4(
            r / 255f,
            g / 255f,
            b / 255f,
            a / 255f);

        [Fact]
        public void FromVector4()
        {
            var rgb = default(Rgb24);
            rgb.FromVector4(Vec(1, 2, 3, 4));

            Assert.Equal(1, rgb.R);
            Assert.Equal(2, rgb.G);
            Assert.Equal(3, rgb.B);
        }

        [Fact]
        public void ToVector4()
        {
            var rgb = new Rgb24(1, 2, 3);

            Assert.Equal(Vec(1, 2, 3), rgb.ToVector4());
        }

        [Fact]
        public void ToRgba32()
        {
            // arrange
            var rgb = new Rgb24(1, 2, 3);
            Rgba32 rgba = default;
            var expected = new Rgba32(1, 2, 3, 255);

            // act
            rgb.ToRgba32(ref rgba);

            // assert
            Assert.Equal(expected, rgba);
        }

        [Fact]
        public void Rgb24_FromBgra5551()
        {
            // arrange
            var rgb = new Rgb24(255, 255, 255);

            // act
            rgb.FromBgra5551(new Bgra5551(1.0f, 1.0f, 1.0f, 1.0f));

            // assert
            Assert.Equal(255, rgb.R);
            Assert.Equal(255, rgb.G);
            Assert.Equal(255, rgb.B);
        }
    }
}
