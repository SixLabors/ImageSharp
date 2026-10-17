// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <content>
/// Verifies extended companding through the single-color and bulk APIs.
/// </content>
/// <param name="testOutputHelper">Receives table precision measurements.</param>
public partial class CompandingTests(ITestOutputHelper testOutputHelper)
{
    private static readonly string[] CompandingCurves =
    [
        nameof(SRgbCompanding), nameof(GammaCompanding), nameof(Rec709Companding), nameof(Rec2020Companding), nameof(LCompanding)
    ];

    /// <summary>
    /// Checks table interpolation between sixteen-bit codes and reports error in sixteen-bit channel levels.
    /// </summary>
    [Fact]
    public void TableInterpolation_PreservesSelectedPrecision()
    {
        foreach (int bitDepth in new[] { 8, 16 })
        {
            foreach ((string curve, bool compress, float input, float error) in CheckTableInterpolation(bitDepth))
            {
                testOutputHelper.WriteLine(
                    $"{curve}, bitDepth={bitDepth}, compress={compress}: error={error * ushort.MaxValue:R} sixteen-bit levels at input={input:R}.");
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            static () =>
            {
                CheckTableInterpolation(8);
                CheckTableInterpolation(16);
            },
            HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Compares table nodes and quarter intervals with the curve formulas at each supported table precision.
    /// </summary>
    /// <param name="bitDepth">The table precision to verify.</param>
    /// <returns>The largest absolute error and its input for each curve and direction.</returns>
    private static (string Curve, bool Compress, float Input, float Error)[] CheckTableInterpolation(int bitDepth)
    {
        // Processing produces fractional codes. Four samples per table interval check interpolation as well as stored entries.
        // Include the endpoint and an odd sample count to cover the table's scalar remainder.
        const int samplesPerInterval = 4;
        const int intervals = ushort.MaxValue * samplesPerInterval;
        Vector4[] source = new Vector4[intervals + 1];
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = new Vector4(i / (float)intervals, 0F, 0F, 1.25F);
        }

        List<(string Curve, bool Compress, float Input, float Error)> measurements = [];

        // Gamma and Rec.709 retain the eight-bit table limit. The other curves also support sixteen-bit SDR tables.
        // Keep the existing eight-bit error bound and the existing one-millionth bound for higher precision.
        ReadOnlySpan<string> curves = bitDepth == 8
            ? CompandingCurves
            : [nameof(SRgbCompanding), nameof(Rec2020Companding), nameof(LCompanding)];

        ApproximateFloatComparer comparer = bitDepth == 8 ? new(0.5F / byte.MaxValue) : Comparer;
        foreach (string curve in curves)
        {
            foreach (bool compress in new[] { false, true })
            {
                Vector4[] actual = source.ToArray();
                ApplyCompanding(curve, compress, actual, bitDepth, PixelColorRange.Sdr);

                float maximumError = 0F;
                float worstInput = 0F;
                for (int i = 0; i < source.Length; i++)
                {
                    float input = source[i].X;
                    float expected = ExpectedComponent(curve, compress, input);
                    Vector4 single = ApplyCompanding(curve, compress, source[i], bitDepth, PixelColorRange.Sdr);
                    Assert.Equal(expected, actual[i].X, comparer);
                    Assert.Equal(expected, single.X, comparer);
                    Assert.Equal(source[i].W, actual[i].W);
                    Assert.Equal(source[i].W, single.W);

                    float error = MathF.Max(MathF.Abs(expected - actual[i].X), MathF.Abs(expected - single.X));
                    if (error > maximumError)
                    {
                        maximumError = error;
                        worstInput = input;
                    }
                }

                measurements.Add((curve, compress, worstInput, maximumError));
            }
        }

        return measurements.ToArray();
    }

    /// <summary>
    /// Checks every eight-bit code against the curve formulas and verifies the bounded table behavior.
    /// </summary>
    [Fact]
    public void EightBitCompanding_MatchesFormulas() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckEightBitCompanding,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Uses half an eight-bit channel level as the table error bound and checks alpha exactly.
    /// </summary>
    private static void CheckEightBitCompanding()
    {
        ApproximateFloatComparer comparer = new(0.5F / byte.MaxValue);
        Vector4[] source = new Vector4[byte.MaxValue + 2];
        for (int i = 0; i <= byte.MaxValue; i++)
        {
            float value = i / (float)byte.MaxValue;
            source[i] = new Vector4(value, value, value, 1.25F);
        }

        // Eight-bit tables clip RGB to their input range and leave alpha unchanged.
        source[^1] = new Vector4(-1F, 2F, 0.5F, -0.25F);

        foreach (string curve in CompandingCurves)
        {
            foreach (bool compress in new[] { false, true })
            {
                Vector4[] actual = source.ToArray();
                ApplyCompanding(curve, compress, actual, 8, PixelColorRange.Sdr);

                for (int i = 0; i < source.Length; i++)
                {
                    Vector4 input = Vector4.Clamp(source[i], Vector4.Zero, Vector4.One);
                    Vector4 expected = new(
                        ExpectedComponent(curve, compress, input.X),
                        ExpectedComponent(curve, compress, input.Y),
                        ExpectedComponent(curve, compress, input.Z),
                        source[i].W);

                    Vector4 single = ApplyCompanding(curve, compress, source[i], 8, PixelColorRange.Sdr);
                    Assert.Equal(expected, actual[i], comparer);
                    Assert.Equal(expected, single, comparer);
                    Assert.Equal(source[i].W, actual[i].W);
                    Assert.Equal(source[i].W, single.W);
                }
            }
        }
    }

    /// <summary>
    /// Checks every compander with full registers, narrower remainders and hardware-disabled execution.
    /// </summary>
    [Fact]
    public void ExtendedCompanding_MatchesFormulas() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckExtendedCompanding,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks fixed power classification and scalar remainders through the shared bulk entry point.
    /// </summary>
    [Fact]
    public void BulkPower_MatchesScalar() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckBulkPower,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks NaN and infinite powers at each SIMD width and in scalar remainders.
    /// </summary>
    [Fact]
    public void NonFinitePowers_MatchScalar() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckNonFinitePowers,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks IEEE power results independently of ordinary finite-value approximation error.
    /// </summary>
    [Fact]
    public void Power_PreservesIeeeValues() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckIeeePowers,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks zero signs, unit bases, infinities and NaNs with finite and nonfinite exponents.
    /// </summary>
    private static void CheckIeeePowers()
    {
        // Seventeen values exercise a full 512-bit block and a scalar tail, or narrower blocks and their tails.
        ReadOnlySpan<float> values =
        [
            -0F, 0F, 1F, -1F, float.PositiveInfinity, float.NegativeInfinity, float.NaN,
            -0F, 0F, 1F, -1F, float.PositiveInfinity, float.NegativeInfinity, float.NaN, 1F, -1F, -0F
        ];

        ReadOnlySpan<float> exponents = [0F, 1F, 2F, 3F, 2.2F, -2F, -3F, float.PositiveInfinity, float.NegativeInfinity, float.NaN];
        foreach (float exponent in exponents)
        {
            float[] actual = values.ToArray();
            TensorPrimitives_.Pow(actual, exponent);

            for (int i = 0; i < actual.Length; i++)
            {
                float expected = MathF.Pow(values[i], exponent);
                Assert.Equal(expected, actual[i], new ApproximateFloatComparer(0F));

                // Numeric equality cannot distinguish the sign of zero. Compare its stored sign too.
                if (expected == 0F || float.IsInfinity(expected))
                {
                    Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(actual[i]));
                }
            }
        }
    }

    /// <summary>
    /// Uses the published curve formulas as an independent reference for every extended compander.
    /// </summary>
    private static void CheckExtendedCompanding()
    {
        CheckSrgbCoefficients();

        ReadOnlySpan<int> lengths = [0, 1, 2, 3, 4, 5, 7, 31, 257];
        Random random = new(42);

        ReadOnlySpan<int> bitDepths = [8, 10, 12, 16, 32];
        foreach (int bitDepth in bitDepths)
        {
            foreach (string curve in CompandingCurves)
            {
                foreach (bool compress in new[] { false, true })
                {
                    foreach (int length in lengths)
                    {
                        // Negative RGB checks the curve's lower segment. Values above one detect unintended SDR clipping.
                        Vector4[] source = random.GenerateRandomVectorArray(length, -2F, 16F);
                        Vector4[] actual = source.ToArray();
                        ApplyCompanding(curve, compress, actual, bitDepth, PixelColorRange.Extended);

                        for (int i = 0; i < source.Length; i++)
                        {
                            Vector4 input = source[i];
                            Vector4 expected = new(
                                ExpectedComponent(curve, compress, input.X),
                                ExpectedComponent(curve, compress, input.Y),
                                ExpectedComponent(curve, compress, input.Z),
                                input.W);

                            Vector4 single = ApplyCompanding(curve, compress, input, bitDepth, PixelColorRange.Extended);

                            // The direct path uses the measured power approximation bound. Scaling retains the
                            // relative bound for HDR values and the absolute bound near zero. Tables use separate checks.
                            Vector4 scale = Vector4.Max(Vector4.Abs(expected), Vector4.One);
                            scale.W = 1F;
                            Assert.True(
                                DirectComparer.Equals(expected / scale, actual[i] / scale),
                                $"{curve}, compress={compress}, bitDepth={bitDepth}, input={input}, expected={expected}, actual={actual[i]}.");

                            Assert.Equal(expected / scale, single / scale, DirectComparer);

                            // Scalar and bulk entry points use the same approximation. Keep their tighter agreement check.
                            Assert.Equal(single / scale, actual[i] / scale, Comparer);

                            // Alpha can be signed or above one too. Companding must preserve its exact stored bits.
                            Assert.Equal(BitConverter.SingleToInt32Bits(input.W), BitConverter.SingleToInt32Bits(actual[i].W));
                            Assert.Equal(BitConverter.SingleToInt32Bits(input.W), BitConverter.SingleToInt32Bits(single.W));
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks the distinct IEC and H.273 sRGB coefficients against their published formulas.
    /// </summary>
    private static void CheckSrgbCoefficients()
    {
        // These values straddle both junctions. Substituting IEC's rounded coefficients for H.273
        // changes the results near black, even though the two curves look similar over most of their range.
        ReadOnlySpan<float> samples = [-0.05F, -0.00305F, 0F, 0.00305F, 0.0031308F, 0.0393F, 0.04045F, 0.5F, 1F, 2F];
        const double alpha = 1.0550107189475866;
        const double beta = 0.0030412825601275209;
        Vector4[] actual = samples.ToArray().Select(value => new Vector4(value)).ToArray();

        CicpCompanding.CompressExtended(actual, CicpTransferCharacteristics.Iec61966_2_1);

        for (int i = 0; i < samples.Length; i++)
        {
            double magnitude = Math.Abs(samples[i]);
            double expected = magnitude < beta ? magnitude * 12.92 : (alpha * Math.Pow(magnitude, 1 / 2.4)) - (alpha - 1);

            // H.273 has different coefficients from IEC sRGB. Check its own formula with the direct approximation bound.
            float reference = (float)Math.CopySign(expected, samples[i]);
            float scale = MathF.Max(MathF.Abs(reference), 1F);
            Assert.Equal(reference / scale, actual[i].X / scale, DirectComparer);
            Assert.Equal(samples[i], actual[i].W);
        }

        for (int i = 0; i < samples.Length; i++)
        {
            actual[i] = new Vector4(samples[i]);
        }

        CicpCompanding.ExpandExtended(actual, CicpTransferCharacteristics.Iec61966_2_1);

        for (int i = 0; i < samples.Length; i++)
        {
            double magnitude = Math.Abs(samples[i]);
            double expected = magnitude < beta * 12.92 ? magnitude / 12.92 : Math.Pow((magnitude + (alpha - 1)) / alpha, 2.4);

            // Retain the same relative HDR bound and absolute near-zero bound as the full curve tests.
            float reference = (float)Math.CopySign(expected, samples[i]);
            float scale = MathF.Max(MathF.Abs(reference), 1F);
            Assert.Equal(reference / scale, actual[i].X / scale, DirectComparer);
            Assert.Equal(samples[i], actual[i].W);
        }
    }

    /// <summary>
    /// Checks approximate finite powers, exact exponent cases and IEEE special values across odd-length spans.
    /// </summary>
    private static void CheckBulkPower()
        => CheckPowerExponents([0F, 1F, 2F, 3F, 2.2F, -2F, -3F, float.PositiveInfinity, float.NegativeInfinity, float.NaN]);

    /// <summary>
    /// Checks nonfinite exponents independently of finite-power approximation errors.
    /// </summary>
    private static void CheckNonFinitePowers()
        => CheckPowerExponents([float.PositiveInfinity, float.NegativeInfinity, float.NaN]);

    /// <summary>
    /// Compares the bulk power API with scalar power for each supplied exponent.
    /// </summary>
    /// <param name="exponents">The exponents to check against the independent scalar implementation.</param>
    private static void CheckPowerExponents(ReadOnlySpan<float> exponents)
    {
        // Seventeen values cover one 512-bit block and a scalar remainder, or narrower blocks and their remainder.
        ReadOnlySpan<float> source = [-16F, -2F, -1F, -0F, 0F, 0.001F, 0.04F, 0.1F, 0.25F, 0.5F, 0.75F, 1F, 2F, 16F,
            float.PositiveInfinity, float.NegativeInfinity, float.NaN];

        foreach (float exponent in exponents)
        {
            float[] actual = source.ToArray();
            TensorPrimitives_.Pow(actual, exponent);

            for (int i = 0; i < actual.Length; i++)
            {
                float expected = (float)Math.Pow(source[i], exponent);

                // Zero, identity and square powers have exact implementations. Unit bases and IEEE special values
                // also have exact rules. General finite powers use the recorded native approximation error bound.
                if (exponent is 0F or 1F or 2F || source[i] is 1F or -1F || expected == 0F || !float.IsFinite(expected))
                {
                    Assert.Equal(expected, actual[i], new ApproximateFloatComparer(0F));
                }
                else
                {
                    // Nonzero finite powers use a relative bound, including small results. This rejects accidental underflow to zero.
                    float scale = MathF.Abs(expected);
                    Assert.Equal(expected / scale, actual[i] / scale, DirectComparer);
                }

                // Equality alone does not distinguish +0 from -0. Power must retain the IEEE sign rules.
                if (expected == 0F || float.IsInfinity(expected))
                {
                    Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(actual[i]));
                }
            }
        }
    }

    /// <summary>
    /// Calls each compander's bulk API, including the configured gamma.
    /// </summary>
    /// <param name="curve">The compander under test.</param>
    /// <param name="compress">Whether to encode linear values.</param>
    /// <param name="values">The colors to transform in place.</param>
    /// <param name="bitDepth">The requested component precision.</param>
    /// <param name="colorRange">The input color range.</param>
    private static void ApplyCompanding(string curve, bool compress, Span<Vector4> values, int bitDepth, PixelColorRange colorRange)
    {
        switch ((curve, compress))
        {
            case (nameof(SRgbCompanding), false):
                SRgbCompanding.Expand(values, bitDepth, colorRange);
                break;
            case (nameof(SRgbCompanding), true):
                SRgbCompanding.Compress(values, bitDepth, colorRange);
                break;
            case (nameof(GammaCompanding), false):
                GammaCompanding.Expand(values, 2.2, bitDepth, colorRange);
                break;
            case (nameof(GammaCompanding), true):
                GammaCompanding.Compress(values, 2.2, bitDepth, colorRange);
                break;
            case (nameof(Rec709Companding), false):
                Rec709Companding.Expand(values, bitDepth, colorRange);
                break;
            case (nameof(Rec709Companding), true):
                Rec709Companding.Compress(values, bitDepth, colorRange);
                break;
            case (nameof(Rec2020Companding), false):
                Rec2020Companding.Expand(values, bitDepth, colorRange);
                break;
            case (nameof(Rec2020Companding), true):
                Rec2020Companding.Compress(values, bitDepth, colorRange);
                break;
            case (nameof(LCompanding), false):
                LCompanding.Expand(values, bitDepth, colorRange);
                break;
            case (nameof(LCompanding), true):
                LCompanding.Compress(values, bitDepth, colorRange);
                break;
        }
    }

    /// <summary>
    /// Calls the corresponding single-color API.
    /// </summary>
    /// <param name="curve">The compander under test.</param>
    /// <param name="compress">Whether to encode linear values.</param>
    /// <param name="value">The input color.</param>
    /// <param name="bitDepth">The requested component precision.</param>
    /// <param name="colorRange">The input color range.</param>
    /// <returns>The converted color.</returns>
    private static Vector4 ApplyCompanding(string curve, bool compress, Vector4 value, int bitDepth, PixelColorRange colorRange)
        => (curve, compress) switch
        {
            (nameof(SRgbCompanding), false) => SRgbCompanding.Expand(value, bitDepth, colorRange),
            (nameof(SRgbCompanding), true) => SRgbCompanding.Compress(value, bitDepth, colorRange),
            (nameof(GammaCompanding), false) => GammaCompanding.Expand(value, 2.2, bitDepth, colorRange),
            (nameof(GammaCompanding), true) => GammaCompanding.Compress(value, 2.2, bitDepth, colorRange),
            (nameof(Rec709Companding), false) => Rec709Companding.Expand(value, bitDepth, colorRange),
            (nameof(Rec709Companding), true) => Rec709Companding.Compress(value, bitDepth, colorRange),
            (nameof(Rec2020Companding), false) => Rec2020Companding.Expand(value, bitDepth, colorRange),
            (nameof(Rec2020Companding), true) => Rec2020Companding.Compress(value, bitDepth, colorRange),
            (nameof(LCompanding), false) => LCompanding.Expand(value, bitDepth, colorRange),
            (nameof(LCompanding), true) => LCompanding.Compress(value, bitDepth, colorRange),
            _ => throw new ArgumentException("Unknown compander.", nameof(curve))
        };

    /// <summary>
    /// Evaluates the established transfer formulas with double intermediates for the reference.
    /// </summary>
    /// <param name="curve">The compander under test.</param>
    /// <param name="compress">Whether to encode linear values.</param>
    /// <param name="value">The input component.</param>
    /// <returns>The reference component.</returns>
    private static float ExpectedComponent(string curve, bool compress, float value)
    {
        double input = value;
        double magnitude = Math.Abs(input);
        double result;
        switch (curve)
        {
            case nameof(SRgbCompanding):
                result = compress
                    ? magnitude <= 0.04045 / 12.92 ? magnitude * 12.92 : (1.055 * Math.Pow(magnitude, 1 / 2.4)) - 0.055
                    : magnitude <= 0.04045 ? magnitude / 12.92 : Math.Pow((magnitude + 0.055) / 1.055, 2.4);
                return (float)Math.CopySign(result, input);
            case nameof(GammaCompanding):
                // Extended gamma transforms the magnitude and retains the sign, as skcms does for its power transfer curves.
                return (float)Math.CopySign(Math.Pow(magnitude, compress ? 1 / 2.2 : 2.2), input);
            case nameof(Rec709Companding):
                result = compress
                    ? input < 0.018 ? input * 4.5 : (1.099 * Math.Pow(input, 0.45)) - 0.099
                    : input < 0.081 ? input / 4.5 : Math.Pow((input + 0.099) / 1.099, 1 / 0.45);
                return (float)result;
            case nameof(Rec2020Companding):
                // Rec.2020 specifies the smooth junction rather than Rec.709's rounded coefficients.
                const double alpha = 1.09929682680944;
                const double beta = 0.018053968510807;
                result = compress
                    ? input < beta ? input * 4.5 : (alpha * Math.Pow(input, 0.45)) - (alpha - 1)
                    : input < beta * 4.5 ? input / 4.5 : Math.Pow((input + (alpha - 1)) / alpha, 1 / 0.45);
                return (float)result;
            default:
                result = compress
                    ? input <= CieConstants.Epsilon ? (input * CieConstants.Kappa) / 100 : (1.16 * Math.Pow(input, 0.3333333)) - 0.16
                    : input <= 0.08 ? (100 * input) / CieConstants.Kappa : Math.Pow((input + 0.16) / 1.16, 3);
                return (float)result;
        }
    }
}
