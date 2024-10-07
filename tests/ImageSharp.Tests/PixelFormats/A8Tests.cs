// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

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
        A8 left = new(16);
        A8 right = new(32);

        Assert.True(left == new A8(16));
        Assert.True(left != right);
        Assert.Equal(left, (object)new A8(16));
    }

    [Fact]
    public void A8_FromScaledVector4()
    {
        // Arrange
        const int expected = 128;
        Vector4 scaled = new A8(.5F).ToScaledVector4();

        // Act
        A8 alpha = A8.FromScaledVector4(scaled);
        byte actual = alpha.PackedValue;

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void A8_ToScaledVector4()
    {
        // Arrange
        A8 alpha = new(.5F);

        // Act
        Vector4 actual = alpha.ToScaledVector4();

        // Assert
        Assert.Equal(0, actual.X);
        Assert.Equal(0, actual.Y);
        Assert.Equal(0, actual.Z);
        Assert.Equal(.5F, actual.W, precision: 2);
    }

    [Fact]
    public void A8_ToVector4()
    {
        // Arrange
        A8 alpha = new(.5F);

        // Act
        Vector4 actual = alpha.ToVector4();

        // Assert
        Assert.Equal(0, actual.X);
        Assert.Equal(0, actual.Y);
        Assert.Equal(0, actual.Z);
        Assert.Equal(.5F, actual.W, precision: 2);
    }

    [Fact]
    public void A8_ToRgba32()
    {
        A8 input = new(128);
        Rgba32 expected = new(0, 0, 0, 128);

        Rgba32 actual = input.ToRgba32();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void A8_FromBgra5551()
    {
        // arrange
        const byte expected = byte.MaxValue;

        // act
        A8 alpha = A8.FromBgra5551(new(0.0f, 0.0f, 0.0f, 1.0f));

        // assert
        Assert.Equal(expected, alpha.PackedValue);
    }

    [Fact]
    public void A8_PixelInformation()
    {
        PixelTypeInfo info = A8.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<A8>() * 8, info.BitsPerPixel);
        Assert.Equal(PixelAlphaRepresentation.Unassociated, info.AlphaRepresentation);
        Assert.Equal(PixelColorType.Alpha, info.ColorType);

        PixelComponentInfo componentInfo = info.ComponentInfo.Value;
        Assert.Equal(1, componentInfo.ComponentCount);
        Assert.Equal(0, componentInfo.Padding);
        Assert.Equal(8, componentInfo.GetComponentPrecision(0));
        Assert.Equal(8, componentInfo.GetMaximumComponentPrecision());
    }
}
