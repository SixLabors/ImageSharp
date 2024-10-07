// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class NormalizedByte4Tests
{
    /// <summary>
    /// Tests the equality operators for equality.
    /// </summary>
    [Fact]
    public void AreEqual()
    {
        NormalizedByte4 color1 = new(0.0f, 0.0f, 0.0f, 0.0f);
        NormalizedByte4 color2 = new(new(0.0f));
        NormalizedByte4 color3 = new(new(1f, 0.0f, 1f, 1f));
        NormalizedByte4 color4 = new(1f, 0.0f, 1f, 1f);

        Assert.Equal(color1, color2);
        Assert.Equal(color3, color4);
    }

    /// <summary>
    /// Tests the equality operators for inequality.
    /// </summary>
    [Fact]
    public void AreNotEqual()
    {
        NormalizedByte4 color1 = new(0.0f, 0.0f, 0.0f, 0.0f);
        NormalizedByte4 color2 = new(new(1f));
        NormalizedByte4 color3 = new(new(1f, 0.0f, 0.0f, 1f));
        NormalizedByte4 color4 = new(1f, 1f, 0.0f, 1f);

        Assert.NotEqual(color1, color2);
        Assert.NotEqual(color3, color4);
    }

    [Fact]
    public void NormalizedByte4_PackedValues()
    {
        Assert.Equal(0xA740DA0D, new NormalizedByte4(0.1f, -0.3f, 0.5f, -0.7f).PackedValue);
        Assert.Equal(958796544U, new NormalizedByte4(0.0008f, 0.15f, 0.30f, 0.45f).PackedValue);
        Assert.Equal(0x0U, new NormalizedByte4(Vector4.Zero).PackedValue);
        Assert.Equal(0x7F7F7F7FU, new NormalizedByte4(Vector4.One).PackedValue);
        Assert.Equal(0x81818181, new NormalizedByte4(-Vector4.One).PackedValue);
    }

    [Fact]
    public void NormalizedByte4_ToVector4()
    {
        Assert.Equal(Vector4.One, new NormalizedByte4(Vector4.One).ToVector4());
        Assert.Equal(Vector4.Zero, new NormalizedByte4(Vector4.Zero).ToVector4());
        Assert.Equal(-Vector4.One, new NormalizedByte4(-Vector4.One).ToVector4());
        Assert.Equal(Vector4.One, new NormalizedByte4(Vector4.One * 1234.0f).ToVector4());
        Assert.Equal(-Vector4.One, new NormalizedByte4(Vector4.One * -1234.0f).ToVector4());
    }

    [Fact]
    public void NormalizedByte4_ToScaledVector4()
    {
        // arrange
        NormalizedByte4 short4 = new(-Vector4.One);

        // act
        Vector4 actual = short4.ToScaledVector4();

        // assert
        Assert.Equal(0, actual.X);
        Assert.Equal(0, actual.Y);
        Assert.Equal(0, actual.Z);
        Assert.Equal(0, actual.W);
    }

    [Fact]
    public void NormalizedByte4_FromScaledVector4()
    {
        // arrange
        Vector4 scaled = new NormalizedByte4(-Vector4.One).ToScaledVector4();
        const uint expected = 0x81818181;

        // act
        NormalizedByte4 pixel = NormalizedByte4.FromScaledVector4(scaled);
        uint actual = pixel.PackedValue;

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NormalizedByte4_FromArgb32()
    {
        // arrange
        Vector4 expected = Vector4.One;

        // act
        NormalizedByte4 pixel = NormalizedByte4.FromArgb32(new(255, 255, 255, 255));

        // assert
        Assert.Equal(expected, pixel.ToScaledVector4());
    }

    [Fact]
    public void NormalizedByte4_FromBgr24()
    {
        // arrange
        Vector4 expected = Vector4.One;

        // act
        NormalizedByte4 pixel = NormalizedByte4.FromBgr24(new(byte.MaxValue, byte.MaxValue, byte.MaxValue));

        // assert
        Assert.Equal(expected, pixel.ToScaledVector4());
    }

    [Fact]
    public void NormalizedByte4_FromGrey8()
    {
        // arrange
        Vector4 expected = Vector4.One;

        // act
        NormalizedByte4 pixel = NormalizedByte4.FromL8(new(byte.MaxValue));

        // assert
        Assert.Equal(expected, pixel.ToScaledVector4());
    }

    [Fact]
    public void NormalizedByte4_FromGrey16()
    {
        // arrange
        Vector4 expected = Vector4.One;

        // act
        NormalizedByte4 pixel = NormalizedByte4.FromL16(new(ushort.MaxValue));

        // assert
        Assert.Equal(expected, pixel.ToScaledVector4());
    }

    [Fact]
    public void NormalizedByte4_FromRgb24()
    {
        // arrange
        Vector4 expected = Vector4.One;

        // act
        NormalizedByte4 pixel = NormalizedByte4.FromRgb24(new(byte.MaxValue, byte.MaxValue, byte.MaxValue));

        // assert
        Assert.Equal(expected, pixel.ToScaledVector4());
    }

    [Fact]
    public void NormalizedByte4_FromRgba32()
    {
        // arrange
        Vector4 expected = Vector4.One;

        // act
        NormalizedByte4 pixel = NormalizedByte4.FromRgba32(new(255, 255, 255, 255));

        // assert
        Assert.Equal(expected, pixel.ToScaledVector4());
    }

    [Fact]
    public void NormalizedByte4_FromBgra5551()
    {
        // arrange
        Vector4 expected = Vector4.One;

        // act
        NormalizedByte4 pixel = NormalizedByte4.FromBgra5551(new(1f, 1f, 1f, 1f));

        // assert
        Assert.Equal(expected, pixel.ToVector4());
    }

    [Fact]
    public void NormalizedByte4_FromRgb48()
    {
        // arrange
        Vector4 expected = Vector4.One;

        // act
        NormalizedByte4 pixel = NormalizedByte4.FromRgb48(new(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue));

        // assert
        Assert.Equal(expected, pixel.ToScaledVector4());
    }

    [Fact]
    public void NormalizedByte4_FromRgba64()
    {
        // arrange
        Vector4 expected = Vector4.One;

        // act
        NormalizedByte4 pixel = NormalizedByte4.FromRgba64(new(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue));

        // assert
        Assert.Equal(expected, pixel.ToScaledVector4());
    }

    [Fact]
    public void NormalizedByte4_ToRgba32()
    {
        // arrange
        NormalizedByte4 pixel = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
        Rgba32 expected = new(Vector4.One);

        // act
        Rgba32 actual = pixel.ToRgba32();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NormalizedByte4_PixelInformation()
    {
        PixelTypeInfo info = NormalizedByte4.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<NormalizedByte4>() * 8, info.BitsPerPixel);
        Assert.Equal(PixelAlphaRepresentation.Unassociated, info.AlphaRepresentation);
        Assert.Equal(PixelColorType.RGB | PixelColorType.Alpha, info.ColorType);

        PixelComponentInfo componentInfo = info.ComponentInfo.Value;
        Assert.Equal(4, componentInfo.ComponentCount);
        Assert.Equal(0, componentInfo.Padding);
        Assert.Equal(8, componentInfo.GetComponentPrecision(0));
        Assert.Equal(8, componentInfo.GetComponentPrecision(1));
        Assert.Equal(8, componentInfo.GetComponentPrecision(2));
        Assert.Equal(8, componentInfo.GetComponentPrecision(3));
        Assert.Equal(8, componentInfo.GetMaximumComponentPrecision());
    }
}
