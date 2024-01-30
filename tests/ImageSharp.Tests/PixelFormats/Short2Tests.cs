// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class Short2Tests
{
    [Fact]
    public void Short2_PackedValues()
    {
        // Test ordering
        Assert.Equal(0x361d2db1U, new Short2(0x2db1, 0x361d).PackedValue);
        Assert.Equal(4294639744, new Short2(127.5f, -5.3f).PackedValue);

        // Test the limits.
        Assert.Equal(0x0U, new Short2(Vector2.Zero).PackedValue);
        Assert.Equal(0x7FFF7FFFU, new Short2(Vector2.One * 0x7FFF).PackedValue);
        Assert.Equal(0x80008000, new Short2(Vector2.One * -0x8000).PackedValue);
    }

    [Fact]
    public void Short2_ToVector2()
    {
        Assert.Equal(Vector2.One * 0x7FFF, new Short2(Vector2.One * 0x7FFF).ToVector2());
        Assert.Equal(Vector2.Zero, new Short2(Vector2.Zero).ToVector2());
        Assert.Equal(Vector2.One * -0x8000, new Short2(Vector2.One * -0x8000).ToVector2());
        Assert.Equal(Vector2.UnitX * 0x7FFF, new Short2(Vector2.UnitX * 0x7FFF).ToVector2());
        Assert.Equal(Vector2.UnitY * 0x7FFF, new Short2(Vector2.UnitY * 0x7FFF).ToVector2());
    }

    [Fact]
    public void Short2_ToVector4()
    {
        Assert.Equal(new Vector4(0x7FFF, 0x7FFF, 0, 1), new Short2(Vector2.One * 0x7FFF).ToVector4());
        Assert.Equal(new Vector4(0, 0, 0, 1), new Short2(Vector2.Zero).ToVector4());
        Assert.Equal(new Vector4(-0x8000, -0x8000, 0, 1), new Short2(Vector2.One * -0x8000).ToVector4());
    }

    [Fact]
    public void Short2_Clamping()
    {
        Assert.Equal(Vector2.One * 0x7FFF, new Short2(Vector2.One * 1234567.0f).ToVector2());
        Assert.Equal(Vector2.One * -0x8000, new Short2(Vector2.One * -1234567.0f).ToVector2());
    }

    [Fact]
    public void Short2_ToScaledVector4()
    {
        // arrange
        Short2 short2 = new(Vector2.One * 0x7FFF);

        // act
        Vector4 actual = short2.ToScaledVector4();

        // assert
        Assert.Equal(1, actual.X);
        Assert.Equal(1, actual.Y);
        Assert.Equal(0, actual.Z);
        Assert.Equal(1, actual.W);
    }

    [Fact]
    public void Short2_FromScaledVector4()
    {
        // arrange
        Short2 short2 = new(Vector2.One * 0x7FFF);
        const ulong expected = 0x7FFF7FFF;

        // act
        Vector4 scaled = short2.ToScaledVector4();
        Short2 pixel = Short2.FromScaledVector4(scaled);
        uint actual = pixel.PackedValue;

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Short2_ToRgba32()
    {
        // arrange
        Short2 short2 = new(127.5f, -5.3f);
        Rgba32 expected = new(128, 127, 0, 255);

        // act
        Rgba32 actual = short2.ToRgba32();

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Short2_FromRgba32_ToRgba32()
    {
        // arrange
        Rgba32 expected = new(20, 38, 0, 255);

        // act
        Short2 short2 = Short2.FromRgba32(expected);
        Rgba32 actual = short2.ToRgba32();

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Short2_FromRgb48()
    {
        // arrange
        Rgb48 expected = new(65535, 65535, 0);

        // act
        Short2 input = Short2.FromRgb48(expected);
        Rgb48 actual = Rgb48.FromScaledVector4(input.ToScaledVector4());

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Short2_FromRgba64()
    {
        // arrange
        Rgba64 expected = new(65535, 65535, 0, 65535);

        // act
        Short2 input = Short2.FromRgba64(expected);
        Rgba64 actual = Rgba64.FromScaledVector4(input.ToScaledVector4());

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Short2_FromBgra5551()
    {
        // act
        Short2 short2 = Short2.FromBgra5551(new Bgra5551(1.0f, 1.0f, 1.0f, 1.0f));

        // assert
        Vector4 actual = short2.ToScaledVector4();
        Assert.Equal(1, actual.X);
        Assert.Equal(1, actual.Y);
        Assert.Equal(0, actual.Z);
        Assert.Equal(1, actual.W);
    }

    [Fact]
    public void Short2_PixelInformation()
    {
        PixelTypeInfo info = Short2.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<Short2>() * 8, info.BitsPerPixel);
        Assert.Equal(PixelAlphaRepresentation.None, info.AlphaRepresentation);
        Assert.Equal(PixelColorType.Red | PixelColorType.Green, info.ColorType);

        PixelComponentInfo componentInfo = info.ComponentInfo.Value;
        Assert.Equal(2, componentInfo.ComponentCount);
        Assert.Equal(0, componentInfo.Padding);
        Assert.Equal(16, componentInfo.GetComponentPrecision(0));
        Assert.Equal(16, componentInfo.GetComponentPrecision(1));
        Assert.Equal(16, componentInfo.GetMaximumComponentPrecision());
    }
}
