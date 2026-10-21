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
    /// <summary>
    /// Verifies that packed construction preserves finite values, signed zero, infinity, and NaN bits.
    /// </summary>
    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)0x8000)]
    [InlineData((ushort)0x3C00)]
    [InlineData((ushort)0x7C00)]
    [InlineData((ushort)0x7E01)]
    [InlineData(ushort.MaxValue)]
    public void Constructor_FromPackedValue(ushort packed)
    {
        HalfSingle pixel = new(packed);

        Assert.Equal(packed, pixel.PackedValue);
    }

    /// <summary>
    /// Verifies that an integer constant selects packed construction while a float selects numeric conversion.
    /// </summary>
    [Fact]
    public void Constructors_DistinguishPackedBitsFromNumericValue()
    {
        // The integer argument stores bit pattern 1. The float argument stores the half-precision encoding of numeric 1.
        HalfSingle packed = new(1);
        HalfSingle numeric = new(1F);

        Assert.Equal((ushort)0x0001, packed.PackedValue);
        Assert.Equal((ushort)0x3C00, numeric.PackedValue);
    }

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
        Assert.Equal(new Vector4((float)Half.MinValue, 0F, 0F, 1F), new HalfSingle((float)Half.MinValue).ToScaledVector4());
        Assert.Equal(new Vector4(0F, 0F, 0F, 1F), new HalfSingle(0F).ToScaledVector4());
        Assert.Equal(new Vector4((float)Half.MaxValue, 0F, 0F, 1F), new HalfSingle((float)Half.MaxValue).ToScaledVector4());
    }

    [Fact]
    public void HalfSingle_FromScaledVector4()
    {
        Assert.Equal((ushort)0xFBFF, HalfSingle.FromScaledVector4(new Vector4((float)Half.MinValue, 0F, 0F, 1F)).PackedValue);
        Assert.Equal((ushort)0, HalfSingle.FromScaledVector4(new Vector4(0F, 0F, 0F, 1F)).PackedValue);
        Assert.Equal((ushort)0x7BFF, HalfSingle.FromScaledVector4(new Vector4((float)Half.MaxValue, 0F, 0F, 1F)).PackedValue);
    }

    [Fact]
    public void HalfSingle_BulkScaledConversionsPreserveFiniteRange() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertHalfSingleBulkScaledConversionsPreserveFiniteRange,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    private static void AssertHalfSingleBulkScaledConversionsPreserveFiniteRange()
    {
        ushort[] packedValues = [0xFBFF, 0, 0x7BFF];
        Vector4[] scaledValues = [new((float)Half.MinValue, 0F, 0F, 1F), new(0F, 0F, 0F, 1F), new((float)Half.MaxValue, 0F, 0F, 1F)];
        HalfSingle[] source = new HalfSingle[17];
        Vector4[] expectedVectors = new Vector4[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            int valueIndex = i % packedValues.Length;
            source[i].PackedValue = packedValues[valueIndex];
            expectedVectors[i] = scaledValues[valueIndex];
        }

        Vector4[] actualVectors = new Vector4[source.Length];
        PixelOperations<HalfSingle>.Instance.ToScaledVector4(Configuration.Default, source, actualVectors);
        Assert.Equal(expectedVectors, actualVectors);

        Vector4[] destructiveSource = [.. expectedVectors];
        HalfSingle[] actualPixels = new HalfSingle[source.Length];
        PixelOperations<HalfSingle>.Instance.FromScaledVector4Destructive(Configuration.Default, destructiveSource, actualPixels);
        Assert.Equal(source, actualPixels);
    }

    [Fact]
    public void HalfSingle_PixelInformation()
    {
        PixelTypeInfo info = HalfSingle.GetPixelTypeInfo();
        Assert.Equal(Unsafe.SizeOf<HalfSingle>() * 8, info.BitsPerPixel);
        Assert.Equal(PixelAlphaRepresentation.None, info.AlphaRepresentation);
        Assert.Equal(PixelColorType.Red, info.ColorType);
        Assert.Equal(PixelColorRange.Extended, info.ColorRange);

        PixelComponentInfo componentInfo = info.ComponentInfo.Value;
        Assert.Equal(1, componentInfo.ComponentCount);
        Assert.Equal(0, componentInfo.Padding);
        Assert.Equal(16, componentInfo.GetComponentPrecision(0));
        Assert.Equal(16, componentInfo.GetMaximumComponentPrecision());
    }
}
