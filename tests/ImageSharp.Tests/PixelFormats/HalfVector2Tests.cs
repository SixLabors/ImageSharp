// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class HalfVector2Tests
{
    [Fact]
    public void HalfVector2_PackedValue()
    {
        Assert.Equal(0u, new HalfVector2(Vector2.Zero).PackedValue);
        Assert.Equal(1006648320u, new HalfVector2(Vector2.One).PackedValue);
        Assert.Equal(3033345638u, new HalfVector2(0.1f, -0.3f).PackedValue);
    }

    [Fact]
    public void HalfVector2_ToVector2()
    {
        Assert.Equal(Vector2.Zero, new HalfVector2(Vector2.Zero).ToVector2());
        Assert.Equal(Vector2.One, new HalfVector2(Vector2.One).ToVector2());
        Assert.Equal(Vector2.UnitX, new HalfVector2(Vector2.UnitX).ToVector2());
        Assert.Equal(Vector2.UnitY, new HalfVector2(Vector2.UnitY).ToVector2());
    }

    [Fact]
    public void HalfVector2_ToScaledVector4()
    {
        // arrange
        HalfVector2 halfVector = new(Vector2.One);

        // act
        Vector4 actual = halfVector.ToScaledVector4();

        // assert
        Assert.Equal(1F, actual.X);
        Assert.Equal(1F, actual.Y);
        Assert.Equal(0, actual.Z);
        Assert.Equal(1, actual.W);
    }

    [Fact]
    public void HalfVector2_FromScaledVector4()
    {
        // arrange
        Vector4 scaled = new HalfVector2(Vector2.One).ToScaledVector4();
        const uint expected = 1006648320u;

        // act
        HalfVector2 halfVector = HalfVector2.FromScaledVector4(scaled);
        uint actual = halfVector.PackedValue;

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HalfVector2_ToVector4()
    {
        // arrange
        HalfVector2 halfVector = new(.5F, .25F);
        Vector4 expected = new(0.5f, .25F, 0, 1);

        // act
        Vector4 actual = halfVector.ToVector4();

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HalfVector2_FromBgra5551()
    {
        // act
        HalfVector2 pixel = HalfVector2.FromBgra5551(new Bgra5551(1f, 1f, 1f, 1f));

        // assert
        Vector4 actual = pixel.ToScaledVector4();
        Assert.Equal(1F, actual.X);
        Assert.Equal(1F, actual.Y);
        Assert.Equal(0, actual.Z);
        Assert.Equal(1, actual.W);
    }

    [Fact]
    public void HalfVector2_PixelInformation()
    {
        PixelTypeInfo info = HalfVector2.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<HalfVector2>() * 8, info.BitsPerPixel);
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
