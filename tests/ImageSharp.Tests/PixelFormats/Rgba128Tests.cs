// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class Rgba128Tests
{
    [Theory]
    [InlineData(1, 2, 3, 4)]
    [InlineData(50, 70, 80, 90)]
    [InlineData(1000, 2000, 3000, 4000)]
    public void Constructor(uint r, uint g, uint b, uint a)
    {
        Rgba128 p = new(r, g, b, a);

        Assert.Equal(r, p.R);
        Assert.Equal(g, p.G);
        Assert.Equal(b, p.B);
    }

    [Fact]
    public void Rgba128_ToVector4()
    {
        Assert.Equal(Vector4.Zero, new Rgba128(0, 0, 0, 0).ToVector4());
        Assert.Equal(Vector4.One, new Rgba128(uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue).ToVector4());
        Assert.Equal(new Vector4(0.5F, 0.5F, 0.5F, 0.5F), new Rgba128(uint.MaxValue / 2, uint.MaxValue / 2, uint.MaxValue / 2, uint.MaxValue / 2).ToVector4());
    }

    [Theory]
    [InlineData(uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue)]
    [InlineData(0, 0, 0, 0)]
    [InlineData(uint.MaxValue / 2, 100, 2222, 3333)]
    public void Rgba128_ToScaledVector4(uint r, uint g, uint b, uint a)
    {
        // arrange
        Rgba128 rgb = new(r, g, b, a);

        float max = uint.MaxValue;
        float rr = r / max;
        float gg = g / max;
        float bb = b / max;
        float aa = a / max;

        // act
        Vector4 actual = rgb.ToScaledVector4();

        // assert
        Assert.Equal(rr, actual.X);
        Assert.Equal(gg, actual.Y);
        Assert.Equal(bb, actual.Z);
        Assert.Equal(aa, actual.W);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(5000, 100, 2222, 3333)]
    public void Rgba128_FromScaledVector4(uint r, uint g, uint b, uint a)
    {
        // arrange
        Rgba128 source = new(r, g, b, a);
        Vector4 scaled = source.ToScaledVector4();

        // act
        Rgba128 actual = Rgba128.FromScaledVector4(scaled);

        // assert
        Assert.Equal(source, actual);
    }

    [Fact]
    public void Rgba128_ToRgba32()
    {
        // arrange
        Rgba128 rgb96 = new((uint)(uint.MaxValue * 0.1), uint.MaxValue / 2, uint.MaxValue, uint.MaxValue);
        Rgba32 expected = new(25, 127, 255, 255);

        // act
        Rgba32 actual = rgb96.ToRgba32();

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Equality_WhenTrue()
    {
        Rgba128 c1 = new(100, 2000, 3000, 4000);
        Rgba128 c2 = new(100, 2000, 3000, 4000);

        Assert.True(c1.Equals(c2));
        Assert.True(c1.GetHashCode() == c2.GetHashCode());
    }

    [Fact]
    public void Equality_WhenFalse()
    {
        Rgba128 c1 = new(100, 2000, 3000, 4000);
        Rgba128 c2 = new(101, 2000, 3000, 4000);
        Rgba128 c3 = new(100, 2000, 3001, 4000);

        Assert.False(c1.Equals(c2));
        Assert.False(c2.Equals(c3));
        Assert.False(c3.Equals(c1));
    }

    [Fact]
    public void Rgba128_FromRgba32()
    {
        // arrange
        Rgba32 source = new(25, 127, 255, 255);
        Rgba128 expected = new(421075225, 2139062143, uint.MaxValue, uint.MaxValue);

        // act
        Rgba128 actual = Rgba128.FromRgba32(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgba128_FromRgb24()
    {
        // arrange
        Rgb24 source = new(25, 127, 255);
        Rgba128 expected = new(421075225, 2139062143, uint.MaxValue, uint.MaxValue);

        // act
        Rgba128 actual = Rgba128.FromRgb24(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgba128_FromRgb48()
    {
        // arrange
        Rgb48 source = new(0, 32767, ushort.MaxValue);
        Rgba128 expected = new(0, 2147450879, uint.MaxValue, uint.MaxValue);

        // act
        Rgba128 actual = Rgba128.FromRgb48(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgba128_FromRgba64()
    {
        // arrange
        Rgba64 source = new(0, 32767, ushort.MaxValue, ushort.MaxValue);
        Rgba128 expected = new(0, 2147450879, uint.MaxValue, uint.MaxValue);

        // act
        Rgba128 actual = Rgba128.FromRgba64(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgba128_FromLa32()
    {
        // arrange
        La32 source = new(32767, ushort.MaxValue);
        Rgba128 expected = new(2147450879, 2147450879, 2147450879, 2147450879);

        // act
        Rgba128 actual = Rgba128.FromLa32(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgba128_FromLa16()
    {
        // arrange
        La16 source = new(127, byte.MaxValue);
        Rgba128 expected = new(2139062143, 2139062143, 2139062143, 2139062143);

        // act
        Rgba128 actual = Rgba128.FromLa16(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgba128_FromL16()
    {
        // arrange
        L16 source = new(32767);
        Rgba128 expected = new(2147450879, 2147450879, 2147450879, 2147450879);

        // act
        Rgba128 actual = Rgba128.FromL16(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgba128_FromL8()
    {
        // arrange
        L8 source = new(127);
        Rgba128 expected = new(2139062143, 2139062143, 2139062143, 2139062143);

        // act
        Rgba128 actual = Rgba128.FromL8(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgba128_FromBgra32()
    {
        // arrange
        Bgra32 source = new(127, 25, 255);
        Rgba128 expected = new(2139062143, 421075225, uint.MaxValue, uint.MaxValue);

        // act
        Rgba128 actual = Rgba128.FromBgra32(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgba128_FromBgr24()
    {
        // arrange
        Bgr24 source = new(127, 25, 255);
        Rgba128 expected = new(2139062143, 421075225, uint.MaxValue, uint.MaxValue);

        // act
        Rgba128 actual = Rgba128.FromBgr24(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgba128_FromBgra5551()
    {
        // arrange
        Bgra5551 source = new(1.0f, 1.0f, 1.0f, 1.0f);
        Rgba128 expected = new(uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue);

        // act
        Rgba128 actual = Rgba128.FromBgra5551(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgba128_FromArgb32()
    {
        // arrange
        Argb32 source = new(127, 25, 255);
        Rgba128 expected = new(2139062143, 421075225, uint.MaxValue, uint.MaxValue);

        // act
        Rgba128 actual = Rgba128.FromArgb32(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgba128_FromAbgr32()
    {
        // arrange
        Abgr32 source = new(127, 25, 255);
        Rgba128 expected = new(2139062143, 421075225, uint.MaxValue, uint.MaxValue);

        // act
        Rgba128 actual = Rgba128.FromAbgr32(source);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgba128_PixelInformation()
    {
        PixelTypeInfo info = Rgba128.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<Rgba128>() * 8, info.BitsPerPixel);
        Assert.Equal(PixelAlphaRepresentation.Unassociated, info.AlphaRepresentation);
        Assert.Equal(PixelColorType.RGB | PixelColorType.Alpha, info.ColorType);

        PixelComponentInfo? maybeComponentInfo = info.ComponentInfo;
        Assert.NotNull(maybeComponentInfo);
        PixelComponentInfo componentInfo = maybeComponentInfo.Value;

        Assert.Equal(4, componentInfo.ComponentCount);
        Assert.Equal(0, componentInfo.Padding);
        Assert.Equal(32, componentInfo.GetComponentPrecision(0));
        Assert.Equal(32, componentInfo.GetComponentPrecision(1));
        Assert.Equal(32, componentInfo.GetComponentPrecision(2));
        Assert.Equal(32, componentInfo.GetComponentPrecision(3));
        Assert.Equal(32, componentInfo.GetMaximumComponentPrecision());
    }
}
