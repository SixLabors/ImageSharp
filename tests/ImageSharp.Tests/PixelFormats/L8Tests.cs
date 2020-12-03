// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

using SixLabors.ImageSharp.PixelFormats;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    [Trait("Category", "PixelFormats")]
    public class L8Tests
    {
        public static readonly TheoryData<byte> LuminanceData
            = new TheoryData<byte>
            {
                0,
                1,
                2,
                3,
                5,
                13,
                31,
                71,
                73,
                79,
                83,
                109,
                127,
                128,
                131,
                199,
                250,
                251,
                254,
                255
            };

        [Theory]
        [InlineData(0)]
        [InlineData(255)]
        [InlineData(10)]
        [InlineData(42)]
        public void L8_PackedValue_EqualsInput(byte input)
            => Assert.Equal(input, new L8(input).PackedValue);

        [Fact]
        public void AreEqual()
        {
            var color1 = new L8(100);
            var color2 = new L8(100);

            Assert.Equal(color1, color2);
        }

        [Fact]
        public void AreNotEqual()
        {
            var color1 = new L8(100);
            var color2 = new L8(200);

            Assert.NotEqual(color1, color2);
        }

        [Fact]
        public void L8_FromScaledVector4()
        {
            // Arrange
            L8 gray = default;
            const byte expected = 128;
            Vector4 scaled = new L8(expected).ToScaledVector4();

            // Act
            gray.FromScaledVector4(scaled);
            byte actual = gray.PackedValue;

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(LuminanceData))]
        public void L8_ToScaledVector4(byte input)
        {
            // Arrange
            var gray = new L8(input);

            // Act
            Vector4 actual = gray.ToScaledVector4();

            // Assert
            float scaledInput = input / 255F;
            Assert.Equal(scaledInput, actual.X);
            Assert.Equal(scaledInput, actual.Y);
            Assert.Equal(scaledInput, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Theory]
        [MemberData(nameof(LuminanceData))]
        public void L8_FromVector4(byte luminance)
        {
            // Arrange
            L8 gray = default;
            var vector = new L8(luminance).ToVector4();

            // Act
            gray.FromVector4(vector);
            byte actual = gray.PackedValue;

            // Assert
            Assert.Equal(luminance, actual);
        }

        [Theory]
        [MemberData(nameof(LuminanceData))]
        public void L8_ToVector4(byte input)
        {
            // Arrange
            var gray = new L8(input);

            // Act
            var actual = gray.ToVector4();

            // Assert
            float scaledInput = input / 255F;
            Assert.Equal(scaledInput, actual.X);
            Assert.Equal(scaledInput, actual.Y);
            Assert.Equal(scaledInput, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Theory]
        [MemberData(nameof(LuminanceData))]
        public void L8_FromRgba32(byte rgb)
        {
            // Arrange
            L8 gray = default;
            byte expected = ColorNumerics.Get8BitBT709Luminance(rgb, rgb, rgb);

            // Act
            gray.FromRgba32(new Rgba32(rgb, rgb, rgb));
            byte actual = gray.PackedValue;

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(LuminanceData))]
        public void L8_ToRgba32(byte luminance)
        {
            // Arrange
            var gray = new L8(luminance);

            // Act
            Rgba32 actual = default;
            gray.ToRgba32(ref actual);

            // Assert
            Assert.Equal(luminance, actual.R);
            Assert.Equal(luminance, actual.G);
            Assert.Equal(luminance, actual.B);
            Assert.Equal(byte.MaxValue, actual.A);
        }

        [Fact]
        public void L8_FromBgra5551()
        {
            // arrange
            var grey = default(L8);
            byte expected = byte.MaxValue;

            // act
            grey.FromBgra5551(new Bgra5551(1.0f, 1.0f, 1.0f, 1.0f));

            // assert
            Assert.Equal(expected, grey.PackedValue);
        }

        public class Rgba32Compatibility
        {
            // ReSharper disable once MemberHidesStaticFromOuterClass
            public static readonly TheoryData<byte> LuminanceData = L8Tests.LuminanceData;

            [Theory]
            [MemberData(nameof(LuminanceData))]
            public void L8_FromRgba32_IsInverseOf_ToRgba32(byte luminance)
            {
                var original = new L8(luminance);

                Rgba32 rgba = default;
                original.ToRgba32(ref rgba);

                L8 mirror = default;
                mirror.FromRgba32(rgba);

                Assert.Equal(original, mirror);
            }

            [Theory]
            [MemberData(nameof(LuminanceData))]
            public void Rgba32_ToL8_IsInverseOf_L8_ToRgba32(byte luminance)
            {
                var original = new L8(luminance);

                Rgba32 rgba = default;
                original.ToRgba32(ref rgba);

                L8 mirror = default;
                mirror.FromRgba32(rgba);

                Assert.Equal(original, mirror);
            }

            [Theory]
            [MemberData(nameof(LuminanceData))]
            public void ToVector4_IsRgba32Compatible(byte luminance)
            {
                var original = new L8(luminance);

                Rgba32 rgba = default;
                original.ToRgba32(ref rgba);

                var l8Vector = original.ToVector4();
                var rgbaVector = original.ToVector4();

                Assert.Equal(l8Vector, rgbaVector, new ApproximateFloatComparer(1e-5f));
            }

            [Theory]
            [MemberData(nameof(LuminanceData))]
            public void FromVector4_IsRgba32Compatible(byte luminance)
            {
                var original = new L8(luminance);

                Rgba32 rgba = default;
                original.ToRgba32(ref rgba);

                var rgbaVector = original.ToVector4();

                L8 mirror = default;
                mirror.FromVector4(rgbaVector);

                Assert.Equal(original, mirror);
            }

            [Theory]
            [MemberData(nameof(LuminanceData))]
            public void ToScaledVector4_IsRgba32Compatible(byte luminance)
            {
                var original = new L8(luminance);

                Rgba32 rgba = default;
                original.ToRgba32(ref rgba);

                Vector4 l8Vector = original.ToScaledVector4();
                Vector4 rgbaVector = original.ToScaledVector4();

                Assert.Equal(l8Vector, rgbaVector, new ApproximateFloatComparer(1e-5f));
            }

            [Theory]
            [MemberData(nameof(LuminanceData))]
            public void FromScaledVector4_IsRgba32Compatible(byte luminance)
            {
                var original = new L8(luminance);

                Rgba32 rgba = default;
                original.ToRgba32(ref rgba);

                Vector4 rgbaVector = original.ToScaledVector4();

                L8 mirror = default;
                mirror.FromScaledVector4(rgbaVector);

                Assert.Equal(original, mirror);
            }
        }
    }
}
