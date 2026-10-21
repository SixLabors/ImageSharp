// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests shared sampled-curve interpolation.
/// </summary>
[Trait("Color", "Conversion")]
public class LutOperatorTests
{
    /// <summary>
    /// Checks homogeneous first-sample lookups with each supported SIMD width and scalar conversion.
    /// </summary>
    [Fact]
    public void ForwardLookup_FirstSample_MatchesAllWidths() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckFirstSample,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks the first table value for zero and values that clamp to zero.
    /// </summary>
    private static void CheckFirstSample()
    {
        // A nonzero first sample distinguishes reading the endpoint from returning the bounded input.
        LutOperator calculator = new([0.25F, 0.5F, 0.75F], inverse: false);
        ReadOnlySpan<float> inputs = [0F, -0F, -1F, float.NegativeInfinity, float.NaN];

        foreach (float input in inputs)
        {
            Assert.Equal(0.25F, calculator.Convert(input));

            // Direct operator calls obey the same hardware-width selection as the shared converter.
            if (Vector128.IsHardwareAccelerated)
            {
                Assert.Equal(Vector128.Create(0.25F), calculator.Convert(Vector128.Create(input)));
            }

            if (Vector256.IsHardwareAccelerated)
            {
                Assert.Equal(Vector256.Create(0.25F), calculator.Convert(Vector256.Create(input)));
            }

            if (Vector512.IsHardwareAccelerated)
            {
                Assert.Equal(Vector512.Create(0.25F), calculator.Convert(Vector512.Create(input)));
            }
        }
    }

    /// <summary>
    /// Checks exact logical endpoints and their zero sign when float coordinates round large table indices.
    /// </summary>
    [Fact]
    public void ForwardLookup_LongTable_ReturnsLastSample() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckLongTableEndpoint,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks scalar and SIMD logical endpoints beyond the float's consecutive-integer range and positive-zero output.
    /// </summary>
    private static void CheckLongTableEndpoint()
    {
        // Single-precision floats represent every integer through 2^24. Logical last indices 2^24 + 1 and 2^24 + 3 round down and up.
        // Reuse one array to check both directions, including logical samples followed by physical padding.
        float[] table = new float[(1 << 24) + 4];
        ReadOnlySpan<int> sampleCounts = [(1 << 24) + 2, table.Length];

        foreach (int sampleCount in sampleCounts)
        {
            table[sampleCount - 1] = 0.75F;
            LutOperator calculator = new(table, inverse: false, sampleCount);

            Assert.Equal(0.75F, calculator.Convert(1F));

            // Direct operator calls obey the same hardware-width selection as the shared converter.
            if (Vector128.IsHardwareAccelerated)
            {
                Assert.Equal(Vector128.Create(0.75F), calculator.Convert(Vector128<float>.One));
            }

            if (Vector256.IsHardwareAccelerated)
            {
                Assert.Equal(Vector256.Create(0.75F), calculator.Convert(Vector256<float>.One));
            }

            if (Vector512.IsHardwareAccelerated)
            {
                Assert.Equal(Vector512.Create(0.75F), calculator.Convert(Vector512<float>.One));
            }

            table[sampleCount - 1] = 0F;
        }

        // Interpolation adds a zero term at the endpoint. A negative-zero final sample therefore produces positive zero.
        // Compare the stored bits because numeric equality does not distinguish the two zero signs.
        LutOperator zeroEndpoint = new([0F, -0F], inverse: false);

        Assert.Equal(0, BitConverter.SingleToInt32Bits(zeroEndpoint.Convert(1F)));

        if (Vector128.IsHardwareAccelerated)
        {
            Assert.Equal(Vector128<int>.Zero, zeroEndpoint.Convert(Vector128<float>.One).AsInt32());
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Assert.Equal(Vector256<int>.Zero, zeroEndpoint.Convert(Vector256<float>.One).AsInt32());
        }

        if (Vector512.IsHardwareAccelerated)
        {
            Assert.Equal(Vector512<int>.Zero, zeroEndpoint.Convert(Vector512<float>.One).AsInt32());
        }
    }

    /// <summary>
    /// Verifies that forward lookup saturates at the table endpoints outside its normalized domain.
    /// </summary>
    /// <param name="input">The normalized lookup input.</param>
    /// <param name="expected">The expected table value.</param>
    [Theory]
    [InlineData(float.NaN, 0.25F)]
    [InlineData(float.NegativeInfinity, 0.25F)]
    [InlineData(float.MinValue, 0.25F)]
    [InlineData(-0.0001F, 0.25F)]
    [InlineData(0F, 0.25F)]
    [InlineData(0.5F, 0.5F)]
    [InlineData(1F, 0.75F)]
    [InlineData(1.0001F, 0.75F)]
    [InlineData(2F, 0.75F)]
    [InlineData(float.MaxValue, 0.75F)]
    [InlineData(float.PositiveInfinity, 0.75F)]
    public void ForwardLookup_ClampsToTableDomain(float input, float expected)
    {
        // Nonzero and nonunit endpoints distinguish table saturation from clamping the output to 0 or 1.
        LutOperator calculator = new([0.25F, 0.5F, 0.75F], inverse: false);

        Assert.Equal(expected, calculator.Convert(input));
    }

    /// <summary>
    /// Checks sampled-curve conversion against the established forward and inverse results.
    /// </summary>
    /// <param name="lut">The sampled curve.</param>
    /// <param name="inverted">Whether to find the curve input from its output.</param>
    /// <param name="input">The value to convert.</param>
    /// <param name="expected">The established interpolated result.</param>
    [Theory]
    [MemberData(nameof(IccConversionDataLut.LutConversionTestData), MemberType = typeof(IccConversionDataLut))]
    internal void LutOperator_WithLut_ReturnsResult(float[] lut, bool inverted, float input, float expected)
    {
        LutOperator calculator = new(lut, inverted);

        float result = calculator.Convert(input);

        // Four decimal places allow single-precision interpolation rounding.
        Assert.Equal(expected, result, precision: 4);

        // The shared unary contract follows the configured direction, including inverse lookup.
        Assert.Equal(expected, calculator.Invoke(input), precision: 4);
    }
}
