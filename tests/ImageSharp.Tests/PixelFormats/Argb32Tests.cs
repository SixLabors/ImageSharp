// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class Argb32Tests
{
    /// <summary>
    /// Tests the equality operators for equality.
    /// </summary>
    [Fact]
    public void AreEqual()
    {
        Argb32 color1 = new(0.0f, 0.0f, 0.0f, 0.0f);
        Argb32 color2 = new(new Vector4(0.0f));
        Argb32 color3 = new(new Vector4(1f, 0.0f, 1f, 1f));
        Argb32 color4 = new(1f, 0.0f, 1f, 1f);
        Argb32 color5 = new(0, 0, 0, 1F);
        Argb32 color6 = Argb32.ParseHex("#000");
        Argb32 color7 = Argb32.ParseHex("#F000");
        Argb32 color8 = Argb32.ParseHex("#000000");
        Argb32 color9 = Argb32.ParseHex("#FF000000");

        Assert.Equal(color1, color2);
        Assert.Equal(color3, color4);
        Assert.Equal(color5, color6);
        Assert.Equal(color5, color7);
        Assert.Equal(color5, color8);
        Assert.Equal(color5, color9);
    }

    /// <summary>
    /// Tests the equality operators for inequality.
    /// </summary>
    [Fact]
    public void AreNotEqual()
    {
        Argb32 color1 = new(0.0f, 0.0f, 0.0f, 0.0f);
        Argb32 color2 = new(new Vector4(1f));
        Argb32 color3 = new(new Vector4(1f, 0.0f, 0.0f, 1f));
        Argb32 color4 = new(1f, 1f, 0.0f, 1f);

        Assert.NotEqual(color1, color2);
        Assert.NotEqual(color3, color4);
    }

    /// <summary>
    /// Tests whether the color constructor correctly assign properties.
    /// </summary>
    [Fact]
    public void ConstructorAssignsProperties()
    {
        Argb32 color1 = new(1, .1f, .133f, .864f);
        Assert.Equal(255, color1.R);
        Assert.Equal((byte)Math.Round(.1f * 255), color1.G);
        Assert.Equal((byte)Math.Round(.133f * 255), color1.B);
        Assert.Equal((byte)Math.Round(.864f * 255), color1.A);

        Argb32 color2 = new(1, .1f, .133f);
        Assert.Equal(255, color2.R);
        Assert.Equal(Math.Round(.1f * 255), color2.G);
        Assert.Equal(Math.Round(.133f * 255), color2.B);
        Assert.Equal(255, color2.A);

        Argb32 color4 = new(new Vector3(1, .1f, .133f));
        Assert.Equal(255, color4.R);
        Assert.Equal(Math.Round(.1f * 255), color4.G);
        Assert.Equal(Math.Round(.133f * 255), color4.B);
        Assert.Equal(255, color4.A);

        Argb32 color5 = new(new Vector4(1, .1f, .133f, .5f));
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
        // 8 digit hex. AARRGGBB
        Argb32 color = Argb32.ParseHex("#AABBCCDD");
        Assert.Equal(0xAA, color.A);
        Assert.Equal(0xBB, color.R);
        Assert.Equal(0xCC, color.G);
        Assert.Equal(0xDD, color.B);

        Assert.Equal("AABBCCDD", color.ToHex());

        color.R = 0;

        Assert.Equal("AA00CCDD", color.ToHex());

        color.A = 255;

        Assert.Equal("FF00CCDD", color.ToHex());
    }

    [Fact]
    public void Argb32_TryParseHex()
    {
        Assert.True(Argb32.TryParseHex("000", out Argb32 color));
        Assert.Equal("FF000000", color.ToHex());

        Assert.True(Argb32.TryParseHex("#000", out color));
        Assert.Equal("FF000000", color.ToHex());

        Assert.True(Argb32.TryParseHex("0000", out color));
        Assert.Equal("00000000", color.ToHex());

        Assert.True(Argb32.TryParseHex("#0000", out color));
        Assert.Equal("00000000", color.ToHex());

        Assert.True(Argb32.TryParseHex("000000", out color));
        Assert.Equal("FF000000", color.ToHex());

        Assert.True(Argb32.TryParseHex("#000000", out color));
        Assert.Equal("FF000000", color.ToHex());

        Assert.True(Argb32.TryParseHex("abc", out color));
        Assert.Equal("FFAABBCC", color.ToHex());

        Assert.True(Argb32.TryParseHex("ABC", out color));
        Assert.Equal("FFAABBCC", color.ToHex());

        Assert.True(Argb32.TryParseHex("#ABC", out color));
        Assert.Equal("FFAABBCC", color.ToHex());

        Assert.True(Argb32.TryParseHex("ABCD", out color));
        Assert.Equal("AABBCCDD", color.ToHex());

        Assert.True(Argb32.TryParseHex("#ABCD", out color));
        Assert.Equal("AABBCCDD", color.ToHex());

        Assert.True(Argb32.TryParseHex("AABBCC", out color));
        Assert.Equal("FFAABBCC", color.ToHex());

        Assert.True(Argb32.TryParseHex("#AABBCC", out color));
        Assert.Equal("FFAABBCC", color.ToHex());

        Assert.True(Argb32.TryParseHex("AABBCCDD", out color));
        Assert.Equal("AABBCCDD", color.ToHex());

        Assert.True(Argb32.TryParseHex("#AABBCCDD", out color));
        Assert.Equal("AABBCCDD", color.ToHex());

        Assert.False(Argb32.TryParseHex(string.Empty, out _));
        Assert.False(Argb32.TryParseHex(null, out _));
        Assert.False(Argb32.TryParseHex("A", out _));
        Assert.False(Argb32.TryParseHex("#A", out _));
        Assert.False(Argb32.TryParseHex("AB", out _));
        Assert.False(Argb32.TryParseHex("#AB", out _));
        Assert.False(Argb32.TryParseHex("ABCDE", out _));
        Assert.False(Argb32.TryParseHex("#ABCDE", out _));
        Assert.False(Argb32.TryParseHex("ABCDEFG", out _));
        Assert.False(Argb32.TryParseHex("#ABCDEFG", out _));
        Assert.False(Argb32.TryParseHex("ABCD123", out _));
        Assert.False(Argb32.TryParseHex("#ABCD123", out _));
        Assert.False(Argb32.TryParseHex("ABCD12345", out _));
        Assert.False(Argb32.TryParseHex("#ABCD12345", out _));
    }

    [Fact]
    public void Argb32_PackedValue()
    {
        Assert.Equal(0x80001a00u, new Argb32(+0.1f, -0.3f, +0.5f, -0.7f).PackedValue);
        Assert.Equal(0x0U, new Argb32(Vector4.Zero).PackedValue);
        Assert.Equal(0xFFFFFFFF, new Argb32(Vector4.One).PackedValue);
    }

    [Fact]
    public void Argb32_ToVector4()
    {
        Assert.Equal(Vector4.One, new Argb32(Vector4.One).ToVector4());
        Assert.Equal(Vector4.Zero, new Argb32(Vector4.Zero).ToVector4());
        Assert.Equal(Vector4.UnitX, new Argb32(Vector4.UnitX).ToVector4());
        Assert.Equal(Vector4.UnitY, new Argb32(Vector4.UnitY).ToVector4());
        Assert.Equal(Vector4.UnitZ, new Argb32(Vector4.UnitZ).ToVector4());
        Assert.Equal(Vector4.UnitW, new Argb32(Vector4.UnitW).ToVector4());
    }

    [Fact]
    public void Argb32_ToScaledVector4()
    {
        // arrange
        Argb32 argb = new(Vector4.One);

        // act
        Vector4 actual = argb.ToScaledVector4();

        // assert
        Assert.Equal(1, actual.X);
        Assert.Equal(1, actual.Y);
        Assert.Equal(1, actual.Z);
        Assert.Equal(1, actual.W);
    }

    [Fact]
    public void Argb32_FromScaledVector4()
    {
        // arrange
        Vector4 scaled = new Argb32(Vector4.One).ToScaledVector4();
        const uint expected = 0xFFFFFFFF;

        // act
        Argb32 pixel = Argb32.FromScaledVector4(scaled);
        uint actual = pixel.PackedValue;

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Argb32_FromBgra5551()
    {
        // arrange
        const uint expected = uint.MaxValue;

        // act
        Argb32 argb = Argb32.FromBgra5551(new Bgra5551(1f, 1f, 1f, 1f));

        // assert
        Assert.Equal(expected, argb.PackedValue);
    }

    [Fact]
    public void Argb32_Clamping()
    {
        Assert.Equal(Vector4.Zero, new Argb32(Vector4.One * -1234.0f).ToVector4());
        Assert.Equal(Vector4.One, new Argb32(Vector4.One * +1234.0f).ToVector4());
    }

    [Fact]
    public void Argb32_PixelInformation()
    {
        PixelTypeInfo info = Argb32.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<Argb32>() * 8, info.BitsPerPixel);
        Assert.Equal(PixelAlphaRepresentation.Unassociated, info.AlphaRepresentation);
        Assert.Equal(PixelColorType.Alpha | PixelColorType.RGB, info.ColorType);

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
