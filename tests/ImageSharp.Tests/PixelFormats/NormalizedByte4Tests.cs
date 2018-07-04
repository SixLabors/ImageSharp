// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class NormalizedByte4Tests
    {
        [Fact]
        public void NormalizedByte4_PackedValues()
        {
            Assert.Equal(0xA740DA0D, new NormalizedByte4(0.1f, -0.3f, 0.5f, -0.7f).PackedValue);
            Assert.Equal((uint)958796544, new NormalizedByte4(0.0008f, 0.15f, 0.30f, 0.45f).PackedValue);
            Assert.Equal((uint)0x0, new NormalizedByte4(Vector4.Zero).PackedValue);
            Assert.Equal((uint)0x7F7F7F7F, new NormalizedByte4(Vector4.One).PackedValue);
            Assert.Equal(0x81818181, new NormalizedByte4(-Vector4.One).PackedValue);
        }

        [Fact]
        public void NormalizedByte4_ToVector4()
        {
            Assert.Equal(Vector4.One, new NormalizedByte4(Vector4.One).ToVector4());
            Assert.Equal(Vector4.Zero, new NormalizedByte4(Vector4.Zero).ToVector4());
            Assert.Equal(-Vector4.One, new NormalizedByte4(-Vector4.One).ToVector4());
            Assert.Equal(Vector4.One, new NormalizedByte4(Vector4.One * 1234.0f).ToVector4());
            Assert.Equal(-Vector4.One, new NormalizedByte4(Vector4.One * -1234.0f).ToVector4());
        }

        [Fact]
        public void NormalizedByte4_ToScaledVector4()
        {
            // arrange
            var short4 = new NormalizedByte4(-Vector4.One);

            // act
            Vector4 actual = short4.ToScaledVector4();

            // assert
            Assert.Equal(0, actual.X);
            Assert.Equal(0, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(0, actual.W);
        }

        [Fact]
        public void NormalizedByte4_PackFromScaledVector4()
        {
            // arrange
            var pixel = default(NormalizedByte4);
            Vector4 scaled = new NormalizedByte4(-Vector4.One).ToScaledVector4();
            uint expected = 0x81818181;

            // act 
            pixel.PackFromScaledVector4(scaled);
            uint actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte4_ToRgb24()
        {
            // arrange
            var short4 = new NormalizedByte4(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Rgb24);
            var expected = new Rgb24(141, 90, 192);

            // act
            short4.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte4_ToRgba32()
        {
            // arrange
            var short4 = new NormalizedByte4(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Rgba32);
            var expected = new Rgba32(141, 90, 192, 39);

            // act
            short4.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte4_ToBgr24()
        {
            // arrange
            var short4 = new NormalizedByte4(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Bgr24);
            var expected = new Bgr24(141, 90, 192);

            // act
            short4.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte4_ToArgb32()
        {
            // arrange
            var short4 = new NormalizedByte4(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Argb32);
            var expected = new Argb32(141, 90, 192, 39);

            // act
            short4.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte4_PackFromRgba32_ToRgba32()
        {
            // arrange
            var short4 = default(NormalizedByte4);
            var actual = default(Rgba32);
            var expected = new Rgba32(9, 115, 202, 127);

            // act 
            short4.PackFromRgba32(new Rgba32(9, 115, 202, 127));
            short4.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte4_PackFromBgra32_ToRgba32()
        {
            // arrange
            var actual = default(Bgra32);
            var short4 = default(NormalizedByte4);
            var expected = new Bgra32(9, 115, 202, 127);

            // act 
            short4.PackFromBgra32(expected);
            short4.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte4_PackFromArgb32_ToRgba32()
        {
            // arrange
            var short4 = default(NormalizedByte4);
            var actual = default(Argb32);
            var expected = new Argb32(9, 115, 202, 127);

            // act 
            short4.PackFromArgb32(expected);
            short4.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte4_PackFromRgb48_ToRgb48()
        {
            // arrange
            var input = default(NormalizedByte4);
            var actual = default(Rgb48);
            var expected = new Rgb48(65535, 65535, 0);

            // act
            input.PackFromRgb48(expected);
            input.ToRgb48(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte4_PackFromRgba64_ToRgba64()
        {
            // arrange
            var input = default(NormalizedByte4);
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
