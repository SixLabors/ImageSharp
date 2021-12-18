// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    [Trait("Category", "PixelFormats")]
    public class Abgr32Tests
    {
        /// <summary>
        /// Tests the equality operators for equality.
        /// </summary>
        [Fact]
        public void AreEqual()
        {
            var color1 = new Abgr32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
            var color2 = new Abgr32(byte.MaxValue, byte.MaxValue, byte.MaxValue);

            Assert.Equal(color1, color2);
        }

        /// <summary>
        /// Tests the equality operators for inequality.
        /// </summary>
        [Fact]
        public void AreNotEqual()
        {
            var color1 = new Abgr32(0, 0, byte.MaxValue, byte.MaxValue);
            var color2 = new Abgr32(byte.MaxValue, byte.MaxValue, byte.MaxValue);

            Assert.NotEqual(color1, color2);
        }

        public static readonly TheoryData<byte, byte, byte, byte> ColorData =
            new()
            {
                { 1, 2, 3, 4 },
                { 4, 5, 6, 7 },
                { 0, 255, 42, 0 },
                { 1, 2, 3, 255 }
            };

        [Theory]
        [MemberData(nameof(ColorData))]
        public void Constructor(byte b, byte g, byte r, byte a)
        {
            var p = new Abgr32(r, g, b, a);

            Assert.Equal(r, p.R);
            Assert.Equal(g, p.G);
            Assert.Equal(b, p.B);
            Assert.Equal(a, p.A);
        }

        [Fact]
        public unsafe void ByteLayoutIsSequentialBgra()
        {
            var color = new Abgr32(1, 2, 3, 4);
            byte* ptr = (byte*)&color;

            Assert.Equal(4, ptr[0]);
            Assert.Equal(3, ptr[1]);
            Assert.Equal(2, ptr[2]);
            Assert.Equal(1, ptr[3]);
        }

        [Theory]
        [MemberData(nameof(ColorData))]
        public void Equality_WhenTrue(byte r, byte g, byte b, byte a)
        {
            var x = new Abgr32(r, g, b, a);
            var y = new Abgr32(r, g, b, a);

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
            var x = new Abgr32(r1, g1, b1, a1);
            var y = new Abgr32(r2, g2, b2, a2);

            Assert.False(x.Equals(y));
            Assert.False(x.Equals((object)y));
        }

        [Fact]
        public void FromRgba32()
        {
            var abgr = default(Abgr32);
            abgr.FromRgba32(new Rgba32(1, 2, 3, 4));

            Assert.Equal(1, abgr.R);
            Assert.Equal(2, abgr.G);
            Assert.Equal(3, abgr.B);
            Assert.Equal(4, abgr.A);
        }

        private static Vector4 Vec(byte r, byte g, byte b, byte a = 255) => new Vector4(
            r / 255f,
            g / 255f,
            b / 255f,
            a / 255f);

        [Fact]
        public void FromVector4()
        {
            var c = default(Abgr32);
            c.FromVector4(Vec(1, 2, 3, 4));

            Assert.Equal(1, c.R);
            Assert.Equal(2, c.G);
            Assert.Equal(3, c.B);
            Assert.Equal(4, c.A);
        }

        [Fact]
        public void ToVector4()
        {
            var abgr = new Abgr32(1, 2, 3, 4);

            Assert.Equal(Vec(1, 2, 3, 4), abgr.ToVector4());
        }

        [Fact]
        public void Abgr32_FromBgra5551()
        {
            // arrange
            var abgr = default(Abgr32);
            uint expected = uint.MaxValue;

            // act
            abgr.FromBgra5551(new Bgra5551(1.0f, 1.0f, 1.0f, 1.0f));

            // assert
            Assert.Equal(expected, abgr.PackedValue);
        }
    }
}
