// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

/// <summary>
/// Tests the <see cref="RgbaVector"/> struct.
/// </summary>
[Trait("Category", "PixelFormats")]
public class RgbaVectorTests
{
    /// <summary>
    /// Tests the equality operators for equality.
    /// </summary>
    [Fact]
    public void AreEqual()
    {
        RgbaVector color1 = new(0, 0, 0F);
        RgbaVector color2 = new(0, 0, 0, 1F);
        RgbaVector color3 = RgbaVector.FromHex("#000");
        RgbaVector color4 = RgbaVector.FromHex("#000F");
        RgbaVector color5 = RgbaVector.FromHex("#000000");
        RgbaVector color6 = RgbaVector.FromHex("#000000FF");

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
        RgbaVector color1 = new(1, 0, 0, 1);
        RgbaVector color2 = new(0, 0, 0, 1);
        RgbaVector color3 = RgbaVector.FromHex("#000");
        RgbaVector color4 = RgbaVector.FromHex("#000000");
        RgbaVector color5 = RgbaVector.FromHex("#FF000000");

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
        RgbaVector color1 = new(1, .1F, .133F, .864F);
        Assert.Equal(1F, color1.R);
        Assert.Equal(.1F, color1.G);
        Assert.Equal(.133F, color1.B);
        Assert.Equal(.864F, color1.A);

        RgbaVector color2 = new(1, .1f, .133f);
        Assert.Equal(1F, color2.R);
        Assert.Equal(.1F, color2.G);
        Assert.Equal(.133F, color2.B);
        Assert.Equal(1F, color2.A);
    }

    /// <summary>
    /// Tests whether FromHex and ToHex work correctly.
    /// </summary>
    [Fact]
    public void FromAndToHex()
    {
        RgbaVector color = RgbaVector.FromHex("#AABBCCDD");
        Assert.Equal(170 / 255F, color.R);
        Assert.Equal(187 / 255F, color.G);
        Assert.Equal(204 / 255F, color.B);
        Assert.Equal(221 / 255F, color.A);

        color.A = 170 / 255F;
        color.B = 187 / 255F;
        color.G = 204 / 255F;
        color.R = 221 / 255F;

        Assert.Equal("DDCCBBAA", color.ToHex());

        color.R = 0;

        Assert.Equal("00CCBBAA", color.ToHex());

        color.A = 255 / 255F;

        Assert.Equal("00CCBBFF", color.ToHex());
    }

    /// <summary>
    /// Tests that the individual float elements are laid out in RGBA order.
    /// </summary>
    [Fact]
    public void FloatLayout()
    {
        RgbaVector color = new(1F, 2, 3, 4);
        Vector4 colorBase = Unsafe.As<RgbaVector, Vector4>(ref Unsafe.Add(ref color, 0));
        float[] ordered = new float[4];
        colorBase.CopyTo(ordered);

        Assert.Equal(1, ordered[0]);
        Assert.Equal(2, ordered[1]);
        Assert.Equal(3, ordered[2]);
        Assert.Equal(4, ordered[3]);
    }

    [Fact]
    public void RgbaVector_FromRgb48()
    {
        // arrange
        Rgb48 expected = new(65535, 0, 65535);

        // act
        RgbaVector input = RgbaVector.FromRgb48(expected);
        Rgb48 actual = Rgb48.FromScaledVector4(input.ToScaledVector4());

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RgbaVector_FromRgba64()
    {
        // arrange
        Rgba64 expected = new(65535, 0, 65535, 0);

        // act
        RgbaVector input = RgbaVector.FromRgba64(expected);
        Rgba64 actual = Rgba64.FromScaledVector4(input.ToScaledVector4());

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RgbaVector_FromBgra5551()
    {
        // arrange
        Vector4 expected = Vector4.One;

        // act
        RgbaVector rgb = RgbaVector.FromBgra5551(new Bgra5551(1.0f, 1.0f, 1.0f, 1.0f));

        // assert
        Assert.Equal(expected, rgb.ToScaledVector4());
    }

    [Fact]
    public void RgbaVector_FromGrey16()
    {
        // arrange
        Vector4 expected = Vector4.One;

        // act
        RgbaVector rgba = RgbaVector.FromL16(new L16(ushort.MaxValue));

        // assert
        Assert.Equal(expected, rgba.ToScaledVector4());
    }

    [Fact]
    public void RgbaVector_FromGrey8()
    {
        // arrange
        Vector4 expected = Vector4.One;

        // act
        RgbaVector rgba = RgbaVector.FromL8(new L8(byte.MaxValue));

        // assert
        Assert.Equal(expected, rgba.ToScaledVector4());
    }

    [Fact]
    public void Issue2048()
    {
        // https://github.com/SixLabors/ImageSharp/issues/2048
        RgbaVector green = Color.Green.ToPixel<RgbaVector>();
        using Image<RgbaVector> source = new(Configuration.Default, 1, 1, green);
        using Image<HalfVector4> clone = source.CloneAs<HalfVector4>();

        Rgba32 srcColor = source[0, 0].ToRgba32();
        Rgba32 cloneColor = clone[0, 0].ToRgba32();

        Assert.Equal(srcColor, cloneColor);
    }

    [Fact]
    public void RgbaVector_PixelInformation()
    {
        PixelTypeInfo info = RgbaVector.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<RgbaVector>() * 8, info.BitsPerPixel);
        Assert.Equal(PixelAlphaRepresentation.Unassociated, info.AlphaRepresentation);
        Assert.Equal(PixelColorType.RGB | PixelColorType.Alpha, info.ColorType);

        PixelComponentInfo componentInfo = info.ComponentInfo.Value;
        Assert.Equal(4, componentInfo.ComponentCount);
        Assert.Equal(0, componentInfo.Padding);
        Assert.Equal(32, componentInfo.GetComponentPrecision(0));
        Assert.Equal(32, componentInfo.GetComponentPrecision(1));
        Assert.Equal(32, componentInfo.GetComponentPrecision(2));
        Assert.Equal(32, componentInfo.GetComponentPrecision(3));
        Assert.Equal(32, componentInfo.GetMaximumComponentPrecision());
    }
}
