// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;
using SixLabors.ImageSharp.Tests.TestUtilities;
using static SixLabors.ImageSharp.ColorProfiles.Icc.IccColorConverter;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc.Operators;

/// <summary>
/// Tests ICC <see cref="CurveOperator"/>
/// </summary>
[Trait("Color", "Conversion")]
public class IccCurveOperatorTests(ITestOutputHelper testOutputHelper)
{
    /// <summary>
    /// Checks powers between adjacent sixteen-bit codes against double power and reports error in float steps.
    /// </summary>
    [Fact]
    public void Gamma_PreservesPrecisionBetweenSixteenBitCodes()
    {
        (int Width, double Absolute, double Relative, int Ulps, int ScalarPowerUlps)[] measurements = CheckPowerPrecision();
        foreach ((int width, double absolute, double relative, int ulps, int scalarPowerUlps) in measurements)
        {
            string path = width == 1 ? "Scalar" : $"Vector{width * 32}";
            testOutputHelper.WriteLine(
                $"{path}: maximum absolute error={absolute:R}, relative error={relative:R}, float steps={ulps}; MathF.Pow float steps={scalarPowerUlps}.");
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            static () => CheckPowerPrecision(),
            HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks that unrelated exceptional lanes do not change a component.
    /// </summary>
    [Fact]
    public void Gamma_DoesNotDependOnNeighbors() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckNeighborIndependence,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    [Theory]
    [MemberData(nameof(IccConversionDataTrc.CurveConversionTestData), MemberType = typeof(IccConversionDataTrc))]
    internal void IccCurveOperator_WithCurveEntry_ReturnsResult(IccCurveTagDataEntry curve, bool inverted, float input, float expected)
    {
        CurveOperator calculator = new(curve, inverted);

        float result = calculator.Convert(input);

        Assert.Equal(expected, result, 4f);
    }

    /// <summary>
    /// Uses double power as the independent reference and retains the existing one-millionth relative error limit.
    /// </summary>
    /// <returns>Maximum error for each scalar or SIMD path, with MathF.Pow measured over the same inputs.</returns>
    private static (int Width, double Absolute, double Relative, int Ulps, int ScalarPowerUlps)[] CheckPowerPrecision()
    {
        ReadOnlySpan<float> exponents = [0.25F, 0.5F, 1F / 2.4F, 1F / 2.2F, 1F, 1.8F, 2F, 2.2F, 2.4F, 3F, 4F];
        ReadOnlySpan<int> widths = [1, 4, 8, 16];
        (int Width, double Absolute, double Relative, int Ulps, int ScalarPowerUlps)[] measurements = new (int, double, double, int, int)[widths.Length];
        InlineArray16<float> input = default;
        InlineArray16<float> output = default;
        Span<float> samples = input;
        Span<float> actual = output;

        foreach (float exponent in exponents)
        {
            CurveOperator curve = new(new IccCurveTagDataEntry(exponent), false);
            for (int first = 0; first <= ushort.MaxValue; first += samples.Length)
            {
                for (int lane = 0; lane < samples.Length; lane++)
                {
                    int cell = first + lane;

                    // Midpoints detect precision loss that a test limited to sixteen-bit code values can miss.
                    // The final entry checks one exactly; the existing dense-code test also checks zero.
                    samples[lane] = cell == ushort.MaxValue ? 1F : (cell + 0.5F) / ushort.MaxValue;
                }

                for (int path = 0; path < widths.Length; path++)
                {
                    int width = widths[path];
                    ConvertCurve(curve, samples, actual, width);
                    for (int lane = 0; lane < samples.Length; lane++)
                    {
                        double reference = Math.Pow(samples[lane], exponent);
                        float expected = (float)reference;
                        Assert.Equal(expected, actual[lane], MathF.Abs(expected) * 0.000001F);

                        double absolute = Math.Abs(actual[lane] - reference);
                        int ulps = Math.Abs(BitConverter.SingleToInt32Bits(actual[lane]) - BitConverter.SingleToInt32Bits(expected));

                        // MathF.Pow differs from rounded double power by at most one float step on these inputs.
                        // Require the same precision from every width, including hardware-disabled execution.
                        Assert.InRange(ulps, 0, 1);

                        float scalarPower = MathF.Pow(samples[lane], exponent);
                        int scalarPowerUlps = Math.Abs(BitConverter.SingleToInt32Bits(scalarPower) - BitConverter.SingleToInt32Bits(expected));
                        ref (int Width, double Absolute, double Relative, int Ulps, int ScalarPowerUlps) maximum = ref measurements[path];
                        maximum = (width, Math.Max(maximum.Absolute, absolute), Math.Max(maximum.Relative, absolute / reference),
                            Math.Max(maximum.Ulps, ulps), Math.Max(maximum.ScalarPowerUlps, scalarPowerUlps));
                    }
                }
            }
        }

        return measurements;
    }

    /// <summary>
    /// Compares a component alone, broadcast across a vector and next to values outside the normalized range.
    /// </summary>
    private static void CheckNeighborIndependence()
    {
        ReadOnlySpan<float> exponents = [0.25F, 1F / 2.4F, 1F / 2.2F, 1.8F, 2.2F, 2.4F, 3F, 4F];
        ReadOnlySpan<float> values = [0.25F, 0.5F, 0.75F];
        ReadOnlySpan<int> widths = [4, 8, 16];
        InlineArray16<float> input = default;
        InlineArray16<float> output = default;
        Span<float> samples = input;
        Span<float> actual = output;

        foreach (float exponent in exponents)
        {
            CurveOperator curve = new(new IccCurveTagDataEntry(exponent), false);
            foreach (float value in values)
            {
                float expected = curve.Convert(value);
                foreach (int width in widths)
                {
                    samples.Fill(value);
                    ConvertCurve(curve, samples, actual, width);
                    foreach (float component in actual)
                    {
                        Assert.Equal(expected, component);
                    }

                    // The tested component remains unchanged; only its neighbors contain exceptional values.
                    samples[0] = float.NaN;
                    samples[width - 1] = float.MaxValue;
                    ConvertCurve(curve, samples, actual, width);
                    Assert.Equal(expected, actual[1]);
                }
            }
        }
    }

    /// <summary>
    /// Applies the selected width to the complete sixteen-component test buffer.
    /// </summary>
    /// <param name="curve">The curve under test.</param>
    /// <param name="source">The sixteen input components.</param>
    /// <param name="destination">The sixteen output components.</param>
    /// <param name="width">One for scalar conversion, or four, eight or sixteen SIMD lanes.</param>
    private static void ConvertCurve(CurveOperator curve, ReadOnlySpan<float> source, Span<float> destination, int width)
    {
        ref float input = ref MemoryMarshal.GetReference(source);
        ref float output = ref MemoryMarshal.GetReference(destination);
        for (int i = 0; i < source.Length; i += width)
        {
            switch (width)
            {
                case 1:
                    destination[i] = curve.Convert(source[i]);
                    break;
                case 4:
                    curve.Convert(Vector128.LoadUnsafe(ref input, (uint)i)).StoreUnsafe(ref output, (uint)i);
                    break;
                case 8:
                    curve.Convert(Vector256.LoadUnsafe(ref input, (uint)i)).StoreUnsafe(ref output, (uint)i);
                    break;
                case 16:
                    curve.Convert(Vector512.LoadUnsafe(ref input, (uint)i)).StoreUnsafe(ref output, (uint)i);
                    break;
            }
        }
    }
}
