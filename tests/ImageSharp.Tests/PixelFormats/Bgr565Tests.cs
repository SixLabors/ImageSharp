// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class Bgr565Tests
    {
        [Fact]
        public void Bgr565_PackedValue()
        {
            Assert.Equal(6160, new Bgr565(0.1F, -0.3F, 0.5F).PackedValue);
            Assert.Equal(0x0, new Bgr565(Vector3.Zero).PackedValue);
            Assert.Equal(0xFFFF, new Bgr565(Vector3.One).PackedValue);
            // Make sure the swizzle is correct.
            Assert.Equal(0xF800, new Bgr565(Vector3.UnitX).PackedValue);
            Assert.Equal(0x07E0, new Bgr565(Vector3.UnitY).PackedValue);
            Assert.Equal(0x001F, new Bgr565(Vector3.UnitZ).PackedValue);
        }

        [Fact]
        public void Bgr565_ToVector3()
        {
            Assert.Equal(Vector3.One, new Bgr565(Vector3.One).ToVector3());
            Assert.Equal(Vector3.Zero, new Bgr565(Vector3.Zero).ToVector3());
            Assert.Equal(Vector3.UnitX, new Bgr565(Vector3.UnitX).ToVector3());
            Assert.Equal(Vector3.UnitY, new Bgr565(Vector3.UnitY).ToVector3());
            Assert.Equal(Vector3.UnitZ, new Bgr565(Vector3.UnitZ).ToVector3());
        }

        [Fact]
        public void Bgr565_ToScaledVector4()
        {
            // arrange
            var bgr = new Bgr565(Vector3.One);

            // act
            Vector4 actual = bgr.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Bgr565_PackFromScaledVector4()
        {
            // arrange
            Vector4 scaled = new Bgr565(Vector3.One).ToScaledVector4();
            int expected = 0xFFFF;
            var pixel = default(Bgr565);

            // act
            pixel.PackFromScaledVector4(scaled);
            ushort actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgr565_Clamping()
        {
            Assert.Equal(Vector3.Zero, new Bgr565(Vector3.One * -1234F).ToVector3());
            Assert.Equal(Vector3.One, new Bgr565(Vector3.One * 1234F).ToVector3());
        }

        [Fact]
        public void Bgr565_ToRgb24()
        {
            // arrange
            var bgra = new Bgr565(0.1F, -0.3F, 0.5F);
            var actual = default(Rgb24);
            var expected = new Rgb24(25, 0, 132);

            // act
            bgra.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgr565_ToRgba32()
        {
            // arrange
            var bgra = new Bgr565(0.1F, -0.3F, 0.5F);
            var actual = default(Rgba32);
            var expected = new Rgba32(25, 0, 132, 255);

            // act
            bgra.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgr565_ToBgr24()
        {
            // arrange
            var bgra = new Bgr565(0.1F, -0.3F, 0.5F);
            var actual = default(Bgr24);
            var expected = new Bgr24(25, 0, 132);

            // act
            bgra.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgr565_ToBgra32()
        {
            // arrange
            var bgra = new Bgr565(0.1F, -0.3F, 0.5F);
            var actual = default(Bgra32);
            var expected = new Bgra32(25, 0, 132, 255);

            // act
            bgra.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgr565_ToArgb32()
        {
            // arrange
            var bgra = new Bgr565(0.1F, -0.3F, 0.5F);
            var actual = default(Argb32);
            var expected = new Argb32(25, 0, 132, 255);

            // act
            bgra.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgr565_PackFromRgb48_ToRgb48()
        {
            // arrange
            var input = default(Bgr565);
            var actual = default(Rgb48);
            var expected = new Rgb48(65535, 0, 65535);

            // act
            input.PackFromRgb48(expected);
            input.ToRgb48(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgr565_PackFromRgba64_ToRgba64()
        {
            // arrange
            var input = default(Bgr565);
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
