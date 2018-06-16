// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class NormalizedByte2Tests
    {
        [Fact]
        public void NormalizedByte2_PackedValue()
        {
            Assert.Equal(0xda0d, new NormalizedByte2(0.1f, -0.3f).PackedValue);
            Assert.Equal(0x0, new NormalizedByte2(Vector2.Zero).PackedValue);
            Assert.Equal(0x7F7F, new NormalizedByte2(Vector2.One).PackedValue);
            Assert.Equal(0x8181, new NormalizedByte2(-Vector2.One).PackedValue);
        }

        [Fact]
        public void NormalizedByte2_ToVector2()
        {
            Assert.Equal(Vector2.One, new NormalizedByte2(Vector2.One).ToVector2());
            Assert.Equal(Vector2.Zero, new NormalizedByte2(Vector2.Zero).ToVector2());
            Assert.Equal(-Vector2.One, new NormalizedByte2(-Vector2.One).ToVector2());
            Assert.Equal(Vector2.One, new NormalizedByte2(Vector2.One * 1234.0f).ToVector2());
            Assert.Equal(-Vector2.One, new NormalizedByte2(Vector2.One * -1234.0f).ToVector2());
        }

        [Fact]
        public void NormalizedByte2_ToVector4()
        {
            Assert.Equal(new Vector4(1, 1, 0, 1), new NormalizedByte2(Vector2.One).ToVector4());
            Assert.Equal(new Vector4(0, 0, 0, 1), new NormalizedByte2(Vector2.Zero).ToVector4());
        }

        [Fact]
        public void NormalizedByte2_ToScaledVector4()
        {
            // arrange
            var byte2 = new NormalizedByte2(-Vector2.One);

            // act
            Vector4 actual = byte2.ToScaledVector4();

            // assert
            Assert.Equal(0, actual.X);
            Assert.Equal(0, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(1F, actual.W);
        }

        [Fact]
        public void NormalizedByte2_PackFromScaledVector4()
        {
            // arrange
            Vector4 scaled = new NormalizedByte2(-Vector2.One).ToScaledVector4();
            var byte2 = default(NormalizedByte2);
            uint expected = 0x8181;

            // act
            byte2.PackFromScaledVector4(scaled);
            uint actual = byte2.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte2_PackFromRgba32()
        {
            // arrange
            var byte2 = new NormalizedByte2();
            var rgba = new Rgba32(141, 90, 0, 0);
            int expected = 0xda0d;

            // act
            byte2.PackFromRgba32(rgba);
            ushort actual = byte2.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte2_ToRgb24()
        {
            // arrange
            var short4 = new NormalizedByte2(0.1f, -0.3f);
            var actual = default(Rgb24);
            var expected = new Rgb24(141, 90, 0);

            // act
            short4.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte2_ToRgba32()
        {
            // arrange
            var short4 = new NormalizedByte2(0.1f, -0.3f);
            var actual = default(Rgba32);
            var expected = new Rgba32(141, 90, 0, 255);

            // act
            short4.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte2_ToBgr24()
        {
            // arrange
            var short4 = new NormalizedByte2(0.1f, -0.3f);
            var actual = default(Bgr24);
            var expected = new Bgr24(141, 90, 0);

            // act
            short4.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte2_ToBgra32()
        {
            // arrange
            var short4 = new NormalizedByte2(0.1f, -0.3f);
            var actual = default(Bgra32);
            var expected = new Bgra32(141, 90, 0, 255);

            // act
            short4.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte2_ToArgb32()
        {
            // arrange
            var short4 = new NormalizedByte2(0.1f, -0.3f);
            var actual = default(Argb32);
            var expected = new Argb32(141, 90, 0, 255);

            // act
            short4.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte2_PackFromRgb48_ToRgb48()
        {
            // arrange
            var input = default(NormalizedByte2);
            var actual = default(Rgb48);
            var expected = new Rgb48(65535, 65535, 0);

            // act
            input.PackFromRgb48(expected);
            input.ToRgb48(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte2_PackFromRgba64_ToRgba64()
        {
            // arrange
            var input = default(NormalizedByte2);
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
