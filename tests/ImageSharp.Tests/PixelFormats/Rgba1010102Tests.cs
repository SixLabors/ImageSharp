// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class Rgba1010102Tests
    {
        [Fact]
        public void Rgba1010102_PackedValue()
        {
            float x = 0x2db;
            float y = 0x36d;
            float z = 0x3b7;
            float w = 0x1;
            Assert.Equal((uint)0x7B7DB6DB, new Rgba1010102(x / 0x3ff, y / 0x3ff, z / 0x3ff, w / 3).PackedValue);

            Assert.Equal((uint)536871014, new Rgba1010102(0.1f, -0.3f, 0.5f, -0.7f).PackedValue);

            // Test the limits.
            Assert.Equal((uint)0x0, new Rgba1010102(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFFFFFF, new Rgba1010102(Vector4.One).PackedValue);
        }

        [Fact]
        public void Rgba1010102_ToVector4()
        {
            Assert.Equal(Vector4.Zero, new Rgba1010102(Vector4.Zero).ToVector4());
            Assert.Equal(Vector4.One, new Rgba1010102(Vector4.One).ToVector4());
        }

        [Fact]
        public void Rgba1010102_ToScaledVector4()
        {
            // arrange
            var rgba = new Rgba1010102(Vector4.One);

            // act
            Vector4 actual = rgba.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Rgba1010102_PackFromScaledVector4()
        {
            // arrange
            var rgba = new Rgba1010102(Vector4.One);
            var actual = default(Rgba1010102);
            uint expected = 0xFFFFFFFF;

            // act
            Vector4 scaled = rgba.ToScaledVector4();
            actual.PackFromScaledVector4(scaled);

            // assert
            Assert.Equal(expected, actual.PackedValue);
        }

        [Fact]
        public void Rgba1010102_Clamping()
        {
            Assert.Equal(Vector4.Zero, new Rgba1010102(Vector4.One * -1234.0f).ToVector4());
            Assert.Equal(Vector4.One, new Rgba1010102(Vector4.One * 1234.0f).ToVector4());
        }

        [Fact]
        public void Rgba1010102_ToRgb24()
        {
            // arrange
            var rgba = new Rgba1010102(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Rgb24);
            var expected = new Rgb24(25, 0, 128);

            // act
            rgba.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba1010102_ToRgba32()
        {
            // arrange
            var rgba = new Rgba1010102(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Rgba32);
            var expected = new Rgba32(25, 0, 128, 0);

            // act
            rgba.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba1010102_ToBgr24()
        {
            // arrange
            var rgba = new Rgba1010102(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Bgr24);
            var expected = new Bgr24(25, 0, 128);

            // act
            rgba.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba1010102_ToBgra32()
        {
            // arrange
            var rgba = new Rgba1010102(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Bgra32);
            var expected = new Bgra32(25, 0, 128, 0);

            // act
            rgba.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba1010102_PackFromRgba32_ToRgba32()
        {
            // arrange
            var rgba = default(Rgba1010102);
            var expected = new Rgba32(25, 0, 128, 0);
            var actual = default(Rgba32);

            // act
            rgba.PackFromRgba32(expected);
            rgba.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba1010102_PackFromBgra32_ToBgra32()
        {
            // arrange
            var rgba = default(Rgba1010102);
            var expected = new Bgra32(25, 0, 128, 0);
            var actual = default(Bgra32);

            // act
            rgba.PackFromBgra32(expected);
            rgba.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba1010102_PackFromArgb32_ToArgb32()
        {
            // arrange
            var rgba = default(Rgba1010102);
            var expected = new Argb32(25, 0, 128, 0);
            var actual = default(Argb32);

            // act
            rgba.PackFromArgb32(expected);
            rgba.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba1010102_PackFromRgb48_ToRgb48()
        {
            // arrange
            var input = default(Rgba1010102);
            var actual = default(Rgb48);
            var expected = new Rgb48(65535, 0, 65535);

            // act
            input.PackFromRgb48(expected);
            input.ToRgb48(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba1010102_PackFromRgba64_ToRgba64()
        {
            // arrange
            var input = default(Rgba1010102);
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
