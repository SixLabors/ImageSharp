// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    [Trait("Category", "PixelFormats")]
    public class La32Tests
    {
        [Fact]
        public void AreEqual()
        {
            var color1 = new La32(3000, 100);
            var color2 = new La32(3000, 100);

            Assert.Equal(color1, color2);
        }

        [Fact]
        public void AreNotEqual()
        {
            var color1 = new La32(12345, 100);
            var color2 = new La32(54321, 100);

            Assert.NotEqual(color1, color2);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(65535, 4294967295)]
        [InlineData(32767, 2147450879)]
        [InlineData(42, 2752554)]
        public void La32_PackedValue_EqualsInput(ushort input, uint packed)
            => Assert.Equal(packed, new La32(input, input).PackedValue);

        [Fact]
        public void La32_FromScaledVector4()
        {
            // Arrange
            La32 gray = default;
            const ushort expected = 32767;
            Vector4 scaled = new La32(expected, expected).ToScaledVector4();

            // Act
            gray.FromScaledVector4(scaled);
            ushort actual = gray.L;
            ushort actualA = gray.A;

            // Assert
            Assert.Equal(expected, actual);
            Assert.Equal(expected, actualA);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(65535)]
        [InlineData(32767)]
        public void La32_ToScaledVector4(ushort input)
        {
            // Arrange
            var gray = new La32(input, input);

            // Act
            Vector4 actual = gray.ToScaledVector4();

            // Assert
            float vectorInput = input / 65535F;
            Assert.Equal(vectorInput, actual.X);
            Assert.Equal(vectorInput, actual.Y);
            Assert.Equal(vectorInput, actual.Z);
            Assert.Equal(vectorInput, actual.W);
        }

        [Fact]
        public void La32_FromVector4()
        {
            // Arrange
            La32 gray = default;
            const ushort expected = 32767;
            var vector = new La32(expected, expected).ToVector4();

            // Act
            gray.FromVector4(vector);
            ushort actual = gray.L;
            ushort actualA = gray.A;

            // Assert
            Assert.Equal(expected, actual);
            Assert.Equal(expected, actualA);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(65535)]
        [InlineData(32767)]
        public void La32_ToVector4(ushort input)
        {
            // Arrange
            var gray = new La32(input, input);

            // Act
            var actual = gray.ToVector4();

            // Assert
            float vectorInput = input / 65535F;
            Assert.Equal(vectorInput, actual.X);
            Assert.Equal(vectorInput, actual.Y);
            Assert.Equal(vectorInput, actual.Z);
            Assert.Equal(vectorInput, actual.W);
        }

        [Fact]
        public void La32_FromRgba32()
        {
            // Arrange
            La32 gray = default;
            const byte rgb = 128;
            ushort scaledRgb = ColorNumerics.UpscaleFrom8BitTo16Bit(rgb);
            ushort expected = ColorNumerics.Get16BitBT709Luminance(scaledRgb, scaledRgb, scaledRgb);

            // Act
            gray.FromRgba32(new Rgba32(rgb, rgb, rgb));
            ushort actual = gray.L;

            // Assert
            Assert.Equal(expected, actual);
            Assert.Equal(ushort.MaxValue, gray.A);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(65535)]
        [InlineData(8100)]
        public void La32_ToRgba32(ushort input)
        {
            // Arrange
            ushort expected = ColorNumerics.DownScaleFrom16BitTo8Bit(input);
            var gray = new La32(input, ushort.MaxValue);

            // Act
            Rgba32 actual = default;
            gray.ToRgba32(ref actual);

            // Assert
            Assert.Equal(expected, actual.R);
            Assert.Equal(expected, actual.G);
            Assert.Equal(expected, actual.B);
            Assert.Equal(byte.MaxValue, actual.A);
        }

        [Fact]
        public void La32_FromBgra5551()
        {
            // arrange
            var gray = default(La32);
            ushort expected = ushort.MaxValue;

            // act
            gray.FromBgra5551(new Bgra5551(1.0f, 1.0f, 1.0f, 1.0f));

            // assert
            Assert.Equal(expected, gray.L);
            Assert.Equal(expected, gray.A);
        }
    }
}
