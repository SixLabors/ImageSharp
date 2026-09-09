// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Components;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Color;

/// <summary>
/// Verifies scalar and SIMD parity for every H.273 transfer characteristic consumed by HEIF color conversion.
/// </summary>
[Trait("Format", "Heif")]
public class HeifTransferFunctionsTests
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
    /// Verifies that every SIMD width matches the scalar inverse transfer function at curve transitions, extrema, and extended-range values.
    /// </summary>
    /// <param name="transferCharacteristicsValue">The transfer-characteristic code point under test.</param>
    [Theory]
    [MemberData(nameof(TransferCharacteristics))]
    public void ToLinearSimdMatchesScalar(int transferCharacteristicsValue)
    {
        CicpTransferCharacteristics transferCharacteristics = (CicpTransferCharacteristics)transferCharacteristicsValue;
        float[] expected = SignalValues.Select(value => HeifTransferFunctions.ToLinear(transferCharacteristics, value)).ToArray();

        Vector128<float> vector128 = HeifTransferFunctions.ToLinear(transferCharacteristics, Vector128.Create(SignalValues.AsSpan(0, Vector128<float>.Count)));
        Vector256<float> vector256 = HeifTransferFunctions.ToLinear(transferCharacteristics, Vector256.Create(SignalValues.AsSpan(0, Vector256<float>.Count)));
        Vector512<float> vector512 = HeifTransferFunctions.ToLinear(transferCharacteristics, Vector512.Create(SignalValues));

        AssertVectorMatchesScalar(expected, vector128, transferCharacteristics);
        AssertVectorMatchesScalar(expected, vector256, transferCharacteristics);
        AssertVectorMatchesScalar(expected, vector512, transferCharacteristics);
    }

    /// <summary>
    /// Verifies that every SIMD width matches the scalar forward transfer function at curve transitions, extrema, and extended-range values.
    /// </summary>
    /// <param name="transferCharacteristicsValue">The transfer-characteristic code point under test.</param>
    [Theory]
    [MemberData(nameof(TransferCharacteristics))]
    public void ToGammaSimdMatchesScalar(int transferCharacteristicsValue)
    {
        CicpTransferCharacteristics transferCharacteristics = (CicpTransferCharacteristics)transferCharacteristicsValue;
        float[] expected = SignalValues.Select(value => HeifTransferFunctions.ToGamma(transferCharacteristics, value)).ToArray();

        Vector128<float> vector128 = HeifTransferFunctions.ToGamma(transferCharacteristics, Vector128.Create(SignalValues.AsSpan(0, Vector128<float>.Count)));
        Vector256<float> vector256 = HeifTransferFunctions.ToGamma(transferCharacteristics, Vector256.Create(SignalValues.AsSpan(0, Vector256<float>.Count)));
        Vector512<float> vector512 = HeifTransferFunctions.ToGamma(transferCharacteristics, Vector512.Create(SignalValues));

        AssertVectorMatchesScalar(expected, vector128, transferCharacteristics);
        AssertVectorMatchesScalar(expected, vector256, transferCharacteristics);
        AssertVectorMatchesScalar(expected, vector512, transferCharacteristics);
    }

    /// <summary>
    /// Compares four SIMD lanes with their scalar results.
    /// </summary>
    /// <param name="expected">The scalar results.</param>
    /// <param name="actual">The SIMD results.</param>
    /// <param name="transferCharacteristics">The transfer characteristic under test.</param>
    private static void AssertVectorMatchesScalar(ReadOnlySpan<float> expected, Vector128<float> actual, CicpTransferCharacteristics transferCharacteristics)
    {
        for (int i = 0; i < Vector128<float>.Count; i++)
        {
            AssertClose(expected[i], actual.GetElement(i), transferCharacteristics, i, 128);
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
        for (int i = 0; i < Vector256<float>.Count; i++)
        {
            AssertClose(expected[i], actual.GetElement(i), transferCharacteristics, i, 256);
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
        for (int i = 0; i < Vector512<float>.Count; i++)
        {
            AssertClose(expected[i], actual.GetElement(i), transferCharacteristics, i, 512);
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
