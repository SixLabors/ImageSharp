// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class Bgra4444Tests
{
    /// <summary>
    /// Tests the equality operators for equality.
    /// </summary>
    [Fact]
    public void AreEqual()
    {
        Bgra4444 color1 = new(0.0f, 0.0f, 0.0f, 0.0f);
        Bgra4444 color2 = new(new Vector4(0.0f));
        Bgra4444 color3 = new(new Vector4(1.0f, 0.0f, 1.0f, 1.0f));
        Bgra4444 color4 = new(1.0f, 0.0f, 1.0f, 1.0f);

        Assert.Equal(color1, color2);
        Assert.Equal(color3, color4);
    }

    /// <summary>
    /// Tests the equality operators for inequality.
    /// </summary>
    [Fact]
    public void AreNotEqual()
    {
        Bgra4444 color1 = new(0.0f, 0.0f, 0.0f, 0.0f);
        Bgra4444 color2 = new(new Vector4(1.0f));
        Bgra4444 color3 = new(new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
        Bgra4444 color4 = new(1.0f, 1.0f, 0.0f, 1.0f);

        Assert.NotEqual(color1, color2);
        Assert.NotEqual(color3, color4);
    }

    [Fact]
    public void Bgra4444_PackedValue()
    {
        Assert.Equal(520, new Bgra4444(0.1f, -0.3f, 0.5f, -0.7f).PackedValue);
        Assert.Equal(0x0, new Bgra4444(Vector4.Zero).PackedValue);
        Assert.Equal(0xFFFF, new Bgra4444(Vector4.One).PackedValue);
        Assert.Equal(0x0F00, new Bgra4444(Vector4.UnitX).PackedValue);
        Assert.Equal(0x00F0, new Bgra4444(Vector4.UnitY).PackedValue);
        Assert.Equal(0x000F, new Bgra4444(Vector4.UnitZ).PackedValue);
        Assert.Equal(0xF000, new Bgra4444(Vector4.UnitW).PackedValue);
    }

    [Fact]
    public void Bgra4444_ToVector4()
    {
        Assert.Equal(Vector4.One, new Bgra4444(Vector4.One).ToVector4());
        Assert.Equal(Vector4.Zero, new Bgra4444(Vector4.Zero).ToVector4());
        Assert.Equal(Vector4.UnitX, new Bgra4444(Vector4.UnitX).ToVector4());
        Assert.Equal(Vector4.UnitY, new Bgra4444(Vector4.UnitY).ToVector4());
        Assert.Equal(Vector4.UnitZ, new Bgra4444(Vector4.UnitZ).ToVector4());
        Assert.Equal(Vector4.UnitW, new Bgra4444(Vector4.UnitW).ToVector4());
    }

    [Fact]
    public void Bgra4444_ToScaledVector4()
    {
        // arrange
        Bgra4444 pixel = new(Vector4.One);

        // act
        Vector4 actual = pixel.ToScaledVector4();

        // assert
        Assert.Equal(1, actual.X);
        Assert.Equal(1, actual.Y);
        Assert.Equal(1, actual.Z);
        Assert.Equal(1, actual.W);
    }

    [Fact]
    public void Bgra4444_ToRgba32()
    {
        // arrange
        Bgra4444 pixel = new(Vector4.One);
        Rgba32 expected = new(Vector4.One);

        // act
        Rgba32 actual = pixel.ToRgba32();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Bgra4444_FromScaledVector4()
    {
        // arrange
        Vector4 scaled = new Bgra4444(Vector4.One).ToScaledVector4();
        const int expected = 0xFFFF;

        // act
        Bgra4444 pixel = Bgra4444.FromScaledVector4(scaled);
        ushort actual = pixel.PackedValue;

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Bgra4444_FromBgra5551()
    {
        // arrange
        const ushort expected = ushort.MaxValue;

        // act
        Bgra4444 pixel = Bgra4444.FromBgra5551(new Bgra5551(1.0f, 1.0f, 1.0f, 1.0f));

        // assert
        Assert.Equal(expected, pixel.PackedValue);
    }

    [Fact]
    public void Bgra4444_FromArgb32()
    {
        // arrange
        const ushort expectedPackedValue = ushort.MaxValue;

        // act
        Bgra4444 pixel = Bgra4444.FromArgb32(new Argb32(255, 255, 255, 255));

        // assert
        Assert.Equal(expectedPackedValue, pixel.PackedValue);
    }

    [Fact]
    public void Bgra4444_FromRgba32()
    {
        // arrange
        const ushort expectedPackedValue1 = ushort.MaxValue;
        const ushort expectedPackedValue2 = 0xFF0F;

        // act
        Bgra4444 bgra1 = Bgra4444.FromRgba32(new Rgba32(255, 255, 255, 255));
        Bgra4444 bgra2 = Bgra4444.FromRgba32(new Rgba32(255, 0, 255, 255));

        // assert
        Assert.Equal(expectedPackedValue1, bgra1.PackedValue);
        Assert.Equal(expectedPackedValue2, bgra2.PackedValue);
    }

    [Fact]
    public void Bgra4444_FromRgb48()
    {
        // arrange
        const ushort expectedPackedValue = ushort.MaxValue;

        // act
        Bgra4444 pixel = Bgra4444.FromRgb48(new Rgb48(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue));

        // assert
        Assert.Equal(expectedPackedValue, pixel.PackedValue);
    }

    [Fact]
    public void Bgra4444_FromRgba64()
    {
        // arrange
        const ushort expectedPackedValue = ushort.MaxValue;

        // act
        Bgra4444 pixel = Bgra4444.FromRgba64(new Rgba64(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue));

        // assert
        Assert.Equal(expectedPackedValue, pixel.PackedValue);
    }

    [Fact]
    public void Bgra4444_FromGrey16()
    {
        // arrange
        const ushort expectedPackedValue = ushort.MaxValue;

        // act
        Bgra4444 pixel = Bgra4444.FromL16(new L16(ushort.MaxValue));

        // assert
        Assert.Equal(expectedPackedValue, pixel.PackedValue);
    }

    [Fact]
    public void Bgra4444_FromGrey8()
    {
        // arrange
        const ushort expectedPackedValue = ushort.MaxValue;

        // act
        Bgra4444 pixel = Bgra4444.FromL8(new L8(byte.MaxValue));

        // assert
        Assert.Equal(expectedPackedValue, pixel.PackedValue);
    }

    [Fact]
    public void Bgra4444_FromBgr24()
    {
        // arrange
        const ushort expectedPackedValue = ushort.MaxValue;

        // act
        Bgra4444 pixel = Bgra4444.FromBgr24(new Bgr24(byte.MaxValue, byte.MaxValue, byte.MaxValue));

        // assert
        Assert.Equal(expectedPackedValue, pixel.PackedValue);
    }

    [Fact]
    public void Bgra4444_FromRgb24()
    {
        // arrange
        const ushort expectedPackedValue = ushort.MaxValue;

        // act
        Bgra4444 pixel = Bgra4444.FromRgb24(new Rgb24(byte.MaxValue, byte.MaxValue, byte.MaxValue));

        // assert
        Assert.Equal(expectedPackedValue, pixel.PackedValue);
    }

    [Fact]
    public void Bgra4444_Clamping()
    {
        Assert.Equal(Vector4.Zero, new Bgra4444(Vector4.One * -1234.0f).ToVector4());
        Assert.Equal(Vector4.One, new Bgra4444(Vector4.One * 1234.0f).ToVector4());
    }

    [Fact]
    public void Bgra4444_PixelInformation()
    {
        PixelTypeInfo info = Bgra4444.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<Bgra4444>() * 8, info.BitsPerPixel);
        Assert.Equal(PixelAlphaRepresentation.Unassociated, info.AlphaRepresentation);
        Assert.Equal(PixelColorType.BGR | PixelColorType.Alpha, info.ColorType);

        PixelComponentInfo componentInfo = info.ComponentInfo.Value;
        Assert.Equal(4, componentInfo.ComponentCount);
        Assert.Equal(0, componentInfo.Padding);
        Assert.Equal(4, componentInfo.GetComponentPrecision(0));
        Assert.Equal(4, componentInfo.GetComponentPrecision(1));
        Assert.Equal(4, componentInfo.GetComponentPrecision(2));
        Assert.Equal(4, componentInfo.GetComponentPrecision(3));
        Assert.Equal(4, componentInfo.GetMaximumComponentPrecision());
    }
}
