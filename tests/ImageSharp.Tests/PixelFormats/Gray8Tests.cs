// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class Gray8Tests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(255)]
        [InlineData(10)]
        [InlineData(42)]
        public void Gray8_PackedValue_EqualsInput(byte input)
        {
            Assert.Equal(input, new Gray8(input).PackedValue);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(255)]
        [InlineData(30)]
        public void Gray8_ToVector4(byte input)
        {
            // arrange
            var gray = new Gray8(input);

            // act
            var actual = gray.ToVector4();

            // assert
            Assert.Equal(input, actual.X);
            Assert.Equal(input, actual.Y);
            Assert.Equal(input, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(255)]
        [InlineData(30)]
        public void Gray8_ToScaledVector4(byte input)
        {
            // arrange
            var gray = new Gray8(input);

            // act
            var actual = gray.ToScaledVector4();

            // assert
            float scaledInput = input / 255f;
            Assert.Equal(scaledInput, actual.X);
            Assert.Equal(scaledInput, actual.Y);
            Assert.Equal(scaledInput, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Gray8_PackFromScaledVector4()
        {
            // arrange
            Gray8 gray = default;
            int expected = 128;
            Vector4 scaled = new Gray8(128).ToScaledVector4();

            // act
            gray.PackFromScaledVector4(scaled);
            byte actual = gray.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Gray8_PackFromScaledVector4_ToRgb24()
        {
            // arrange
            Rgb24 actual = default;
            Gray8 gray = default;
            var expected = new Rgb24(128, 128, 128);
            Vector4 scaled = new Gray8(128).ToScaledVector4();

            // act
            gray.PackFromScaledVector4(scaled);
            gray.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Gray8_PackFromScaledVector4_ToRgba32()
        {
            // arrange
            Rgba32 actual = default;
            Gray8 gray = default;
            var expected = new Rgba32(128, 128, 128, 255);
            Vector4 scaled = new Gray8(128).ToScaledVector4();

            // act
            gray.PackFromScaledVector4(scaled);
            gray.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Gray8_PackFromScaledVector4_ToBgr24()
        {
            // arrange
            Bgr24 actual = default;
            Gray8 gray = default;
            var expected = new Bgr24(128, 128, 128);
            Vector4 scaled = new Gray8(128).ToScaledVector4();

            // act
            gray.PackFromScaledVector4(scaled);
            gray.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Gray8_PackFromScaledVector4_ToBgra32()
        {
            // arrange
            Bgra32 actual = default;
            Gray8 gray = default;
            var expected = new Bgra32(128,128,128);
            Vector4 scaled = new Gray8(128).ToScaledVector4();

            // act
            gray.PackFromScaledVector4(scaled);
            gray.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Gray8_PackFromScaledVector4_ToArgb32()
        {
            // arrange
            Gray8 gray = default;
            Argb32 actual = default;
            var expected = new Argb32(128, 128, 128);
            Vector4 scaled = new Gray8(128).ToScaledVector4();

            // act
            gray.PackFromScaledVector4(scaled);
            gray.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Gray8_PackFromScaledVector4_ToRgba64()
        {
            // arrange
            Gray8 gray = default;
            Rgba64 actual = default;
            var expected = new Rgba64(65535, 65535, 65535, 65535);
            Vector4 scaled = new Gray8(255).ToScaledVector4();

            // act
            gray.PackFromScaledVector4(scaled);
            gray.ToRgba64(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Gray8_PackFromRgb48_ToRgb48()
        {
            // arrange
            var gray = default(Gray8);
            var actual = default(Rgb48);
            var expected = new Rgb48(0, 0, 0);

            // act
            gray.PackFromRgb48(expected);
            gray.ToRgb48(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Gray8_PackFromRgba64_ToRgba64()
        {
            // arrange
            var gray = default(Gray8);
            var actual = default(Rgba64);
            var expected = new Rgba64(0, 0, 0, 65535);

            // act
            gray.PackFromRgba64(expected);
            gray.ToRgba64(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }
    }
}
