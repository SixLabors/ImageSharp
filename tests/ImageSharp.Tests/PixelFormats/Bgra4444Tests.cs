// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class Bgra4444Tests
    {
        [Fact]
        public void Bgra4444_PackedValue()
        {
            Assert.Equal(520, new Bgra4444(0.1f, -0.3f, 0.5f, -0.7f).PackedValue);
            Assert.Equal(0x0, new Bgra4444(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFF, new Bgra4444(Vector4.One).PackedValue);
            Assert.Equal(0x0F00, new Bgra4444(Vector4.UnitX).PackedValue);
            Assert.Equal(0x00F0, new Bgra4444(Vector4.UnitY).PackedValue);
            Assert.Equal(0x000F, new Bgra4444(Vector4.UnitZ).PackedValue);
            Assert.Equal(0xF000, new Bgra4444(Vector4.UnitW).PackedValue);
        }

        [Fact]
        public void Bgra4444_ToVector4()
        {
            Assert.Equal(Vector4.One, new Bgra4444(Vector4.One).ToVector4());
            Assert.Equal(Vector4.Zero, new Bgra4444(Vector4.Zero).ToVector4());
            Assert.Equal(Vector4.UnitX, new Bgra4444(Vector4.UnitX).ToVector4());
            Assert.Equal(Vector4.UnitY, new Bgra4444(Vector4.UnitY).ToVector4());
            Assert.Equal(Vector4.UnitZ, new Bgra4444(Vector4.UnitZ).ToVector4());
            Assert.Equal(Vector4.UnitW, new Bgra4444(Vector4.UnitW).ToVector4());
        }

        [Fact]
        public void Bgra4444_ToScaledVector4()
        {
            // arrange
            var bgra = new Bgra4444(Vector4.One);

            // act
            Vector4 actual = bgra.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Bgra4444_PackFromScaledVector4()
        {
            // arrange
            Vector4 scaled = new Bgra4444(Vector4.One).ToScaledVector4();
            int expected = 0xFFFF;
            var bgra = default(Bgra4444);

            // act
            bgra.PackFromScaledVector4(scaled);
            ushort actual = bgra.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra4444_Clamping()
        {
            Assert.Equal(Vector4.Zero, new Bgra4444(Vector4.One * -1234.0f).ToVector4());
            Assert.Equal(Vector4.One, new Bgra4444(Vector4.One * 1234.0f).ToVector4());
        }

        [Fact]
        public void Bgra4444_ToRgb24()
        {
            // arrange
            var bgra = new Bgra4444(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Rgb24);
            var expected = new Rgb24(34, 0, 136);

            // act
            bgra.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra4444_ToRgba32()
        {
            // arrange
            var bgra = new Bgra4444(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Rgba32);
            var expected = new Rgba32(34, 0, 136, 0);

            // act
            bgra.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra4444_ToBgr24()
        {
            // arrange
            var bgra = new Bgra4444(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Bgr24);
            var expected = new Bgr24(34, 0, 136);

            // act
            bgra.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra4444_ToBgra32()
        {
            // arrange
            var bgra = new Bgra4444(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Bgra32);
            var expected = new Bgra32(34, 0, 136, 0);

            // act
            bgra.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra4444_ToArgb32()
        {
            // arrange
            var bgra = new Bgra4444(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Argb32);
            var expected = new Argb32(34, 0, 136, 0);

            // act
            bgra.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra4444_PackFromRgba32_ToRgba32()
        {
            // arrange
            var bgra = default(Bgra4444);
            var actual = default(Rgba32);
            var expected = new Rgba32(34, 0, 136, 0);

            // act
            bgra.PackFromRgba32(expected);
            bgra.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra4444_PackFromBgra32_ToBgra32()
        {
            // arrange
            var bgra = default(Bgra4444);
            var actual = default(Bgra32);
            var expected = new Bgra32(34, 0, 136, 0);

            // act
            bgra.PackFromBgra32(expected);
            bgra.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra4444_PackFromArgb32_ToArgb32()
        {
            // arrange
            var bgra = default(Bgra4444);
            var actual = default(Argb32);
            var expected = new Argb32(34, 0, 136, 0);

            // act
            bgra.PackFromArgb32(expected);
            bgra.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra4444_PackFromRgb48_ToRgb48()
        {
            // arrange
            var input = default(Bgra4444);
            var actual = default(Rgb48);
            var expected = new Rgb48(65535, 0, 65535);

            // act
            input.PackFromRgb48(expected);
            input.ToRgb48(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra4444_PackFromRgba64_ToRgba64()
        {
            // arrange
            var input = default(Bgra4444);
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
