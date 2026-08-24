// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class NormalizedByte2Tests
{
    [Fact]
    public void NormalizedByte2_PackedValue()
    {
        Assert.Equal(0xda0d, new NormalizedByte2(0.1f, -0.3f).PackedValue);
        Assert.Equal(0x0, new NormalizedByte2(Vector2.Zero).PackedValue);
        Assert.Equal(0x7F7F, new NormalizedByte2(Vector2.One).PackedValue);
        Assert.Equal(0x8181, new NormalizedByte2(-Vector2.One).PackedValue);
    }

    [Fact]
    public void NormalizedByte2_ToVector2()
    {
        Assert.Equal(Vector2.One, new NormalizedByte2(Vector2.One).ToVector2());
        Assert.Equal(Vector2.Zero, new NormalizedByte2(Vector2.Zero).ToVector2());
        Assert.Equal(-Vector2.One, new NormalizedByte2(-Vector2.One).ToVector2());
        Assert.Equal(Vector2.One, new NormalizedByte2(Vector2.One * 1234.0f).ToVector2());
        Assert.Equal(-Vector2.One, new NormalizedByte2(Vector2.One * -1234.0f).ToVector2());
    }

    [Fact]
    public void NormalizedByte2_MinimumStorageCodeDecodesAsNegativeOne() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertNormalizedByte2MinimumStorageCodeDecodesAsNegativeOne,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    private static void AssertNormalizedByte2MinimumStorageCodeDecodesAsNegativeOne()
    {
        NormalizedByte2 pixel = new() { PackedValue = 0x8080 };
        Vector4 expectedNative = new(-1F, -1F, 0F, 1F);
        Vector4 expectedScaled = new(0F, 0F, 0F, 1F);

        Assert.Equal(expectedNative, pixel.ToVector4());
        Assert.Equal(expectedScaled, pixel.ToScaledVector4());

        NormalizedByte2[] source = new NormalizedByte2[17];
        Vector4[] native = new Vector4[source.Length];
        Vector4[] scaled = new Vector4[source.Length];
        Array.Fill(source, pixel);

        PixelOperations<NormalizedByte2>.Instance.ToVector4(Configuration.Default, source, native);
        PixelOperations<NormalizedByte2>.Instance.ToVector4(Configuration.Default, source, scaled, PixelConversionModifiers.Scale);

        for (int i = 0; i < source.Length; i++)
        {
            Assert.Equal(expectedNative, native[i]);
            Assert.Equal(expectedScaled, scaled[i]);
        }

        Vector4[] destructiveSource = new Vector4[source.Length];
        Array.Fill(destructiveSource, expectedScaled);
        NormalizedByte2[] actualPixels = new NormalizedByte2[source.Length];
        PixelOperations<NormalizedByte2>.Instance.FromVector4Destructive(Configuration.Default, destructiveSource, actualPixels, PixelConversionModifiers.Scale);
        Assert.All(actualPixels, actual => Assert.Equal((ushort)0x8181, actual.PackedValue));
    }

    [Fact]
    public void NormalizedByte2_ToVector4()
    {
        Assert.Equal(new Vector4(1, 1, 0, 1), new NormalizedByte2(Vector2.One).ToVector4());
        Assert.Equal(new Vector4(0, 0, 0, 1), new NormalizedByte2(Vector2.Zero).ToVector4());
    }

    [Fact]
    public void NormalizedByte2_ToScaledVector4()
    {
        // arrange
        NormalizedByte2 pixel = new(-Vector2.One);

        // act
        Vector4 actual = pixel.ToScaledVector4();

        // assert
        Assert.Equal(0, actual.X);
        Assert.Equal(0, actual.Y);
        Assert.Equal(0, actual.Z);
        Assert.Equal(1F, actual.W);
    }

    [Fact]
    public void NormalizedByte2_FromScaledVector4()
    {
        // arrange
        Vector4 scaled = new NormalizedByte2(-Vector2.One).ToScaledVector4();
        const uint expected = 0x8181;

        // act
        NormalizedByte2 pixel = NormalizedByte2.FromScaledVector4(scaled);
        uint actual = pixel.PackedValue;

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NormalizedByte2_FromBgra5551()
    {
        // arrange
        Vector4 expected = new(1, 1, 0, 1);

        // act
        NormalizedByte2 pixel = NormalizedByte2.FromBgra5551(new Bgra5551(1f, 1f, 1f, 1f));

        // assert
        Assert.Equal(expected, pixel.ToVector4());
    }

    [Fact]
    public void NormalizedByte2_PixelInformation()
    {
        PixelTypeInfo info = NormalizedByte2.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<NormalizedByte2>() * 8, info.BitsPerPixel);
        Assert.Equal(PixelAlphaRepresentation.None, info.AlphaRepresentation);
        Assert.Equal(PixelColorType.Red | PixelColorType.Green, info.ColorType);

        PixelComponentInfo componentInfo = info.ComponentInfo.Value;
        Assert.Equal(2, componentInfo.ComponentCount);
        Assert.Equal(0, componentInfo.Padding);
        Assert.Equal(8, componentInfo.GetComponentPrecision(0));
        Assert.Equal(8, componentInfo.GetComponentPrecision(1));
        Assert.Equal(8, componentInfo.GetMaximumComponentPrecision());
    }
}
