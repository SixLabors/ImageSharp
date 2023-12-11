// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats;
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
        HalfSingle halfSingle = new(0.5f);
        Vector4 expected = new(0.5f, 0, 0, 1);

        // act
        Vector4 actual = halfSingle.ToVector4();

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HalfSingle_ToScaledVector4()
    {
        // arrange
        HalfSingle halfSingle = new(-1F);

        // act
        Vector4 actual = halfSingle.ToScaledVector4();

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
        int expected = 48128;
        HalfSingle halfSingle = default;

        // act
        halfSingle.FromScaledVector4(scaled);
        ushort actual = halfSingle.PackedValue;

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HalfSingle_PixelInformation()
    {
        PixelTypeInfo info = HalfSingle.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<HalfSingle>() * 8, info.BitsPerPixel);
        Assert.Equal(1, info.ComponentCount);
        Assert.Equal(PixelAlphaRepresentation.None, info.AlphaRepresentation);
        Assert.Equal(PixelComponentPrecision.Half, info.ComponentPrecision);
    }
}
