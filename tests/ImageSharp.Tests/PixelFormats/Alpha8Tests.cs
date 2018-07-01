// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class Alpha8Tests
    {
        [Fact]
        public void Alpha8_PackedValue()
        {
            // Test the limits.
            Assert.Equal(0x0, new Alpha8(0F).PackedValue);
            Assert.Equal(0xFF, new Alpha8(1F).PackedValue);

            // Test clamping.
            Assert.Equal(0x0, new Alpha8(-1234F).PackedValue);
            Assert.Equal(0xFF, new Alpha8(1234F).PackedValue);

            // Test ordering
            Assert.Equal(124, new Alpha8(124F / 0xFF).PackedValue);
            Assert.Equal(26, new Alpha8(0.1F).PackedValue);
        }

        [Fact]
        public void Alpha8_ToVector4()
        {
            // arrange
            var alpha = new Alpha8(.5F);

            // act
            var actual = alpha.ToVector4();

            // assert
            Assert.Equal(0, actual.X);
            Assert.Equal(0, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(.5F, actual.W, 2);
        }

        [Fact]
        public void Alpha8_ToScaledVector4()
        {
            // arrange
            var alpha = new Alpha8(.5F);

            // act
            Vector4 actual = alpha.ToScaledVector4();

            // assert
            Assert.Equal(0, actual.X);
            Assert.Equal(0, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(.5F, actual.W, 2);
        }

        [Fact]
        public void Alpha8_PackFromScaledVector4()
        {
            // arrange
            Alpha8 alpha = default;
            int expected = 128;
            Vector4 scaled = new Alpha8(.5F).ToScaledVector4();

            // act
            alpha.PackFromScaledVector4(scaled);
            byte actual = alpha.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Alpha8_PackFromScaledVector4_ToRgb24()
        {
            // arrange
            Rgb24 actual = default;
            Alpha8 alpha = default;
            var expected = new Rgb24(0, 0, 0);
            Vector4 scaled = new Alpha8(.5F).ToScaledVector4();

            // act
            alpha.PackFromScaledVector4(scaled);
            alpha.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Alpha8_PackFromScaledVector4_ToRgba32()
        {
            // arrange
            Rgba32 actual = default;
            Alpha8 alpha = default;
            var expected = new Rgba32(0, 0, 0, 128);
            Vector4 scaled = new Alpha8(.5F).ToScaledVector4();

            // act
            alpha.PackFromScaledVector4(scaled);
            alpha.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Alpha8_PackFromScaledVector4_ToBgr24()
        {
            // arrange
            Bgr24 actual = default;
            Alpha8 alpha = default;
            var expected = new Bgr24(0, 0, 0);
            Vector4 scaled = new Alpha8(.5F).ToScaledVector4();

            // act
            alpha.PackFromScaledVector4(scaled);
            alpha.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Alpha8_PackFromScaledVector4_ToBgra32()
        {
            // arrange
            Bgra32 actual = default;
            Alpha8 alpha = default;
            var expected = new Bgra32(0, 0, 0, 128);
            Vector4 scaled = new Alpha8(.5F).ToScaledVector4();

            // act
            alpha.PackFromScaledVector4(scaled);
            alpha.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Alpha8_PackFromScaledVector4_ToArgb32()
        {
            // arrange
            Alpha8 alpha = default;
            Argb32 actual = default;
            var expected = new Argb32(0, 0, 0, 128);
            Vector4 scaled = new Alpha8(.5F).ToScaledVector4();

            // act
            alpha.PackFromScaledVector4(scaled);
            alpha.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Alpha8_PackFromScaledVector4_ToRgba64()
        {
            // arrange
            Alpha8 alpha = default;
            Rgba64 actual = default;
            var expected = new Rgba64(0, 0, 0, 65535);
            Vector4 scaled = new Alpha8(1F).ToScaledVector4();

            // act
            alpha.PackFromScaledVector4(scaled);
            alpha.ToRgba64(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Alpha8_PackFromRgb48_ToRgb48()
        {
            // arrange
            var alpha = default(Alpha8);
            var actual = default(Rgb48);
            var expected = new Rgb48(0, 0, 0);

            // act
            alpha.PackFromRgb48(expected);
            alpha.ToRgb48(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Alpha8_PackFromRgba64_ToRgba64()
        {
            // arrange
            var alpha = default(Alpha8);
            var actual = default(Rgba64);
            var expected = new Rgba64(0, 0, 0, 65535);

            // act
            alpha.PackFromRgba64(expected);
            alpha.ToRgba64(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }
    }
}
