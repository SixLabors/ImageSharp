// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class Abgr32Tests
{
    /// <summary>
    /// Tests the equality operators for equality.
    /// </summary>
    [Fact]
    public void AreEqual()
    {
        Abgr32 color1 = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
        Abgr32 color2 = new(byte.MaxValue, byte.MaxValue, byte.MaxValue);

        Assert.Equal(color1, color2);
    }

    /// <summary>
    /// Tests the equality operators for inequality.
    /// </summary>
    [Fact]
    public void AreNotEqual()
    {
        Abgr32 color1 = new(0, 0, byte.MaxValue, byte.MaxValue);
        Abgr32 color2 = new(byte.MaxValue, byte.MaxValue, byte.MaxValue);

        Assert.NotEqual(color1, color2);
    }

    public static readonly TheoryData<byte, byte, byte, byte> ColorData =
        new()
        {
            { 1, 2, 3, 4 },
            { 4, 5, 6, 7 },
            { 0, 255, 42, 0 },
            { 1, 2, 3, 255 }
        };

    [Theory]
    [MemberData(nameof(ColorData))]
    public void Constructor(byte b, byte g, byte r, byte a)
    {
        Abgr32 p = new(r, g, b, a);

        Assert.Equal(r, p.R);
        Assert.Equal(g, p.G);
        Assert.Equal(b, p.B);
        Assert.Equal(a, p.A);
    }

    [Fact]
    public unsafe void ByteLayoutIsSequentialBgra()
    {
        Abgr32 color = new(1, 2, 3, 4);
        byte* ptr = (byte*)&color;

        Assert.Equal(4, ptr[0]);
        Assert.Equal(3, ptr[1]);
        Assert.Equal(2, ptr[2]);
        Assert.Equal(1, ptr[3]);
    }

    [Theory]
    [MemberData(nameof(ColorData))]
    public void Equality_WhenTrue(byte r, byte g, byte b, byte a)
    {
        Abgr32 x = new(r, g, b, a);
        Abgr32 y = new(r, g, b, a);

        Assert.True(x.Equals(y));
        Assert.True(x.Equals((object)y));
        Assert.Equal(x.GetHashCode(), y.GetHashCode());
    }

    [Theory]
    [InlineData(1, 2, 3, 4, 1, 2, 3, 5)]
    [InlineData(0, 0, 255, 0, 0, 0, 244, 0)]
    [InlineData(0, 255, 0, 0, 0, 244, 0, 0)]
    [InlineData(1, 255, 0, 0, 0, 255, 0, 0)]
    public void Equality_WhenFalse(byte b1, byte g1, byte r1, byte a1, byte b2, byte g2, byte r2, byte a2)
    {
        Abgr32 x = new(r1, g1, b1, a1);
        Abgr32 y = new(r2, g2, b2, a2);

        Assert.False(x.Equals(y));
        Assert.False(x.Equals((object)y));
    }

    [Fact]
    public void FromRgba32()
    {
        Abgr32 abgr = Abgr32.FromRgba32(new(1, 2, 3, 4));

        Assert.Equal(1, abgr.R);
        Assert.Equal(2, abgr.G);
        Assert.Equal(3, abgr.B);
        Assert.Equal(4, abgr.A);
    }

    private static Vector4 Vec(byte r, byte g, byte b, byte a = 255) => new(
        r / 255f,
        g / 255f,
        b / 255f,
        a / 255f);

    [Fact]
    public void FromVector4()
    {
        Abgr32 c = Abgr32.FromVector4(Vec(1, 2, 3, 4));

        Assert.Equal(1, c.R);
        Assert.Equal(2, c.G);
        Assert.Equal(3, c.B);
        Assert.Equal(4, c.A);
    }

    [Fact]
    public void ToVector4()
    {
        Abgr32 abgr = new(1, 2, 3, 4);

        Assert.Equal(Vec(1, 2, 3, 4), abgr.ToVector4());
    }

    [Fact]
    public void Abgr32_FromBgra5551()
    {
        // arrange
        const uint expected = uint.MaxValue;

        // act
        Abgr32 abgr = Abgr32.FromBgra5551(new(1f, 1f, 1f, 1f));

        // assert
        Assert.Equal(expected, abgr.PackedValue);
    }

    [Fact]
    public void Abgr32_PixelInformation()
    {
        PixelTypeInfo info = Abgr32.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<Abgr32>() * 8, info.BitsPerPixel);
        Assert.Equal(PixelAlphaRepresentation.Unassociated, info.AlphaRepresentation);
        Assert.Equal(PixelColorType.Alpha | PixelColorType.BGR, info.ColorType);

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
