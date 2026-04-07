// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class Rgb96Tests
{
    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(50, 70, 80)]
    [InlineData(1000, 2000, 3000)]
    public void Constructor(uint r, uint g, uint b)
    {
        Rgb96 p = new(r, g, b);

        Assert.Equal(r, p.R);
        Assert.Equal(g, p.G);
        Assert.Equal(b, p.B);
    }

    [Fact]
    public void Rgb96_ToVector4()
    {
        Assert.Equal(Vector4.UnitW, new Rgb96(0, 0, 0).ToVector4());
        Assert.Equal(Vector4.One, new Rgb96(uint.MaxValue, uint.MaxValue, uint.MaxValue).ToVector4());
        Assert.Equal(new Vector4(0.5F, 0.5F, 0.5F, 1.0F), new Rgb96(uint.MaxValue / 2, uint.MaxValue / 2, uint.MaxValue / 2).ToVector4());
    }

    [Theory]
    [InlineData(uint.MaxValue, uint.MaxValue, uint.MaxValue)]
    [InlineData(0, 0, 0)]
    [InlineData(uint.MaxValue / 2, 100, 2222)]
    public void Rgb96_ToScaledVector4(uint r, uint g, uint b)
    {
        // arrange
        Rgb96 rgb = new(r, g, b);

        float max = uint.MaxValue;
        float rr = r / max;
        float gg = g / max;
        float bb = b / max;

        // act
        Vector4 actual = rgb.ToScaledVector4();

        // assert
        Assert.Equal(rr, actual.X);
        Assert.Equal(gg, actual.Y);
        Assert.Equal(bb, actual.Z);
        Assert.Equal(1.0F, actual.W);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(5000, 100, 2222)]
    public void Rgb96_FromScaledVector4(uint r, uint g, uint b)
    {
        // arrange
        Rgb96 source = new(r, g, b);
        Vector4 scaled = source.ToScaledVector4();

        // act
        Rgb96 actual = Rgb96.FromScaledVector4(scaled);

        // assert
        Assert.Equal(source, actual);
    }

    [Fact]
    public void Rgb96_ToRgba32()
    {
        // arrange
        Rgb96 rgb96 = new((uint)(uint.MaxValue * 0.1f), uint.MaxValue / 2, uint.MaxValue);
        Rgba32 expected = new(25, 127, 255, 255);

        // act
        Rgba32 actual = rgb96.ToRgba32();

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Equality_WhenTrue()
    {
        Rgb96 c1 = new(100, 2000, 3000);
        Rgb96 c2 = new(100, 2000, 3000);

        Assert.True(c1.Equals(c2));
        Assert.True(c1.GetHashCode() == c2.GetHashCode());
    }

    [Fact]
    public void Equality_WhenFalse()
    {
        Rgb96 c1 = new(100, 2000, 3000);
        Rgb96 c2 = new(101, 2000, 3000);
        Rgb96 c3 = new(100, 2000, 3001);

        Assert.False(c1.Equals(c2));
        Assert.False(c2.Equals(c3));
        Assert.False(c3.Equals(c1));
    }

    [Fact]
    public void Rgb96_FromRgba32()
    {
        // arrange
        Rgba32 source = new(25, 127, 255, 255);
        Rgb96 expected = new(421075225, 2139062143, uint.MaxValue);

        // act
        Rgb96 actual = Rgb96.FromRgba32(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgb96_FromRgb24()
    {
        // arrange
        Rgb24 source = new(25, 127, 255);
        Rgb96 expected = new(421075225, 2139062143, uint.MaxValue);

        // act
        Rgb96 actual = Rgb96.FromRgb24(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgb96_FromRgb48()
    {
        // arrange
        Rgb48 source = new(0, 32767, ushort.MaxValue);
        Rgb96 expected = new(0, 2147450879, uint.MaxValue);

        // act
        Rgb96 actual = Rgb96.FromRgb48(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgb96_FromRgba64()
    {
        // arrange
        Rgba64 source = new(0, 32767, ushort.MaxValue, ushort.MaxValue);
        Rgb96 expected = new(0, 2147450879, uint.MaxValue);

        // act
        Rgb96 actual = Rgb96.FromRgba64(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgb96_FromLa32()
    {
        // arrange
        La32 source = new(32767, ushort.MaxValue);
        Rgb96 expected = new(2147450879, 2147450879, 2147450879);

        // act
        Rgb96 actual = Rgb96.FromLa32(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgb96_FromLa16()
    {
        // arrange
        La16 source = new(127, byte.MaxValue);
        Rgb96 expected = new(2139062143, 2139062143, 2139062143);

        // act
        Rgb96 actual = Rgb96.FromLa16(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgb96_FromL16()
    {
        // arrange
        L16 source = new(32767);
        Rgb96 expected = new(2147450879, 2147450879, 2147450879);

        // act
        Rgb96 actual = Rgb96.FromL16(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgb96_FromL8()
    {
        // arrange
        L8 source = new(127);
        Rgb96 expected = new(2139062143, 2139062143, 2139062143);

        // act
        Rgb96 actual = Rgb96.FromL8(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgb96_FromBgra32()
    {
        // arrange
        Bgra32 source = new(127, 25, 255);
        Rgb96 expected = new(2139062143, 421075225, uint.MaxValue);

        // act
        Rgb96 actual = Rgb96.FromBgra32(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgb96_FromBgr24()
    {
        // arrange
        Bgr24 source = new(127, 25, 255);
        Rgb96 expected = new(2139062143, 421075225, uint.MaxValue);

        // act
        Rgb96 actual = Rgb96.FromBgr24(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgb96_FromBgra5551()
    {
        // arrange
        Bgra5551 source = new(1.0f, 1.0f, 1.0f, 1.0f);
        Rgb96 expected = new(uint.MaxValue, uint.MaxValue, uint.MaxValue);

        // act
        Rgb96 actual = Rgb96.FromBgra5551(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgb96_FromArgb32()
    {
        // arrange
        Argb32 source = new(127, 25, 255);
        Rgb96 expected = new(2139062143, 421075225, uint.MaxValue);

        // act
        Rgb96 actual = Rgb96.FromArgb32(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgb96_FromAbgr32()
    {
        // arrange
        Abgr32 source = new(127, 25, 255);
        Rgb96 expected = new(2139062143, 421075225, uint.MaxValue);

        // act
        Rgb96 actual = Rgb96.FromAbgr32(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgb96_PixelInformation()
    {
        PixelTypeInfo info = Rgb96.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<Rgb96>() * 8, info.BitsPerPixel);
        Assert.Equal(PixelAlphaRepresentation.None, info.AlphaRepresentation);
        Assert.Equal(PixelColorType.RGB, info.ColorType);

        PixelComponentInfo componentInfo = info.ComponentInfo.Value;
        Assert.Equal(3, componentInfo.ComponentCount);
        Assert.Equal(0, componentInfo.Padding);
        Assert.Equal(32, componentInfo.GetComponentPrecision(0));
        Assert.Equal(32, componentInfo.GetComponentPrecision(1));
        Assert.Equal(32, componentInfo.GetComponentPrecision(2));
        Assert.Equal(32, componentInfo.GetMaximumComponentPrecision());
    }
}
