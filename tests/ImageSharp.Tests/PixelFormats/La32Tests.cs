// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class La32Tests
{
    [Fact]
    public void AreEqual()
    {
        La32 color1 = new(3000, 100);
        La32 color2 = new(3000, 100);

        Assert.Equal(color1, color2);
    }

    [Fact]
    public void AreNotEqual()
    {
        La32 color1 = new(12345, 100);
        La32 color2 = new(54321, 100);

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
        const ushort expected = 32767;
        Vector4 scaled = new La32(expected, expected).ToScaledVector4();

        // Act
        La32 pixel = La32.FromScaledVector4(scaled);
        ushort actual = pixel.L;
        ushort actualA = pixel.A;

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
        La32 pixel = new(input, input);

        // Act
        Vector4 actual = pixel.ToScaledVector4();

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
        const ushort expected = 32767;
        Vector4 vector = new La32(expected, expected).ToVector4();

        // Act
        La32 pixel = La32.FromVector4(vector);
        ushort actual = pixel.L;
        ushort actualA = pixel.A;

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
        La32 pixel = new(input, input);

        // Act
        Vector4 actual = pixel.ToVector4();

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
        const byte rgb = 128;
        ushort scaledRgb = ColorNumerics.From8BitTo16Bit(rgb);
        ushort expected = ColorNumerics.Get16BitBT709Luminance(scaledRgb, scaledRgb, scaledRgb);

        // Act
        La32 pixel = La32.FromRgba32(new Rgba32(rgb, rgb, rgb));
        ushort actual = pixel.L;

        // Assert
        Assert.Equal(expected, actual);
        Assert.Equal(ushort.MaxValue, pixel.A);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65535)]
    [InlineData(8100)]
    public void La32_ToRgba32(ushort input)
    {
        // Arrange
        ushort expected = ColorNumerics.From16BitTo8Bit(input);
        La32 pixel = new(input, ushort.MaxValue);

        // Act
        Rgba32 actual = pixel.ToRgba32();

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
        const ushort expected = ushort.MaxValue;

        // act
        La32 pixel = La32.FromBgra5551(new Bgra5551(1f, 1f, 1f, 1f));

        // assert
        Assert.Equal(expected, pixel.L);
        Assert.Equal(expected, pixel.A);
    }

    [Fact]
    public void La32_PixelInformation()
    {
        PixelTypeInfo info = La32.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<La32>() * 8, info.BitsPerPixel);
        Assert.Equal(PixelAlphaRepresentation.Unassociated, info.AlphaRepresentation);
        Assert.Equal(PixelColorType.Luminance | PixelColorType.Alpha, info.ColorType);

        PixelComponentInfo componentInfo = info.ComponentInfo.Value;
        Assert.Equal(2, componentInfo.ComponentCount);
        Assert.Equal(0, componentInfo.Padding);
        Assert.Equal(16, componentInfo.GetComponentPrecision(0));
        Assert.Equal(16, componentInfo.GetComponentPrecision(1));
        Assert.Equal(16, componentInfo.GetMaximumComponentPrecision());
    }
}
