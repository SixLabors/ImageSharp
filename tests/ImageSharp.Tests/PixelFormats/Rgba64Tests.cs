// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    [Trait("Category", "PixelFormats")]
    public class Rgba64Tests
    {
        [Fact]
        public void Rgba64_PackedValues()
        {
            Assert.Equal(0x73334CCC2666147BUL, new Rgba64(5243, 9830, 19660, 29491).PackedValue);

            // Test the limits.
            Assert.Equal(0x0UL, new Rgba64(0, 0, 0, 0).PackedValue);
            Assert.Equal(
                0xFFFFFFFFFFFFFFFF,
                new Rgba64(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue).PackedValue);

            // Test data ordering
            Assert.Equal(0xC7AD8F5C570A1EB8, new Rgba64(0x1EB8, 0x570A, 0x8F5C, 0xC7AD).PackedValue);
        }

        [Fact]
        public void Rgba64_ToVector4()
        {
            Assert.Equal(Vector4.Zero, new Rgba64(0, 0, 0, 0).ToVector4());
            Assert.Equal(Vector4.One, new Rgba64(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue).ToVector4());
        }

        [Theory]
        [InlineData(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue)]
        [InlineData(0, 0, 0, 0)]
        [InlineData(ushort.MaxValue / 2, 100, 2222, 33333)]
        public void Rgba64_ToScaledVector4(ushort r, ushort g, ushort b, ushort a)
        {
            // arrange
            var short2 = new Rgba64(r, g, b, a);

            float max = ushort.MaxValue;
            float rr = r / max;
            float gg = g / max;
            float bb = b / max;
            float aa = a / max;

            // act
            Vector4 actual = short2.ToScaledVector4();

            // assert
            Assert.Equal(rr, actual.X);
            Assert.Equal(gg, actual.Y);
            Assert.Equal(bb, actual.Z);
            Assert.Equal(aa, actual.W);
        }

        [Theory]
        [InlineData(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue)]
        [InlineData(0, 0, 0, 0)]
        [InlineData(ushort.MaxValue / 2, 100, 2222, 33333)]
        public void Rgba64_FromScaledVector4(ushort r, ushort g, ushort b, ushort a)
        {
            // arrange
            var source = new Rgba64(r, g, b, a);

            // act
            Vector4 scaled = source.ToScaledVector4();

            Rgba64 actual = default;
            actual.FromScaledVector4(scaled);

            // assert
            Assert.Equal(source, actual);
        }

        [Fact]
        public void Rgba64_Clamping()
        {
            var zero = default(Rgba64);
            var one = default(Rgba64);
            zero.FromVector4(Vector4.One * -1234.0f);
            one.FromVector4(Vector4.One * 1234.0f);
            Assert.Equal(Vector4.Zero, zero.ToVector4());
            Assert.Equal(Vector4.One, one.ToVector4());
        }

        [Fact]
        public void Rgba64_ToRgba32()
        {
            // arrange
            var rgba64 = new Rgba64(5140, 9766, 19532, 29555);
            var actual = default(Rgba32);
            var expected = new Rgba32(20, 38, 76, 115);

            // act
            rgba64.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba64_FromBgra5551()
        {
            // arrange
            var rgba = default(Rgba64);
            ushort expected = ushort.MaxValue;

            // act
            rgba.FromBgra5551(new Bgra5551(1.0f, 1.0f, 1.0f, 1.0f));

            // assert
            Assert.Equal(expected, rgba.R);
            Assert.Equal(expected, rgba.G);
            Assert.Equal(expected, rgba.B);
            Assert.Equal(expected, rgba.A);
        }

        [Fact]
        public void Equality_WhenTrue()
        {
            var c1 = new Rgba64(100, 2000, 3000, 40000);
            var c2 = new Rgba64(100, 2000, 3000, 40000);

            Assert.True(c1.Equals(c2));
            Assert.True(c1.GetHashCode() == c2.GetHashCode());
        }

        [Fact]
        public void Equality_WhenFalse()
        {
            var c1 = new Rgba64(100, 2000, 3000, 40000);
            var c2 = new Rgba64(101, 2000, 3000, 40000);
            var c3 = new Rgba64(100, 2000, 3000, 40001);

            Assert.False(c1.Equals(c2));
            Assert.False(c2.Equals(c3));
            Assert.False(c3.Equals(c1));
        }

        [Fact]
        public void Rgba64_FromRgba32()
        {
            var source = new Rgba32(20, 38, 76, 115);
            var expected = new Rgba64(5140, 9766, 19532, 29555);

            Rgba64 actual = default;
            actual.FromRgba32(source);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ConstructFrom_Rgba32()
        {
            var expected = new Rgba64(5140, 9766, 19532, 29555);
            var source = new Rgba32(20, 38, 76, 115);
            var actual = new Rgba64(source);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ConstructFrom_Bgra32()
        {
            var expected = new Rgba64(5140, 9766, 19532, 29555);
            var source = new Bgra32(20, 38, 76, 115);
            var actual = new Rgba64(source);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ConstructFrom_Argb32()
        {
            var expected = new Rgba64(5140, 9766, 19532, 29555);
            var source = new Argb32(20, 38, 76, 115);
            var actual = new Rgba64(source);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ConstructFrom_Rgb24()
        {
            var expected = new Rgba64(5140, 9766, 19532, ushort.MaxValue);
            var source = new Rgb24(20, 38, 76);
            var actual = new Rgba64(source);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ConstructFrom_Bgr24()
        {
            var expected = new Rgba64(5140, 9766, 19532, ushort.MaxValue);
            var source = new Bgr24(20, 38, 76);
            var actual = new Rgba64(source);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ConstructFrom_Vector4()
        {
            var source = new Vector4(0f, 0.2f, 0.5f, 1f);
            Rgba64 expected = default;
            expected.FromScaledVector4(source);

            var actual = new Rgba64(source);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ToRgba32_Retval()
        {
            // arrange
            var source = new Rgba64(5140, 9766, 19532, 29555);
            var expected = new Rgba32(20, 38, 76, 115);

            // act
            var actual = source.ToRgba32();

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ToBgra32_Retval()
        {
            // arrange
            var source = new Rgba64(5140, 9766, 19532, 29555);
            var expected = new Bgra32(20, 38, 76, 115);

            // act
            var actual = source.ToBgra32();

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ToArgb32_Retval()
        {
            // arrange
            var source = new Rgba64(5140, 9766, 19532, 29555);
            var expected = new Argb32(20, 38, 76, 115);

            // act
            var actual = source.ToArgb32();

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ToRgb24_Retval()
        {
            // arrange
            var source = new Rgba64(5140, 9766, 19532, 29555);
            var expected = new Rgb24(20, 38, 76);

            // act
            var actual = source.ToRgb24();

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ToBgr24_Retval()
        {
            // arrange
            var source = new Rgba64(5140, 9766, 19532, 29555);
            var expected = new Bgr24(20, 38, 76);

            // act
            var actual = source.ToBgr24();

            // assert
            Assert.Equal(expected, actual);
        }
    }
}
