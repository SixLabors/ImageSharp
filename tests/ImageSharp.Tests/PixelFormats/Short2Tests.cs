// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class Short2Tests
    {
        [Fact]
        public void Short2_PackedValues()
        {
            // Test ordering
            Assert.Equal((uint)0x361d2db1, new Short2(0x2db1, 0x361d).PackedValue);
            Assert.Equal(4294639744, new Short2(127.5f, -5.3f).PackedValue);
            // Test the limits.
            Assert.Equal((uint)0x0, new Short2(Vector2.Zero).PackedValue);
            Assert.Equal((uint)0x7FFF7FFF, new Short2(Vector2.One * 0x7FFF).PackedValue);
            Assert.Equal(0x80008000, new Short2(Vector2.One * -0x8000).PackedValue);
        }

        [Fact]
        public void Short2_ToVector2()
        {
            Assert.Equal(Vector2.One * 0x7FFF, new Short2(Vector2.One * 0x7FFF).ToVector2());
            Assert.Equal(Vector2.Zero, new Short2(Vector2.Zero).ToVector2());
            Assert.Equal(Vector2.One * -0x8000, new Short2(Vector2.One * -0x8000).ToVector2());
            Assert.Equal(Vector2.UnitX * 0x7FFF, new Short2(Vector2.UnitX * 0x7FFF).ToVector2());
            Assert.Equal(Vector2.UnitY * 0x7FFF, new Short2(Vector2.UnitY * 0x7FFF).ToVector2());
        }

        [Fact]
        public void Short2_ToVector4()
        {
            Assert.Equal(new Vector4(0x7FFF, 0x7FFF, 0, 1), (new Short2(Vector2.One * 0x7FFF)).ToVector4());
            Assert.Equal(new Vector4(0, 0, 0, 1), (new Short2(Vector2.Zero)).ToVector4());
            Assert.Equal(new Vector4(-0x8000, -0x8000, 0, 1), (new Short2(Vector2.One * -0x8000)).ToVector4());
        }

        [Fact]
        public void Short2_Clamping()
        {
            Assert.Equal(Vector2.One * 0x7FFF, new Short2(Vector2.One * 1234567.0f).ToVector2());
            Assert.Equal(Vector2.One * -0x8000, new Short2(Vector2.One * -1234567.0f).ToVector2());
        }

        [Fact]
        public void Short2_ToScaledVector4()
        {
            // arrange
            var short2 = new Short2(Vector2.One * 0x7FFF);

            // act
            Vector4 actual = short2.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Short2_PackFromScaledVector4()
        {
            // arrange
            var pixel = default(Short2);
            var short2 = new Short2(Vector2.One * 0x7FFF);
            ulong expected = 0x7FFF7FFF;

            // act 
            Vector4 scaled = short2.ToScaledVector4();
            pixel.PackFromScaledVector4(scaled);
            uint actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short2_ToRgb24()
        {
            // arrange
            var short2 = new Short2(127.5f, -5.3f);
            var actual = default(Rgb24);
            var expected = new Rgb24(128, 127, 0);

            // act
            short2.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short2_ToRgba32()
        {
            // arrange
            var short2 = new Short2(127.5f, -5.3f);
            var actual = default(Rgba32);
            var expected = new Rgba32(128, 127, 0, 255);

            // act
            short2.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short2_ToBgr24()
        {
            // arrange
            var short2 = new Short2(127.5f, -5.3f);
            var actual = default(Bgr24);
            var expected = new Bgr24(128, 127, 0);

            // act
            short2.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short2_ToArgb32()
        {
            // arrange
            var short2 = new Short2(127.5f, -5.3f);
            var actual = default(Argb32);
            var expected = new Argb32(128, 127, 0, 255);

            // act
            short2.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short2_ToBgra32()
        {
            // arrange
            var short2 = new Short2(127.5f, -5.3f);
            var actual = default(Bgra32);
            var expected = new Bgra32(128, 127, 0, 255);

            // act
            short2.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short2_PackFromRgba32_ToRgba32()
        {
            // arrange
            var short2 = default(Short2);
            var actual = default(Rgba32);
            var expected = new Rgba32(20, 38, 0, 255);

            // act 
            short2.PackFromRgba32(expected);
            short2.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short2_PackFromRgb48_ToRgb48()
        {
            // arrange
            var input = default(Short2);
            var actual = default(Rgb48);
            var expected = new Rgb48(65535, 65535, 0);

            // act
            input.PackFromRgb48(expected);
            input.ToRgb48(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short2_PackFromRgba64_ToRgba64()
        {
            // arrange
            var input = default(Short2);
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
