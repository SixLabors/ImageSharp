// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class NormalizedByte2Tests
{
    [Fact]
    public void NormalizedByte2_PackedValue()
    {
        Assert.Equal(0xda0d, new NormalizedByte2(0.1f, -0.3f).PackedValue);
        Assert.Equal(0x0, new NormalizedByte2(Vector2.Zero).PackedValue);
        Assert.Equal(0x7F7F, new NormalizedByte2(Vector2.One).PackedValue);
        Assert.Equal(0x8181, new NormalizedByte2(-Vector2.One).PackedValue);
    }

    [Fact]
    public void NormalizedByte2_ToVector2()
    {
        Assert.Equal(Vector2.One, new NormalizedByte2(Vector2.One).ToVector2());
        Assert.Equal(Vector2.Zero, new NormalizedByte2(Vector2.Zero).ToVector2());
        Assert.Equal(-Vector2.One, new NormalizedByte2(-Vector2.One).ToVector2());
        Assert.Equal(Vector2.One, new NormalizedByte2(Vector2.One * 1234.0f).ToVector2());
        Assert.Equal(-Vector2.One, new NormalizedByte2(Vector2.One * -1234.0f).ToVector2());
    }

    [Fact]
    public void NormalizedByte2_ToVector4()
    {
        Assert.Equal(new(1, 1, 0, 1), new NormalizedByte2(Vector2.One).ToVector4());
        Assert.Equal(new(0, 0, 0, 1), new NormalizedByte2(Vector2.Zero).ToVector4());
    }

    [Fact]
    public void NormalizedByte2_ToScaledVector4()
    {
        // arrange
        NormalizedByte2 pixel = new(-Vector2.One);

        // act
        Vector4 actual = pixel.ToScaledVector4();

        // assert
        Assert.Equal(0, actual.X);
        Assert.Equal(0, actual.Y);
        Assert.Equal(0, actual.Z);
        Assert.Equal(1F, actual.W);
    }

    [Fact]
    public void NormalizedByte2_FromScaledVector4()
    {
        // arrange
        Vector4 scaled = new NormalizedByte2(-Vector2.One).ToScaledVector4();
        const uint expected = 0x8181;

        // act
        NormalizedByte2 pixel = NormalizedByte2.FromScaledVector4(scaled);
        uint actual = pixel.PackedValue;

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NormalizedByte2_FromBgra5551()
    {
        // arrange
        Vector4 expected = new(1, 1, 0, 1);

        // act
        NormalizedByte2 pixel = NormalizedByte2.FromBgra5551(new(1f, 1f, 1f, 1f));

        // assert
        Assert.Equal(expected, pixel.ToVector4());
    }

    [Fact]
    public void NormalizedByte2_PixelInformation()
    {
        PixelTypeInfo info = NormalizedByte2.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<NormalizedByte2>() * 8, info.BitsPerPixel);
        Assert.Equal(PixelAlphaRepresentation.None, info.AlphaRepresentation);
        Assert.Equal(PixelColorType.Red | PixelColorType.Green, info.ColorType);

        PixelComponentInfo componentInfo = info.ComponentInfo.Value;
        Assert.Equal(2, componentInfo.ComponentCount);
        Assert.Equal(0, componentInfo.Padding);
        Assert.Equal(8, componentInfo.GetComponentPrecision(0));
        Assert.Equal(8, componentInfo.GetComponentPrecision(1));
        Assert.Equal(8, componentInfo.GetMaximumComponentPrecision());
    }
}
