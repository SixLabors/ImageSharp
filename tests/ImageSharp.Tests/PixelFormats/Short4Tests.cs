// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class Short4Tests
    {
        [Fact]
        public void Short4_PackedValues()
        {
            var shortValue1 = new Short4(11547, 12653, 29623, 193);
            var shortValue2 = new Short4(0.1f, -0.3f, 0.5f, -0.7f);

            Assert.Equal((ulong)0x00c173b7316d2d1b, shortValue1.PackedValue);
            Assert.Equal(18446462598732840960, shortValue2.PackedValue);
            Assert.Equal((ulong)0x0, new Short4(Vector4.Zero).PackedValue);
            Assert.Equal((ulong)0x7FFF7FFF7FFF7FFF, new Short4(Vector4.One * 0x7FFF).PackedValue);
            Assert.Equal(0x8000800080008000, new Short4(Vector4.One * -0x8000).PackedValue);
        }

        [Fact]
        public void Short4_ToVector4()
        {
            Assert.Equal(Vector4.One * 0x7FFF, new Short4(Vector4.One * 0x7FFF).ToVector4());
            Assert.Equal(Vector4.Zero, new Short4(Vector4.Zero).ToVector4());
            Assert.Equal(Vector4.One * -0x8000, new Short4(Vector4.One * -0x8000).ToVector4());
            Assert.Equal(Vector4.UnitX * 0x7FFF, new Short4(Vector4.UnitX * 0x7FFF).ToVector4());
            Assert.Equal(Vector4.UnitY * 0x7FFF, new Short4(Vector4.UnitY * 0x7FFF).ToVector4());
            Assert.Equal(Vector4.UnitZ * 0x7FFF, new Short4(Vector4.UnitZ * 0x7FFF).ToVector4());
            Assert.Equal(Vector4.UnitW * 0x7FFF, new Short4(Vector4.UnitW * 0x7FFF).ToVector4());
        }

        [Fact]
        public void Short4_ToScaledVector4()
        {
            // arrange
            var short4 = new Short4(Vector4.One * 0x7FFF);

            // act
            Vector4 actual = short4.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Short4_PackFromScaledVector4()
        {
            // arrange
            var short4 = new Short4(Vector4.One * 0x7FFF);
            Vector4 scaled = short4.ToScaledVector4();
            long expected = 0x7FFF7FFF7FFF7FFF;

            // act
            var pixel = default(Short4);
            pixel.PackFromScaledVector4(scaled);
            ulong actual = pixel.PackedValue;

            // assert
            Assert.Equal((ulong)expected, pixel.PackedValue);
        }

        [Fact]
        public void Short4_Clamping()
        {
            // arrange
            var short1 = new Short4(Vector4.One * 1234567.0f);
            var short2 = new Short4(Vector4.One * -1234567.0f);

            // act
            var vector1 = short1.ToVector4();
            var vector2 = short2.ToVector4();

            // assert
            Assert.Equal(Vector4.One * 0x7FFF, vector1);
            Assert.Equal(Vector4.One * -0x8000, vector2);
        }

        [Fact]
        public void Short4_ToRgb24()
        {
            // arrange
            var shortValue = new Short4(11547, 12653, 29623, 193);
            var actual = default(Rgb24);
            var expected = new Rgb24(172, 177, 243);

            // act
            shortValue.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short4_ToBgr24()
        {
            // arrange
            var shortValue = new Short4(11547, 12653, 29623, 193);
            var actual = default(Bgr24);
            var expected = new Bgr24(172, 177, 243);

            // act
            shortValue.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short4_ToRgba32()
        {
            // arrange
            var shortValue = new Short4(11547, 12653, 29623, 193);
            var actual = default(Rgba32);
            var expected = new Rgba32(172, 177, 243, 128);

            // act
            shortValue.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short4_ToBgra32()
        {
            // arrange
            var shortValue = new Short4(11547, 12653, 29623, 193);
            var actual = default(Bgra32);
            var expected = new Bgra32(172, 177, 243, 128);

            // act
            shortValue.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short4_ToArgb32()
        {
            // arrange
            var shortValue = new Short4(11547, 12653, 29623, 193);
            var actual = default(Argb32);
            var expected = new Argb32(172, 177, 243, 128);

            // act
            shortValue.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short4_PackFromRgba32_ToRgba32()
        {
            // arrange
            var short4 = default(Short4);
            var actual = default(Rgba32);
            var expected = new Rgba32(20, 38, 0, 255);

            // act 
            short4.PackFromRgba32(expected);
            short4.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short4_PackFromBgra32_ToRgba32()
        {
            // arrange
            var short4 = default(Short4);
            var actual = default(Bgra32);
            var expected = new Bgra32(20, 38, 0, 255);

            // act 
            short4.PackFromBgra32(expected);
            short4.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short4_PackFromArgb32_ToRgba32()
        {
            // arrange
            var short4 = default(Short4);
            var actual = default(Argb32);
            var expected = new Argb32(20, 38, 0, 255);

            // act 
            short4.PackFromArgb32(expected);
            short4.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short4_PackFromRgb48_ToRgb48()
        {
            // arrange
            var input = default(Short4);
            var actual = default(Rgb48);
            var expected = new Rgb48(65535, 0, 65535);

            // act
            input.PackFromRgb48(expected);
            input.ToRgb48(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short4_PackFromRgba64_ToRgba64()
        {
            // arrange
            var input = default(Short4);
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
