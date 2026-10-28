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
using static SixLabors.ImageSharp.ColorProfiles.ColorProfileConverterExtensionsIcc;
using static SixLabors.ImageSharp.ColorProfiles.Icc.IccColorConverter;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc;

/// <summary>
/// Checks ICC operators at each SIMD width and with hardware intrinsics disabled.
/// </summary>
public class IccColorConverterTests
{
    /// <summary>
    /// Checks a v2 curve-only source against LittleCMS without treating its curves as a perceptual lookup table.
    /// </summary>
    [Fact]
    public void V2CurveProfile_MatchesNativeBlackPointConversion() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckV2CurveProfile,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Converts RGB black through the real sRGB2014 and Fogra39 profiles at every execution width.
    /// </summary>
    private static void CheckV2CurveProfile()
    {
        ColorProfileConverter converter = new(new ColorConversionOptions
        {
            SourceIccProfile = TestIccProfiles.GetProfile(TestIccProfiles.StandardRgbV2),
            TargetIccProfile = TestIccProfiles.GetProfile(TestIccProfiles.Fogra39)
        });

        // LittleCMS, with perceptual intent and optimization/cache disabled, gives these CMYK amounts
        // for RGB black. Values use [0,1] ink units, not LittleCMS's [0,100] float CMYK units.
        // The largest measured difference was 1.30E-5; round upward to 2E-5 for single-precision differences.
        Vector4 expected = new(0.770153344F, 0.665583253F, 0.546013594F, 0.999984741F);
        ApproximateFloatComparer comparer = new(2E-5F);
        Rgb[] source = new Rgb[31];
        Cmyk[] actual = new Cmyk[source.Length];

        // Thirty-one pixels exercise all complete SIMD groups and the scalar remainder without changing the source color.
        converter.Convert<Rgb, Cmyk>(source, actual);
        foreach (Cmyk value in actual)
        {
            Assert.Equal(expected, value.ToScaledVector4(), comparer);
        }

        Assert.Equal(expected, converter.Convert<Rgb, Cmyk>(default(Rgb)).ToScaledVector4(), comparer);
    }

    /// <summary>
    /// Checks that floating-point PCS adjustment retains negative XYZ at every SIMD width and in the scalar fallback.
    /// </summary>
    [Fact]
    public void FloatingPcsConversion_RetainsNegativeXyz() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckFloatingPcsConversion,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Connects identical XYZ encodings through their forward and reverse perceptual black-point adjustments.
    /// </summary>
    private static void CheckFloatingPcsConversion()
    {
        IccProfile profile = CompactSrgbV4Profile.Profile;
        ConversionParams source = new(profile, true, IccRenderingIntent.Perceptual, IccInterpolationMethod.Auto);
        ConversionParams target = new(profile, false, IccRenderingIntent.Perceptual, IccInterpolationMethod.Auto);
        PcsOperator operation = new(source, target, new ColorConversionOptions());
        Vector4[] inputs = new Vector4[31];
        Vector4[] actual = new Vector4[inputs.Length];

        // The unbounded CMM model retains intermediate XYZ; PcsOperator links the original paper.
        // Inverse black-point adjustments must cancel, including for negative colors and values above one.
        // Thirty-one colors cover sixteen-, eight- and four-lane blocks plus a three-color scalar tail.
        for (int i = 0; i < inputs.Length; i++)
        {
            inputs[i] = new Vector4((i - 16) / 16F, (8 - i) / 16F, i / 16F, 1F);
        }

        new IccColorConverter<PcsOperator>(operation).Convert(inputs, actual);

        // The stored adjustment coefficients are rounded floats. Their forward/reverse composition
        // left at most 1.686E-7 error across scalar and SIMD paths. Round upward to 2E-7 for float differences.
        const float tolerance = 2E-7F;
        Assert.Equal(inputs, actual, new ApproximateFloatComparer(tolerance));
        for (int i = 0; i < inputs.Length; i++)
        {
            Assert.Equal(inputs[i], operation.Convert(inputs[i]), new ApproximateFloatComparer(tolerance));
        }
    }

    /// <summary>
    /// Checks that setup-time sRGB specialization preserves the general curve formula at every execution width.
    /// </summary>
    [Fact]
    public void SelectedSRgbCurves_MatchGeneralFormula() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckSelectedSRgbCurves,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Covers forward and inverse coefficients, HDR inputs, signed zero and exceptional values without changing the power calculation.
    /// </summary>
    private static void CheckSelectedSRgbCurves()
    {
        IccTagDataEntry[] entries =
        [
            IccConversionDataTrc.ParamCurve4,
            new IccParametricCurveTagDataEntry(new IccParametricCurve(3F, 1F, 0F, 1F, 0F))
        ];

        Random random = new(42);
        Vector4[] source = random.GenerateRandomVectorArray(129, -16F, 16F);
        source[0] = new Vector4(-0F, 0F, 1F, .5F);
        source[1] = new Vector4(float.NaN, float.PositiveInfinity, float.NegativeInfinity, .5F);

        foreach (IccTagDataEntry entry in entries)
        {
            foreach (bool inverse in new[] { false, true })
            {
                TrcOperator general = new([entry, entry, entry], inverse);
                Assert.True(general.TryGetSRgb(out SRgbTrcOperator selected));

                // Compare the same formula and power implementation. Specialization must only remove setup decisions.
                Vector4[] expected = new Vector4[source.Length];
                Vector4[] actual = new Vector4[source.Length];
                new IccColorConverter<TrcOperator>(general).Convert(source, expected);
                new IccColorConverter<SRgbTrcOperator>(selected).Convert(source, actual);
                Assert.Equal(expected, actual, new ApproximateFloatComparer(0F));
            }
        }
    }

    /// <summary>
    /// Checks packed integer interpolation against normalized float tables at every SIMD width.
    /// </summary>
    [Fact]
    public void EncodedCluts_MatchFloatTables() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckEncodedCluts,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Covers node widths, both integer precisions, grid boundaries, and both interpolation methods.
    /// </summary>
    private static void CheckEncodedCluts()
    {
        byte[][] grids = [[2], [2, 2], [2, 2, 2], [2, 2, 2, 2], [1, 1, byte.MaxValue]];
        ReadOnlySpan<float> levels = [0F, .25F, .5F, .75F, 1F];
        Random random = new(42);
        Vector4[] source = random.GenerateRandomVectorArray(129, -.25F, 1.25F);
        source[0] = Vector4.Zero;
        source[1] = Vector4.One;
        source[2] = new Vector4(float.NaN);
        source[3] = new Vector4(float.PositiveInfinity, float.NegativeInfinity, float.NaN, 1F);
        Vector4[] vertexSource = new Vector4[31];
        Vector4[] vertexActual = new Vector4[vertexSource.Length];

        foreach (byte[] grid in grids)
        {
            int nodes = grid.Aggregate(1, static (product, points) => product * points);
            for (int channels = 1; channels <= 4; channels++)
            {
                float[] values = new float[nodes * channels];
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = levels[i % levels.Length];
                }

                IccClut template = new(values, grid, IccClutDataType.Float, channels);
                foreach (bool sixteenBit in new[] { false, true })
                {
                    // Use the existing writer for sample encoding. No test-specific byte packing is needed.
                    using IccDataWriter writer = new();
                    _ = sixteenBit ? writer.WriteClut16(template) : writer.WriteClut8(template);
                    byte[] encoded = writer.GetData();
                    IccClutDataType type = sixteenBit ? IccClutDataType.UInt16 : IccClutDataType.UInt8;
                    IccClut materialized = new(encoded.AsMemory(), grid, type, channels);
                    float[] referenceValues = new float[materialized.SampleCount];
                    for (int i = 0; i < referenceValues.Length; i++)
                    {
                        referenceValues[i] = materialized.GetSample(i);
                    }

                    IccClut reference = new(referenceValues, grid, IccClutDataType.Float, channels);

                    foreach (bool trilinear in new[] { false, true })
                    {
                        // Keep the actual table encoded. The separate reference uses the established float interpolation.
                        IccClut packed = new(encoded.AsMemory(), grid, type, channels);
                        IccTagDataEntry[] inputCurves = Enumerable.Repeat<IccTagDataEntry>(IccConversionDataTrc.IdentityCurve, grid.Length).ToArray();
                        IccTagDataEntry[] outputCurves = Enumerable.Repeat<IccTagDataEntry>(IccConversionDataTrc.IdentityCurve, channels).ToArray();
                        IccLutAToBTagDataEntry entry = new(outputCurves, null, null, null, packed, inputCurves);
                        IccColorConverter<LutColorOperator> converter = new(new LutColorOperator(entry, trilinear));
                        ClutOperator expected = new(reference, trilinear);
                        Vector4[] actual = new Vector4[source.Length];
                        converter.Convert(source, actual);

                        for (int i = 0; i < source.Length; i++)
                        {
                            // Compare error directly. Decimal rounding can put adjacent floats on opposite sides of a decimal midpoint.
                            Assert.Equal(expected.Convert(source[i]), actual[i], new ApproximateFloatComparer(1E-6F));
                        }

                        // A homogeneous row at the first grid vertex must return that node. The stored samples
                        // provide the expected values directly, including zero for unused output components.
                        Vector4 firstNode = new(
                            referenceValues[0],
                            channels > 1 ? referenceValues[1] : 0F,
                            channels > 2 ? referenceValues[2] : 0F,
                            channels > 3 ? referenceValues[3] : 0F);

                        converter.Convert(vertexSource, vertexActual);
                        foreach (Vector4 value in vertexActual)
                        {
                            Assert.Equal(firstNode, value, new ApproximateFloatComparer(1E-6F));
                        }
                    }
                }
            }
        }
    }

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
    /// Checks that legacy matrices do not change Lab or RGB input, at each SIMD width and in the scalar fallback.
    /// </summary>
    [Fact]
    public void LegacyMatrices_IgnoreNonXyzInputs() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckLegacyMatrices,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Compares each legacy LUT with an equivalent LUT whose unused matrix is identity.
    /// </summary>
    private static void CheckLegacyMatrices()
    {
        IEnumerable<object[]> data = IccConversionDataLutEntry.Lut8ConversionTestData.Concat(IccConversionDataLutEntry.Lut16ConversionTestData);
        foreach (object[] test in data)
        {
            foreach (IccColorSpaceType inputColorSpace in new[] { IccColorSpaceType.CieLab, IccColorSpaceType.Rgb })
            {
                // ICC.1 sections 10.10 and 10.11 permit this matrix only for XYZ input.
                // Removing the matrix must therefore leave every Lab or RGB result unchanged.
                SampledLutColorOperator actualOperator;
                SampledLutColorOperator identityOperator;
                if (test[0] is IccLut8TagDataEntry lut8)
                {
                    actualOperator = new(lut8, inputColorSpace, false);
                    IccLut8TagDataEntry identity = new(lut8.InputValues, lut8.ClutValues, lut8.OutputValues);
                    identityOperator = new(identity, inputColorSpace, false);
                }
                else
                {
                    IccLut16TagDataEntry lut16 = (IccLut16TagDataEntry)test[0];
                    actualOperator = new(lut16, inputColorSpace, false);
                    IccLut16TagDataEntry identity = new(lut16.InputValues, lut16.ClutValues, lut16.OutputValues);
                    identityOperator = new(identity, inputColorSpace, false);
                }

                // 31 samples traverse sixteen-, eight- and four-lane blocks, then a three-sample scalar remainder.
                Vector4[] source = Enumerable.Repeat((Vector4)test[1], 31).ToArray();
                Vector4[] actual = new Vector4[source.Length];
                IccColorConverter<SampledLutColorOperator> converter = new(actualOperator);
                converter.Convert(source, actual);

                for (int i = 0; i < source.Length; i++)
                {
                    Vector4 expected = identityOperator.Convert(source[i]);
                    VectorAssert.Equal(expected, actual[i], 4);
                    VectorAssert.Equal(expected, actualOperator.Convert(source[i]), 4);
                }
            }
        }
    }

    /// <summary>
    /// Checks scalar/SIMD agreement across every normalized sixteen-bit code, including extreme ICC exponents.
    /// </summary>
    [Fact]
    public void Gamma_MatchesScalarAcrossSixteenBitCodes() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckGamma,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Uses the scalar ICC curve as the execution reference. Separate formula tests check approximation accuracy.
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
                        // ICC permits exponents up to 255. Compare the same curve across widths without imposing
                        // MathF.Pow accuracy on the selected approximation. Preserve the tight scalar/SIMD bound.
                        Assert.Equal(curve.Convert(input[i + lane]), values[lane], new ApproximateFloatComparer(1E-6F));
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
    /// Checks the largest grid index and constant axes before SIMD table gathers.
    /// </summary>
    [Fact]
    public void Cluts_HandleGridBounds() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckClutGridBounds,
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
                    Vector4 bounded = ColorNumerics.ClampToFinite(source[i], Vector4.Zero, Vector4.One);
                    Vector3 fractions = dimensions == 3 ? new(bounded.X, bounded.Y, bounded.Z) : new(bounded.Y, bounded.Z, bounded.W);
                    float weight = trilinear ? fractions.X * fractions.Y * fractions.Z : MathF.Min(fractions.X, MathF.Min(fractions.Y, fractions.Z));
                    float expected = dimensions == 3 ? weight : weight + (((0.25F + (0.5F * weight)) - weight) * bounded.X);
                    VectorAssert.Equal(new Vector4(expected, 0F, 0F, 0F), actual[i], 5);
                }
            }
        }
    }

    /// <summary>
    /// Uses an identity axis with the largest byte-sized grid and two single-node axes.
    /// </summary>
    private static void CheckClutGridBounds()
    {
        float[] values = new float[byte.MaxValue];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = i / (float)(values.Length - 1);
        }

        // Only X varies. The other axes have one node, so their index and stride must remain zero.
        IccClut clut = new(values, [byte.MaxValue, 1, 1], IccClutDataType.Float, 1);
        IccTagDataEntry[] inputCurves = Enumerable.Repeat<IccTagDataEntry>(IccConversionDataTrc.IdentityCurve, 3).ToArray();
        IccLutAToBTagDataEntry entry = new([IccConversionDataTrc.IdentityCurve], null, null, null, clut, inputCurves);
        ReadOnlySpan<float> samples = [0F, 1F, .25F, .5F, .75F, -.25F, 1.25F, float.NaN, float.PositiveInfinity,
            float.NegativeInfinity, MathF.BitDecrement(1F)];

        foreach (bool trilinear in new[] { false, true })
        {
            IccColorConverter<LutColorOperator> converter = new(new LutColorOperator(entry, trilinear));

            // Thirty-one colors exercise a 512-bit block, narrower blocks, and a scalar remainder.
            Vector4[] source = new Vector4[31];
            Vector4[] actual = new Vector4[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                source[i] = new Vector4(samples[i % samples.Length]);
            }

            converter.Convert(source, actual);
            for (int i = 0; i < source.Length; i++)
            {
                // The identity ramp returns bounded X; NaN follows the existing clamp rule and becomes zero.
                float expected = ColorNumerics.ClampToFinite(source[i].X, 0F, 1F);
                VectorAssert.Equal(new Vector4(expected, 0F, 0F, 0F), actual[i], 5);
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
                    // The forward square is exact; the inverse square root uses the measured approximation bound.
                    ApproximateFloatComparer comparer = new(toPcs ? 1E-5F : 5E-4F);
                    Assert.Equal(expected[i], actual[i], comparer);
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
    /// Checks scalar and SIMD agreement throughout the normal float range, including each mantissa interval's upper edge.
    /// </summary>
    [Fact]
    public void Gamma_MatchesScalarAcrossBinaryRanges() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
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
            InlineArray16<float> reference = default;
            Span<float> inputs = input;
            Span<float> actual = output;
            Span<float> expected = reference;
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
                        expected[lane] = curve.Convert(inputs[lane]);
                    }

                    curve.Convert(Vector512.LoadUnsafe(ref inputs[0])).StoreUnsafe(ref actual[0]);

                    // Mathematical accuracy is measured separately over the recorded native comparison range.
                    // Here every width must match scalar conversion, including subnormal results and overflow.
                    Assert.Equal<float>(expected, actual);

                    for (int i = 0; i < inputs.Length; i += Vector256<float>.Count)
                    {
                        curve.Convert(Vector256.LoadUnsafe(ref inputs[i])).StoreUnsafe(ref actual[0], (uint)i);
                    }

                    Assert.Equal<float>(expected, actual);

                    for (int i = 0; i < inputs.Length; i += Vector128<float>.Count)
                    {
                        curve.Convert(Vector128.LoadUnsafe(ref inputs[i])).StoreUnsafe(ref actual[0], (uint)i);
                    }

                    Assert.Equal<float>(expected, actual);
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
    /// Checks each curve at the supported SIMD widths and through scalar conversion.
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

            // Direct operators require a supported width, as in shared conversion dispatch.
            // Each store writes only its own width; compare every written lane to check lane order and broadcasts.
            if (Vector512.IsHardwareAccelerated)
            {
                curve.Convert(Vector512.Create(input)).StoreUnsafe(ref MemoryMarshal.GetReference(values));
                foreach (float value in values)
                {
                    Assert.Equal(expected, value, 4);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                curve.Convert(Vector256.Create(input)).StoreUnsafe(ref MemoryMarshal.GetReference(values));
                foreach (float value in values[..Vector256<float>.Count])
                {
                    Assert.Equal(expected, value, 4);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                curve.Convert(Vector128.Create(input)).StoreUnsafe(ref MemoryMarshal.GetReference(values));
                foreach (float value in values[..Vector128<float>.Count])
                {
                    Assert.Equal(expected, value, 4);
                }
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
            IIccColorConverter converter = group.Key switch
            {
                IccLut8TagDataEntry entry => new IccColorConverter<SampledLutColorOperator>(new(entry, IccColorSpaceType.CieXyz, false)),
                IccLut16TagDataEntry entry => new IccColorConverter<SampledLutColorOperator>(new(entry, IccColorSpaceType.CieXyz, false)),
                IccLutAToBTagDataEntry entry => new IccColorConverter<LutColorOperator>(new(entry, false)),
                IccLutBToATagDataEntry entry => new IccColorConverter<LutColorOperator>(new(entry, false)),
                _ => throw new InvalidOperationException("Unexpected ICC fixture tag.")
            };

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
                // Retain the recorded approximation's relative bound. One subnormal float step covers rounding
                // where the relative bound is smaller than the spacing between representable values.
                float tolerance = MathF.Max(float.Epsilon, MathF.Abs(expected) * 5E-4F);
                Assert.Equal(expected, actual[i], tolerance);
            }
        }
    }
}
