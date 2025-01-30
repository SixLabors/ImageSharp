// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class Short4Tests
{
    [Fact]
    public void Short4_PackedValues()
    {
        Short4 shortValue1 = new(11547, 12653, 29623, 193);
        Short4 shortValue2 = new(0.1f, -0.3f, 0.5f, -0.7f);

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
        Short4 short4 = new(Vector4.One * 0x7FFF);

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
        Short4 short4 = new(Vector4.One * 0x7FFF);
        Vector4 scaled = short4.ToScaledVector4();
        const long expected = 0x7FFF7FFF7FFF7FFF;

        // act
        Short4 pixel = Short4.FromScaledVector4(scaled);

        // assert
        Assert.Equal((ulong)expected, pixel.PackedValue);
    }

    [Fact]
    public void Short4_Clamping()
    {
        // arrange
        Short4 short1 = new(Vector4.One * 1234567.0f);
        Short4 short2 = new(Vector4.One * -1234567.0f);

        // act
        Vector4 vector1 = short1.ToVector4();
        Vector4 vector2 = short2.ToVector4();

        // assert
        Assert.Equal(Vector4.One * 0x7FFF, vector1);
        Assert.Equal(Vector4.One * -0x8000, vector2);
    }

    [Fact]
    public void Short4_ToRgba32()
    {
        // arrange
        Short4 shortValue = new(11547, 12653, 29623, 193);
        Rgba32 expected = new(172, 177, 243, 128);

        // act
        Rgba32 actual = shortValue.ToRgba32();

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Short4_FromRgba32_ToRgba32()
    {
        // arrange
        Rgba32 expected = new(20, 38, 0, 255);

        // act
        Short4 short4 = Short4.FromRgba32(expected);
        Rgba32 actual = short4.ToRgba32();

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Short4_FromBgra32_ToRgba32()
    {
        // arrange
        Bgra32 expected = new(20, 38, 0, 255);

        // act
        Short4 short4 = Short4.FromBgra32(expected);
        Rgba32 temp = short4.ToRgba32();
        Bgra32 actual = Bgra32.FromRgba32(temp);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Short4_FromArgb32_ToRgba32()
    {
        // arrange
        Argb32 expected = new(20, 38, 0, 255);

        // act
        Short4 short4 = Short4.FromArgb32(expected);
        Rgba32 temp = short4.ToRgba32();
        Argb32 actual = Argb32.FromRgba32(temp);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Short4_FromAbgrb32_ToRgba32()
    {
        // arrange
        Abgr32 expected = new(20, 38, 0, 255);

        // act
        Short4 short4 = Short4.FromAbgr32(expected);
        Rgba32 temp = short4.ToRgba32();
        Abgr32 actual = Abgr32.FromRgba32(temp);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Short4_FromRgb48_ToRgb48()
    {
        // arrange
        Rgb48 expected = new(65535, 0, 65535);

        // act
        Short4 input = Short4.FromRgb48(expected);
        Rgb48 actual = Rgb48.FromScaledVector4(input.ToScaledVector4());

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Short4_FromRgba64_ToRgba64()
    {
        // arrange
        Rgba64 expected = new(65535, 0, 65535, 0);

        // act
        Short4 input = Short4.FromRgba64(expected);
        Rgba64 actual = Rgba64.FromScaledVector4(input.ToScaledVector4());

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Short4_FromBgra5551()
    {
        // arrange
        Vector4 expected = Vector4.One;

        // act
        Short4 short4 = Short4.FromBgra5551(new(1.0f, 1.0f, 1.0f, 1.0f));

        // assert
        Assert.Equal(expected, short4.ToScaledVector4());
    }

    [Fact]
    public void Short4_PixelInformation()
    {
        PixelTypeInfo info = Short4.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<Short4>() * 8, info.BitsPerPixel);
        Assert.Equal(PixelAlphaRepresentation.Unassociated, info.AlphaRepresentation);
        Assert.Equal(PixelColorType.RGB | PixelColorType.Alpha, info.ColorType);

        PixelComponentInfo componentInfo = info.ComponentInfo.Value;
        Assert.Equal(4, componentInfo.ComponentCount);
        Assert.Equal(0, componentInfo.Padding);
        Assert.Equal(16, componentInfo.GetComponentPrecision(0));
        Assert.Equal(16, componentInfo.GetComponentPrecision(1));
        Assert.Equal(16, componentInfo.GetComponentPrecision(2));
        Assert.Equal(16, componentInfo.GetComponentPrecision(3));
        Assert.Equal(16, componentInfo.GetMaximumComponentPrecision());
    }
}
