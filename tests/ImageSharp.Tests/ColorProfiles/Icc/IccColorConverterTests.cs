// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;
using SixLabors.ImageSharp.Tests.TestUtilities;
using static SixLabors.ImageSharp.ColorProfiles.Icc.IccColorConverter;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc;

/// <summary>
/// Checks ICC operators at each SIMD width and with hardware intrinsics disabled.
/// </summary>
public class IccColorConverterTests
{
    /// <summary>
    /// Checks curve formulas and their inverses against the existing independent expected values.
    /// </summary>
    [Fact]
    public void Curves_MatchExpectedValues() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckCurves,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks profile stage ordering, mixed lanes, all block widths, in-place conversion and scalar tails.
    /// </summary>
    [Fact]
    public void Tables_MatchExpectedValues() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckTables,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks SIMD gamma evaluation across every normalized sixteen-bit code.
    /// </summary>
    [Fact]
    public void Gamma_MatchesMathFPowAcrossSixteenBitCodes() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckGamma,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Uses MathF.Pow as the gamma contract and tests the full normalized input domain at each SIMD width.
    /// </summary>
    private static void CheckGamma()
    {
        ReadOnlySpan<float> exponents = [1F / 255F, 0.25F, 1F, 2F, 2.2F, 2.4F, 3F, 4F, 4.1F, 255F, 1F / 2.2F, 1F / 2.4F];
        float[] input = new float[ushort.MaxValue + 1];
        for (int i = 0; i < input.Length; i++)
        {
            input[i] = i / (float)ushort.MaxValue;
        }

        foreach (float exponent in exponents)
        {
            CurveOperator curve = new(new IccCurveTagDataEntry(exponent), false);
            foreach (int width in new[] { 4, 8, 16 })
            {
                InlineArray16<float> output = default;
                Span<float> values = output;
                for (int i = 0; i < input.Length; i += width)
                {
                    ref float source = ref input[i];
                    ref float destination = ref MemoryMarshal.GetReference(values);
                    switch (width)
                    {
                        case 4:
                            curve.Convert(Vector128.LoadUnsafe(ref source)).StoreUnsafe(ref destination);
                            break;
                        case 8:
                            curve.Convert(Vector256.LoadUnsafe(ref source)).StoreUnsafe(ref destination);
                            break;
                        case 16:
                            curve.Convert(Vector512.LoadUnsafe(ref source)).StoreUnsafe(ref destination);
                            break;
                    }

                    for (int lane = 0; lane < width; lane++)
                    {
                        // SIMD powers need not round like scalar powf. Require absolute error below one millionth,
                        // more than fifteen times smaller than one normalized sixteen-bit code step.
                        Assert.Equal(MathF.Pow(input[i + lane], exponent), values[lane], 0.000001F);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks tetrahedral and multilinear interpolation with mixed lanes through every hardware path.
    /// </summary>
    [Fact]
    public void Cluts_MatchCellWeights() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckClutWeights,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks the XYZ and Lab achromatic axes with full vector blocks and a scalar tail.
    /// </summary>
    [Fact]
    public void Gray_MatchesAchromaticAxis() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckGray,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Uses tables with known corner weights to check tetrahedron selection and adjacent four-dimensional slices.
    /// </summary>
    private static void CheckClutWeights()
    {
        ReadOnlySpan<Vector4> samples =
        [
            new(0.75F, 0.5F, 0.25F, 0.5F), new(0.75F, 0.25F, 0.5F, 0.75F),
            new(0.5F, 0.25F, 0.75F, 0.25F), new(0.5F, 0.75F, 0.25F, 0.75F),
            new(0.25F, 0.75F, 0.5F, 0.25F), new(0.25F, 0.5F, 0.75F, 0.5F),
            new(0F), new(1F), new(0.5F), new(-0.25F, 0.5F, 0.75F, 0.25F), new(1.25F, 0.5F, 0.75F, 0.25F)
        ];

        foreach (int dimensions in new[] { 3, 4 })
        {
            // Only the three-dimensional upper corner is one. Its tetrahedral weight is the minimum
            // axis fraction; multilinear interpolation gives their product. The second four-dimensional
            // slice applies 0.25 + 0.5*weight, so blending its first axis has an independent expected result.
            float[] values = dimensions == 3
                ? [0F, 0F, 0F, 0F, 0F, 0F, 0F, 1F]
                : [0F, 0F, 0F, 0F, 0F, 0F, 0F, 1F, 0.25F, 0.25F, 0.25F, 0.25F, 0.25F, 0.25F, 0.25F, 0.75F];
            byte[] grid = dimensions == 3 ? [2, 2, 2] : [2, 2, 2, 2];
            IccClut clut = new(values, grid, IccClutDataType.Float, 1);
            IccTagDataEntry[] inputCurves = Enumerable.Repeat<IccTagDataEntry>(IccConversionDataTrc.IdentityCurve, dimensions).ToArray();
            IccLutAToBTagDataEntry entry = new([IccConversionDataTrc.IdentityCurve], null, null, null, clut, inputCurves);

            foreach (bool trilinear in new[] { false, true })
            {
                IccColorConverter<LutColorOperator> converter = new(new LutColorOperator(entry, trilinear));
                Vector4[] source = new Vector4[31];
                Vector4[] actual = new Vector4[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    source[i] = samples[i % samples.Length];
                }

                converter.Convert(source, actual);
                for (int i = 0; i < source.Length; i++)
                {
                    Vector4 bounded = Numerics.Clamp(source[i], Vector4.Zero, Vector4.One);
                    Vector3 fractions = dimensions == 3 ? new(bounded.X, bounded.Y, bounded.Z) : new(bounded.Y, bounded.Z, bounded.W);
                    float weight = trilinear ? fractions.X * fractions.Y * fractions.Z : MathF.Min(fractions.X, MathF.Min(fractions.Y, fractions.Z));
                    float expected = dimensions == 3 ? weight : weight + (((0.25F + (0.5F * weight)) - weight) * bounded.X);
                    VectorAssert.Equal(new Vector4(expected, 0F, 0F, 0F), actual[i], 5);
                }
            }
        }
    }

    /// <summary>
    /// Checks a gamma-two gray curve against squared device values and square-root PCS values.
    /// </summary>
    private static void CheckGray()
    {
        ReadOnlySpan<float> samples = [0F, 0.25F, 0.5F, 0.75F, 1F];
        foreach (IccColorSpaceType pcs in new[] { IccColorSpaceType.CieXyz, IccColorSpaceType.CieLab })
        {
            foreach (bool toPcs in new[] { false, true })
            {
                IccColorConverter<GrayTrcOperator> converter = new(new GrayTrcOperator(new IccCurveTagDataEntry(2F), pcs, toPcs));
                Vector4[] source = new Vector4[31];
                Vector4[] expected = new Vector4[source.Length];
                Vector4[] actual = new Vector4[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    float value = samples[i % samples.Length];
                    if (toPcs)
                    {
                        source[i] = new Vector4(value, 0.3F, 0.7F, 1F);
                        expected[i] = pcs == IccColorSpaceType.CieLab
                            ? new CieLab(value * value * 100F, 0F, 0F).ToScaledVector4()
                            : new CieXyz(KnownIlluminants.D50Icc.ToVector3() * (value * value)).ToScaledVector4();
                    }
                    else
                    {
                        source[i] = pcs == IccColorSpaceType.CieLab
                            ? new CieLab(value * 100F, 10F, 20F).ToScaledVector4()
                            : new CieXyz(0.3F, value, 0.7F).ToScaledVector4();
                        expected[i] = new Vector4(MathF.Sqrt(value));
                    }
                }

                converter.Convert(source, actual);
                for (int i = 0; i < source.Length; i++)
                {
                    VectorAssert.Equal(expected[i], actual[i], 5);
                }
            }
        }
    }

    /// <summary>
    /// Checks signed bases and IEEE special values against MathF.Pow at each SIMD width.
    /// </summary>
    [Fact]
    public void Gamma_PreservesSpecialValues() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckSpecialPowers,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks powers throughout the normal float range, including each mantissa interval's upper edge.
    /// </summary>
    [Fact]
    public void Gamma_MatchesMathPowAcrossBinaryRanges() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckPowerRanges,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Varies the binary exponent separately from the significand so extreme values do not hide ordinary SIMD blocks.
    /// </summary>
    private static void CheckPowerRanges()
    {
        ReadOnlySpan<float> exponents = [1F / 255F, 0.25F, 1F / 2.4F, 2.2F, 2.4F, 3F, 4F, 4.1F];
        foreach (float exponent in exponents)
        {
            CurveOperator curve = new(new IccCurveTagDataEntry(exponent), false);
            InlineArray16<float> input = default;
            InlineArray16<float> output = default;
            Span<float> inputs = input;
            Span<float> actual = output;
            for (int binaryExponent = -126; binaryExponent <= 127; binaryExponent++)
            {
                for (int interval = 0; interval < 64; interval += inputs.Length)
                {
                    for (int lane = 0; lane < inputs.Length; lane++)
                    {
                        // Cover 64 evenly spaced significands, including values immediately below their boundaries.
                        // ScaleB then sets the IEEE binary exponent exactly, without decimal scaling error.
                        float significand = MathF.BitDecrement(1F + ((interval + lane + 1) / 64F));
                        inputs[lane] = MathF.ScaleB(significand, binaryExponent);
                    }

                    curve.Convert(Vector512.LoadUnsafe(ref inputs[0])).StoreUnsafe(ref actual[0]);
                    AssertPowerValues(inputs, actual, exponent);
                    for (int i = 0; i < inputs.Length; i += Vector256<float>.Count)
                    {
                        curve.Convert(Vector256.LoadUnsafe(ref inputs[i])).StoreUnsafe(ref actual[0], (uint)i);
                    }

                    AssertPowerValues(inputs, actual, exponent);
                    for (int i = 0; i < inputs.Length; i += Vector128<float>.Count)
                    {
                        curve.Convert(Vector128.LoadUnsafe(ref inputs[i])).StoreUnsafe(ref actual[0], (uint)i);
                    }

                    AssertPowerValues(inputs, actual, exponent);
                }
            }
        }
    }

    /// <summary>
    /// Checks the ICC gamma lower bound, identity curves and IEEE special values at every SIMD width.
    /// </summary>
    private static void CheckSpecialPowers()
    {
        // The final two values straddle the normal/subnormal boundary in IEEE single precision.
        float smallestNormal = MathF.ScaleB(1F, -126);
        float[] inputs =
        [
            float.NegativeInfinity,
            -2F,
            -1F,
            -0F,
            0F,
            0.5F,
            1F,
            2F,
            float.PositiveInfinity,
            float.NaN,
            float.Epsilon,
            -float.Epsilon,
            float.MaxValue,
            float.MinValue,
            smallestNormal,
            MathF.BitDecrement(smallestNormal)
        ];
        ReadOnlySpan<float> exponents = [0F, 1F, 2F, 3F, -1F, -2F, 0.5F, 2.2F, float.PositiveInfinity, float.NegativeInfinity, float.NaN];
        foreach (float exponent in exponents)
        {
            CurveOperator curve = new(new IccCurveTagDataEntry(exponent), false);
            InlineArray16<float> actual = default;
            Span<float> values = actual;
            curve.Convert(Vector512.LoadUnsafe(ref inputs[0])).StoreUnsafe(ref MemoryMarshal.GetReference(values));
            AssertPowerValues(inputs, values, exponent);

            for (int i = 0; i < inputs.Length; i += Vector256<float>.Count)
            {
                curve.Convert(Vector256.LoadUnsafe(ref inputs[i])).StoreUnsafe(ref MemoryMarshal.GetReference(values), (uint)i);
            }

            AssertPowerValues(inputs, values, exponent);

            for (int i = 0; i < inputs.Length; i += Vector128<float>.Count)
            {
                curve.Convert(Vector128.LoadUnsafe(ref inputs[i])).StoreUnsafe(ref MemoryMarshal.GetReference(values), (uint)i);
            }

            AssertPowerValues(inputs, values, exponent);
        }
    }

    /// <summary>
    /// Checks each curve at 128, 256 and 512 bits, including the runtime's scalar intrinsic implementations.
    /// </summary>
    private static void CheckCurves()
    {
        foreach (object[] row in IccConversionDataTrc.CurveConversionTestData.Concat(IccConversionDataTrc.ParametricCurveConversionTestData))
        {
            CurveOperator curve = new((IccTagDataEntry)row[0], (bool)row[1]);
            float input = System.Convert.ToSingle(row[2]);
            float expected = System.Convert.ToSingle(row[3]);
            InlineArray16<float> actual = default;
            Span<float> values = actual;

            // Each store writes only its own width. Comparing every written lane also checks lane order and broadcasts.
            curve.Convert(Vector512.Create(input)).StoreUnsafe(ref MemoryMarshal.GetReference(values));
            foreach (float value in values)
            {
                Assert.Equal(expected, value, 4);
            }

            curve.Convert(Vector256.Create(input)).StoreUnsafe(ref MemoryMarshal.GetReference(values));
            foreach (float value in values[..Vector256<float>.Count])
            {
                Assert.Equal(expected, value, 4);
            }

            curve.Convert(Vector128.Create(input)).StoreUnsafe(ref MemoryMarshal.GetReference(values));
            foreach (float value in values[..Vector128<float>.Count])
            {
                Assert.Equal(expected, value, 4);
            }

            Assert.Equal(expected, curve.Convert(input), 4);
        }
    }

    /// <summary>
    /// Reuses the LUT tests' expected values, mixing inputs for the same table across a complete row.
    /// </summary>
    private static void CheckTables()
    {
        IEnumerable<object[]> data = IccConversionDataLutEntry.Lut8ConversionTestData
            .Concat(IccConversionDataLutEntry.Lut16ConversionTestData)
            .Concat(IccConversionDataLutAB.LutAToBConversionTestData)
            .Concat(IccConversionDataLutAB.LutBToAConversionTestData);

        foreach (IGrouping<object, object[]> group in data.GroupBy(row => row[0]))
        {
            LutColorOperator colorOperator = group.Key switch
            {
                IccLut8TagDataEntry entry => new(entry, false),
                IccLut16TagDataEntry entry => new(entry, false),
                IccLutAToBTagDataEntry entry => new(entry, false),
                IccLutBToATagDataEntry entry => new(entry, false),
                _ => throw new InvalidOperationException("Unexpected ICC fixture tag.")
            };

            IccColorConverter<LutColorOperator> converter = new(colorOperator);
            object[][] cases = group.ToArray();

            // 31 colors exercise sixteen-, eight- and four-lane blocks plus a three-color scalar tail.
            // The fixture period changes inputs within blocks, exposing channel and lane mixing.
            Vector4[] source = new Vector4[31];
            Vector4[] actual = new Vector4[source.Length + 1];
            Vector4 sentinel = new(-1F);
            actual[^1] = sentinel;
            for (int i = 0; i < source.Length; i++)
            {
                source[i] = (Vector4)cases[i % cases.Length][1];
            }

            converter.Convert(source, actual);
            for (int i = 0; i < source.Length; i++)
            {
                VectorAssert.Equal((Vector4)cases[i % cases.Length][2], actual[i], 4);
            }

            // ICC callers reuse their PCS row in place. Conversion must also leave storage beyond that row untouched.
            Assert.Equal(sentinel, actual[^1]);
            converter.Convert(source, source);
            for (int i = 0; i < source.Length; i++)
            {
                VectorAssert.Equal((Vector4)cases[i % cases.Length][2], source[i], 4);
            }
        }
    }

    /// <summary>
    /// Checks ICC gamma values and zero signs exactly; ordinary finite results use the dense gamma test's float error bound.
    /// </summary>
    /// <param name="inputs">The power bases.</param>
    /// <param name="actual">The SIMD results.</param>
    /// <param name="exponent">The tested exponent.</param>
    private static void AssertPowerValues(ReadOnlySpan<float> inputs, ReadOnlySpan<float> actual, float exponent)
    {
        for (int i = 0; i < inputs.Length; i++)
        {
            // A double reference rounds once to float. MathF.Pow can underflow too early:
            // Pow(2.9910849E-21F, 2.2F) returns zero, but its double result rounds to float.Epsilon.
            // LittleCMS limits negative ICC gamma inputs to zero, except for an identity curve.
            // A matrix can produce negative device values outside the target gamut. HDR companding has a separate signed contract.
            float expected = exponent == 1F ? inputs[i] : (float)Math.Pow(inputs[i] < 0F ? 0F : inputs[i], exponent);
            if (expected == 0F)
            {
                int expectedBits = BitConverter.SingleToInt32Bits(expected);
                int actualBits = BitConverter.SingleToInt32Bits(actual[i]);
                if (expectedBits != actualBits)
                {
                    Assert.Fail($"Pow({inputs[i]:R}, {exponent:R}): expected bits {expectedBits:X8}, actual bits {actualBits:X8}.");
                }
            }
            else if (!float.IsFinite(expected))
            {
                Assert.Equal(expected, actual[i]);
            }
            else
            {
                Assert.Equal(expected, actual[i], MathF.Abs(expected) * 0.000001F);
            }
        }
    }
}
