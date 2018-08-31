// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class Bgra32Tests
    {
        public static readonly TheoryData<byte, byte, byte, byte> ColorData =
            new TheoryData<byte, byte, byte, byte>()
                {
                    { 1, 2, 3, 4 }, { 4, 5, 6, 7 }, { 0, 255, 42, 0 }, { 1, 2, 3, 255 }
                };

        [Theory]
        [MemberData(nameof(ColorData))]
        public void Constructor(byte b, byte g, byte r, byte a)
        {
            var p = new Bgra32(r, g, b, a);

            Assert.Equal(r, p.R);
            Assert.Equal(g, p.G);
            Assert.Equal(b, p.B);
            Assert.Equal(a, p.A);
        }

        [Fact]
        public unsafe void ByteLayoutIsSequentialBgra()
        {
            var color = new Bgra32(1, 2, 3, 4);
            byte* ptr = (byte*)&color;

            Assert.Equal(3, ptr[0]);
            Assert.Equal(2, ptr[1]);
            Assert.Equal(1, ptr[2]);
            Assert.Equal(4, ptr[3]);
        }

        [Theory]
        [MemberData(nameof(ColorData))]
        public void Equality_WhenTrue(byte b, byte g, byte r, byte a)
        {
            var x = new Bgra32(r, g, b, a);
            var y = new Bgra32(r, g, b, a);

            Assert.True(x.Equals(y));
            Assert.True(x.Equals((object)y));
            Assert.Equal(x.GetHashCode(), y.GetHashCode());
        }

        [Theory]
        [InlineData(1, 2, 3, 4, 1, 2, 3, 5)]
        [InlineData(0, 0, 255, 0, 0, 0, 244, 0)]
        [InlineData(0, 255, 0, 0, 0, 244, 0, 0)]
        [InlineData(1, 255, 0, 0, 0, 255, 0, 0)]
        public void Equality_WhenFalse(byte b1, byte g1, byte r1, byte a1, byte b2, byte g2, byte r2, byte a2)
        {
            var x = new Bgra32(r1, g1, b1, a1);
            var y = new Bgra32(r2, g2, b2, a2);

            Assert.False(x.Equals(y));
            Assert.False(x.Equals((object)y));
        }


        [Fact]
        public void PackFromRgba32()
        {
            var rgb = default(Rgb24);
            rgb.PackFromRgba32(new Rgba32(1, 2, 3, 4));

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
        public void PackFromVector4()
        {
            var c = default(Bgra32);
            c.PackFromVector4(Vec(1, 2, 3, 4));

            Assert.Equal(1, c.R);
            Assert.Equal(2, c.G);
            Assert.Equal(3, c.B);
            Assert.Equal(4, c.A);
        }

        [Fact]
        public void ToVector4()
        {
            var rgb = new Bgra32(1, 2, 3, 4);

            Assert.Equal(Vec(1, 2, 3, 4), rgb.ToVector4());
        }

        [Fact]
        public void ToRgb24()
        {
            var c = new Bgra32(1, 2, 3, 4);
            var dest = default(Rgb24);

            c.ToRgb24(ref dest);

            Assert.Equal(new Rgb24(1, 2, 3), dest);
        }

        [Fact]
        public void ToRgba32()
        {
            var c = new Bgra32(1, 2, 3, 4);
            var rgba = default(Rgba32);

            c.ToRgba32(ref rgba);

            Assert.Equal(new Rgba32(1, 2, 3, 4), rgba);
        }

        [Fact]
        public void ToBgr24()
        {
            var rgb = new Bgra32(1, 2, 3, 4);
            var bgr = default(Bgr24);

            rgb.ToBgr24(ref bgr);

            Assert.Equal(new Bgr24(1, 2, 3), bgr);
        }

        [Fact]
        public void ToBgra32()
        {
            var rgb = new Bgra32(1, 2, 3, 4);
            var bgra = default(Bgra32);

            rgb.ToBgra32(ref bgra);

            Assert.Equal(new Bgra32(1, 2, 3, 4), bgra);
        }

        [Fact]
        public void Bgra32_PackFromRgb48_ToRgb48()
        {
            // arrange
            var input = default(Bgra32);
            var actual = default(Rgb48);
            var expected = new Rgb48(65535, 0, 65535);

            // act
            input.PackFromRgb48(expected);
            input.ToRgb48(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra32_PackFromRgba64_ToRgba64()
        {
            // arrange
            var input = default(Bgra32);
            var actual = default(Rgba64);
            var expected = new Rgba64(65535, 0, 65535, 0);

            // act
            input.PackFromRgba64(expected);
            input.ToRgba64(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }
    }
}