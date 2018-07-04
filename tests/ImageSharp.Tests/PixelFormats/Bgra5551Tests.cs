// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class Bgra5551Tests
    {
        [Fact]
        public void Bgra5551_PackedValue()
        {
            float x = 0x1a;
            float y = 0x16;
            float z = 0xd;
            float w = 0x1;
            Assert.Equal(0xeacd, new Bgra5551(x / 0x1f, y / 0x1f, z / 0x1f, w).PackedValue);
            Assert.Equal(3088, new Bgra5551(0.1f, -0.3f, 0.5f, -0.7f).PackedValue);
            // Test the limits.
            Assert.Equal(0x0, new Bgra5551(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFF, new Bgra5551(Vector4.One).PackedValue);
        }

        [Fact]
        public void Bgra5551_ToVector4()
        {
            Assert.Equal(Vector4.Zero, new Bgra5551(Vector4.Zero).ToVector4());
            Assert.Equal(Vector4.One, new Bgra5551(Vector4.One).ToVector4());
        }

        [Fact]
        public void Bgra5551_ToScaledVector4()
        {
            // arrange
            var bgra = new Bgra5551(Vector4.One);

            // act 
            Vector4 actual = bgra.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Bgra5551_PackFromScaledVector4()
        {
            // arrange
            Vector4 scaled = new Bgra5551(Vector4.One).ToScaledVector4();
            int expected = 0xFFFF;
            var pixel = default(Bgra5551);
            pixel.PackFromScaledVector4(scaled);

            // act
            pixel.PackFromScaledVector4(scaled);
            ushort actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_Clamping()
        {
            Assert.Equal(Vector4.Zero, new Bgra5551(Vector4.One * -1234.0f).ToVector4());
            Assert.Equal(Vector4.One, new Bgra5551(Vector4.One * 1234.0f).ToVector4());
        }

        [Fact]
        public void Bgra5551_ToRgb24()
        {
            // arrange
            var bgra = new Bgra5551(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Rgb24);
            var expected = new Rgb24(24, 0, 131);

            // act
            bgra.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_Rgba32()
        {
            // arrange
            var bgra = new Bgra5551(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Rgba32);
            var expected = new Rgba32(24, 0, 131, 0);

            // act
            bgra.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_ToBgr24()
        {
            // arrange
            var bgra = new Bgra5551(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Bgr24);
            var expected = new Bgr24(24, 0, 131);

            // act
            bgra.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_Bgra32()
        {
            // arrange
            var bgra = new Bgra5551(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Bgra32);
            var expected = new Bgra32(24, 0, 131, 0);

            // act
            bgra.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_ToArgb32()
        {
            // arrange
            var bgra = new Bgra5551(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Argb32);
            var expected = new Argb32(24, 0, 131, 0);

            // act
            bgra.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_PackFromRgba32_ToRgba32()
        {
            // arrange
            var bgra = default(Bgra5551);
            var expected = new Rgba32(24, 0, 131, 0);
            var actual = default(Rgba32);

            // act
            bgra.PackFromRgba32(expected);
            bgra.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_PackFromBgra32_ToBgra32()
        {
            // arrange
            var bgra = default(Bgra5551);
            var expected = new Bgra32(24, 0, 131, 0);
            var actual = default(Bgra32);

            // act
            bgra.PackFromBgra32(expected);
            bgra.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_PackFromArgb32_ToArgb32()
        {
            // arrange
            var bgra = default(Bgra5551);
            var expected = new Argb32(24, 0, 131, 0);
            var actual = default(Argb32);

            // act
            bgra.PackFromArgb32(expected);
            bgra.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_PackFromRgb48_ToRgb48()
        {
            // arrange
            var input = default(Bgra5551);
            var actual = default(Rgb48);
            var expected = new Rgb48(65535, 0, 65535);

            // act
            input.PackFromRgb48(expected);
            input.ToRgb48(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_PackFromRgba64_ToRgba64()
        {
            // arrange
            var input = default(Bgra5551);
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
