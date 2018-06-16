// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class NormalizedShort2Tests
    {
        [Fact]
        public void NormalizedShort2_PackedValue()
        {
            Assert.Equal(0xE6672CCC, new NormalizedShort2(0.35f, -0.2f).PackedValue);
            Assert.Equal(3650751693, new NormalizedShort2(0.1f, -0.3f).PackedValue);
            Assert.Equal((uint)0x0, new NormalizedShort2(Vector2.Zero).PackedValue);
            Assert.Equal((uint)0x7FFF7FFF, new NormalizedShort2(Vector2.One).PackedValue);
            Assert.Equal(0x80018001, new NormalizedShort2(-Vector2.One).PackedValue);
            // TODO: I don't think this can ever pass since the bytes are already truncated.
            // Assert.Equal(3650751693, n.PackedValue);
        }

        [Fact]
        public void NormalizedShort2_ToVector2()
        {
            Assert.Equal(Vector2.One, new NormalizedShort2(Vector2.One).ToVector2());
            Assert.Equal(Vector2.Zero, new NormalizedShort2(Vector2.Zero).ToVector2());
            Assert.Equal(-Vector2.One, new NormalizedShort2(-Vector2.One).ToVector2());
            Assert.Equal(Vector2.One, new NormalizedShort2(Vector2.One * 1234.0f).ToVector2());
            Assert.Equal(-Vector2.One, new NormalizedShort2(Vector2.One * -1234.0f).ToVector2());
        }

        [Fact]
        public void NormalizedShort2_ToVector4()
        {
            Assert.Equal(new Vector4(1, 1, 0, 1), (new NormalizedShort2(Vector2.One)).ToVector4());
            Assert.Equal(new Vector4(0, 0, 0, 1), (new NormalizedShort2(Vector2.Zero)).ToVector4());
        }

        [Fact]
        public void NormalizedShort2_ToScaledVector4()
        {
            // arrange
            var short2 = new NormalizedShort2(-Vector2.One);

            // act
            Vector4 actual = short2.ToScaledVector4();

            // assert
            Assert.Equal(0, actual.X);
            Assert.Equal(0, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void NormalizedShort2_PackFromScaledVector4()
        {
            // arrange
            Vector4 scaled = new NormalizedShort2(-Vector2.One).ToScaledVector4();
            var short2 = default(NormalizedShort2);
            uint expected = 0x80018001;

            // act
            short2.PackFromScaledVector4(scaled);
            uint actual = short2.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort2_PackFromRgba32_ToRgb24()
        {
            // arrange
            var actual = default(Rgb24);
            var short2 = new NormalizedShort2();
            var rgba = new Rgba32(141, 90, 0, 0);
            var expected = new Rgb24(141, 90, 0);

            // act
            short2.PackFromRgba32(rgba);
            short2.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort2_ToRgb24()
        {
            // arrange
            var short2 = new NormalizedShort2(0.1f, -0.3f);
            var actual = default(Rgb24);
            var expected = new Rgb24(141, 90, 0);

            // act
            short2.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort2_ToRgba32()
        {
            // arrange
            var short2 = new NormalizedShort2(0.1f, -0.3f);
            var actual = default(Rgba32);
            var expected = new Rgba32(141, 90, 0, 255);

            // act
            short2.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort2_ToBgr24()
        {
            // arrange
            var short2 = new NormalizedShort2(0.1f, -0.3f);
            var actual = default(Bgr24);
            var expected = new Bgr24(141, 90, 0);

            // act
            short2.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort2_ToBgra32()
        {
            // arrange
            var short2 = new NormalizedShort2(0.1f, -0.3f);
            var actual = default(Bgra32);
            var expected = new Bgra32(141, 90, 0, 255);

            // act
            short2.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort2_ToArgb32()
        {
            // arrange
            var short2 = new NormalizedShort2(0.1f, -0.3f);
            var actual = default(Argb32);
            var expected = new Argb32(141, 90, 0, 255);

            // act
            short2.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort2_PackFromRgb48_ToRgb48()
        {
            // arrange
            var input = default(NormalizedShort2);
            var actual = default(Rgb48);
            var expected = new Rgb48(65535, 65535, 0);

            // act
            input.PackFromRgb48(expected);
            input.ToRgb48(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort2_PackFromRgba64_ToRgba64()
        {
            // arrange
            var input = default(NormalizedShort2);
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
