// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class La16Tests
{
    public static readonly TheoryData<byte> LuminanceData
        = new() { 0, 1, 2, 3, 5, 13, 31, 71, 73, 79, 83, 109, 127, 128, 131, 199, 250, 251, 254, 255 };

    [Theory]
    [InlineData(0, 0)]
    [InlineData(255, 65535)]
    [InlineData(10, 2570)]
    [InlineData(42, 10794)]
    public void La16_PackedValue_EqualsPackedInput(byte input, ushort packed)
        => Assert.Equal(packed, new La16(input, input).PackedValue);

    [Fact]
    public void AreEqual()
    {
        La16 color1 = new(100, 50);
        La16 color2 = new(100, 50);

        Assert.Equal(color1, color2);
    }

    [Fact]
    public void AreNotEqual()
    {
        La16 color1 = new(100, 50);
        La16 color2 = new(200, 50);

        Assert.NotEqual(color1, color2);
    }

    [Fact]
    public void La16_FromScaledVector4()
    {
        // Arrange
        const ushort expected = 32896;
        Vector4 scaled = new La16(128, 128).ToScaledVector4();

        // Act
        La16 gray = La16.FromScaledVector4(scaled);
        ushort actual = gray.PackedValue;

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(LuminanceData))]
    public void La16_ToScaledVector4(byte input)
    {
        // Arrange
        La16 gray = new(input, input);

        // Act
        Vector4 actual = gray.ToScaledVector4();

        // Assert
        float scaledInput = input / 255F;
        Assert.Equal(scaledInput, actual.X);
        Assert.Equal(scaledInput, actual.Y);
        Assert.Equal(scaledInput, actual.Z);
        Assert.Equal(scaledInput, actual.W);
    }

    [Theory]
    [MemberData(nameof(LuminanceData))]
    public void La16_FromVector4(byte luminance)
    {
        // Arrange
        Vector4 vector = new La16(luminance, luminance).ToVector4();

        // Act
        La16 gray = La16.FromVector4(vector);
        byte actualL = gray.L;
        byte actualA = gray.A;

        // Assert
        Assert.Equal(luminance, actualL);
        Assert.Equal(luminance, actualA);
    }

    [Theory]
    [MemberData(nameof(LuminanceData))]
    public void La16_ToVector4(byte input)
    {
        // Arrange
        La16 gray = new(input, input);

        // Act
        Vector4 actual = gray.ToVector4();

        // Assert
        float scaledInput = input / 255F;
        Assert.Equal(scaledInput, actual.X);
        Assert.Equal(scaledInput, actual.Y);
        Assert.Equal(scaledInput, actual.Z);
        Assert.Equal(scaledInput, actual.W);
    }

    [Theory]
    [MemberData(nameof(LuminanceData))]
    public void La16_FromRgba32(byte rgb)
    {
        // Arrange
        byte expected = ColorNumerics.Get8BitBT709Luminance(rgb, rgb, rgb);

        // Act
        La16 gray = La16.FromRgba32(new Rgba32(rgb, rgb, rgb));
        byte actual = gray.L;

        // Assert
        Assert.Equal(expected, actual);
        Assert.Equal(255, gray.A);
    }

    [Theory]
    [MemberData(nameof(LuminanceData))]
    public void La16_ToRgba32(byte luminance)
    {
        // Arrange
        La16 gray = new(luminance, luminance);

        // Act
        Rgba32 actual = gray.ToRgba32();

        // Assert
        Assert.Equal(luminance, actual.R);
        Assert.Equal(luminance, actual.G);
        Assert.Equal(luminance, actual.B);
        Assert.Equal(luminance, actual.A);
    }

    [Fact]
    public void La16_FromBgra5551()
    {
        // arrange
        const byte expected = byte.MaxValue;

        // act
        La16 grey = La16.FromBgra5551(new Bgra5551(1.0f, 1.0f, 1.0f, 1.0f));

        // assert
        Assert.Equal(expected, grey.L);
        Assert.Equal(expected, grey.A);
    }

    public class Rgba32Compatibility
    {
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public static readonly TheoryData<byte> LuminanceData = La16Tests.LuminanceData;

        [Theory]
        [MemberData(nameof(LuminanceData))]
        public void La16_FromRgba32_IsInverseOf_ToRgba32(byte luminance)
        {
            La16 original = new(luminance, luminance);

            Rgba32 rgba = original.ToRgba32();

            La16 mirror = La16.FromRgba32(rgba);

            Assert.Equal(original, mirror);
        }

        [Theory]
        [MemberData(nameof(LuminanceData))]
        public void Rgba32_ToLa16_IsInverseOf_La16_ToRgba32(byte luminance)
        {
            La16 original = new(luminance, luminance);

            Rgba32 rgba = original.ToRgba32();

            La16 mirror = La16.FromRgba32(rgba);

            Assert.Equal(original, mirror);
        }

        [Theory]
        [MemberData(nameof(LuminanceData))]
        public void ToVector4_IsRgba32Compatible(byte luminance)
        {
            La16 original = new(luminance, luminance);

            Rgba32 rgba = original.ToRgba32();

            Vector4 la16Vector = original.ToVector4();
            Vector4 rgbaVector = rgba.ToVector4();

            Assert.Equal(la16Vector, rgbaVector, new ApproximateFloatComparer(1e-5f));
        }

        [Theory]
        [MemberData(nameof(LuminanceData))]
        public void FromVector4_IsRgba32Compatible(byte luminance)
        {
            La16 original = new(luminance, luminance);

            Rgba32 rgba = original.ToRgba32();
            Vector4 rgbaVector = rgba.ToVector4();

            La16 mirror = La16.FromVector4(rgbaVector);

            Assert.Equal(original, mirror);
        }

        [Theory]
        [MemberData(nameof(LuminanceData))]
        public void ToScaledVector4_IsRgba32Compatible(byte luminance)
        {
            La16 original = new(luminance, luminance);

            Rgba32 rgba = original.ToRgba32();

            Vector4 la16Vector = original.ToScaledVector4();
            Vector4 rgbaVector = rgba.ToScaledVector4();

            Assert.Equal(la16Vector, rgbaVector, new ApproximateFloatComparer(1e-5f));
        }

        [Theory]
        [MemberData(nameof(LuminanceData))]
        public void FromScaledVector4_IsRgba32Compatible(byte luminance)
        {
            La16 original = new(luminance, luminance);

            Rgba32 rgba = original.ToRgba32();
            Vector4 rgbaVector = rgba.ToScaledVector4();

            La16 mirror = La16.FromScaledVector4(rgbaVector);

            Assert.Equal(original, mirror);
        }

        [Fact]
        public void La16_PixelInformation()
        {
            PixelTypeInfo info = La16.GetPixelTypeInfo();
            Assert.Equal(Unsafe.SizeOf<La16>() * 8, info.BitsPerPixel);
            Assert.Equal(PixelAlphaRepresentation.Unassociated, info.AlphaRepresentation);
            Assert.Equal(PixelColorType.Luminance | PixelColorType.Alpha, info.ColorType);

            PixelComponentInfo componentInfo = info.ComponentInfo.Value;
            Assert.Equal(2, componentInfo.ComponentCount);
            Assert.Equal(0, componentInfo.Padding);
            Assert.Equal(8, componentInfo.GetComponentPrecision(0));
            Assert.Equal(8, componentInfo.GetComponentPrecision(1));
            Assert.Equal(8, componentInfo.GetMaximumComponentPrecision());
        }
    }
}
