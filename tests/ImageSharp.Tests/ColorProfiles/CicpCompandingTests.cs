// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Verifies scalar and SIMD parity for every H.273 transfer characteristic consumed by HEIF color conversion.
/// </summary>
[Trait("Color", "Companding")]
public class CicpCompandingTests
{
    private static readonly float[] SignalValues =
    [
        -0.5F,
        -0.25F,
        -0.081247F,
        -0.01F,
        0F,
        0.0031308F,
        0.01F,
        0.04045F,
        1F / 12F,
        0.18F,
        0.25F,
        0.5F,
        0.75F,
        1F,
        1.25F,
        2F,
    ];

    /// <summary>
    /// Gets every defined AV1-signallable transfer characteristic, including the deterministic unspecified fallback.
    /// </summary>
    public static TheoryData<int> TransferCharacteristics { get; } = new()
    {
        (int)CicpTransferCharacteristics.ItuRBt709_6,
        (int)CicpTransferCharacteristics.Unspecified,
        (int)CicpTransferCharacteristics.Gamma2_2,
        (int)CicpTransferCharacteristics.Gamma2_8,
        (int)CicpTransferCharacteristics.ItuRBt601_7,
        (int)CicpTransferCharacteristics.SmpteSt240,
        (int)CicpTransferCharacteristics.Linear,
        (int)CicpTransferCharacteristics.Log100,
        (int)CicpTransferCharacteristics.Log100Sqrt,
        (int)CicpTransferCharacteristics.Iec61966_2_4,
        (int)CicpTransferCharacteristics.ItuRBt1361_0,
        (int)CicpTransferCharacteristics.Iec61966_2_1,
        (int)CicpTransferCharacteristics.ItuRBt2020_2_10bit,
        (int)CicpTransferCharacteristics.ItuRBt2020_2_12bit,
        (int)CicpTransferCharacteristics.SmpteSt2084,
        (int)CicpTransferCharacteristics.SmpteSt428_1,
        (int)CicpTransferCharacteristics.AribStdB67,
    };

    /// <summary>
    /// Checks eight-bit table accuracy, scalar/bulk parity, and unchanged alpha under each hardware configuration.
    /// </summary>
    [Fact]
    public void EightBitTablesMatchTransferCurves() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckEightBitTables,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks every eight-bit input code and the logarithmic cutoffs against the direct curves.
    /// </summary>
    private static void CheckEightBitTables()
    {
        // Half of one eight-bit level is the rounding interval for an output channel.
        // The analytic tests retain their existing, tighter floating-point tolerance.
        ApproximateFloatComparer comparer = new(0.5F / byte.MaxValue);

        // Cover all 256 input codes and both logarithmic cutoffs. Alpha exceeds one so
        // clipping or transforming alpha cannot pass unnoticed.
        Vector4[] source = new Vector4[byte.MaxValue + 3];
        for (int i = 0; i <= byte.MaxValue; i++)
        {
            float value = i / (float)byte.MaxValue;
            source[i] = new Vector4(value, value, value, 1.25F);
        }

        source[^2] = new Vector4(0.01F, 0.01F, 0.01F, -0.25F);
        source[^1] = new Vector4(0.00316227766F, 0.00316227766F, 0.00316227766F, 0.5F);

        foreach (ITheoryDataRow row in TransferCharacteristics)
        {
            CicpTransferCharacteristics characteristic = (CicpTransferCharacteristics)(int)row.GetData()[0];
            Vector4[] expanded = source.ToArray();
            Vector4[] compressed = source.ToArray();

            CicpCompanding.Expand(expanded, characteristic, 8, PixelColorRange.Sdr);
            CicpCompanding.Compress(compressed, characteristic, 8, PixelColorRange.Sdr);

            for (int i = 0; i < source.Length; i++)
            {
                // Direct evaluation checks the table coefficients and cache key independently
                // of the single-color and bulk table entry points.
                float linear = CicpCompanding.ToLinear(characteristic, source[i].X);
                float nonlinear = CicpCompanding.ToGamma(characteristic, source[i].X);
                Vector4 expectedExpanded = new(linear, linear, linear, source[i].W);
                Vector4 expectedCompressed = new(nonlinear, nonlinear, nonlinear, source[i].W);
                Vector4 scalarExpanded = CicpCompanding.Expand(source[i], characteristic, 8, PixelColorRange.Sdr);
                Vector4 scalarCompressed = CicpCompanding.Compress(source[i], characteristic, 8, PixelColorRange.Sdr);

                Assert.Equal(expectedExpanded, expanded[i], comparer);
                Assert.Equal(expectedCompressed, compressed[i], comparer);
                Assert.Equal(expectedExpanded, scalarExpanded, comparer);
                Assert.Equal(expectedCompressed, scalarCompressed, comparer);

                Assert.Equal(source[i].W, expanded[i].W);
                Assert.Equal(source[i].W, compressed[i].W);
                Assert.Equal(source[i].W, scalarExpanded.W);
                Assert.Equal(source[i].W, scalarCompressed.W);
            }
        }
    }

    /// <summary>
    /// Verifies that every SIMD width matches the scalar inverse transfer function at curve transitions, extrema, and extended-range values.
    /// </summary>
    [Fact]
    public void ToLinearSimdMatchesScalar() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckToLinear,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks every signaled curve with the current hardware feature set.
    /// </summary>
    private static void CheckToLinear()
    {
        foreach (ITheoryDataRow row in TransferCharacteristics)
        {
            object[] values = row.GetData();
            ToLinearSimdMatchesScalarCase((int)values[0]);
        }
    }

    /// <summary>
    /// Checks all input values at every width and through the bulk API.
    /// </summary>
    /// <param name="transferCharacteristicsValue">The signaled characteristic.</param>
    private static void ToLinearSimdMatchesScalarCase(int transferCharacteristicsValue)
    {
        CicpTransferCharacteristics transferCharacteristics = (CicpTransferCharacteristics)transferCharacteristicsValue;
        float[] expected = SignalValues.Select(value => CicpCompanding.ToLinear(transferCharacteristics, value)).ToArray();

        for (int offset = 0; offset < SignalValues.Length; offset += Vector128<float>.Count)
        {
            Vector128<float> actual = CicpCompanding.ToLinear(transferCharacteristics, Vector128.Create(SignalValues.AsSpan(offset)));
            AssertVectorMatchesScalar(expected.AsSpan(offset), actual, transferCharacteristics);
        }

        for (int offset = 0; offset < SignalValues.Length; offset += Vector256<float>.Count)
        {
            Vector256<float> actual = CicpCompanding.ToLinear(transferCharacteristics, Vector256.Create(SignalValues.AsSpan(offset)));
            AssertVectorMatchesScalar(expected.AsSpan(offset), actual, transferCharacteristics);
        }

        for (int offset = 0; offset < SignalValues.Length; offset += Vector512<float>.Count)
        {
            Vector512<float> actual = CicpCompanding.ToLinear(transferCharacteristics, Vector512.Create(SignalValues.AsSpan(offset)));
            AssertVectorMatchesScalar(expected.AsSpan(offset), actual, transferCharacteristics);
        }

        ReadOnlySpan<int> bitDepths = [10, 12, 16, 32];
        foreach (int bitDepth in bitDepths)
        {
            // Higher-precision entry points must retain extended values and the analytic accuracy bound.
            Vector4[] colors = SignalValues.Select(value => new Vector4(value, value, value, 0.25F)).ToArray();
            CicpCompanding.Expand(colors, transferCharacteristics, bitDepth, PixelColorRange.Sdr);

            for (int i = 0; i < colors.Length; i++)
            {
                Vector4 input = new(SignalValues[i], SignalValues[i], SignalValues[i], 0.25F);
                Vector4 single = CicpCompanding.Expand(input, transferCharacteristics, bitDepth, PixelColorRange.Sdr);

                AssertClose(expected[i], single.X, transferCharacteristics, i, 0);
                AssertClose(expected[i], single.Y, transferCharacteristics, i, 0);
                AssertClose(expected[i], single.Z, transferCharacteristics, i, 0);
                Assert.Equal(0.25F, single.W);

                AssertClose(expected[i], colors[i].X, transferCharacteristics, i, 0);
                AssertClose(expected[i], colors[i].Y, transferCharacteristics, i, 0);
                AssertClose(expected[i], colors[i].Z, transferCharacteristics, i, 0);
                Assert.Equal(0.25F, colors[i].W);
            }
        }
    }

    /// <summary>
    /// Verifies that every SIMD width matches the scalar forward transfer function at curve transitions, extrema, and extended-range values.
    /// </summary>
    [Fact]
    public void ToGammaSimdMatchesScalar() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckToGamma,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks every signaled curve with the current hardware feature set.
    /// </summary>
    private static void CheckToGamma()
    {
        foreach (ITheoryDataRow row in TransferCharacteristics)
        {
            object[] values = row.GetData();
            ToGammaSimdMatchesScalarCase((int)values[0]);
        }
    }

    /// <summary>
    /// Checks all input values at every width and through the bulk API.
    /// </summary>
    /// <param name="transferCharacteristicsValue">The signaled characteristic.</param>
    private static void ToGammaSimdMatchesScalarCase(int transferCharacteristicsValue)
    {
        CicpTransferCharacteristics transferCharacteristics = (CicpTransferCharacteristics)transferCharacteristicsValue;
        float[] expected = SignalValues.Select(value => CicpCompanding.ToGamma(transferCharacteristics, value)).ToArray();

        for (int offset = 0; offset < SignalValues.Length; offset += Vector128<float>.Count)
        {
            Vector128<float> actual = CicpCompanding.ToGamma(transferCharacteristics, Vector128.Create(SignalValues.AsSpan(offset)));
            AssertVectorMatchesScalar(expected.AsSpan(offset), actual, transferCharacteristics);
        }

        for (int offset = 0; offset < SignalValues.Length; offset += Vector256<float>.Count)
        {
            Vector256<float> actual = CicpCompanding.ToGamma(transferCharacteristics, Vector256.Create(SignalValues.AsSpan(offset)));
            AssertVectorMatchesScalar(expected.AsSpan(offset), actual, transferCharacteristics);
        }

        for (int offset = 0; offset < SignalValues.Length; offset += Vector512<float>.Count)
        {
            Vector512<float> actual = CicpCompanding.ToGamma(transferCharacteristics, Vector512.Create(SignalValues.AsSpan(offset)));
            AssertVectorMatchesScalar(expected.AsSpan(offset), actual, transferCharacteristics);
        }

        ReadOnlySpan<int> bitDepths = [10, 12, 16, 32];
        foreach (int bitDepth in bitDepths)
        {
            // Higher-precision entry points must retain extended values and the analytic accuracy bound.
            Vector4[] colors = SignalValues.Select(value => new Vector4(value, value, value, 0.25F)).ToArray();
            CicpCompanding.Compress(colors, transferCharacteristics, bitDepth, PixelColorRange.Sdr);

            for (int i = 0; i < colors.Length; i++)
            {
                Vector4 input = new(SignalValues[i], SignalValues[i], SignalValues[i], 0.25F);
                Vector4 single = CicpCompanding.Compress(input, transferCharacteristics, bitDepth, PixelColorRange.Sdr);

                AssertClose(expected[i], single.X, transferCharacteristics, i, 0);
                AssertClose(expected[i], single.Y, transferCharacteristics, i, 0);
                AssertClose(expected[i], single.Z, transferCharacteristics, i, 0);
                Assert.Equal(0.25F, single.W);

                AssertClose(expected[i], colors[i].X, transferCharacteristics, i, 0);
                AssertClose(expected[i], colors[i].Y, transferCharacteristics, i, 0);
                AssertClose(expected[i], colors[i].Z, transferCharacteristics, i, 0);
                Assert.Equal(0.25F, colors[i].W);
            }
        }
    }

    /// <summary>
    /// Compares four SIMD lanes with their scalar results.
    /// </summary>
    /// <param name="expected">The scalar results.</param>
    /// <param name="actual">The SIMD results.</param>
    /// <param name="transferCharacteristics">The transfer characteristic under test.</param>
    private static void AssertVectorMatchesScalar(ReadOnlySpan<float> expected, Vector128<float> actual, CicpTransferCharacteristics transferCharacteristics)
    {
        InlineArray16<float> storage = default;
        Span<float> values = storage;
        actual.CopyTo(values);

        for (int i = 0; i < Vector128<float>.Count; i++)
        {
            AssertClose(expected[i], values[i], transferCharacteristics, i, 128);
        }
    }

    /// <summary>
    /// Compares eight SIMD lanes with their scalar results.
    /// </summary>
    /// <param name="expected">The scalar results.</param>
    /// <param name="actual">The SIMD results.</param>
    /// <param name="transferCharacteristics">The transfer characteristic under test.</param>
    private static void AssertVectorMatchesScalar(ReadOnlySpan<float> expected, Vector256<float> actual, CicpTransferCharacteristics transferCharacteristics)
    {
        InlineArray16<float> storage = default;
        Span<float> values = storage;
        actual.CopyTo(values);

        for (int i = 0; i < Vector256<float>.Count; i++)
        {
            AssertClose(expected[i], values[i], transferCharacteristics, i, 256);
        }
    }

    /// <summary>
    /// Compares sixteen SIMD lanes with their scalar results.
    /// </summary>
    /// <param name="expected">The scalar results.</param>
    /// <param name="actual">The SIMD results.</param>
    /// <param name="transferCharacteristics">The transfer characteristic under test.</param>
    private static void AssertVectorMatchesScalar(ReadOnlySpan<float> expected, Vector512<float> actual, CicpTransferCharacteristics transferCharacteristics)
    {
        InlineArray16<float> storage = default;
        Span<float> values = storage;
        actual.CopyTo(values);

        for (int i = 0; i < Vector512<float>.Count; i++)
        {
            AssertClose(expected[i], values[i], transferCharacteristics, i, 512);
        }
    }

    /// <summary>
    /// Verifies one SIMD lane within the tolerance of the runtime vector exponential and logarithm kernels.
    /// </summary>
    /// <param name="expected">The scalar result.</param>
    /// <param name="actual">The SIMD result.</param>
    /// <param name="transferCharacteristics">The transfer characteristic under test.</param>
    /// <param name="lane">The SIMD lane index.</param>
    /// <param name="width">The SIMD register width.</param>
    private static void AssertClose(float expected, float actual, CicpTransferCharacteristics transferCharacteristics, int lane, int width)
    {
        float tolerance = MathF.Max(2E-5F, MathF.Abs(expected) * 2E-5F);
        Assert.True(
            MathF.Abs(expected - actual) <= tolerance,
            $"{transferCharacteristics} at {width}-bit lane {lane}: expected {expected:R}, actual {actual:R}, tolerance {tolerance:R}.");
    }
}
