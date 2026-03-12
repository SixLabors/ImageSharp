// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class Rg32Tests
{
    [Fact]
    public void Rg32_PackedValues()
    {
        const float x = 0xb6dc;
        const float y = 0xA59f;
        Assert.Equal(0xa59fb6dc, new Rg32(x / 0xffff, y / 0xffff).PackedValue);
        Assert.Equal(6554U, new Rg32(0.1f, -0.3f).PackedValue);

        // Test the limits.
        Assert.Equal(0x0U, new Rg32(Vector2.Zero).PackedValue);
        Assert.Equal(0xFFFFFFFF, new Rg32(Vector2.One).PackedValue);
    }

    [Fact]
    public void Rg32_ToVector2()
    {
        Assert.Equal(Vector2.Zero, new Rg32(Vector2.Zero).ToVector2());
        Assert.Equal(Vector2.One, new Rg32(Vector2.One).ToVector2());
    }

    [Fact]
    public void Rg32_ToScaledVector4()
    {
        // arrange
        Rg32 rg32 = new(Vector2.One);

        // act
        Vector4 actual = rg32.ToScaledVector4();

        // assert
        Assert.Equal(1, actual.X);
        Assert.Equal(1, actual.Y);
        Assert.Equal(0, actual.Z);
        Assert.Equal(1, actual.W);
    }

    [Fact]
    public void Rg32_FromScaledVector4()
    {
        // arrange
        Rg32 rg32 = new(Vector2.One);
        const uint expected = 0xFFFFFFFF;

        // act
        Vector4 scaled = rg32.ToScaledVector4();
        Rg32 pixel = Rg32.FromScaledVector4(scaled);
        uint actual = pixel.PackedValue;

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rg32_FromBgra5551()
    {
        // arrange
        const uint expected = 0xFFFFFFFF;

        // act
        Rg32 rg32 = Rg32.FromBgra5551(new Bgra5551(1f, 1f, 1f, 1f));

        // assert
        Assert.Equal(expected, rg32.PackedValue);
    }

    [Fact]
    public void Rg32_Clamping()
    {
        Assert.Equal(Vector2.Zero, new Rg32(Vector2.One * -1234.0f).ToVector2());
        Assert.Equal(Vector2.One, new Rg32(Vector2.One * 1234.0f).ToVector2());
    }

    [Fact]
    public void Rg32_PixelInformation()
    {
        PixelTypeInfo info = Rg32.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<Rg32>() * 8, info.BitsPerPixel);
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
