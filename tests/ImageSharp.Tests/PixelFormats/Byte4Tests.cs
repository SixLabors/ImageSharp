// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class Byte4Tests
    {
        [Fact]
        public void Byte4_PackedValue()
        {
            Assert.Equal((uint)128, new Byte4(127.5f, -12.3f, 0.5f, -0.7f).PackedValue);
            Assert.Equal((uint)0x1a7b362d, new Byte4(0x2d, 0x36, 0x7b, 0x1a).PackedValue);
            Assert.Equal((uint)0x0, new Byte4(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFFFFFF, new Byte4(Vector4.One * 255).PackedValue);
        }

        [Fact]
        public void Byte4_ToVector4()
        {
            Assert.Equal(Vector4.One * 255, new Byte4(Vector4.One * 255).ToVector4());
            Assert.Equal(Vector4.Zero, new Byte4(Vector4.Zero).ToVector4());
            Assert.Equal(Vector4.UnitX * 255, new Byte4(Vector4.UnitX * 255).ToVector4());
            Assert.Equal(Vector4.UnitY * 255, new Byte4(Vector4.UnitY * 255).ToVector4());
            Assert.Equal(Vector4.UnitZ * 255, new Byte4(Vector4.UnitZ * 255).ToVector4());
            Assert.Equal(Vector4.UnitW * 255, new Byte4(Vector4.UnitW * 255).ToVector4());
        }

        [Fact]
        public void Byte4_ToScaledVector4()
        {
            // arrange
            var byte4 = new Byte4(Vector4.One * 255);

            // act
            Vector4 actual = byte4.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Byte4_PackFromScaledVector4()
        {
            // arrange
            Vector4 scaled = new Byte4(Vector4.One * 255).ToScaledVector4();
            var pixel = default(Byte4);
            uint expected = 0xFFFFFFFF;

            // act
            pixel.PackFromScaledVector4(scaled);
            uint actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Byte4_Clamping()
        {
            Assert.Equal(Vector4.Zero, new Byte4(Vector4.One * -1234.0f).ToVector4());
            Assert.Equal(Vector4.One * 255, new Byte4(Vector4.One * 1234.0f).ToVector4());
        }

        [Fact]
        public void Byte4_ToRgb24()
        {
            // arrange
            var byte4 = new Byte4(127.5f, -12.3f, 0.5f, -0.7f);
            var actual = default(Rgb24);
            var expected = new Rgb24(128, 0, 0);

            // act
            byte4.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Byte4_Rgba32()
        {
            // arrange
            var byte4 = new Byte4(127.5f, -12.3f, 0.5f, -0.7f);
            var actual = default(Rgba32);
            var expected = new Rgba32(128, 0, 0, 0);

            // act
            byte4.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Byte4_ToBgr24()
        {
            // arrange
            var byte4 = new Byte4(127.5f, -12.3f, 0.5f, -0.7f);
            var actual = default(Bgr24);
            var expected = new Bgr24(128, 0, 0);

            // act
            byte4.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Byte4_Bgra32()
        {
            // arrange
            var byte4 = new Byte4(127.5f, -12.3f, 0.5f, -0.7f);
            var actual = default(Bgra32);
            var expected = new Bgra32(128, 0, 0, 0);

            // act
            byte4.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Byte4_Argb32()
        {
            // arrange
            var byte4 = new Byte4(127.5f, -12.3f, 0.5f, -0.7f);
            var actual = default(Argb32);
            var expected = new Argb32(128, 0, 0, 0);

            // act
            byte4.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Byte4_PackFromRgba32_ToRgba32()
        {
            // arrange
            var byte4 = default(Byte4);
            var actual = default(Rgba32);
            var expected = new Rgba32(20, 38, 0, 255);

            // act
            byte4.PackFromRgba32(expected);
            byte4.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Byte4_PackFromBgra32_ToBgra32()
        {
            // arrange
            var byte4 = default(Byte4);
            var actual = default(Bgra32);
            var expected = new Bgra32(20, 38, 0, 255);

            // act
            byte4.PackFromBgra32(expected);
            byte4.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Byte4_PackFromArgb32_ToArgb32()
        {
            // arrange
            var byte4 = default(Byte4);
            var actual = default(Argb32);
            var expected = new Argb32(20, 38, 0, 255);

            // act
            byte4.PackFromArgb32(expected);
            byte4.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Byte4_PackFromRgb48_ToRgb48()
        {
            // arrange
            var input = default(Byte4);
            var actual = default(Rgb48);
            var expected = new Rgb48(65535, 0, 65535);

            // act
            input.PackFromRgb48(expected);
            input.ToRgb48(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Byte4_PackFromRgba64_ToRgba64()
        {
            // arrange
            var input = default(Byte4);
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
