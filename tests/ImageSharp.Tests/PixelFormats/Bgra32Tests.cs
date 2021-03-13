// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    [Trait("Category", "PixelFormats")]
    public class Bgra32Tests
    {
        /// <summary>
        /// Tests the equality operators for equality.
        /// </summary>
        [Fact]
        public void AreEqual()
        {
            var color1 = new Bgra32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
            var color2 = new Bgra32(byte.MaxValue, byte.MaxValue, byte.MaxValue);

            Assert.Equal(color1, color2);
        }

        /// <summary>
        /// Tests the equality operators for inequality.
        /// </summary>
        [Fact]
        public void AreNotEqual()
        {
            var color1 = new Bgra32(0, 0, byte.MaxValue, byte.MaxValue);
            var color2 = new Bgra32(byte.MaxValue, byte.MaxValue, byte.MaxValue);

            Assert.NotEqual(color1, color2);
        }

        public static readonly TheoryData<byte, byte, byte, byte> ColorData =
            new TheoryData<byte, byte, byte, byte>
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
            var c = default(Bgra32);
            c.FromVector4(Vec(1, 2, 3, 4));

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
        public void Bgra32_FromBgra5551()
        {
            // arrange
            var bgra = default(Bgra32);
            uint expected = uint.MaxValue;

            // act
            bgra.FromBgra5551(new Bgra5551(1.0f, 1.0f, 1.0f, 1.0f));

            // assert
            Assert.Equal(expected, bgra.PackedValue);
        }
    }
}
