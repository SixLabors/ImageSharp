// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class Gray8Tests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(255)]
        [InlineData(10)]
        [InlineData(42)]
        public void Gray8_PackedValue_EqualsInput(byte input)
            => Assert.Equal(input, new Gray8(input).PackedValue);

        [Fact]
        public void Gray8_FromScaledVector4()
        {
            // Arrange
            Gray8 gray = default;
            const byte expected = 128;
            Vector4 scaled = new Gray8(expected).ToScaledVector4();

            // Act
            gray.FromScaledVector4(scaled);
            byte actual = gray.PackedValue;

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(255)]
        [InlineData(30)]
        public void Gray8_ToScaledVector4(byte input)
        {
            // Arrange
            var gray = new Gray8(input);

            // Act
            Vector4 actual = gray.ToScaledVector4();

            // Assert
            float scaledInput = input / 255F;
            Assert.Equal(scaledInput, actual.X);
            Assert.Equal(scaledInput, actual.Y);
            Assert.Equal(scaledInput, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Gray8_FromVector4()
        {
            // Arrange
            Gray8 gray = default;
            const int expected = 128;
            var vector = new Gray8(expected).ToVector4();

            // Act
            gray.FromVector4(vector);
            byte actual = gray.PackedValue;

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(255)]
        [InlineData(30)]
        public void Gray8_ToVector4(byte input)
        {
            // Arrange
            var gray = new Gray8(input);

            // Act
            var actual = gray.ToVector4();

            // Assert
            float scaledInput = input / 255F;
            Assert.Equal(scaledInput, actual.X);
            Assert.Equal(scaledInput, actual.Y);
            Assert.Equal(scaledInput, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Gray8_FromRgba32()
        {
            // Arrange
            Gray8 gray = default;
            const byte rgb = 128;
            byte expected = ImageMaths.Get8BitBT709Luminance(rgb, rgb, rgb);

            // Act
            gray.FromRgba32(new Rgba32(rgb, rgb, rgb));
            byte actual = gray.PackedValue;

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(255)]
        [InlineData(30)]
        public void Gray8_ToRgba32(byte input)
        {
            // Arrange
            var gray = new Gray8(input);

            // Act
            var actual = gray.ToRgba32();

            // Assert
            Assert.Equal(input, actual.R);
            Assert.Equal(input, actual.G);
            Assert.Equal(input, actual.B);
            Assert.Equal(byte.MaxValue, actual.A);
        }
    }
}