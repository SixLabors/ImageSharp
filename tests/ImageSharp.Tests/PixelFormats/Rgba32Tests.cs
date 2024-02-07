// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

/// <summary>
/// Tests the <see cref="Rgba32"/> struct.
/// </summary>
[Trait("Category", "PixelFormats")]
public class Rgba32Tests
{
    /// <summary>
    /// Tests the equality operators for equality.
    /// </summary>
    [Fact]
    public void AreEqual()
    {
        Rgba32 color1 = new(0, 0, 0);
        Rgba32 color2 = new(0, 0, 0, 1F);
        Rgba32 color3 = Rgba32.ParseHex("#000");
        Rgba32 color4 = Rgba32.ParseHex("#000F");
        Rgba32 color5 = Rgba32.ParseHex("#000000");
        Rgba32 color6 = Rgba32.ParseHex("#000000FF");

        Assert.Equal(color1, color2);
        Assert.Equal(color1, color3);
        Assert.Equal(color1, color4);
        Assert.Equal(color1, color5);
        Assert.Equal(color1, color6);
    }

    /// <summary>
    /// Tests the equality operators for inequality.
    /// </summary>
    [Fact]
    public void AreNotEqual()
    {
        Rgba32 color1 = new(255, 0, 0, 255);
        Rgba32 color2 = new(0, 0, 0, 255);
        Rgba32 color3 = Rgba32.ParseHex("#000");
        Rgba32 color4 = Rgba32.ParseHex("#000000");
        Rgba32 color5 = Rgba32.ParseHex("#FF000000");

        Assert.NotEqual(color1, color2);
        Assert.NotEqual(color1, color3);
        Assert.NotEqual(color1, color4);
        Assert.NotEqual(color1, color5);
    }

    /// <summary>
    /// Tests whether the color constructor correctly assign properties.
    /// </summary>
    [Fact]
    public void ConstructorAssignsProperties()
    {
        Rgba32 color1 = new(1, .1f, .133f, .864f);
        Assert.Equal(255, color1.R);
        Assert.Equal((byte)Math.Round(.1f * 255), color1.G);
        Assert.Equal((byte)Math.Round(.133f * 255), color1.B);
        Assert.Equal((byte)Math.Round(.864f * 255), color1.A);

        Rgba32 color2 = new(1, .1f, .133f);
        Assert.Equal(255, color2.R);
        Assert.Equal(Math.Round(.1f * 255), color2.G);
        Assert.Equal(Math.Round(.133f * 255), color2.B);
        Assert.Equal(255, color2.A);

        Rgba32 color4 = new(new Vector3(1, .1f, .133f));
        Assert.Equal(255, color4.R);
        Assert.Equal(Math.Round(.1f * 255), color4.G);
        Assert.Equal(Math.Round(.133f * 255), color4.B);
        Assert.Equal(255, color4.A);

        Rgba32 color5 = new(new Vector4(1, .1f, .133f, .5f));
        Assert.Equal(255, color5.R);
        Assert.Equal(Math.Round(.1f * 255), color5.G);
        Assert.Equal(Math.Round(.133f * 255), color5.B);
        Assert.Equal(Math.Round(.5f * 255), color5.A);
    }

    /// <summary>
    /// Tests whether FromHex and ToHex work correctly.
    /// </summary>
    [Fact]
    public void FromAndToHex()
    {
        // 8 digit hex matches css4 spec. RRGGBBAA
        Rgba32 color = Rgba32.ParseHex("#AABBCCDD"); // 170, 187, 204, 221
        Assert.Equal(170, color.R);
        Assert.Equal(187, color.G);
        Assert.Equal(204, color.B);
        Assert.Equal(221, color.A);

        Assert.Equal("AABBCCDD", color.ToHex());

        color.R = 0;

        Assert.Equal("00BBCCDD", color.ToHex());

        color.A = 255;

        Assert.Equal("00BBCCFF", color.ToHex());
    }

    /// <summary>
    /// Tests that the individual byte elements are laid out in RGBA order.
    /// </summary>
    [Fact]
    public unsafe void ByteLayout()
    {
        Rgba32 color = new(1, 2, 3, 4);
        byte* colorBase = (byte*)&color;
        Assert.Equal(1, colorBase[0]);
        Assert.Equal(2, colorBase[1]);
        Assert.Equal(3, colorBase[2]);
        Assert.Equal(4, colorBase[3]);

        Assert.Equal(4, sizeof(Rgba32));
    }

    [Fact]
    public void Rgba32_PackedValues()
    {
        Assert.Equal(0x80001Au, new Rgba32(+0.1f, -0.3f, +0.5f, -0.7f).PackedValue);

        // Test the limits.
        Assert.Equal(0x0U, new Rgba32(Vector4.Zero).PackedValue);
        Assert.Equal(0xFFFFFFFF, new Rgba32(Vector4.One).PackedValue);
    }

    [Fact]
    public void Rgba32_ToVector4()
    {
        Assert.Equal(Vector4.One, new Rgba32(Vector4.One).ToVector4());
        Assert.Equal(Vector4.Zero, new Rgba32(Vector4.Zero).ToVector4());
        Assert.Equal(Vector4.UnitX, new Rgba32(Vector4.UnitX).ToVector4());
        Assert.Equal(Vector4.UnitY, new Rgba32(Vector4.UnitY).ToVector4());
        Assert.Equal(Vector4.UnitZ, new Rgba32(Vector4.UnitZ).ToVector4());
        Assert.Equal(Vector4.UnitW, new Rgba32(Vector4.UnitW).ToVector4());
    }

    [Fact]
    public void Rgba32_ToScaledVector4()
    {
        // arrange
        Rgba32 rgba = new(Vector4.One);

        // act
        Vector4 actual = rgba.ToScaledVector4();

        // assert
        Assert.Equal(1, actual.X);
        Assert.Equal(1, actual.Y);
        Assert.Equal(1, actual.Z);
        Assert.Equal(1, actual.W);
    }

    [Fact]
    public void Rgba32_FromScaledVector4()
    {
        // arrange
        Rgba32 rgba = new(Vector4.One);
        const uint expected = 0xFFFFFFFF;

        // act
        Vector4 scaled = rgba.ToScaledVector4();
        Rgba32 actual = Rgba32.FromScaledVector4(scaled);

        // assert
        Assert.Equal(expected, actual.PackedValue);
    }

    [Fact]
    public void Rgba32_Clamping()
    {
        Assert.Equal(Vector4.Zero, new Rgba32(Vector4.One * -1234.0f).ToVector4());
        Assert.Equal(Vector4.One, new Rgba32(Vector4.One * +1234.0f).ToVector4());
    }

    [Fact]
    public void Rgba32_ToRgba32()
    {
        // arrange
        Rgba32 rgba = new(+0.1f, -0.3f, +0.5f, -0.7f);
        Rgba32 expected = new(0x1a, 0, 0x80, 0);

        // act
        Rgba32 actual = Rgba32.FromRgba32(rgba);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgba32_FromRgba32_ToRgba32()
    {
        // arrange
        Rgba32 expected = new(0x1a, 0, 0x80, 0);

        // act
        Rgba32 rgba = Rgba32.FromRgba32(expected);
        Rgba32 actual = Rgba32.FromRgba32(rgba);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgba32_FromBgra32_ToRgba32()
    {
        // arrange
        Bgra32 expected = new(0x1a, 0, 0x80, 0);

        // act
        Rgba32 rgba = Rgba32.FromBgra32(expected);
        Bgra32 actual = Bgra32.FromRgba32(rgba);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgba32_FromAbgr32_ToRgba32()
    {
        // arrange
        Abgr32 expected = new(0x1a, 0, 0x80, 0);

        // act
        Rgba32 rgba = Rgba32.FromAbgr32(expected);
        Abgr32 actual = Abgr32.FromRgba32(rgba);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgba32_FromArgb32_ToArgb32()
    {
        // arrange
        Argb32 expected = new(0x1a, 0, 0x80, 0);

        // act
        Rgba32 rgba = Rgba32.FromArgb32(expected);
        Argb32 actual = Argb32.FromRgba32(rgba);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgba32_FromRgb48()
    {
        // arrange
        Rgb48 expected = new(65535, 0, 65535);

        // act
        Rgba32 input = Rgba32.FromRgb48(expected);
        Rgb48 actual = Rgb48.FromScaledVector4(input.ToScaledVector4());

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgba32_FromRgba64()
    {
        // arrange
        Rgba64 expected = new(65535, 0, 65535, 0);

        // act
        Rgba32 input = Rgba32.FromRgba64(expected);
        Rgba64 actual = Rgba64.FromScaledVector4(input.ToScaledVector4());

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgba32_FromBgra5551()
    {
        // arrange
        const uint expected = 0xFFFFFFFF;

        // act
        Rgba32 rgb = Rgba32.FromBgra5551(new Bgra5551(1f, 1f, 1f, 1f));

        // assert
        Assert.Equal(expected, rgb.PackedValue);
    }

    [Fact]
    public void Rgba32_PixelInformation()
    {
        PixelTypeInfo info = Rgba32.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<Rgba32>() * 8, info.BitsPerPixel);
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
