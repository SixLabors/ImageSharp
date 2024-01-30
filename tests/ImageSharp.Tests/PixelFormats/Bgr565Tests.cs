// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class Bgr565Tests
{
    /// <summary>
    /// Tests the equality operators for equality.
    /// </summary>
    [Fact]
    public void AreEqual()
    {
        Bgr565 color1 = new(0.0f, 0.0f, 0.0f);
        Bgr565 color2 = new(new Vector3(0.0f));
        Bgr565 color3 = new(new Vector3(1.0f, 0.0f, 1.0f));
        Bgr565 color4 = new(1.0f, 0.0f, 1.0f);

        Assert.Equal(color1, color2);
        Assert.Equal(color3, color4);
    }

    /// <summary>
    /// Tests the equality operators for inequality.
    /// </summary>
    [Fact]
    public void AreNotEqual()
    {
        Bgr565 color1 = new(0.0f, 0.0f, 0.0f);
        Bgr565 color2 = new(new Vector3(1.0f));
        Bgr565 color3 = new(new Vector3(1.0f, 0.0f, 0.0f));
        Bgr565 color4 = new(1.0f, 1.0f, 0.0f);

        Assert.NotEqual(color1, color2);
        Assert.NotEqual(color3, color4);
    }

    [Fact]
    public void Bgr565_PackedValue()
    {
        Assert.Equal(6160, new Bgr565(0.1F, -0.3F, 0.5F).PackedValue);
        Assert.Equal(0x0, new Bgr565(Vector3.Zero).PackedValue);
        Assert.Equal(0xFFFF, new Bgr565(Vector3.One).PackedValue);

        // Make sure the swizzle is correct.
        Assert.Equal(0xF800, new Bgr565(Vector3.UnitX).PackedValue);
        Assert.Equal(0x07E0, new Bgr565(Vector3.UnitY).PackedValue);
        Assert.Equal(0x001F, new Bgr565(Vector3.UnitZ).PackedValue);
    }

    [Fact]
    public void Bgr565_ToVector3()
    {
        Assert.Equal(Vector3.One, new Bgr565(Vector3.One).ToVector3());
        Assert.Equal(Vector3.Zero, new Bgr565(Vector3.Zero).ToVector3());
        Assert.Equal(Vector3.UnitX, new Bgr565(Vector3.UnitX).ToVector3());
        Assert.Equal(Vector3.UnitY, new Bgr565(Vector3.UnitY).ToVector3());
        Assert.Equal(Vector3.UnitZ, new Bgr565(Vector3.UnitZ).ToVector3());
    }

    [Fact]
    public void Bgr565_ToScaledVector4()
    {
        // arrange
        Bgr565 bgr = new(Vector3.One);

        // act
        Vector4 actual = bgr.ToScaledVector4();

        // assert
        Assert.Equal(1, actual.X);
        Assert.Equal(1, actual.Y);
        Assert.Equal(1, actual.Z);
        Assert.Equal(1, actual.W);
    }

    [Fact]
    public void Bgr565_FromScaledVector4()
    {
        // arrange
        Vector4 scaled = new Bgr565(Vector3.One).ToScaledVector4();
        const int expected = 0xFFFF;

        // act
        Bgr565 pixel = Bgr565.FromScaledVector4(scaled);
        ushort actual = pixel.PackedValue;

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Bgr565_FromBgra5551()
    {
        // arrange
        const ushort expected = ushort.MaxValue;

        // act
        Bgr565 bgr = Bgr565.FromBgra5551(new Bgra5551(1.0f, 1.0f, 1.0f, 1.0f));

        // assert
        Assert.Equal(expected, bgr.PackedValue);
    }

    [Fact]
    public void Bgr565_FromArgb32()
    {
        // arrange
        const ushort expected1 = ushort.MaxValue;
        const ushort expected2 = ushort.MaxValue;

        // act
        Bgr565 bgr1 = Bgr565.FromArgb32(new Argb32(1.0f, 1.0f, 1.0f, 1.0f));
        Bgr565 bgr2 = Bgr565.FromArgb32(new Argb32(1.0f, 1.0f, 1.0f, 0.0f));

        // assert
        Assert.Equal(expected1, bgr1.PackedValue);
        Assert.Equal(expected2, bgr2.PackedValue);
    }

    [Fact]
    public void Bgr565_FromRgba32()
    {
        // arrange
        const ushort expected1 = ushort.MaxValue;
        const ushort expected2 = ushort.MaxValue;

        // act
        Bgr565 bgr1 = Bgr565.FromRgba32(new Rgba32(1.0f, 1.0f, 1.0f, 1.0f));
        Bgr565 bgr2 = Bgr565.FromRgba32(new Rgba32(1.0f, 1.0f, 1.0f, 0.0f));

        // assert
        Assert.Equal(expected1, bgr1.PackedValue);
        Assert.Equal(expected2, bgr2.PackedValue);
    }

    [Fact]
    public void Bgr565_ToRgba32()
    {
        // arrange
        Bgr565 pixel = new(Vector3.One);
        Rgba32 expected = new(Vector4.One);

        // act
        Rgba32 actual = pixel.ToRgba32();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Bgra565_FromRgb48()
    {
        // arrange
        const ushort expectedPackedValue = ushort.MaxValue;

        // act
        Bgr565 bgr = Bgr565.FromRgb48(new Rgb48(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue));

        // assert
        Assert.Equal(expectedPackedValue, bgr.PackedValue);
    }

    [Fact]
    public void Bgra565_FromRgba64()
    {
        // arrange
        const ushort expectedPackedValue = ushort.MaxValue;

        // act
        Bgr565 bgr = Bgr565.FromRgba64(new Rgba64(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue));

        // assert
        Assert.Equal(expectedPackedValue, bgr.PackedValue);
    }

    [Fact]
    public void Bgr565_FromBgr24()
    {
        // arrange
        const ushort expected = ushort.MaxValue;

        // act
        Bgr565 bgr = Bgr565.FromBgr24(new Bgr24(byte.MaxValue, byte.MaxValue, byte.MaxValue));

        // assert
        Assert.Equal(expected, bgr.PackedValue);
    }

    [Fact]
    public void Bgr565_FromRgb24()
    {
        // arrange
        const ushort expected = ushort.MaxValue;

        // act
        Bgr565 bgr = Bgr565.FromRgb24(new Rgb24(byte.MaxValue, byte.MaxValue, byte.MaxValue));

        // assert
        Assert.Equal(expected, bgr.PackedValue);
    }

    [Fact]
    public void Bgr565_FromGrey8()
    {
        // arrange
        const ushort expected = ushort.MaxValue;

        // act
        Bgr565 bgr = Bgr565.FromL8(new L8(byte.MaxValue));

        // assert
        Assert.Equal(expected, bgr.PackedValue);
    }

    [Fact]
    public void Bgr565_FromGrey16()
    {
        // arrange
        const ushort expected = ushort.MaxValue;

        // act
        Bgr565 bgr = Bgr565.FromL16(new L16(ushort.MaxValue));

        // assert
        Assert.Equal(expected, bgr.PackedValue);
    }

    [Fact]
    public void Bgr565_Clamping()
    {
        Assert.Equal(Vector3.Zero, new Bgr565(Vector3.One * -1234F).ToVector3());
        Assert.Equal(Vector3.One, new Bgr565(Vector3.One * 1234F).ToVector3());
    }

    [Fact]
    public void Bgr565_PixelInformation()
    {
        PixelTypeInfo info = Bgr565.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<Bgr565>() * 8, info.BitsPerPixel);
        Assert.Equal(PixelAlphaRepresentation.None, info.AlphaRepresentation);
        Assert.Equal(PixelColorType.BGR, info.ColorType);

        PixelComponentInfo componentInfo = info.ComponentInfo.Value;
        Assert.Equal(3, componentInfo.ComponentCount);
        Assert.Equal(0, componentInfo.Padding);
        Assert.Equal(5, componentInfo.GetComponentPrecision(0));
        Assert.Equal(6, componentInfo.GetComponentPrecision(1));
        Assert.Equal(5, componentInfo.GetComponentPrecision(2));
        Assert.Equal(6, componentInfo.GetMaximumComponentPrecision());
    }
}
