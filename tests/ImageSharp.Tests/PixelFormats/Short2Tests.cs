// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class Short2Tests
{
    [Fact]
    public void Short2_PackedValues()
    {
        // Test ordering
        Assert.Equal(0x361d2db1U, new Short2(0x2db1, 0x361d).PackedValue);
        Assert.Equal(4294639744, new Short2(127.5f, -5.3f).PackedValue);

        // Test the limits.
        Assert.Equal(0x0U, new Short2(Vector2.Zero).PackedValue);
        Assert.Equal(0x7FFF7FFFU, new Short2(Vector2.One * 0x7FFF).PackedValue);
        Assert.Equal(0x80008000, new Short2(Vector2.One * -0x8000).PackedValue);
    }

    [Fact]
    public void Short2_ToVector2()
    {
        Assert.Equal(Vector2.One * 0x7FFF, new Short2(Vector2.One * 0x7FFF).ToVector2());
        Assert.Equal(Vector2.Zero, new Short2(Vector2.Zero).ToVector2());
        Assert.Equal(Vector2.One * -0x8000, new Short2(Vector2.One * -0x8000).ToVector2());
        Assert.Equal(Vector2.UnitX * 0x7FFF, new Short2(Vector2.UnitX * 0x7FFF).ToVector2());
        Assert.Equal(Vector2.UnitY * 0x7FFF, new Short2(Vector2.UnitY * 0x7FFF).ToVector2());
    }

    [Fact]
    public void Short2_ToVector4()
    {
        Assert.Equal(new Vector4(0x7FFF, 0x7FFF, 0, 1), new Short2(Vector2.One * 0x7FFF).ToVector4());
        Assert.Equal(new Vector4(0, 0, 0, 1), new Short2(Vector2.Zero).ToVector4());
        Assert.Equal(new Vector4(-0x8000, -0x8000, 0, 1), new Short2(Vector2.One * -0x8000).ToVector4());
    }

    [Fact]
    public void Short2_Clamping()
    {
        Assert.Equal(Vector2.One * 0x7FFF, new Short2(Vector2.One * 1234567.0f).ToVector2());
        Assert.Equal(Vector2.One * -0x8000, new Short2(Vector2.One * -1234567.0f).ToVector2());
    }

    [Fact]
    public void Short2_ToScaledVector4()
    {
        Assert.Equal(new Vector4(1F, 1F, 0F, 1F), new Short2(Vector2.One * short.MaxValue).ToScaledVector4());
        Assert.Equal(new Vector4(0F, 0F, 0F, 1F), new Short2(Vector2.One * short.MinValue).ToScaledVector4());
    }

    [Fact]
    public void Short2_FromScaledVector4()
    {
        Assert.Equal(0x80008000U, Short2.FromScaledVector4(new Vector4(0F, 0F, 0F, 1F)).PackedValue);
        Assert.Equal(0x7FFF7FFFU, Short2.FromScaledVector4(Vector4.One).PackedValue);
    }

    [Fact]
    public void Short2_BulkScaledConversionsCoverFullSignedRange() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertShort2BulkScaledConversionsCoverFullSignedRange,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    private static void AssertShort2BulkScaledConversionsCoverFullSignedRange()
    {
        const int length = 17;
        Short2[] source = new Short2[length];
        Vector4[] expectedVectors = new Vector4[length];

        for (int i = 0; i < length; i++)
        {
            bool minimum = (i & 1) == 0;
            source[i].PackedValue = minimum ? 0x80008000U : 0x7FFF7FFFU;
            expectedVectors[i] = minimum ? new Vector4(0F, 0F, 0F, 1F) : new Vector4(1F, 1F, 0F, 1F);
        }

        Vector4[] actualVectors = new Vector4[length];
        PixelOperations<Short2>.Instance.ToVector4(Configuration.Default, source, actualVectors, PixelConversionModifiers.Scale);
        Assert.Equal(expectedVectors, actualVectors);

        Vector4[] destructiveSource = [.. expectedVectors];
        Short2[] actualPixels = new Short2[length];
        PixelOperations<Short2>.Instance.FromVector4Destructive(Configuration.Default, destructiveSource, actualPixels, PixelConversionModifiers.Scale);
        Assert.Equal(source, actualPixels);
    }

    [Fact]
    public void Short2_ToRgba32()
    {
        // arrange
        Short2 short2 = new(127.5f, -5.3f);
        Rgba32 expected = new(128, 127, 0, 255);

        // act
        Rgba32 actual = short2.ToRgba32();

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Short2_FromRgba32_ToRgba32()
    {
        // arrange
        Rgba32 expected = new(20, 38, 0, 255);

        // act
        Short2 short2 = Short2.FromRgba32(expected);
        Rgba32 actual = short2.ToRgba32();

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Short2_FromRgb48()
    {
        // arrange
        Rgb48 expected = new(65535, 65535, 0);

        // act
        Short2 input = Short2.FromRgb48(expected);
        Rgb48 actual = Rgb48.FromScaledVector4(input.ToScaledVector4());

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Short2_FromRgba64()
    {
        // arrange
        Rgba64 expected = new(65535, 65535, 0, 65535);

        // act
        Short2 input = Short2.FromRgba64(expected);
        Rgba64 actual = Rgba64.FromScaledVector4(input.ToScaledVector4());

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Short2_FromBgra5551()
    {
        // act
        Short2 short2 = Short2.FromBgra5551(new Bgra5551(1.0f, 1.0f, 1.0f, 1.0f));

        // assert
        Vector4 actual = short2.ToScaledVector4();
        Assert.Equal(1, actual.X);
        Assert.Equal(1, actual.Y);
        Assert.Equal(0, actual.Z);
        Assert.Equal(1, actual.W);
    }

    [Fact]
    public void Short2_PixelInformation()
    {
        PixelTypeInfo info = Short2.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<Short2>() * 8, info.BitsPerPixel);
        Assert.Equal(PixelAlphaRepresentation.None, info.AlphaRepresentation);
        Assert.Equal(PixelColorType.Red | PixelColorType.Green, info.ColorType);

        PixelComponentInfo componentInfo = info.ComponentInfo.Value;
        Assert.Equal(2, componentInfo.ComponentCount);
        Assert.Equal(0, componentInfo.Padding);
        Assert.Equal(16, componentInfo.GetComponentPrecision(0));
        Assert.Equal(16, componentInfo.GetComponentPrecision(1));
        Assert.Equal(16, componentInfo.GetMaximumComponentPrecision());
    }
}
