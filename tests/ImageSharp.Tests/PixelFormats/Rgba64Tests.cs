// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class Rgba64Tests
    {
        [Fact]
        public void Rgba64_PackedValues()
        {
            Assert.Equal((ulong)0x73334CCC2666147B, new Rgba64(5243, 9830, 19660, 29491).PackedValue);
            Assert.Equal((ulong)0x73334CCC2666147B, new Rgba64(0.08f, 0.15f, 0.30f, 0.45f).PackedValue);
            var rgba = new Rgba64(0x73334CCC2666147B);
            Assert.Equal(5243, rgba.R);
            Assert.Equal(9830, rgba.G);
            Assert.Equal(19660, rgba.B);
            Assert.Equal(29491, rgba.A);

            // Test the limits.
            Assert.Equal((ulong)0x0, new Rgba64(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFFFFFFFFFFFFFF, new Rgba64(Vector4.One).PackedValue);
            // Test data ordering
            Assert.Equal(0xC7AD8F5C570A1EB8, new Rgba64(((float)0x1EB8) / 0xffff, ((float)0x570A) / 0xffff, ((float)0x8F5C) / 0xffff, ((float)0xC7AD) / 0xffff).PackedValue);
            Assert.Equal(0xC7AD8F5C570A1EB8, new Rgba64(0.12f, 0.34f, 0.56f, 0.78f).PackedValue);
        }

        [Fact]
        public void Rgba64_ToVector4()
        {
            Assert.Equal(Vector4.Zero, new Rgba64(Vector4.Zero).ToVector4());
            Assert.Equal(Vector4.One, new Rgba64(Vector4.One).ToVector4());
        }

        [Fact]
        public void Rgba64_ToScaledVector4()
        {
            // arrange
            var short2 = new Rgba64(Vector4.One);

            // act
            Vector4 actual = short2.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Rgba64_PackFromScaledVector4()
        {
            // arrange
            var pixel = default(Rgba64);
            var short4 = new Rgba64(Vector4.One);
            const ulong expected = 0xFFFFFFFFFFFFFFFF;

            // act 
            Vector4 scaled = short4.ToScaledVector4();
            pixel.PackFromScaledVector4(scaled);
            ulong actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba64_Clamping()
        {
            Assert.Equal(Vector4.Zero, new Rgba64(Vector4.One * -1234.0f).ToVector4());
            Assert.Equal(Vector4.One, new Rgba64(Vector4.One * 1234.0f).ToVector4());
        }

        [Fact]
        public void Rgba64_ToRgb24()
        {
            // arrange
            var rgba64 = new Rgba64(0.08f, 0.15f, 0.30f, 0.45f);
            var actual = default(Rgb24);
            var expected = new Rgb24(20, 38, 76);

            // act
            rgba64.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba64_ToRgba32()
        {
            // arrange
            var rgba64 = new Rgba64(0.08f, 0.15f, 0.30f, 0.45f);
            var actual = default(Rgba32);
            var expected = new Rgba32(20, 38, 76, 115);

            // act
            rgba64.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba64_ToArgb32()
        {
            // arrange
            var rgba64 = new Rgba64(0.08f, 0.15f, 0.30f, 0.45f);
            var actual = default(Argb32);
            var expected = new Argb32(20, 38, 76, 115);

            // act
            rgba64.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba64_ToBgr24()
        {
            // arrange
            var rgba64 = new Rgba64(0.08f, 0.15f, 0.30f, 0.45f);
            var actual = default(Bgr24);
            var expected = new Bgr24(20, 38, 76);

            // act
            rgba64.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba64_ToBgra32()
        {
            // arrange
            var rgba64 = new Rgba64(0.08f, 0.15f, 0.30f, 0.45f);
            var actual = default(Bgra32);
            var expected = new Bgra32(20, 38, 76, 115);

            // act
            rgba64.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba64_PackFromRgba32_ToRgba32()
        {
            // arrange
            var rgba64 = default(Rgba64);
            var actual = default(Rgba32);
            var expected = new Rgba32(20, 38, 76, 115);

            // act 
            rgba64.PackFromRgba32(expected);
            rgba64.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgb48_PackFromRgb48_ToRgb48()
        {
            // arrange
            var input = default(Rgba64);
            var actual = default(Rgb48);
            var expected = new Rgb48(65535, 0, 65535);

            // act
            input.PackFromRgb48(expected);
            input.ToRgb48(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba64_PackFromRgba64_ToRgba64()
        {
            // arrange
            var input = default(Rgba64);
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
