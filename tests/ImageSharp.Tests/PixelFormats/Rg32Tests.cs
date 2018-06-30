// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class Rg32Tests
    {
        [Fact]
        public void Rg32_PackedValues()
        {
            float x = 0xb6dc;
            float y = 0xA59f;
            Assert.Equal(0xa59fb6dc, new Rg32(x / 0xffff, y / 0xffff).PackedValue);
            Assert.Equal((uint)6554, new Rg32(0.1f, -0.3f).PackedValue);

            // Test the limits.
            Assert.Equal((uint)0x0, new Rg32(Vector2.Zero).PackedValue);
            Assert.Equal(0xFFFFFFFF, new Rg32(Vector2.One).PackedValue);
        }

        [Fact]
        public void Rg32_ToVector2()
        {
            Assert.Equal(Vector2.Zero, new Rg32(Vector2.Zero).ToVector2());
            Assert.Equal(Vector2.One, new Rg32(Vector2.One).ToVector2());
        }

        [Fact]
        public void Rg32_ToScaledVector4()
        {
            // arrange
            var rg32 = new Rg32(Vector2.One);

            // act
            Vector4 actual = rg32.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Rg32_PackFromScaledVector4()
        {
            // arrange
            var rg32 = new Rg32(Vector2.One);
            var pixel = default(Rg32);
            uint expected = 0xFFFFFFFF;

            // act
            Vector4 scaled = rg32.ToScaledVector4();
            pixel.PackFromScaledVector4(scaled);
            uint actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rg32_Clamping()
        {
            Assert.Equal(Vector2.Zero, new Rg32(Vector2.One * -1234.0f).ToVector2());
            Assert.Equal(Vector2.One, new Rg32(Vector2.One * 1234.0f).ToVector2());
        }

        [Fact]
        public void Rg32_ToRgb24()
        {
            // arrange
            var rg32 = new Rg32(0.1f, -0.3f);
            var actual = default(Rgb24);
            var expected = new Rgb24(25, 0, 0);

            // act
            rg32.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rg32_ToRgba32()
        {
            // arrange
            var rg32 = new Rg32(0.1f, -0.3f);
            var actual = default(Rgba32);
            var expected = new Rgba32(25, 0, 0, 255);

            // act
            rg32.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rg32_ToBgr24()
        {
            // arrange
            var rg32 = new Rg32(0.1f, -0.3f);
            var actual = default(Bgr24);
            var expected = new Bgr24(25, 0, 0);

            // act
            rg32.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rg32_ToArgb32()
        {
            // arrange
            var rg32 = new Rg32(0.1f, -0.3f);
            var actual = default(Argb32);
            var expected = new Argb32(25, 0, 0, 255);

            // act
            rg32.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rg32_PackFromRgb48_ToRgb48()
        {
            // arrange
            var input = default(Rg32);
            var actual = default(Rgb48);
            var expected = new Rgb48(65535, 65535, 0);

            // act
            input.PackFromRgb48(expected);
            input.ToRgb48(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rg32_PackFromRgba64_ToRgba64()
        {
            // arrange
            var input = default(Rg32);
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
