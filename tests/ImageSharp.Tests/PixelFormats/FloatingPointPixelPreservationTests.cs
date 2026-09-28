// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class FloatingPointPixelPreservationTests
{
    /// <summary>
    /// The four half-precision pixel types keep the same DirectX component order and bit layout.
    /// </summary>
    [Fact]
    public void FourComponentHalfPixelsKeepDirectXLayout()
    {
        Vector4 components = new(1F, 2F, -0F, -1F);
        const ulong expected = 0xBC00800040003C00UL;

        Assert.Equal(expected, new HalfVector4(components).PackedValue);
        Assert.Equal(expected, new HalfVector4P(components).PackedValue);
        Assert.Equal(expected, new RgbaHalf(components).PackedValue);
        Assert.Equal(expected, new RgbaHalfP(components).PackedValue);
    }

    /// <summary>
    /// Associated binary32 pixels keep the DirectX component order and signed zero.
    /// </summary>
    [Fact]
    public void AssociatedFloatPixelsKeepDirectXLayout()
    {
        RgbaVectorP[] pixels = [new(1F, 2F, -0F, -1F)];
        ReadOnlySpan<float> components = MemoryMarshal.Cast<RgbaVectorP, float>(pixels);

        Assert.Equal(4, components.Length);
        Assert.Equal(BitConverter.SingleToInt32Bits(1F), BitConverter.SingleToInt32Bits(components[0]));
        Assert.Equal(BitConverter.SingleToInt32Bits(2F), BitConverter.SingleToInt32Bits(components[1]));
        Assert.Equal(BitConverter.SingleToInt32Bits(-0F), BitConverter.SingleToInt32Bits(components[2]));
        Assert.Equal(BitConverter.SingleToInt32Bits(-1F), BitConverter.SingleToInt32Bits(components[3]));
    }

    /// <summary>
    /// Floating-point formats preserve finite component values outside the unit interval.
    /// </summary>
    [Fact]
    public void ScaledFloatStoragePreservesOutOfRangeComponents()
    {
        Vector4 source = new(2F, -1F, .5F, 1F);

        Assert.Equal(new Vector4(2F, 0F, 0F, 1F), HalfSingle.FromUnassociatedScaledVector4(source).ToUnassociatedScaledVector4());
        Assert.Equal(new Vector4(2F, -1F, 0F, 1F), HalfVector2.FromUnassociatedScaledVector4(source).ToUnassociatedScaledVector4());
        Assert.Equal(source, HalfVector4.FromUnassociatedScaledVector4(source).ToUnassociatedScaledVector4());
        Assert.Equal(source, RgbaHalf.FromUnassociatedScaledVector4(source).ToUnassociatedScaledVector4());
        Assert.Equal(source, RgbaVector.FromUnassociatedScaledVector4(source).ToUnassociatedScaledVector4());
        Assert.Equal(source, HalfVector4P.FromUnassociatedScaledVector4(source).ToUnassociatedScaledVector4());
        Assert.Equal(source, RgbaHalfP.FromUnassociatedScaledVector4(source).ToUnassociatedScaledVector4());
        Assert.Equal(source, RgbaVectorP.FromUnassociatedScaledVector4(source).ToUnassociatedScaledVector4());
    }

    /// <summary>
    /// Four-component floating-point formats do not bound alpha to the unit interval.
    /// </summary>
    [Fact]
    public void ScaledFloatStoragePreservesOutOfRangeAlpha()
    {
        Vector4 source = new(2F, -1F, .5F, 2F);

        Assert.Equal(source, HalfVector4.FromUnassociatedScaledVector4(source).ToUnassociatedScaledVector4());
        Assert.Equal(source, RgbaHalf.FromUnassociatedScaledVector4(source).ToUnassociatedScaledVector4());
        Assert.Equal(source, RgbaVector.FromUnassociatedScaledVector4(source).ToUnassociatedScaledVector4());
        Assert.Equal(source, HalfVector4P.FromAssociatedScaledVector4(source).ToAssociatedScaledVector4());
        Assert.Equal(source, RgbaHalfP.FromAssociatedScaledVector4(source).ToAssociatedScaledVector4());
        Assert.Equal(source, RgbaVectorP.FromAssociatedScaledVector4(source).ToAssociatedScaledVector4());
    }

    /// <summary>
    /// Bounded destinations saturate finite floating-point values outside the unit interval.
    /// </summary>
    [Fact]
    public void FloatToBoundedBulkConversionSaturatesOutOfRangeComponents()
    {
        Rgba32[] expectedSingle32 = [new(255, 0, 0, 255), new(0, 0, 0, 255), new(0, 0, 0, 255), new(128, 0, 0, 255), new(255, 0, 0, 255)];
        Rgba64[] expectedSingle64 = [new(65535, 0, 0, 65535), new(0, 0, 0, 65535), new(0, 0, 0, 65535), new(32768, 0, 0, 65535), new(65535, 0, 0, 65535)];
        Rgba32[] expectedTwo32 = [new(255, 0, 0, 255), new(0, 255, 0, 255), new(0, 191, 0, 255), new(128, 0, 0, 255), new(255, 0, 0, 255)];
        Rgba64[] expectedTwo64 = [new(65535, 0, 0, 65535), new(0, 65535, 0, 65535), new(0, 49151, 0, 65535), new(32768, 0, 0, 65535), new(65535, 0, 0, 65535)];
        Rgba32[] expectedFour32 = [new(255, 0, 128, 255), new(0, 255, 64, 255), new(0, 191, 255, 255), new(128, 0, 255, 128), new(255, 0, 191, 64)];
        Rgba64[] expectedFour64 = [new(65535, 0, 32768, 65535), new(0, 65535, 16384, 65535), new(0, 49151, 65535, 65535), new(32768, 0, 65535, 32768), new(65535, 0, 49151, 16384)];

        AssertBoundedConversion<HalfSingle>(expectedSingle32, expectedSingle64);
        AssertBoundedConversion<HalfVector2>(expectedTwo32, expectedTwo64);
        AssertBoundedConversion<HalfVector4>(expectedFour32, expectedFour64);
        AssertBoundedConversion<RgbaHalf>(expectedFour32, expectedFour64);
        AssertBoundedConversion<RgbaVector>(expectedFour32, expectedFour64);
        AssertBoundedConversion<HalfVector4P>(expectedFour32, expectedFour64);
        AssertBoundedConversion<RgbaHalfP>(expectedFour32, expectedFour64);
        AssertBoundedConversion<RgbaVectorP>(expectedFour32, expectedFour64);
    }

    /// <summary>
    /// Bounded pixel formats saturate nonfinite components in both scalar and bulk conversions.
    /// </summary>
    [Fact]
    public void BoundedPixelFormatsSaturateNonfiniteComponents()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertBoundedPixelFormatsSaturateNonfiniteComponents,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks each bounded color or vector destination through its scalar and bulk entry points.
    /// </summary>
    private static void AssertBoundedPixelFormatsSaturateNonfiniteComponents()
    {
        Vector4 input = new(float.NaN, float.PositiveInfinity, float.NegativeInfinity, 1F);
        RgbaVector[] source = new RgbaVector[65];
        Array.Fill(source, RgbaVector.FromUnassociatedScaledVector4(input));

        AssertBoundedSaturation<Abgr32>(input, source);
        AssertBoundedSaturation<Abgr32P>(input, source);
        AssertBoundedSaturation<Argb32>(input, source);
        AssertBoundedSaturation<Argb32P>(input, source);
        AssertBoundedSaturation<Bgr24>(input, source);
        AssertBoundedSaturation<Bgr565>(input, source);
        AssertBoundedSaturation<Bgra32>(input, source);
        AssertBoundedSaturation<Bgra32P>(input, source);
        AssertBoundedSaturation<Bgra4444>(input, source);
        AssertBoundedSaturation<Bgra5551>(input, source);
        AssertBoundedSaturation<Rg32>(input, source);
        AssertBoundedSaturation<Rgb24>(input, source);
        AssertBoundedSaturation<Rgb48>(input, source);
        AssertBoundedSaturation<Rgb96>(input, source);
        AssertBoundedSaturation<Rgba1010102>(input, source);
        AssertBoundedSaturation<Rgba32>(input, source);
        AssertBoundedSaturation<Rgba32P>(input, source);
        AssertBoundedSaturation<Rgba64>(input, source);
        AssertBoundedSaturation<Rgba128>(input, source);
        AssertBoundedSaturation<Byte4>(input, source);
        AssertBoundedSaturation<Short2>(input, source);
        AssertBoundedSaturation<Short4>(input, source);
        AssertBoundedSaturation<NormalizedByte2>(input, source);
        AssertBoundedSaturation<NormalizedByte4>(input, source);
        AssertBoundedSaturation<NormalizedByte4P>(input, source);
        AssertBoundedSaturation<NormalizedShort2>(input, source);
        AssertBoundedSaturation<NormalizedShort4>(input, source);

        // An all-NaN color input isolates the lower endpoint of luminance and alpha-only storage.
        Vector4 monochromeInput = new(float.NaN, float.NaN, float.NaN, float.PositiveInfinity);
        Array.Fill(source, RgbaVector.FromUnassociatedScaledVector4(monochromeInput));

        AssertBoundedSaturation<A8>(monochromeInput, source, Vector4.UnitW);
        AssertBoundedSaturation<L8>(monochromeInput, source, Vector4.UnitW);
        AssertBoundedSaturation<L16>(monochromeInput, source, Vector4.UnitW);
        AssertBoundedSaturation<La16>(monochromeInput, source, Vector4.UnitW);
        AssertBoundedSaturation<La32>(monochromeInput, source, Vector4.UnitW);
    }

    /// <summary>
    /// Checks a destination's scalar storage and its generic bulk conversion against fixed saturation endpoints.
    /// </summary>
    /// <typeparam name="TPixel">The bounded destination pixel format.</typeparam>
    /// <param name="input">The input components.</param>
    /// <param name="source">The floating-point source pixels.</param>
    private static void AssertBoundedSaturation<TPixel>(Vector4 input, ReadOnlySpan<RgbaVector> source)
        where TPixel : unmanaged, IPixel<TPixel>
        => AssertBoundedSaturation<TPixel>(input, source, new Vector4(0F, 1F, 0F, 1F));

    /// <summary>
    /// Checks a destination's scalar storage and generic bulk conversion against specified saturation endpoints.
    /// </summary>
    /// <typeparam name="TPixel">The bounded destination pixel format.</typeparam>
    /// <param name="input">The input components.</param>
    /// <param name="source">The floating-point source pixels.</param>
    /// <param name="expected">The expected scaled components.</param>
    private static void AssertBoundedSaturation<TPixel>(Vector4 input, ReadOnlySpan<RgbaVector> source, Vector4 expected)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Assert.Equal(expected, TPixel.FromUnassociatedScaledVector4(input).ToUnassociatedScaledVector4());

        TPixel[] destination = new TPixel[source.Length];
        PixelOperations<TPixel>.Instance.From(Configuration.Default, source, destination);

        foreach (TPixel pixel in destination)
        {
            Assert.Equal(expected, pixel.ToUnassociatedScaledVector4());
        }
    }

    /// <summary>
    /// Generic conversion normalizes bounded sources but keeps floating-point values until bounded storage.
    /// </summary>
    [Fact]
    public void SinglePixelConversionAppliesScaleAtEachPixelFormat()
    {
        Byte4[] byteSource = [new Byte4(128F, 0F, 255F, 255F)];
        RgbaVector[] floatFromByte = new RgbaVector[1];
        HalfVector4[] halfSource = [new HalfVector4(0F, 0F, 0F, 1F)];
        RgbaVector[] floatFromHalf = new RgbaVector[1];
        RgbaVector[] hdrSource = [RgbaVector.FromUnassociatedScaledVector4(new Vector4(2.5F, .4F, 0F, 1F))];
        RgbaHalf[] halfFromFloat = new RgbaHalf[1];
        Rgba32[] boundedFromFloat = new Rgba32[1];

        PixelOperations<RgbaVector>.Instance.From(Configuration.Default, byteSource, floatFromByte);
        PixelOperations<RgbaVector>.Instance.From(Configuration.Default, halfSource, floatFromHalf);
        PixelOperations<RgbaHalf>.Instance.From(Configuration.Default, hdrSource, halfFromFloat);
        PixelOperations<Rgba32>.Instance.From(Configuration.Default, hdrSource, boundedFromFloat);

        Assert.Equal(128F / 255F, floatFromByte[0].ToScaledVector4().X);
        Assert.Equal(0F, floatFromHalf[0].ToScaledVector4().X);
        Assert.Equal(2.5F, halfFromFloat[0].ToScaledVector4().X);
        Assert.Equal(new Rgba32(255, 102, 0, 255), boundedFromFloat[0]);
    }

    /// <summary>
    /// Bulk conversion changes alpha representation once and preserves out-of-range color values.
    /// </summary>
    [Fact]
    public void BulkConversionChangesAlphaRepresentationOnce()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertBulkConversionChangesAlphaRepresentationOnce,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks the association result and its inverse using distinct values in adjacent pixels.
    /// </summary>
    private static void AssertBulkConversionChangesAlphaRepresentationOnce()
    {
        Vector4[] inputs =
        [
            new(2F, -.5F, .25F, .5F),
            new(0F, 3F, -1F, .25F),
            new(-4F, 0F, .5F, 1F),
            new(1F, 2F, 3F, 2F),
            new(5F, -2F, 1F, .125F)
        ];

        Vector4[] expectedAssociated =
        [
            new(1F, -.25F, .125F, .5F),
            new(0F, .75F, -.25F, .25F),
            new(-4F, 0F, .5F, 1F),
            new(2F, 4F, 6F, 2F),
            new(.625F, -.25F, .125F, .125F)
        ];

        RgbaVector[] straight = new RgbaVector[17];
        RgbaHalfP[] associated = new RgbaHalfP[straight.Length];
        RgbaVector[] restored = new RgbaVector[straight.Length];

        for (int i = 0; i < straight.Length; i++)
        {
            straight[i] = RgbaVector.FromUnassociatedScaledVector4(inputs[i % inputs.Length]);
        }

        PixelOperations<RgbaHalfP>.Instance.From(Configuration.Default, straight, associated);
        PixelOperations<RgbaVector>.Instance.From(Configuration.Default, associated, restored);

        for (int i = 0; i < straight.Length; i++)
        {
            Assert.Equal(expectedAssociated[i % expectedAssociated.Length], associated[i].ToAssociatedScaledVector4());
            Assert.Equal(inputs[i % inputs.Length], restored[i].ToUnassociatedScaledVector4());
        }
    }

    /// <summary>
    /// Matching associated representations keep stored color even when alpha is zero.
    /// Straight input is multiplied by zero when stored as associated color.
    /// </summary>
    [Fact]
    public void BulkConversionAtZeroAlphaFollowsSourceRepresentation()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertBulkConversionAtZeroAlphaFollowsSourceRepresentation,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks both alpha-zero rules with different stored colors in adjacent pixels.
    /// </summary>
    private static void AssertBulkConversionAtZeroAlphaFollowsSourceRepresentation()
    {
        Vector4[] inputs =
        [
            new(2F, -1F, .5F, 0F),
            new(-3F, 4F, .25F, 0F),
            new(.125F, .375F, .75F, 0F),
            new(0F, 1F, 2F, 0F),
            new(1F, 0F, -.5F, 0F)
        ];

        RgbaHalfP[] associatedSource = new RgbaHalfP[17];
        HalfVector4P[] associatedDestination = new HalfVector4P[associatedSource.Length];
        RgbaVector[] straightSource = new RgbaVector[associatedSource.Length];
        RgbaHalfP[] multipliedDestination = new RgbaHalfP[associatedSource.Length];

        for (int i = 0; i < associatedSource.Length; i++)
        {
            Vector4 value = inputs[i % inputs.Length];
            associatedSource[i] = RgbaHalfP.FromAssociatedScaledVector4(value);
            straightSource[i] = RgbaVector.FromUnassociatedScaledVector4(value);
        }

        PixelOperations<HalfVector4P>.Instance.From(Configuration.Default, associatedSource, associatedDestination);
        PixelOperations<RgbaHalfP>.Instance.From(Configuration.Default, straightSource, multipliedDestination);

        for (int i = 0; i < associatedSource.Length; i++)
        {
            Assert.Equal(inputs[i % inputs.Length], associatedDestination[i].ToAssociatedScaledVector4());
            Assert.Equal(Vector4.Zero, multipliedDestination[i].ToAssociatedScaledVector4());
        }
    }

    /// <summary>
    /// Bounded storage applies its existing nonfinite rules to floating-point input.
    /// </summary>
    [Fact]
    public void FloatToBoundedSpanConversionHandlesNonfiniteComponents()
    {
        Vector4[] inputs =
        [
            new(float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.PositiveInfinity),
            new(float.PositiveInfinity, float.NaN, float.NegativeInfinity, 1F),
            new(float.NegativeInfinity, float.PositiveInfinity, float.NaN, 0F),
            new(2F, -1F, .5F, 1F),
            new(.25F, .75F, 0F, .5F)
        ];

        Rgba32[] expected32 = [new(0, 255, 0, 255), new(255, 0, 0, 255), new(0, 255, 0, 0), new(255, 0, 128, 255), new(64, 191, 0, 128)];
        Rgba64[] expected64 = [new(0, 65535, 0, 65535), new(65535, 0, 0, 65535), new(0, 65535, 0, 0), new(65535, 0, 32768, 65535), new(16384, 49151, 0, 32768)];
        RgbaVector[] source = new RgbaVector[17];
        Rgba32[] destination32 = new Rgba32[source.Length];
        Rgba64[] destination64 = new Rgba64[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            source[i] = RgbaVector.FromUnassociatedScaledVector4(inputs[i % inputs.Length]);
        }

        PixelOperations<Rgba32>.Instance.From(Configuration.Default, source, destination32);
        PixelOperations<Rgba64>.Instance.From(Configuration.Default, source, destination64);

        for (int i = 0; i < source.Length; i++)
        {
            Assert.Equal(expected32[i % expected32.Length], destination32[i]);
            Assert.Equal(expected64[i % expected64.Length], destination64[i]);
        }
    }

    /// <summary>
    /// Float-to-float bulk conversion keeps IEEE special values and the sign of zero.
    /// </summary>
    [Fact]
    public void FloatToHalfBulkConversionPreservesSpecialValues()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertFloatToHalfBulkConversionPreservesSpecialValues,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks special-value conversion at each supported instruction width.
    /// </summary>
    private static void AssertFloatToHalfBulkConversionPreservesSpecialValues()
    {
        Vector4[] inputs =
        [
            new(-0F, float.PositiveInfinity, float.NegativeInfinity, float.NaN),
            new(2.5F, -1F, .5F, 1F),
            new(0F, 65504F, -65504F, 0F),
            new(.25F, .75F, 1.5F, 2F),
            new(float.PositiveInfinity, float.NegativeInfinity, float.NaN, -0F)
        ];

        RgbaVector[] source = new RgbaVector[17];
        HalfVector4[] destination = new HalfVector4[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            source[i] = RgbaVector.FromUnassociatedScaledVector4(inputs[i % inputs.Length]);
        }

        PixelOperations<HalfVector4>.Instance.From(Configuration.Default, source, destination);

        for (int i = 0; i < destination.Length; i++)
        {
            ulong packed = destination[i].PackedValue;

            // NaN payload bits are not fixed; mask only that component and check its classification.
            switch (i % inputs.Length)
            {
                case 0:
                    Assert.Equal(0x0000FC007C008000UL, packed & 0x0000FFFFFFFFFFFFUL);
                    Assert.True(float.IsNaN(destination[i].ToVector4().W));
                    break;
                case 1:
                    Assert.Equal(0x3C003800BC004100UL, packed);
                    break;
                case 2:
                    Assert.Equal(0x0000FBFF7BFF0000UL, packed);
                    break;
                case 3:
                    Assert.Equal(0x40003E003A003400UL, packed);
                    break;
                default:
                    Assert.Equal(0x80000000FC007C00UL, packed & 0xFFFF0000FFFFFFFFUL);
                    Assert.True(float.IsNaN(destination[i].ToVector4().Z));
                    break;
            }
        }
    }

    /// <summary>
    /// Scalar and SIMD half-precision packing use nearest-even rounding, overflow to infinity, and signed zero.
    /// </summary>
    [Fact]
    public void HalfVector4BulkPackingMatchesBinary16BoundaryBits()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertHalfVector4BulkPackingMatchesBinary16BoundaryBits,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks half-precision boundary bits at each supported instruction width.
    /// </summary>
    private static void AssertHalfVector4BulkPackingMatchesBinary16BoundaryBits()
    {
        Vector4[] inputs =
        [
            new(1.00048828125F, 65504F, 65520F, -0F),
            new(-1.00048828125F, -65504F, -65520F, 0F),
            new(.5F, -.5F, 2F, -2F),
            new(0F, 1F, -1F, 4F),
            new(.25F, .75F, 1.5F, -4F)
        ];

        ulong[] expectedBits =
        [
            0x80007C007BFF3C00,
            0x0000FC00FBFFBC00,
            0xC0004000B8003800,
            0x4400BC003C000000,
            0xC4003E003A003400
        ];

        Vector4[] source = new Vector4[17];
        HalfVector4[] actual = new HalfVector4[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            source[i] = inputs[i % inputs.Length];
        }

        PixelOperations<HalfVector4>.Instance.FromVector4Destructive(Configuration.Default, source, actual, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);

        for (int i = 0; i < actual.Length; i++)
        {
            Assert.Equal(expectedBits[i % expectedBits.Length], actual[i].PackedValue);
        }
    }

    /// <summary>
    /// HalfSingle preserves scaled input identically in scalar and bulk conversions.
    /// </summary>
    [Fact]
    public void HalfSingle_ScaledInputIsPreserved() => AssertScaledInputIsPreserved<HalfSingle>();

    /// <summary>
    /// HalfVector2 preserves scaled input identically in scalar and bulk conversions.
    /// </summary>
    [Fact]
    public void HalfVector2_ScaledInputIsPreserved() => AssertScaledInputIsPreserved<HalfVector2>();

    /// <summary>
    /// HalfVector4 preserves scaled input identically in scalar and bulk conversions.
    /// </summary>
    [Fact]
    public void HalfVector4_ScaledInputIsPreserved() => AssertScaledInputIsPreserved<HalfVector4>();

    /// <summary>
    /// HalfVector4P preserves scaled input identically in scalar and bulk conversions.
    /// </summary>
    [Fact]
    public void HalfVector4P_ScaledInputIsPreserved() => AssertScaledInputIsPreserved<HalfVector4P>();

    /// <summary>
    /// RgbaVector preserves scaled input identically in scalar and bulk conversions.
    /// </summary>
    [Fact]
    public void RgbaVector_ScaledInputIsPreserved() => AssertScaledInputIsPreserved<RgbaVector>();

    /// <summary>
    /// RgbaHalf preserves scaled input identically in scalar and bulk conversions.
    /// </summary>
    [Fact]
    public void RgbaHalf_ScaledInputIsPreserved() => AssertScaledInputIsPreserved<RgbaHalf>();

    /// <summary>
    /// RgbaHalfP preserves scaled input identically in scalar and bulk conversions.
    /// </summary>
    [Fact]
    public void RgbaHalfP_ScaledInputIsPreserved() => AssertScaledInputIsPreserved<RgbaHalfP>();

    /// <summary>
    /// Associated binary32 pixels preserve unassociated scaled input in scalar and bulk conversions.
    /// </summary>
    [Fact]
    public void RgbaVectorP_ScaledInputIsPreserved() => AssertScaledInputIsPreserved<RgbaVectorP>();

    /// <summary>
    /// Raw half storage and scaled vectors preserve IEEE special values.
    /// </summary>
    [Fact]
    public void HalfVector4_ScaledOutputPreservesSpecialValues() => AssertScaledOutputPreservesSpecialValues();

    /// <summary>
    /// Associated half-vector conversion preserves the stored component values.
    /// </summary>
    [Fact]
    public void HalfVector4P_AssociatedScaledInputIsPreserved() => AssertAssociatedScaledInputIsPreserved<HalfVector4P>();

    /// <summary>
    /// Associated half-RGBA conversion preserves the stored component values.
    /// </summary>
    [Fact]
    public void RgbaHalfP_AssociatedScaledInputIsPreserved() => AssertAssociatedScaledInputIsPreserved<RgbaHalfP>();

    /// <summary>
    /// Associated binary32 pixels preserve stored values in scalar and bulk conversions.
    /// </summary>
    [Fact]
    public void RgbaVectorP_AssociatedScaledInputIsPreserved() => AssertAssociatedScaledInputIsPreserved<RgbaVectorP>();

    /// <summary>
    /// Checks that the scaled modifier does not change finite floating-point input.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    private static void AssertScaledInputIsPreserved<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Vector4[] inputs =
        [
            new(2.5F, -2F, .5F, 1F),
            new(.25F, .5F, .75F, .5F),
            new(-3F, 4F, .25F, 0F),
            new(65504F, -65504F, 0F, 1F)
        ];

        // Seventeen pixels exercise wide registers and the narrower remainder paths.
        Vector4[] source = new Vector4[17];
        TPixel[] expected = new TPixel[source.Length];
        TPixel[] actual = new TPixel[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            int sample = i % inputs.Length;
            source[i] = inputs[sample];
            expected[i] = TPixel.FromUnassociatedVector4(source[i]);
            Assert.Equal(expected[i], TPixel.FromUnassociatedScaledVector4(source[i]));
        }

        // Associated formats otherwise interpret the vectors using their native alpha representation.
        PixelOperations<TPixel>.Instance.FromVector4Destructive(Configuration.Default, source, actual, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Checks the bounded result through the generic bulk conversion entry points.
    /// </summary>
    /// <typeparam name="TPixel">The floating-point source pixel format.</typeparam>
    /// <param name="expected32">The expected eight-bit pixels for each input value.</param>
    /// <param name="expected64">The expected sixteen-bit pixels for each input value.</param>
    private static void AssertBoundedConversion<TPixel>(Rgba32[] expected32, Rgba64[] expected64)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Vector4[] inputs =
        [
            new(2F, -1F, .5F, 1F),
            new(-3F, 3F, .25F, 2F),
            new(0F, .75F, 1.5F, 1F),
            new(.5F, 0F, 1F, .5F),
            new(1F, 0F, .75F, .25F)
        ];

        TPixel[] source = new TPixel[17];
        Rgba32[] destination32 = new Rgba32[source.Length];
        Rgba64[] destination64 = new Rgba64[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            // Five samples shift between four-, eight-, and sixteen-pixel blocks.
            source[i] = TPixel.FromUnassociatedScaledVector4(inputs[i % inputs.Length]);
        }

        PixelOperations<Rgba32>.Instance.From(Configuration.Default, source, destination32);
        PixelOperations<Rgba64>.Instance.From(Configuration.Default, source, destination64);

        for (int i = 0; i < source.Length; i++)
        {
            Assert.Equal(expected32[i % expected32.Length], destination32[i]);
            Assert.Equal(expected64[i % expected64.Length], destination64[i]);
        }
    }

    /// <summary>
    /// Checks that the scaled modifier does not change associated floating-point input.
    /// </summary>
    /// <typeparam name="TPixel">The associated destination pixel format.</typeparam>
    private static void AssertAssociatedScaledInputIsPreserved<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Vector4[] inputs =
        [
            new(2.5F, -2F, .5F, 1F),
            new(1F, .5F, 1.5F, 2F),
            new(.125F, .25F, .375F, .5F),
            new(-3F, 4F, .25F, 0F)
        ];

        Vector4[] source = new Vector4[17];
        TPixel[] expected = new TPixel[source.Length];
        TPixel[] actual = new TPixel[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            int sample = i % inputs.Length;
            source[i] = inputs[sample];
            expected[i] = TPixel.FromAssociatedVector4(source[i]);
            Assert.Equal(expected[i], TPixel.FromAssociatedScaledVector4(source[i]));
        }

        PixelOperations<TPixel>.Instance.FromVector4Destructive(Configuration.Default, source, actual, PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply);
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Checks native storage and scaled output independently of integer conversion semantics.
    /// </summary>
    private static void AssertScaledOutputPreservesSpecialValues()
    {
        Vector4 native = new(float.PositiveInfinity, float.NegativeInfinity, float.NaN, 65504F);
        HalfVector4 pixel = HalfVector4.FromVector4(native);
        Assert.True(float.IsPositiveInfinity(pixel.ToVector4().X));
        Assert.True(float.IsNegativeInfinity(pixel.ToVector4().Y));
        Assert.True(float.IsNaN(pixel.ToVector4().Z));

        Vector4 expected = pixel.ToVector4();
        Assert.True(float.IsPositiveInfinity(expected.X));
        Assert.True(float.IsNegativeInfinity(expected.Y));
        Assert.True(float.IsNaN(expected.Z));
        Assert.Equal(65504F, expected.W);
        Assert.True(float.IsPositiveInfinity(new HalfSingle(float.PositiveInfinity).ToScaledVector4().X));
        Assert.True(float.IsNaN(new HalfSingle(float.NaN).ToScaledVector4().X));
        Vector4 halfVector2 = new HalfVector2(new Vector2(float.PositiveInfinity, float.NaN)).ToScaledVector4();
        Assert.True(float.IsPositiveInfinity(halfVector2.X));
        Assert.True(float.IsNaN(halfVector2.Y));

    }
}
