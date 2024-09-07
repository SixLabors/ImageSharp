// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class Rgba64Tests
{
    [Fact]
    public void Rgba64_PackedValues()
    {
        Assert.Equal(0x73334CCC2666147BUL, new Rgba64(5243, 9830, 19660, 29491).PackedValue);

        // Test the limits.
        Assert.Equal(0x0UL, new Rgba64(0, 0, 0, 0).PackedValue);
        Assert.Equal(
            0xFFFFFFFFFFFFFFFF,
            new Rgba64(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue).PackedValue);

        // Test data ordering
        Assert.Equal(0xC7AD8F5C570A1EB8, new Rgba64(0x1EB8, 0x570A, 0x8F5C, 0xC7AD).PackedValue);
    }

    [Fact]
    public void Rgba64_ToVector4()
    {
        Assert.Equal(Vector4.Zero, new Rgba64(0, 0, 0, 0).ToVector4());
        Assert.Equal(Vector4.One, new Rgba64(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue).ToVector4());
    }

    [Theory]
    [InlineData(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue)]
    [InlineData(0, 0, 0, 0)]
    [InlineData(ushort.MaxValue / 2, 100, 2222, 33333)]
    public void Rgba64_ToScaledVector4(ushort r, ushort g, ushort b, ushort a)
    {
        // arrange
        Rgba64 short2 = new(r, g, b, a);

        float max = ushort.MaxValue;
        float rr = r / max;
        float gg = g / max;
        float bb = b / max;
        float aa = a / max;

        // act
        Vector4 actual = short2.ToScaledVector4();

        // assert
        Assert.Equal(rr, actual.X);
        Assert.Equal(gg, actual.Y);
        Assert.Equal(bb, actual.Z);
        Assert.Equal(aa, actual.W);
    }

    [Theory]
    [InlineData(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue)]
    [InlineData(0, 0, 0, 0)]
    [InlineData(ushort.MaxValue / 2, 100, 2222, 33333)]
    public void Rgba64_FromScaledVector4(ushort r, ushort g, ushort b, ushort a)
    {
        // arrange
        Rgba64 source = new(r, g, b, a);

        // act
        Vector4 scaled = source.ToScaledVector4();

        Rgba64 actual = Rgba64.FromScaledVector4(scaled);

        // assert
        Assert.Equal(source, actual);
    }

    [Fact]
    public void Rgba64_Clamping()
    {
        Rgba64 zero = Rgba64.FromVector4(Vector4.One * -1234.0f);
        Rgba64 one = Rgba64.FromVector4(Vector4.One * 1234.0f);

        Assert.Equal(Vector4.Zero, zero.ToVector4());
        Assert.Equal(Vector4.One, one.ToVector4());
    }

    [Fact]
    public void Rgba64_ToRgba32()
    {
        // arrange
        Rgba64 rgba64 = new(5140, 9766, 19532, 29555);
        Rgba32 expected = new(20, 38, 76, 115);

        // act
        Rgba32 actual = rgba64.ToRgba32();

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgba64_FromBgra5551()
    {
        // arrange
        const ushort expected = ushort.MaxValue;

        // act
        Rgba64 rgba = Rgba64.FromBgra5551(new(1.0f, 1.0f, 1.0f, 1.0f));

        // assert
        Assert.Equal(expected, rgba.R);
        Assert.Equal(expected, rgba.G);
        Assert.Equal(expected, rgba.B);
        Assert.Equal(expected, rgba.A);
    }

    [Fact]
    public void Equality_WhenTrue()
    {
        Rgba64 c1 = new(100, 2000, 3000, 40000);
        Rgba64 c2 = new(100, 2000, 3000, 40000);

        Assert.True(c1.Equals(c2));
        Assert.True(c1.GetHashCode() == c2.GetHashCode());
    }

    [Fact]
    public void Equality_WhenFalse()
    {
        Rgba64 c1 = new(100, 2000, 3000, 40000);
        Rgba64 c2 = new(101, 2000, 3000, 40000);
        Rgba64 c3 = new(100, 2000, 3000, 40001);

        Assert.False(c1.Equals(c2));
        Assert.False(c2.Equals(c3));
        Assert.False(c3.Equals(c1));
    }

    [Fact]
    public void Rgba64_FromRgba32()
    {
        Rgba32 source = new(20, 38, 76, 115);
        Rgba64 expected = new(5140, 9766, 19532, 29555);

        Rgba64 actual = Rgba64.FromRgba32(source);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ConstructFrom_Rgba32()
    {
        Rgba64 expected = new(5140, 9766, 19532, 29555);
        Rgba32 source = new(20, 38, 76, 115);
        Rgba64 actual = new(source);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ConstructFrom_Bgra32()
    {
        Rgba64 expected = new(5140, 9766, 19532, 29555);
        Bgra32 source = new(20, 38, 76, 115);
        Rgba64 actual = new(source);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ConstructFrom_Argb32()
    {
        Rgba64 expected = new(5140, 9766, 19532, 29555);
        Argb32 source = new(20, 38, 76, 115);
        Rgba64 actual = new(source);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ConstructFrom_Abgr32()
    {
        Rgba64 expected = new(5140, 9766, 19532, 29555);
        Abgr32 source = new(20, 38, 76, 115);
        Rgba64 actual = new(source);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ConstructFrom_Rgb24()
    {
        Rgba64 expected = new(5140, 9766, 19532, ushort.MaxValue);
        Rgb24 source = new(20, 38, 76);
        Rgba64 actual = new(source);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ConstructFrom_Bgr24()
    {
        Rgba64 expected = new(5140, 9766, 19532, ushort.MaxValue);
        Bgr24 source = new(20, 38, 76);
        Rgba64 actual = new(source);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ConstructFrom_Vector4()
    {
        Vector4 source = new(0f, 0.2f, 0.5f, 1f);
        Rgba64 expected = Rgba64.FromScaledVector4(source);

        Rgba64 actual = new(source);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ToRgba32_Retval()
    {
        // arrange
        Rgba64 source = new(5140, 9766, 19532, 29555);
        Rgba32 expected = new(20, 38, 76, 115);

        // act
        Rgba32 actual = source.ToRgba32();

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ToBgra32_Retval()
    {
        // arrange
        Rgba64 source = new(5140, 9766, 19532, 29555);
        Bgra32 expected = new(20, 38, 76, 115);

        // act
        Bgra32 actual = source.ToBgra32();

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ToArgb32_Retval()
    {
        // arrange
        Rgba64 source = new(5140, 9766, 19532, 29555);
        Argb32 expected = new(20, 38, 76, 115);

        // act
        Argb32 actual = source.ToArgb32();

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ToAbgr32_Retval()
    {
        // arrange
        Rgba64 source = new(5140, 9766, 19532, 29555);
        Abgr32 expected = new(20, 38, 76, 115);

        // act
        Abgr32 actual = source.ToAbgr32();

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ToRgb24_Retval()
    {
        // arrange
        Rgba64 source = new(5140, 9766, 19532, 29555);
        Rgb24 expected = new(20, 38, 76);

        // act
        Rgb24 actual = source.ToRgb24();

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ToBgr24_Retval()
    {
        // arrange
        Rgba64 source = new(5140, 9766, 19532, 29555);
        Bgr24 expected = new(20, 38, 76);

        // act
        Bgr24 actual = source.ToBgr24();

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rgba64_PixelInformation()
    {
        PixelTypeInfo info = Rgba64.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<Rgba64>() * 8, info.BitsPerPixel);
        Assert.Equal(PixelAlphaRepresentation.Unassociated, info.AlphaRepresentation);
        Assert.Equal(PixelColorType.RGB | PixelColorType.Alpha, info.ColorType);

        PixelComponentInfo componentInfo = info.ComponentInfo.Value;
        Assert.Equal(4, componentInfo.ComponentCount);
        Assert.Equal(0, componentInfo.Padding);
        Assert.Equal(16, componentInfo.GetComponentPrecision(0));
        Assert.Equal(16, componentInfo.GetComponentPrecision(1));
        Assert.Equal(16, componentInfo.GetComponentPrecision(2));
        Assert.Equal(16, componentInfo.GetComponentPrecision(3));
        Assert.Equal(16, componentInfo.GetMaximumComponentPrecision());
    }
}
