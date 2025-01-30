// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class Bgr24Tests
{
    [Fact]
    public void AreEqual()
    {
        Bgr24 color1 = new(byte.MaxValue, 0, byte.MaxValue);
        Bgr24 color2 = new(byte.MaxValue, 0, byte.MaxValue);

        Assert.Equal(color1, color2);
    }

    [Fact]
    public void AreNotEqual()
    {
        Bgr24 color1 = new(byte.MaxValue, 0, 0);
        Bgr24 color2 = new(byte.MaxValue, 0, byte.MaxValue);

        Assert.NotEqual(color1, color2);
    }

    public static readonly TheoryData<byte, byte, byte> ColorData = new() { { 1, 2, 3 }, { 4, 5, 6 }, { 0, 255, 42 } };

    [Theory]
    [MemberData(nameof(ColorData))]
    public void Constructor(byte r, byte g, byte b)
    {
        Rgb24 p = new(r, g, b);

        Assert.Equal(r, p.R);
        Assert.Equal(g, p.G);
        Assert.Equal(b, p.B);
    }

    [Fact]
    public unsafe void ByteLayoutIsSequentialBgr()
    {
        Bgr24 color = new(1, 2, 3);
        byte* ptr = (byte*)&color;

        Assert.Equal(3, ptr[0]);
        Assert.Equal(2, ptr[1]);
        Assert.Equal(1, ptr[2]);
    }

    [Theory]
    [MemberData(nameof(ColorData))]
    public void Equals_WhenTrue(byte r, byte g, byte b)
    {
        Bgr24 x = new(r, g, b);
        Bgr24 y = new(r, g, b);

        Assert.True(x.Equals(y));
        Assert.True(x.Equals((object)y));
        Assert.Equal(x.GetHashCode(), y.GetHashCode());
    }

    [Theory]
    [InlineData(1, 2, 3, 1, 2, 4)]
    [InlineData(0, 255, 0, 0, 244, 0)]
    [InlineData(1, 255, 0, 0, 255, 0)]
    public void Equals_WhenFalse(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        Bgr24 a = new(r1, g1, b1);
        Bgr24 b = new(r2, g2, b2);

        Assert.False(a.Equals(b));
        Assert.False(a.Equals((object)b));
    }

    [Fact]
    public void FromRgba32()
    {
        Bgr24 rgb = Bgr24.FromRgba32(new(1, 2, 3, 4));

        Assert.Equal(1, rgb.R);
        Assert.Equal(2, rgb.G);
        Assert.Equal(3, rgb.B);
    }

    private static Vector4 Vec(byte r, byte g, byte b, byte a = 255) => new(
        r / 255f,
        g / 255f,
        b / 255f,
        a / 255f);

    [Fact]
    public void FromVector4()
    {
        Bgr24 rgb = Bgr24.FromVector4(Vec(1, 2, 3, 4));

        Assert.Equal(1, rgb.R);
        Assert.Equal(2, rgb.G);
        Assert.Equal(3, rgb.B);
    }

    [Fact]
    public void ToVector4()
    {
        Bgr24 rgb = new(1, 2, 3);

        Assert.Equal(Vec(1, 2, 3), rgb.ToVector4());
    }

    [Fact]
    public void Bgr24_FromBgra5551()
    {
        // act
        Bgr24 bgr = Bgr24.FromBgra5551(new(1f, 1f, 1f, 1f));

        // assert
        Assert.Equal(255, bgr.R);
        Assert.Equal(255, bgr.G);
        Assert.Equal(255, bgr.B);
    }

    [Fact]
    public void Bgr24_PixelInformation()
    {
        PixelTypeInfo info = Bgr24.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<Bgr24>() * 8, info.BitsPerPixel);
        Assert.Equal(PixelAlphaRepresentation.None, info.AlphaRepresentation);
        Assert.Equal(PixelColorType.BGR, info.ColorType);

        PixelComponentInfo componentInfo = info.ComponentInfo.Value;
        Assert.Equal(3, componentInfo.ComponentCount);
        Assert.Equal(0, componentInfo.Padding);
        Assert.Equal(8, componentInfo.GetComponentPrecision(0));
        Assert.Equal(8, componentInfo.GetComponentPrecision(1));
        Assert.Equal(8, componentInfo.GetComponentPrecision(2));
        Assert.Equal(8, componentInfo.GetMaximumComponentPrecision());
    }
}
