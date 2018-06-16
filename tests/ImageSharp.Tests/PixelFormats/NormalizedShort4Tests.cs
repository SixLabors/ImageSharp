// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class NormalizedShort4Tests
    {
        [Fact]
        public void NormalizedShort4_PackedValues()
        {
            Assert.Equal(0xa6674000d99a0ccd, new NormalizedShort4(0.1f, -0.3f, 0.5f, -0.7f).PackedValue);
            Assert.Equal((ulong)4150390751449251866, new NormalizedShort4(0.0008f, 0.15f, 0.30f, 0.45f).PackedValue);
            Assert.Equal((ulong)0x0, new NormalizedShort4(Vector4.Zero).PackedValue);
            Assert.Equal((ulong)0x7FFF7FFF7FFF7FFF, new NormalizedShort4(Vector4.One).PackedValue);
            Assert.Equal(0x8001800180018001, new NormalizedShort4(-Vector4.One).PackedValue);
        }

        [Fact]
        public void NormalizedShort4_ToVector4()
        {
            // Test ToVector4
            Assert.Equal(Vector4.One, new NormalizedShort4(Vector4.One).ToVector4());
            Assert.Equal(Vector4.Zero, new NormalizedShort4(Vector4.Zero).ToVector4());
            Assert.Equal(-Vector4.One, new NormalizedShort4(-Vector4.One).ToVector4());
            Assert.Equal(Vector4.One, new NormalizedShort4(Vector4.One * 1234.0f).ToVector4());
            Assert.Equal(-Vector4.One, new NormalizedShort4(Vector4.One * -1234.0f).ToVector4());
        }

        [Fact]
        public void NormalizedShort4_ToScaledVector4()
        {
            // arrange
            var short4 = new NormalizedShort4(Vector4.One);

            // act
            Vector4 actual = short4.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void NormalizedShort4_PackFromScaledVector4()
        {
            // arrange
            var pixel = default(NormalizedShort4);
            Vector4 scaled = new NormalizedShort4(Vector4.One).ToScaledVector4();
            ulong expected = (ulong)0x7FFF7FFF7FFF7FFF;

            // act 
            pixel.PackFromScaledVector4(scaled);
            ulong actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort4_ToRgb24()
        {
            // arrange
            var short4 = new NormalizedShort4(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Rgb24);
            var expected = new Rgb24(141, 90, 192);

            // act
            short4.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort4_ToRgba32()
        {
            // arrange
            var short4 = new NormalizedShort4(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Rgba32);
            var expected = new Rgba32(141, 90, 192, 39);

            // act
            short4.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort4_ToBgr24()
        {
            // arrange
            var short4 = new NormalizedShort4(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Bgr24);
            var expected = new Bgr24(141, 90, 192);

            // act
            short4.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort4_ToArgb32()
        {
            // arrange
            var short4 = new NormalizedShort4(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Argb32);
            var expected = new Argb32(141, 90, 192, 39);

            // act
            short4.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort4_PackFromRgba32_ToRgba32()
        {
            // arrange
            var short4 = default(NormalizedShort4);
            var expected = new Rgba32(9, 115, 202, 127);
            var actual = default(Rgba32);

            // act 
            short4.PackFromRgba32(expected);
            short4.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort4_PackFromBgra32_ToRgba32()
        {
            // arrange
            var short4 = default(NormalizedShort4);
            var actual = default(Bgra32);
            var expected = new Bgra32(9, 115, 202, 127);

            // act 
            short4.PackFromBgra32(expected);
            short4.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort4_PackFromArgb32_ToRgba32()
        {
            // arrange
            var short4 = default(NormalizedShort4);
            var actual = default(Argb32);
            var expected = new Argb32(9, 115, 202, 127);

            // act 
            short4.PackFromArgb32(expected);
            short4.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort4_PackFromRgb48_ToRgb48()
        {
            // arrange
            var input = default(NormalizedShort4);
            var actual = default(Rgb48);
            var expected = new Rgb48(65535, 65535, 0);

            // act
            input.PackFromRgb48(expected);
            input.ToRgb48(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort4_PackFromRgba64_ToRgba64()
        {
            // arrange
            var input = default(NormalizedShort4);
            var actual = default(Rgba64);
            var expected = new Rgba64(65535, 65535, 0, 65535);

            // act
            input.PackFromRgba64(expected);
            input.ToRgba64(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }
    }
}
