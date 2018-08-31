// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class Rgb24Tests
    {
        public static readonly TheoryData<byte, byte, byte> ColorData =
            new TheoryData<byte, byte, byte>() { { 1, 2, 3 }, { 4, 5, 6 }, { 0, 255, 42 } };

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
            var rgb = default(Rgb24);
            rgb.PackFromVector4(Vec(1, 2, 3, 4));

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
        public void ToRgb24()
        {
            var rgb = new Rgb24(1, 2, 3);
            var dest = default(Rgb24);

            rgb.ToRgb24(ref dest);

            Assert.Equal(rgb, dest);
        }

        [Fact]
        public void ToRgba32()
        {
            var rgb = new Rgb24(1, 2, 3);
            var rgba = default(Rgba32);

            rgb.ToRgba32(ref rgba);

            Assert.Equal(new Rgba32(1, 2, 3, 255), rgba);
        }

        [Fact]
        public void ToBgr24()
        {
            var rgb = new Rgb24(1, 2, 3);
            var bgr = default(Bgr24);

            rgb.ToBgr24(ref bgr);

            Assert.Equal(new Bgr24(1, 2, 3), bgr);
        }

        [Fact]
        public void ToBgra32()
        {
            var rgb = new Rgb24(1, 2, 3);
            var bgra = default(Bgra32);

            rgb.ToBgra32(ref bgra);

            Assert.Equal(new Bgra32(1, 2, 3, 255), bgra);
        }

        [Fact]
        public void Rgb24_PackFromRgb48_ToRgb48()
        {
            // arrange
            var input = default(Rgb24);
            var actual = default(Rgb48);
            var expected = new Rgb48(65535, 0, 65535);

            // act
            input.PackFromRgb48(expected);
            input.ToRgb48(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgb24_PackFromRgba64_ToRgba64()
        {
            // arrange
            var input = default(Rgb24);
            var actual = default(Rgba64);
            var expected = new Rgba64(65535, 0, 65535, 65535);

            // act
            input.PackFromRgba64(expected);
            input.ToRgba64(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }
    }
}