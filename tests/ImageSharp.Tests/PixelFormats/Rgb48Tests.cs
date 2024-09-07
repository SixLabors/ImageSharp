// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class Rgb48Tests
{
    [Fact]
    public void Rgb48_Values()
    {
        Rgba64 rgb = new(5243, 9830, 19660, 29491);

        Assert.Equal(5243, rgb.R);
        Assert.Equal(9830, rgb.G);
        Assert.Equal(19660, rgb.B);
        Assert.Equal(29491, rgb.A);
    }

    [Fact]
    public void Rgb48_ToVector4()
        => Assert.Equal(Vector4.One, new Rgb48(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue).ToVector4());

    [Fact]
    public void Rgb48_ToScaledVector4()
        => Assert.Equal(Vector4.One, new Rgb48(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue).ToVector4());

    [Fact]
    public void Rgb48_FromScaledVector4()
    {
        // arrange
        Rgb48 short3 = new(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue);
        Rgb48 expected = new(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue);

        // act
        Vector4 scaled = short3.ToScaledVector4();
        Rgb48 pixel = Rgb48.FromScaledVector4(scaled);

        // assert
        Assert.Equal(expected, pixel);
    }

    [Fact]
    public void Rgb48_ToRgba32()
    {
        // arrange
        Rgb48 rgba48 = new(5140, 9766, 19532);
        Rgba32 expected = new(20, 38, 76, 255);

        // act
        Rgba32 actual = rgba48.ToRgba32();

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgb48_FromBgra5551()
    {
        // arrange
        const ushort expected = ushort.MaxValue;

        // act
        Rgb48 rgb = Rgb48.FromBgra5551(new(1f, 1f, 1f, 1f));

        // assert
        Assert.Equal(expected, rgb.R);
        Assert.Equal(expected, rgb.G);
        Assert.Equal(expected, rgb.B);
    }

    [Fact]
    public void Rgb48_PixelInformation()
    {
        PixelTypeInfo info = Rgb48.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<Rgb48>() * 8, info.BitsPerPixel);
        Assert.Equal(PixelAlphaRepresentation.None, info.AlphaRepresentation);
        Assert.Equal(PixelColorType.RGB, info.ColorType);

        PixelComponentInfo componentInfo = info.ComponentInfo.Value;
        Assert.Equal(3, componentInfo.ComponentCount);
        Assert.Equal(0, componentInfo.Padding);
        Assert.Equal(16, componentInfo.GetComponentPrecision(0));
        Assert.Equal(16, componentInfo.GetComponentPrecision(1));
        Assert.Equal(16, componentInfo.GetComponentPrecision(2));
        Assert.Equal(16, componentInfo.GetMaximumComponentPrecision());
    }
}
