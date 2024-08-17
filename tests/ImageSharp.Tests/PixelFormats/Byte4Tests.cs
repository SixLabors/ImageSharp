// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class Byte4Tests
{
    /// <summary>
    /// Tests the equality operators for equality.
    /// </summary>
    [Fact]
    public void AreEqual()
    {
        Byte4 color1 = new(0f, 0f, 0f, 0f);
        Byte4 color2 = new(new Vector4(0f));
        Byte4 color3 = new(new Vector4(1f, 0f, 1f, 1f));
        Byte4 color4 = new(1f, 0f, 1f, 1f);

        Assert.Equal(color1, color2);
        Assert.Equal(color3, color4);
    }

    /// <summary>
    /// Tests the equality operators for inequality.
    /// </summary>
    [Fact]
    public void AreNotEqual()
    {
        Byte4 color1 = new(0f, 0f, 0f, 0f);
        Byte4 color2 = new(new Vector4(1f));
        Byte4 color3 = new(new Vector4(1f, 0f, 0f, 1f));
        Byte4 color4 = new(1f, 1f, 0f, 1f);

        Assert.NotEqual(color1, color2);
        Assert.NotEqual(color3, color4);
    }

    [Fact]
    public void Byte4_PackedValue()
    {
        Assert.Equal(128U, new Byte4(127.5f, -12.3f, 0.5f, -0.7f).PackedValue);
        Assert.Equal(0x1a7b362dU, new Byte4(0x2d, 0x36, 0x7b, 0x1a).PackedValue);
        Assert.Equal(0x0U, new Byte4(Vector4.Zero).PackedValue);
        Assert.Equal(0xFFFFFFFF, new Byte4(Vector4.One * 255).PackedValue);
    }

    [Fact]
    public void Byte4_ToVector4()
    {
        Assert.Equal(Vector4.One * 255, new Byte4(Vector4.One * 255).ToVector4());
        Assert.Equal(Vector4.Zero, new Byte4(Vector4.Zero).ToVector4());
        Assert.Equal(Vector4.UnitX * 255, new Byte4(Vector4.UnitX * 255).ToVector4());
        Assert.Equal(Vector4.UnitY * 255, new Byte4(Vector4.UnitY * 255).ToVector4());
        Assert.Equal(Vector4.UnitZ * 255, new Byte4(Vector4.UnitZ * 255).ToVector4());
        Assert.Equal(Vector4.UnitW * 255, new Byte4(Vector4.UnitW * 255).ToVector4());
    }

    [Fact]
    public void Byte4_ToScaledVector4()
    {
        // arrange
        Byte4 pixel = new(Vector4.One * 255);

        // act
        Vector4 actual = pixel.ToScaledVector4();

        // assert
        Assert.Equal(1, actual.X);
        Assert.Equal(1, actual.Y);
        Assert.Equal(1, actual.Z);
        Assert.Equal(1, actual.W);
    }

    [Fact]
    public void Byte4_ToRgba32()
    {
        // arrange
        Byte4 pixel = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
        Rgba32 expected = new(Vector4.One);

        // act
        Rgba32 actual = pixel.ToRgba32();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Byte4_FromScaledVector4()
    {
        // arrange
        Vector4 scaled = new Byte4(Vector4.One * 255).ToScaledVector4();
        const uint expected = 0xFFFFFFFF;

        // act
        Byte4 pixel = Byte4.FromScaledVector4(scaled);
        uint actual = pixel.PackedValue;

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Byte4_FromArgb32()
    {
        // arrange
        const uint expectedPackedValue = uint.MaxValue;

        // act
        Byte4 pixel = Byte4.FromArgb32(new Argb32(255, 255, 255, 255));

        // assert
        Assert.Equal(expectedPackedValue, pixel.PackedValue);
    }

    [Fact]
    public void Byte4_FromBgr24()
    {
        // arrange
        const uint expectedPackedValue = uint.MaxValue;

        // act
        Byte4 pixel = Byte4.FromBgr24(new Bgr24(byte.MaxValue, byte.MaxValue, byte.MaxValue));

        // assert
        Assert.Equal(expectedPackedValue, pixel.PackedValue);
    }

    [Fact]
    public void Byte4_FromGrey8()
    {
        // arrange
        const uint expectedPackedValue = uint.MaxValue;

        // act
        Byte4 pixel = Byte4.FromL8(new L8(byte.MaxValue));

        // assert
        Assert.Equal(expectedPackedValue, pixel.PackedValue);
    }

    [Fact]
    public void Byte4_FromGrey16()
    {
        // arrange
        const uint expectedPackedValue = uint.MaxValue;

        // act
        Byte4 pixel = Byte4.FromL16(new L16(ushort.MaxValue));

        // assert
        Assert.Equal(expectedPackedValue, pixel.PackedValue);
    }

    [Fact]
    public void Byte4_FromRgb24()
    {
        // arrange
        const uint expectedPackedValue = uint.MaxValue;

        // act
        Byte4 pixel = Byte4.FromRgb24(new Rgb24(byte.MaxValue, byte.MaxValue, byte.MaxValue));

        // assert
        Assert.Equal(expectedPackedValue, pixel.PackedValue);
    }

    [Fact]
    public void Byte4_FromBgra5551()
    {
        // arrange
        const uint expected = 0xFFFFFFFF;

        // act
        Byte4 pixel = Byte4.FromBgra5551(new Bgra5551(1f, 1f, 1f, 1f));

        // assert
        Assert.Equal(expected, pixel.PackedValue);
    }

    [Fact]
    public void Byte4_FromRgba32()
    {
        // arrange
        const uint expectedPackedValue1 = uint.MaxValue;

        // act
        Byte4 pixel = Byte4.FromRgba32(new Rgba32(255, 255, 255, 255));

        // assert
        Assert.Equal(expectedPackedValue1, pixel.PackedValue);
    }

    [Fact]
    public void Byte4_FromRgb48()
    {
        // arrange
        const uint expectedPackedValue = uint.MaxValue;

        // act
        Byte4 pixel = Byte4.FromRgb48(new Rgb48(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue));

        // assert
        Assert.Equal(expectedPackedValue, pixel.PackedValue);
    }

    [Fact]
    public void Byte4_FromRgba64()
    {
        // arrange
        const uint expectedPackedValue = uint.MaxValue;

        // act
        Byte4 pixel = Byte4.FromRgba64(new Rgba64(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue));

        // assert
        Assert.Equal(expectedPackedValue, pixel.PackedValue);
    }

    [Fact]
    public void Byte4_Clamping()
    {
        Assert.Equal(Vector4.Zero, new Byte4(Vector4.One * -1234.0f).ToVector4());
        Assert.Equal(Vector4.One * 255, new Byte4(Vector4.One * 1234.0f).ToVector4());
    }

    [Fact]
    public void Byte4_PixelInformation()
    {
        PixelTypeInfo info = Byte4.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<Byte4>() * 8, info.BitsPerPixel);
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
