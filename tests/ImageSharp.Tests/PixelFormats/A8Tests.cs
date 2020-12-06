// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    [Trait("Category", "PixelFormats")]
    public class A8Tests
    {
        [Fact]
        public void A8_Constructor()
        {
            // Test the limits.
            Assert.Equal(byte.MinValue, new A8(0F).PackedValue);
            Assert.Equal(byte.MaxValue, new A8(1F).PackedValue);

            // Test clamping.
            Assert.Equal(byte.MinValue, new A8(-1234F).PackedValue);
            Assert.Equal(byte.MaxValue, new A8(1234F).PackedValue);

            // Test ordering
            Assert.Equal(124, new A8(124F / byte.MaxValue).PackedValue);
            Assert.Equal(26, new A8(0.1F).PackedValue);
        }

        [Fact]
        public void A8_Equality()
        {
            var left = new A8(16);
            var right = new A8(32);

            Assert.True(left == new A8(16));
            Assert.True(left != right);
            Assert.Equal(left, (object)new A8(16));
        }

        [Fact]
        public void A8_FromScaledVector4()
        {
            // Arrange
            A8 alpha = default;
            int expected = 128;
            Vector4 scaled = new A8(.5F).ToScaledVector4();

            // Act
            alpha.FromScaledVector4(scaled);
            byte actual = alpha.PackedValue;

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void A8_ToScaledVector4()
        {
            // Arrange
            var alpha = new A8(.5F);

            // Act
            Vector4 actual = alpha.ToScaledVector4();

            // Assert
            Assert.Equal(0, actual.X);
            Assert.Equal(0, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(.5F, actual.W, 2);
        }

        [Fact]
        public void A8_ToVector4()
        {
            // Arrange
            var alpha = new A8(.5F);

            // Act
            var actual = alpha.ToVector4();

            // Assert
            Assert.Equal(0, actual.X);
            Assert.Equal(0, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(.5F, actual.W, 2);
        }

        [Fact]
        public void A8_ToRgba32()
        {
            var input = new A8(128);
            var expected = new Rgba32(0, 0, 0, 128);

            Rgba32 actual = default;
            input.ToRgba32(ref actual);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void A8_FromBgra5551()
        {
            // arrange
            var alpha = default(A8);
            byte expected = byte.MaxValue;

            // act
            alpha.FromBgra5551(new Bgra5551(0.0f, 0.0f, 0.0f, 1.0f));

            // assert
            Assert.Equal(expected, alpha.PackedValue);
        }
    }
}
