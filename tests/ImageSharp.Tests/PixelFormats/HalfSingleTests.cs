// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class HalfSingleTests
{
    [Fact]
    public void HalfSingle_PackedValue()
    {
        Assert.Equal(11878, new HalfSingle(0.1F).PackedValue);
        Assert.Equal(46285, new HalfSingle(-0.3F).PackedValue);

        // Test limits
        Assert.Equal(15360, new HalfSingle(1F).PackedValue);
        Assert.Equal(0, new HalfSingle(0F).PackedValue);
        Assert.Equal(48128, new HalfSingle(-1F).PackedValue);
    }

    [Fact]
    public void HalfSingle_ToVector4()
    {
        // arrange
        HalfSingle pixel = new(0.5f);
        Vector4 expected = new(0.5f, 0, 0, 1);

        // act
        Vector4 actual = pixel.ToVector4();

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HalfSingle_ToScaledVector4()
    {
        // arrange
        HalfSingle pixel = new(-1F);

        // act
        Vector4 actual = pixel.ToScaledVector4();

        // assert
        Assert.Equal(0, actual.X);
        Assert.Equal(0, actual.Y);
        Assert.Equal(0, actual.Z);
        Assert.Equal(1, actual.W);
    }

    [Fact]
    public void HalfSingle_FromScaledVector4()
    {
        // arrange
        Vector4 scaled = new HalfSingle(-1F).ToScaledVector4();
        const int expected = 48128;

        // act
        HalfSingle pixel = HalfSingle.FromScaledVector4(scaled);
        ushort actual = pixel.PackedValue;

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HalfSingle_PixelInformation()
    {
        PixelTypeInfo info = HalfSingle.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<HalfSingle>() * 8, info.BitsPerPixel);
        Assert.Equal(PixelAlphaRepresentation.None, info.AlphaRepresentation);
        Assert.Equal(PixelColorType.Red, info.ColorType);

        PixelComponentInfo componentInfo = info.ComponentInfo.Value;
        Assert.Equal(1, componentInfo.ComponentCount);
        Assert.Equal(0, componentInfo.Padding);
        Assert.Equal(16, componentInfo.GetComponentPrecision(0));
        Assert.Equal(16, componentInfo.GetMaximumComponentPrecision());
    }
}
