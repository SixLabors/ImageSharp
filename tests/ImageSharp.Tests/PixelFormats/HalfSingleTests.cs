// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class HalfSingleTests
{
    [Fact]
    public void HalfSingle_PackedValue()
    {
        Assert.Equal(11878, new HalfSingle(0.1F).PackedValue);
        Assert.Equal(46285, new HalfSingle(-0.3F).PackedValue);

        // Test limits
        Assert.Equal(15360, new HalfSingle(1F).PackedValue);
        Assert.Equal(0, new HalfSingle(0F).PackedValue);
        Assert.Equal(48128, new HalfSingle(-1F).PackedValue);
    }

    [Fact]
    public void HalfSingle_ToVector4()
    {
        // arrange
        HalfSingle pixel = new(-2F);
        Vector4 expected = new(-2F, 0, 0, 1);

        // act
        Vector4 actual = pixel.ToVector4();

        // assert
        Assert.Equal(expected, actual);
        Assert.Equal((float)Half.MinValue, new HalfSingle((float)Half.MinValue).ToSingle());
        Assert.Equal((float)Half.MaxValue, new HalfSingle((float)Half.MaxValue).ToSingle());
    }

    [Fact]
    public void HalfSingle_ToScaledVector4()
    {
        Assert.Equal(new Vector4(0F, 0F, 0F, 1F), new HalfSingle((float)Half.MinValue).ToScaledVector4());
        Assert.Equal(new Vector4(.5F, 0F, 0F, 1F), new HalfSingle(0F).ToScaledVector4());
        Assert.Equal(new Vector4(1F, 0F, 0F, 1F), new HalfSingle((float)Half.MaxValue).ToScaledVector4());
    }

    [Fact]
    public void HalfSingle_FromScaledVector4()
    {
        Assert.Equal((ushort)0xFBFF, HalfSingle.FromScaledVector4(new Vector4(0F, 0F, 0F, 1F)).PackedValue);
        Assert.Equal((ushort)0, HalfSingle.FromScaledVector4(new Vector4(.5F, 0F, 0F, 1F)).PackedValue);
        Assert.Equal((ushort)0x7BFF, HalfSingle.FromScaledVector4(new Vector4(1F, 0F, 0F, 1F)).PackedValue);
    }

    [Fact]
    public void HalfSingle_BulkScaledConversionsCoverFiniteRange() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertHalfSingleBulkScaledConversionsCoverFiniteRange,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    private static void AssertHalfSingleBulkScaledConversionsCoverFiniteRange()
    {
        ushort[] packedValues = [0xFBFF, 0, 0x7BFF];
        Vector4[] scaledValues = [new(0F, 0F, 0F, 1F), new(.5F, 0F, 0F, 1F), new(1F, 0F, 0F, 1F)];
        HalfSingle[] source = new HalfSingle[17];
        Vector4[] expectedVectors = new Vector4[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            int valueIndex = i % packedValues.Length;
            source[i].PackedValue = packedValues[valueIndex];
            expectedVectors[i] = scaledValues[valueIndex];
        }

        Vector4[] actualVectors = new Vector4[source.Length];
        PixelOperations<HalfSingle>.Instance.ToVector4(Configuration.Default, source, actualVectors, PixelConversionModifiers.Scale);
        Assert.Equal(expectedVectors, actualVectors);

        Vector4[] destructiveSource = [.. expectedVectors];
        HalfSingle[] actualPixels = new HalfSingle[source.Length];
        PixelOperations<HalfSingle>.Instance.FromVector4Destructive(Configuration.Default, destructiveSource, actualPixels, PixelConversionModifiers.Scale);
        Assert.Equal(source, actualPixels);
    }

    [Fact]
    public void HalfSingle_PixelInformation()
    {
        PixelTypeInfo info = HalfSingle.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<HalfSingle>() * 8, info.BitsPerPixel);
        Assert.Equal(PixelAlphaRepresentation.None, info.AlphaRepresentation);
        Assert.Equal(PixelColorType.Red, info.ColorType);

        PixelComponentInfo componentInfo = info.ComponentInfo.Value;
        Assert.Equal(1, componentInfo.ComponentCount);
        Assert.Equal(0, componentInfo.Padding);
        Assert.Equal(16, componentInfo.GetComponentPrecision(0));
        Assert.Equal(16, componentInfo.GetMaximumComponentPrecision());
    }
}
