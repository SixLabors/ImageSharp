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
    /// Checks the final sample when its index cannot be represented exactly as a float.
    /// </summary>
    [Fact]
    public void ForwardLookup_LongTable_ReturnsLastSample() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckLongTableEndpoint,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks scalar and SIMD endpoint reads beyond the float's consecutive-integer range.
    /// </summary>
    private static void CheckLongTableEndpoint()
    {
        // Binary32 represents every integer through 2^24. This table's last index is 2^24 + 3,
        // which rounds up to the array length. The lookup must still read its actual last sample.
        float[] table = new float[(1 << 24) + 4];
        table[^1] = 0.75F;
        LutOperator calculator = new(table, inverse: false);

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

    [Theory]
    [MemberData(nameof(IccConversionDataLut.LutConversionTestData), MemberType = typeof(IccConversionDataLut))]
    internal void LutOperator_WithLut_ReturnsResult(float[] lut, bool inverted, float input, float expected)
    {
        LutOperator calculator = new(lut, inverted);

        float result = calculator.Convert(input);

        Assert.Equal(expected, result, 4f);

        // The shared unary contract follows the configured direction, including inverse lookup.
        Assert.Equal(expected, calculator.Invoke(input), 4f);
    }
}
