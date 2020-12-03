// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    [Trait("Category", "PixelFormats")]
    public class Short4Tests
    {
        [Fact]
        public void Short4_PackedValues()
        {
            var shortValue1 = new Short4(11547, 12653, 29623, 193);
            var shortValue2 = new Short4(0.1f, -0.3f, 0.5f, -0.7f);

            Assert.Equal(0x00c173b7316d2d1bUL, shortValue1.PackedValue);
            Assert.Equal(18446462598732840960, shortValue2.PackedValue);
            Assert.Equal(0x0UL, new Short4(Vector4.Zero).PackedValue);
            Assert.Equal(0x7FFF7FFF7FFF7FFFUL, new Short4(Vector4.One * 0x7FFF).PackedValue);
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
        public void Short4_FromScaledVector4()
        {
            // arrange
            var short4 = new Short4(Vector4.One * 0x7FFF);
            Vector4 scaled = short4.ToScaledVector4();
            const long expected = 0x7FFF7FFF7FFF7FFF;

            // act
            var pixel = default(Short4);
            pixel.FromScaledVector4(scaled);

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
        public void Short4_FromRgba32_ToRgba32()
        {
            // arrange
            var short4 = default(Short4);
            var actual = default(Rgba32);
            var expected = new Rgba32(20, 38, 0, 255);

            // act
            short4.FromRgba32(expected);
            short4.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short4_FromBgra32_ToRgba32()
        {
            // arrange
            var short4 = default(Short4);
            var actual = default(Bgra32);
            var expected = new Bgra32(20, 38, 0, 255);

            // act
            short4.FromBgra32(expected);
            Rgba32 temp = default;
            short4.ToRgba32(ref temp);
            actual.FromRgba32(temp);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short4_FromArgb32_ToRgba32()
        {
            // arrange
            var short4 = default(Short4);
            var actual = default(Argb32);
            var expected = new Argb32(20, 38, 0, 255);

            // act
            short4.FromArgb32(expected);
            Rgba32 temp = default;
            short4.ToRgba32(ref temp);
            actual.FromRgba32(temp);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short4_FromRgb48_ToRgb48()
        {
            // arrange
            var input = default(Short4);
            var actual = default(Rgb48);
            var expected = new Rgb48(65535, 0, 65535);

            // act
            input.FromRgb48(expected);
            actual.FromScaledVector4(input.ToScaledVector4());

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short4_FromRgba64_ToRgba64()
        {
            // arrange
            var input = default(Short4);
            var actual = default(Rgba64);
            var expected = new Rgba64(65535, 0, 65535, 0);

            // act
            input.FromRgba64(expected);
            actual.FromScaledVector4(input.ToScaledVector4());

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short4_FromBgra5551()
        {
            // arrange
            var short4 = default(Short4);
            Vector4 expected = Vector4.One;

            // act
            short4.FromBgra5551(new Bgra5551(1.0f, 1.0f, 1.0f, 1.0f));

            // assert
            Assert.Equal(expected, short4.ToScaledVector4());
        }
    }
}
