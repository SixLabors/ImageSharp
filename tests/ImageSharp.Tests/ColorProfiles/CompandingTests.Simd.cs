// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
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
public partial class CompandingTests
{
    private static readonly string[] CompandingCurves =
    [
        nameof(SRgbCompanding), nameof(GammaCompanding), nameof(Rec709Companding), nameof(Rec2020Companding), nameof(LCompanding)
    ];

    // PQ and HLG expose extended component conversion, but not the precision-selected SDR table APIs above.
    // Original libjxl v0.13.0, revision 91837342b24034078780b687f57f670a7def8153.
    // TF_PQ_Base and TF_HLG_Base supply these independent fixed component equations and samples.
    private static readonly Vector3[] NativeTransferInputs =
    [
        new(-0F, 0F, 9.999999747e-05F),
        new(-0.25F, 0.1000000015F, 0.5F),
        new(1F, 1.100000024F, 1.25F),
        new(0.01999999955F, 0.400000006F, 0.8000000119F),
        new(-0.05000000075F, 0.3000000119F, 1.200000048F),
    ];

    private static readonly Vector3[][] NativePqExpanded =
    [
        [
            new(0F, 0F, 2.798862697e-09F),
            new(-0.02021245472F, 0.001272806316F, 0.3617478907F),
            new(39.2156868F, 104.1421967F, 504.5805664F),
            new(3.400606874e-05F, 0.1272467524F, 6.098739624F),
            new(-0.0002353005402F, 0.03936560079F, 292.5645752F),
        ],
        [
            new(0F, 0F, 7.1370998e-10F),
            new(-0.00515417615F, 0.0003245655971F, 0.09224570543F),
            new(10F, 26.55625916F, 128.668045F),
            new(8.671547221e-06F, 0.03244791925F, 1.555178523F),
            new(-6.000163921e-05F, 0.01003822777F, 74.60396576F),
        ],
        [
            new(0F, 0F, 7.137099939e-11F),
            new(-0.0005154176033F, 3.245655898e-05F, 0.009224571288F),
            new(1F, 2.65562582F, 12.86680412F),
            new(8.671547107e-07F, 0.003244792111F, 0.1555178463F),
            new(-6.000163921e-06F, 0.001003822777F, 7.46039629F),
        ],
    ];

    private static readonly Vector3[][] NativePqCompressed =
    [
        [
            new(0F, 0F, 0.0338553898F),
            new(-0.4636211991F, 0.3783048391F, 0.5326556563F),
            new(0.6046490669F, 0.6147310734F, 0.628311038F),
            new(0.2492498606F, 0.5100672245F, 0.5812025666F),
            new(-0.3191877306F, 0.4814449251F, 0.6239675879F),
        ],
        [
            new(0F, 0F, 0.06233686209F),
            new(-0.6025591493F, 0.5080783963F, 0.6765847802F),
            new(0.7518270612F, 0.7622197866F, 0.7761653662F),
            new(0.3570123911F, 0.6525785923F, 0.7275251746F),
            new(-0.44028157F, 0.6218628287F, 0.7717114687F),
        ],
        [
            new(0F, 0F, 0.1499457359F),
            new(-0.8517026305F, 0.7518271208F, 0.9265466928F),
            new(1F, 1.009956598F, 1.023248911F),
            new(0.5791332722F, 0.9025723934F, 0.9765438437F),
            new(-0.6765848398F, 0.8714864254F, 1.019012094F),
        ],
    ];

    private static readonly Vector3[] NativeHlgExpanded =
    [
        new(0F, 0F, 3.333333165e-09F),
        new(-0.02083333395F, 0.003333333414F, 0.08333333582F),
        new(1F, 1.731466889F, 3.974631786F),
        new(0.0001333333203F, 0.05333333462F, 0.3427838981F),
        new(-0.0008333333535F, 0.03000000305F, 3.01097846F),
    ];

    private static readonly Vector3[] NativeHlgCompressed =
    [
        new(0F, 0F, 0.01732050814F),
        new(-0.7385492921F, 0.5440894961F, 0.8716434836F),
        new(1F, 1.017439127F, 1.040772319F),
        new(0.2449489683F, 0.8294972777F, 0.9590049386F),
        new(-0.3872983456F, 0.7742521167F, 1.033327818F),
    ];

    /// <summary>
    /// Checks that tensor traversal transforms every component, including positions occupied by alpha in RGBA data.
    /// </summary>
    [Fact]
    public void UnaryTraversal_TransformsEveryComponent() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckUnaryTraversal,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks complete SIMD blocks and scalar remainders using exact squares.
    /// </summary>
    private static void CheckUnaryTraversal()
    {
        ReadOnlySpan<int> lengths = [0, 1, 3, 4, 5, 7, 8, 9, 15, 16, 17, 31, 33];
        ReadOnlySpan<float> samples = [-2F, -1F, 0.5F, 2F];
        TensorPrimitives_.PowOperator operation = new(2F);

        foreach (int length in lengths)
        {
            float[] actual = new float[length];
            for (int i = 0; i < actual.Length; i++)
            {
                // Every fourth value is two: an RGBA-specific traversal would incorrectly leave it unchanged.
                actual[i] = samples[i % samples.Length];
            }

            TensorPrimitives_.Invoke(actual, operation);

            for (int i = 0; i < actual.Length; i++)
            {
                float input = samples[i % samples.Length];
                Assert.Equal(input * input, actual[i]);
            }
        }
    }

    /// <summary>
    /// Checks that companding transforms RGB and retains the exact alpha bits in scalar and bulk conversion.
    /// </summary>
    [Fact]
    public void OperatorCompanding_PreservesAlpha() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckOperatorCompanding,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks finite and IEEE alpha values across complete SIMD blocks and remaining colors.
    /// </summary>
    private static void CheckOperatorCompanding()
    {
        ReadOnlySpan<int> lengths = [0, 1, 3, 4, 5, 7, 8, 9, 15, 16, 17, 31, 33];
        ReadOnlySpan<float> alpha = [0F, -0F, 0.5F, 2F, float.NaN, float.NegativeInfinity, float.PositiveInfinity];
        TensorPrimitives_.PowOperator operation = new(2F);

        foreach (int length in lengths)
        {
            Vector4[] actual = new Vector4[length];
            for (int i = 0; i < actual.Length; i++)
            {
                // Exact squares test the traversal without introducing transfer-curve approximation error.
                actual[i] = new Vector4(0.5F, 2F, -3F, alpha[i % alpha.Length]);
            }

            CompandingUtilities.Compand(actual, operation, PixelColorRange.Extended);

            for (int i = 0; i < actual.Length; i++)
            {
                float inputAlpha = alpha[i % alpha.Length];
                Vector4 single = CompandingUtilities.Compand(new Vector4(0.5F, 2F, -3F, inputAlpha), operation, PixelColorRange.Extended);

                Assert.Equal(new Vector3(0.25F, 4F, 9F), new Vector3(actual[i].X, actual[i].Y, actual[i].Z));
                Assert.Equal(new Vector3(0.25F, 4F, 9F), new Vector3(single.X, single.Y, single.Z));
                Assert.Equal(BitConverter.SingleToInt32Bits(inputAlpha), BitConverter.SingleToInt32Bits(actual[i].W));
                Assert.Equal(BitConverter.SingleToInt32Bits(inputAlpha), BitConverter.SingleToInt32Bits(single.W));
            }
        }
    }

    /// <summary>
    /// Compares all eight-bit codes with the curve formulas, and examines the SDR bounds.
    /// </summary>
    [Fact]
    public void EightBitCompanding_MatchesFormulas() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckEightBitCompanding,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Uses half of an eight-bit channel level as the error bound. Alpha must not change.
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

        // SDR companding clips RGB to [0,1]. Alpha does not change.
        source[^1] = new Vector4(-1F, 2F, 0.5F, -0.25F);

        foreach (string curve in CompandingCurves)
        {
            foreach (bool compress in new[] { false, true })
            {
                Vector4[] actual = source.ToArray();
                ApplyCompanding(curve, compress, actual, PixelColorRange.Sdr);

                for (int i = 0; i < source.Length; i++)
                {
                    Vector4 input = Vector4.Clamp(source[i], Vector4.Zero, Vector4.One);
                    Vector4 expected = new(
                        ExpectedComponent(curve, compress, input.X),
                        ExpectedComponent(curve, compress, input.Y),
                        ExpectedComponent(curve, compress, input.Z),
                        source[i].W);

                    Vector4 single = ApplyCompanding(curve, compress, source[i], PixelColorRange.Sdr);
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
    /// Checks IEEE power results independently of ordinary finite-value approximation error.
    /// </summary>
    [Fact]
    public void Power_PreservesIeeeValues() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckIeeePowers,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks zero, unit bases, infinity and NaN with finite and nonfinite exponents.
    /// </summary>
    private static void CheckIeeePowers()
    {
        // Seventeen values exercise a full 512-bit block and a scalar tail, or narrower blocks and their tails.
        // The power operator requires bases that are zero or more. It does not define the sign of the result for negative zero.
        ReadOnlySpan<float> values =
        [
            0F, 1F, float.PositiveInfinity, float.NaN, 0F, 1F, float.PositiveInfinity, float.NaN,
            0F, 1F, float.PositiveInfinity, float.NaN, 0F, 1F, float.PositiveInfinity, float.NaN, 0F
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

        foreach (string curve in CompandingCurves)
        {
            foreach (bool compress in new[] { false, true })
            {
                foreach (int length in lengths)
                {
                    // Negative RGB tests the lower segment of the curve. Values above one find SDR clipping that is not intended.
                    Vector4[] source = random.GenerateRandomVectorArray(length, -2F, 16F);
                    Vector4[] actual = source.ToArray();
                    ApplyCompanding(curve, compress, actual, PixelColorRange.Extended);

                    for (int i = 0; i < source.Length; i++)
                    {
                        Vector4 input = source[i];
                        Vector4 expected = new(
                            ExpectedComponent(curve, compress, input.X),
                            ExpectedComponent(curve, compress, input.Y),
                            ExpectedComponent(curve, compress, input.Z),
                            input.W);

                        Vector4 single = ApplyCompanding(curve, compress, input, PixelColorRange.Extended);

                        // The direct path uses the measured error bound of the power approximation.
                        // The scaling keeps the relative bound for HDR values and the absolute bound near zero.
                        Vector4 scale = Vector4.Max(Vector4.Abs(expected), Vector4.One);
                        scale.W = 1F;
                        Assert.True(
                            DirectComparer.Equals(expected / scale, actual[i] / scale),
                            $"{curve}, compress={compress}, input={input}, expected={expected}, actual={actual[i]}.");

                        Assert.Equal(expected / scale, single / scale, DirectComparer);

                        // The scalar and bulk entry points use the same approximation. Thus, the test keeps their tighter agreement test.
                        Assert.Equal(single / scale, actual[i] / scale, Comparer);

                        // Alpha can also be negative or above one. Companding must keep its exact stored bits.
                        Assert.Equal(BitConverter.SingleToInt32Bits(input.W), BitConverter.SingleToInt32Bits(actual[i].W));
                        Assert.Equal(BitConverter.SingleToInt32Bits(input.W), BitConverter.SingleToInt32Bits(single.W));
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
    /// Compares the bulk power API with scalar power for each supplied exponent.
    /// </summary>
    /// <param name="exponents">The exponents to check against the independent scalar implementation.</param>
    private static void CheckPowerExponents(ReadOnlySpan<float> exponents)
    {
        // Seventeen values cover one 512-bit block and a scalar remainder, or narrower blocks and their remainder.
        // The power operator requires bases that are zero or more.
        ReadOnlySpan<float> source = [0F, 0.001F, 0.04F, 0.1F, 0.25F, 0.5F, 0.75F, 1F, 1.5F, 2F, 4F, 8F, 16F, 100F,
            float.PositiveInfinity, float.NaN, 0F];

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
    /// <param name="colorRange">The input color range.</param>
    private static void ApplyCompanding(string curve, bool compress, Span<Vector4> values, PixelColorRange colorRange)
    {
        switch ((curve, compress))
        {
            case (nameof(SRgbCompanding), false):
                SRgbCompanding.Expand(values, colorRange);
                break;
            case (nameof(SRgbCompanding), true):
                SRgbCompanding.Compress(values, colorRange);
                break;
            case (nameof(GammaCompanding), false):
                GammaCompanding.Expand(values, 2.2, colorRange);
                break;
            case (nameof(GammaCompanding), true):
                GammaCompanding.Compress(values, 2.2, colorRange);
                break;
            case (nameof(Rec709Companding), false):
                Rec709Companding.Expand(values, colorRange);
                break;
            case (nameof(Rec709Companding), true):
                Rec709Companding.Compress(values, colorRange);
                break;
            case (nameof(Rec2020Companding), false):
                Rec2020Companding.Expand(values, colorRange);
                break;
            case (nameof(Rec2020Companding), true):
                Rec2020Companding.Compress(values, colorRange);
                break;
            case (nameof(LCompanding), false):
                LCompanding.Expand(values, colorRange);
                break;
            case (nameof(LCompanding), true):
                LCompanding.Compress(values, colorRange);
                break;
        }
    }

    /// <summary>
    /// Calls the corresponding single-color API.
    /// </summary>
    /// <param name="curve">The compander under test.</param>
    /// <param name="compress">Whether to encode linear values.</param>
    /// <param name="value">The input color.</param>
    /// <param name="colorRange">The input color range.</param>
    /// <returns>The converted color.</returns>
    private static Vector4 ApplyCompanding(string curve, bool compress, Vector4 value, PixelColorRange colorRange)
        => (curve, compress) switch
        {
            (nameof(SRgbCompanding), false) => SRgbCompanding.Expand(value, colorRange),
            (nameof(SRgbCompanding), true) => SRgbCompanding.Compress(value, colorRange),
            (nameof(GammaCompanding), false) => GammaCompanding.Expand(value, 2.2, colorRange),
            (nameof(GammaCompanding), true) => GammaCompanding.Compress(value, 2.2, colorRange),
            (nameof(Rec709Companding), false) => Rec709Companding.Expand(value, colorRange),
            (nameof(Rec709Companding), true) => Rec709Companding.Compress(value, colorRange),
            (nameof(Rec2020Companding), false) => Rec2020Companding.Expand(value, colorRange),
            (nameof(Rec2020Companding), true) => Rec2020Companding.Compress(value, colorRange),
            (nameof(LCompanding), false) => LCompanding.Expand(value, colorRange),
            (nameof(LCompanding), true) => LCompanding.Compress(value, colorRange),
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

    /// <summary>
    /// Checks scalar colors and complete SIMD groups with narrower and scalar tails against native component equations.
    /// </summary>
    [Fact]
    public void ComponentCompanding_MatchesNative() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckNativeComponentCompanding,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks the component APIs against original native equations without fitting native SIMD approximations outside their unit domain.
    /// </summary>
    private static void CheckNativeComponentCompanding()
    {
        ReadOnlySpan<float> peaks = [255F, 1000F, 10000F];
        ReadOnlySpan<int> lengths = [0, 1, 2, 3, 17, 33];
        Vector3 sentinel = new(-123F, 456F, -789F);

        for (int curve = 0; curve < 2; curve++)
        {
            int peakCount = curve == 0 ? peaks.Length : 1;
            for (int peakIndex = 0; peakIndex < peakCount; peakIndex++)
            {
                for (int direction = 0; direction < 2; direction++)
                {
                    bool compress = direction != 0;
                    Vector3[] expected = curve == 0
                        ? (compress ? NativePqCompressed[peakIndex] : NativePqExpanded[peakIndex])
                        : (compress ? NativeHlgCompressed : NativeHlgExpanded);

                    foreach (int length in lengths)
                    {
                        Vector4[] rgba = new Vector4[length + 1];
                        for (int i = 0; i < length; i++)
                        {
                            Vector3 input = NativeTransferInputs[i % NativeTransferInputs.Length];
                            rgba[i] = new Vector4(input, TransferAlpha(i));
                            Vector4 scalar = ApplyNativeCurve(curve, compress, rgba[i], peaks[peakIndex]);
                            AssertNativeComponents(expected[i % expected.Length], scalar,
                                $"curve={curve}, compress={compress}, peak={peaks[peakIndex]}, input={input}, scalar");
                            Assert.Equal(BitConverter.SingleToInt32Bits(rgba[i].W), BitConverter.SingleToInt32Bits(scalar.W));
                        }

                        rgba[^1] = new Vector4(sentinel, -17F);
                        ApplyNativeCurve(curve, compress, rgba.AsSpan(0, length), peaks[peakIndex]);

                        for (int i = 0; i < length; i++)
                        {
                            Vector3 reference = expected[i % expected.Length];
                            Vector3 input = NativeTransferInputs[i % NativeTransferInputs.Length];
                            AssertNativeComponents(reference, rgba[i],
                                $"curve={curve}, compress={compress}, peak={peaks[peakIndex]}, input={input}, RGBA");
                            Assert.Equal(BitConverter.SingleToInt32Bits(TransferAlpha(i)), BitConverter.SingleToInt32Bits(rgba[i].W));
                        }

                        Assert.Equal(new Vector4(sentinel, -17F), rgba[^1]);
                    }

                    // The public signed extension retains both zero signs. Native scalar helpers return positive zero for both inputs.
                    Vector4 zero = ApplyNativeCurve(curve, compress, new Vector4(-0F, 0F, -0F, .375F), peaks[peakIndex]);
                    Assert.Equal(unchecked((int)0x80000000), BitConverter.SingleToInt32Bits(zero.X));
                    Assert.Equal(0, BitConverter.SingleToInt32Bits(zero.Y));
                    Assert.Equal(unchecked((int)0x80000000), BitConverter.SingleToInt32Bits(zero.Z));
                }
            }
        }

        // PQ expansion above its pole has no real result. The color bounds map NaN to their lower limit.
        Vector4 invalid = new(16F, -16F, 16F, .375F);
        Vector4 bounded = new(new Vector3(ColorNumerics.MinColorValue), invalid.W);
        Assert.Equal(bounded, PqCompanding.Expand(invalid, 255F));

        Vector4[] invalidRow = [invalid, invalid, invalid, invalid, invalid];
        PqCompanding.Expand(invalidRow.AsSpan(), 255F);
        Assert.All(invalidRow, color => Assert.Equal(bounded, color));
    }

    /// <summary>
    /// Applies one public PQ or HLG component curve to a color.
    /// </summary>
    /// <param name="curve">Zero selects PQ; one selects HLG.</param>
    /// <param name="compress">Whether to encode linear input.</param>
    /// <param name="value">The input color.</param>
    /// <param name="peak">The linear PQ normalization luminance.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    private static Vector4 ApplyNativeCurve(int curve, bool compress, Vector4 value, float peak)
        => curve == 0
            ? (compress ? PqCompanding.Compress(value, peak) : PqCompanding.Expand(value, peak))
            : (compress ? HlgCompanding.Compress(value) : HlgCompanding.Expand(value));

    /// <summary>
    /// Applies one public PQ or HLG component curve to RGB colors with alpha.
    /// </summary>
    /// <param name="curve">Zero selects PQ; one selects HLG.</param>
    /// <param name="compress">Whether to encode linear input.</param>
    /// <param name="values">The input and output colors.</param>
    /// <param name="peak">The linear PQ normalization luminance.</param>
    private static void ApplyNativeCurve(int curve, bool compress, Span<Vector4> values, float peak)
    {
        if (curve == 0)
        {
            if (compress)
            {
                PqCompanding.Compress(values, peak);
            }
            else
            {
                PqCompanding.Expand(values, peak);
            }
        }
        else if (compress)
        {
            HlgCompanding.Compress(values);
        }
        else
        {
            HlgCompanding.Expand(values);
        }
    }

    /// <summary>
    /// Compares native RGB values with the existing power-approximation bound in normalized component units.
    /// </summary>
    /// <param name="expected">The original native result.</param>
    /// <param name="actual">The converted RGB color. Alpha is checked separately.</param>
    /// <param name="context">The curve, peak, input and public API layout.</param>
    private static void AssertNativeComponents(Vector3 expected, Vector4 actual, string context)
    {
        for (int component = 0; component < 3; component++)
        {
            float scale = MathF.Max(1F, MathF.Abs(expected[component]));
            string message = $"{context}, component={component}, expected={expected[component]:R}, actual={actual[component]:R}, "
                + $"Vector128={Vector128.IsHardwareAccelerated}, Vector256={Vector256.IsHardwareAccelerated}, "
                + $"Vector512={Vector512.IsHardwareAccelerated}, FMA={Fma.IsSupported}";

            Assert.True(DirectComparer.Equals(expected[component] / scale, actual[component] / scale), message);
        }
    }

    /// <summary>
    /// Supplies alpha values whose exact bits must survive each color transfer.
    /// </summary>
    /// <param name="index">The input color index.</param>
    /// <returns>Signed zero, fractional alpha or a NaN with a fixed payload.</returns>
    private static float TransferAlpha(int index)
        => (index % 3) switch
        {
            0 => -0F,
            1 => .375F,
            _ => BitConverter.Int32BitsToSingle(unchecked((int)0xFFC12345))
        };
}
