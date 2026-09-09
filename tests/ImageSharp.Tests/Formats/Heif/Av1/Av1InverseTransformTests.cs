// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 forward and inverse transform reconstruction across supported sizes, precisions, and intrinsic tiers.
/// </summary>
[Trait("Format", "Avif")]
public class Av1InverseTransformTests
{
    /// <summary>
    /// The hardware configurations covering every transform SIMD tier and the scalar fallback.
    /// </summary>
    private const HwIntrinsics TransformConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies sparse scalar and SIMD kernels against complete scalar transforms with poisoned unused storage.
    /// </summary>
    [Fact]
    public void SparseOperatorsMatchFullTransforms()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertSparseOperatorsMatchFullTransforms, TransformConfigurations);

    /// <summary>
    /// Verifies the coefficient bounds against every position in each permitted scan prefix.
    /// </summary>
    [Theory]
    [MemberData(nameof(Av1ForwardTransformTests.ValidTransformCases), MemberType = typeof(Av1ForwardTransformTests))]
    public void SparseBoundsContainEveryCodedCoefficient(int transformTypeValue, int transformSizeValue, int bitDepth)
    {
        Av1TransformType transformType = (Av1TransformType)transformTypeValue;
        Av1TransformSize transformSize = (Av1TransformSize)transformSizeValue;
        int stride = transformSize.GetAdjusted().GetWidth();
        ReadOnlySpan<short> scan = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType).Scan;
        int lastColumn = 0;
        int lastRow = 0;
        for (int index = 0; index < scan.Length; index++)
        {
            lastColumn = Math.Max(lastColumn, scan[index] % stride);
            lastRow = Math.Max(lastRow, scan[index] / stride);
            Av1Transform2dFlipConfiguration config = Av1Transform2dFlipConfiguration.CreateInverse(transformType, transformSize, bitDepth);
            config.ConfigureInverseSparsity(index + 1, bitDepth);
            Assert.True(lastColumn < config.NonzeroWidth, $"Horizontal bound at EOB {index + 1} excludes column {lastColumn}.");
            Assert.True(lastRow < config.NonzeroHeight, $"Vertical bound at EOB {index + 1} excludes row {lastRow}.");
        }
    }

    /// <summary>
    /// Exercises every supported sparse operator family in each hardware configuration.
    /// </summary>
    private static void AssertSparseOperatorsMatchFullTransforms()
    {
        AssertSparseOperatorParity<Av1Inverse2dTransformer.Dct8Low1Operator, Av1Inverse2dTransformer.Dct8Operator>(8);
        AssertSparseOperatorParity<Av1Inverse2dTransformer.Dct16Low1Operator, Av1Inverse2dTransformer.Dct16Operator>(16);
        AssertSparseOperatorParity<Av1Inverse2dTransformer.Dct16Low8Operator, Av1Inverse2dTransformer.Dct16Operator>(16);
        AssertSparseOperatorParity<Av1Inverse2dTransformer.Dct32Low1Operator, Av1Inverse2dTransformer.Dct32Operator>(32);
        AssertSparseOperatorParity<Av1Inverse2dTransformer.Dct32Low8Operator, Av1Inverse2dTransformer.Dct32Operator>(32);
        AssertSparseOperatorParity<Av1Inverse2dTransformer.Dct32Low16Operator, Av1Inverse2dTransformer.Dct32Operator>(32);
        AssertSparseOperatorParity<Av1Inverse2dTransformer.Dct64Low1Operator, Av1Inverse2dTransformer.Dct64Operator>(64);
        AssertSparseOperatorParity<Av1Inverse2dTransformer.Dct64Low8Operator, Av1Inverse2dTransformer.Dct64Operator>(64);
        AssertSparseOperatorParity<Av1Inverse2dTransformer.Dct64Low16Operator, Av1Inverse2dTransformer.Dct64Operator>(64);
        AssertSparseOperatorParity<Av1Inverse2dTransformer.Dct64Low32Operator, Av1Inverse2dTransformer.Dct64Operator>(64);
        AssertSparseOperatorParity<Av1Inverse2dTransformer.Adst8Low1Operator, Av1Inverse2dTransformer.Adst8Operator>(8);
        AssertSparseOperatorParity<Av1Inverse2dTransformer.Adst16Low1Operator, Av1Inverse2dTransformer.Adst16Operator>(16);
        AssertSparseOperatorParity<Av1Inverse2dTransformer.Adst16Low8Operator, Av1Inverse2dTransformer.Adst16Operator>(16);
    }

    /// <summary>
    /// Compares independent SIMD lanes and scalar output with the complete transform of the same coefficient prefix.
    /// </summary>
    /// <typeparam name="TSparse">The sparse operator being checked.</typeparam>
    /// <typeparam name="TFull">The complete scalar operator used for the arithmetic comparison.</typeparam>
    /// <param name="length">The transform axis length.</param>
    private static void AssertSparseOperatorParity<TSparse, TFull>(int length)
        where TSparse : struct, Av1Inverse2dTransformer.IAv1Transform1dOperator
        where TFull : struct, Av1Inverse2dTransformer.IAv1Transform1dOperator
    {
        const int cosBit = 12;
        int[] input = new int[length];
        int[] expected = new int[length];
        int[] actual = new int[length];
        int[] scalarStep = new int[length];
        Av1TransformVector<Vector128<int>> input128 = default;
        Av1TransformVector<Vector128<int>> output128 = default;
        Av1TransformVector<Vector256<int>> input256 = default;
        Av1TransformVector<Vector256<int>> output256 = default;

        foreach (byte range in new byte[] { 16, 18, 20 })
        {
            InlineArray12<byte> stageRange = default;
            for (int index = 0; index < Av1Transform2dFlipConfiguration.MaxStageNumber; index++)
            {
                stageRange[index] = range;
            }

            for (int index = 0; index < length; index++)
            {
                input256[index] = Vector256.Create(
                    GetInputValue(index, 0),
                    GetInputValue(index, 1),
                    GetInputValue(index, 2),
                    GetInputValue(index, 3),
                    GetInputValue(index, 4),
                    GetInputValue(index, 5),
                    GetInputValue(index, 6),
                    GetInputValue(index, 7));

                input128[index] = input256[index].GetLower();
                output128[index] = Vector128.Create(int.MinValue);
                output256[index] = Vector256.Create(int.MinValue);
            }

            // Unused input positions remain nonzero. Only the selected low-frequency prefix may affect the result,
            // and aliased input/stage storage exposes a network that overwrites a coefficient before consuming it.
            TSparse.Transform(ref input128, ref output128, ref input128, cosBit, stageRange);
            TSparse.Transform(ref input256, ref output256, ref input256, cosBit, stageRange);
            for (int lane = 0; lane < Vector256<int>.Count; lane++)
            {
                for (int index = 0; index < length; index++)
                {
                    input[index] = index < TSparse.InputLength ? GetInputValue(index, lane) : 0;
                }

                Array.Fill(scalarStep, int.MinValue);
                TFull.Transform(input, expected, scalarStep, cosBit, stageRange);
                for (int index = TSparse.InputLength; index < length; index++)
                {
                    input[index] = GetInputValue(index, lane);
                }

                Array.Fill(actual, int.MinValue);
                TSparse.Transform(input, actual, input, cosBit, stageRange);
                Assert.Equal(expected, actual);
                for (int index = 0; index < length; index++)
                {
                    Assert.Equal(expected[index], output256[index].GetElement(lane));
                    if (lane < Vector128<int>.Count)
                    {
                        Assert.Equal(expected[index], output128[index].GetElement(lane));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Verifies DCT operator parity across the supported hardware feature levels.
    /// </summary>
    [Fact]
    public void DctOperatorsProduceIdenticalScalarAndSimdResults()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertDctOperatorParity, TransformConfigurations);

    /// <summary>
    /// Verifies ADST operator parity across the supported hardware feature levels.
    /// </summary>
    [Fact]
    public void AdstOperatorsProduceIdenticalScalarAndSimdResults()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertAdstOperatorParity, TransformConfigurations);

    /// <summary>
    /// Verifies identity operator parity across the supported hardware feature levels.
    /// </summary>
    [Fact]
    public void IdentityOperatorsProduceIdenticalScalarAndSimdResults()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertIdentityOperatorParity, TransformConfigurations);

    /// <summary>
    /// Verifies the reference widened operations at the twelve-bit inverse row-stage bounds.
    /// </summary>
    [Fact]
    public void TwelveBitWideIntermediatesMatchReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertTwelveBitWideIntermediateParity, TransformConfigurations);

    /// <summary>
    /// Verifies the inverse DCT operators against their scalar implementations.
    /// </summary>
    private static void AssertDctOperatorParity()
    {
        AssertOperatorParity<Av1Inverse2dTransformer.Dct4Operator>(4);
        AssertOperatorParity<Av1Inverse2dTransformer.Dct8Operator>(8);
        AssertOperatorParity<Av1Inverse2dTransformer.Dct16Operator>(16);
        AssertOperatorParity<Av1Inverse2dTransformer.Dct32Operator>(32);
        AssertOperatorParity<Av1Inverse2dTransformer.Dct64Operator>(64);
    }

    /// <summary>
    /// Verifies the inverse ADST operators against their scalar implementations.
    /// </summary>
    private static void AssertAdstOperatorParity()
    {
        AssertOperatorParity<Av1Inverse2dTransformer.Adst4Operator>(4);
        AssertOperatorParity<Av1Inverse2dTransformer.Adst8Operator>(8);
        AssertOperatorParity<Av1Inverse2dTransformer.Adst16Operator>(16);
    }

    /// <summary>
    /// Verifies the inverse identity operators against their scalar implementations.
    /// </summary>
    private static void AssertIdentityOperatorParity()
    {
        AssertOperatorParity<Av1Inverse2dTransformer.Identity4Operator>(4);
        AssertOperatorParity<Av1Inverse2dTransformer.Identity8Operator>(8);
        AssertOperatorParity<Av1Inverse2dTransformer.Identity16Operator>(16);
        AssertOperatorParity<Av1Inverse2dTransformer.Identity32Operator>(32);
    }

    /// <summary>
    /// Exercises the exact ADST4 rounding and identity-product overflows that are possible at a twenty-bit row range.
    /// </summary>
    private static void AssertTwelveBitWideIntermediateParity()
    {
        const int cosBit = 12;
        InlineArray12<byte> stageRange = default;
        for (int index = 0; index < Av1Transform2dFlipConfiguration.MaxStageNumber; index++)
        {
            stageRange[index] = 20;
        }

        Av1TransformVector<Vector128<int>> adstInput128 = default;
        adstInput128.V0 = Vector128.Create(196_118, -196_118, 196_117, -196_117);
        adstInput128.V1 = Vector128.Create(196_117, -196_117, 196_117, -196_117);
        adstInput128.V2 = Vector128.Create(196_117, -196_117, 196_117, -196_117);
        adstInput128.V3 = Vector128.Create(196_117, -196_117, 196_117, -196_117);
        Av1TransformVector<Vector256<int>> adstInput256 = default;
        adstInput256.V0 = Vector256.Create(196_118, -196_118, 196_117, -196_117, 196_118, -196_118, 196_117, -196_117);
        adstInput256.V1 = Vector256.Create(196_117, -196_117, 196_117, -196_117, 196_117, -196_117, 196_117, -196_117);
        adstInput256.V2 = adstInput256.V1;
        adstInput256.V3 = adstInput256.V1;
        Av1TransformVector<Vector128<int>> adstOutput128 = default;
        Av1TransformVector<Vector128<int>> adstStep128 = default;
        Av1TransformVector<Vector256<int>> adstOutput256 = default;
        Av1TransformVector<Vector256<int>> adstStep256 = default;

        Av1Inverse2dTransformer.Adst4Operator.Transform(
            ref adstInput128,
            ref adstOutput128,
            ref adstStep128,
            cosBit,
            stageRange);

        Av1Inverse2dTransformer.Adst4Operator.Transform(
            ref adstInput256,
            ref adstOutput256,
            ref adstStep256,
            cosBit,
            stageRange);

        // These are the exact outputs of the reference decoder's signed Int64 terminal round. The first positive lane has an
        // Int32 fixed-point sum of 2,147,482,471, so adding the 2,048 rounding bias in Int32 would wrap.
        Vector128<int> adstExpected0 = Vector128.Create(524_288, -524_288, 524_287, -524_287);
        Vector128<int> adstExpected1 = Vector128.Create(33_612, -33_612, 33_612, -33_612);
        Vector128<int> adstExpected2 = Vector128.Create(160_112, -160_112, 160_111, -160_111);
        Vector128<int> adstExpected3 = Vector128.Create(77_567, -77_567, 77_566, -77_566);
        Assert.Equal(adstExpected0, adstOutput128.V0);
        Assert.Equal(adstExpected1, adstOutput128.V1);
        Assert.Equal(adstExpected2, adstOutput128.V2);
        Assert.Equal(adstExpected3, adstOutput128.V3);
        Assert.Equal(Vector256.Create(adstExpected0, adstExpected0), adstOutput256.V0);
        Assert.Equal(Vector256.Create(adstExpected1, adstExpected1), adstOutput256.V1);
        Assert.Equal(Vector256.Create(adstExpected2, adstExpected2), adstOutput256.V2);
        Assert.Equal(Vector256.Create(adstExpected3, adstExpected3), adstOutput256.V3);

        Vector128<int> identityInput128 = Vector128.Create(524_287, -524_288, 524_286, -524_287);
        Vector256<int> identityInput256 = Vector256.Create(
            524_287,
            -524_288,
            524_286,
            -524_287,
            370_727,
            -370_728,
            262_143,
            -262_144);

        AssertWidenedIdentityOperator<Av1Inverse2dTransformer.Identity4Operator>(
            4,
            identityInput128,
            Vector128.Create(741_503, -741_504, 741_501, -741_503),
            identityInput256,
            Vector256.Create(741_503, -741_504, 741_501, -741_503, 524_322, -524_323, 370_751, -370_752),
            stageRange);

        AssertWidenedIdentityOperator<Av1Inverse2dTransformer.Identity16Operator>(
            16,
            identityInput128,
            Vector128.Create(1_483_005, -1_483_008, 1_483_002, -1_483_005),
            identityInput256,
            Vector256.Create(1_483_005, -1_483_008, 1_483_002, -1_483_005, 1_048_643, -1_048_646, 741_501, -741_504),
            stageRange);
    }

    /// <summary>
    /// Verifies one identity operator against exact reference widened fixed-point results.
    /// </summary>
    /// <typeparam name="TOperator">The inverse identity operator.</typeparam>
    /// <param name="length">The identity-transform length.</param>
    /// <param name="input128">The four-lane bounded input.</param>
    /// <param name="expected128">The exact four-lane result.</param>
    /// <param name="input256">The eight-lane bounded input.</param>
    /// <param name="expected256">The exact eight-lane result.</param>
    /// <param name="stageRange">The twelve-bit inverse row-stage range.</param>
    private static void AssertWidenedIdentityOperator<TOperator>(
        int length,
        Vector128<int> input128,
        Vector128<int> expected128,
        Vector256<int> input256,
        Vector256<int> expected256,
        InlineArray12<byte> stageRange)
        where TOperator : struct, Av1Inverse2dTransformer.IAv1Transform1dOperator
    {
        const int cosBit = 12;
        Av1TransformVector<Vector128<int>> values128 = default;
        Av1TransformVector<Vector128<int>> output128 = default;
        Av1TransformVector<Vector128<int>> step128 = default;
        Av1TransformVector<Vector256<int>> values256 = default;
        Av1TransformVector<Vector256<int>> output256 = default;
        Av1TransformVector<Vector256<int>> step256 = default;

        for (int index = 0; index < length; index++)
        {
            values128[index] = input128;
            values256[index] = input256;
        }

        TOperator.Transform(ref values128, ref output128, ref step128, cosBit, stageRange);
        TOperator.Transform(ref values256, ref output256, ref step256, cosBit, stageRange);

        for (int index = 0; index < length; index++)
        {
            Assert.Equal(expected128, output128[index]);
            Assert.Equal(expected256, output256[index]);
        }
    }

    [Theory]
    [InlineData((int)Av1TransformSize.Size4x4, 0, -4)]
    [InlineData((int)Av1TransformSize.Size8x8, -1, -4)]
    [InlineData((int)Av1TransformSize.Size16x16, -2, -4)]
    [InlineData((int)Av1TransformSize.Size32x32, -2, -4)]
    [InlineData((int)Av1TransformSize.Size64x64, -2, -4)]
    [InlineData((int)Av1TransformSize.Size4x8, 0, -4)]
    [InlineData((int)Av1TransformSize.Size8x4, 0, -4)]
    [InlineData((int)Av1TransformSize.Size8x16, -1, -4)]
    [InlineData((int)Av1TransformSize.Size16x8, -1, -4)]
    [InlineData((int)Av1TransformSize.Size16x32, -1, -4)]
    [InlineData((int)Av1TransformSize.Size32x16, -1, -4)]
    [InlineData((int)Av1TransformSize.Size32x64, -1, -4)]
    [InlineData((int)Av1TransformSize.Size64x32, -1, -4)]
    [InlineData((int)Av1TransformSize.Size4x16, -1, -4)]
    [InlineData((int)Av1TransformSize.Size16x4, -1, -4)]
    [InlineData((int)Av1TransformSize.Size8x32, -2, -4)]
    [InlineData((int)Av1TransformSize.Size32x8, -2, -4)]
    [InlineData((int)Av1TransformSize.Size16x64, -2, -4)]
    [InlineData((int)Av1TransformSize.Size64x16, -2, -4)]
    public void InverseConfigurationUsesNormativeShifts(int transformSizeValue, int firstShift, int secondShift)
    {
        Av1TransformSize transformSize = (Av1TransformSize)transformSizeValue;
        Av1Transform2dFlipConfiguration config = Av1Transform2dFlipConfiguration.CreateInverse(Av1TransformType.DctDct, transformSize, 8);

        Assert.Equal(firstShift, config.Shift0);
        Assert.Equal(secondShift, config.Shift1);
        Assert.Equal(0, config.Shift2);
        Assert.Equal(12, config.CosBitColumn);
        Assert.Equal(12, config.CosBitRow);
    }

    [Theory]
    [InlineData(8, 16, 16)]
    [InlineData(10, 18, 16)]
    [InlineData(12, 20, 18)]
    public void InverseConfigurationUsesNormativeStageRanges(int bitDepth, byte rowRange, byte columnRange)
    {
        Av1Transform2dFlipConfiguration config = Av1Transform2dFlipConfiguration.CreateInverse(
            Av1TransformType.AdstAdst,
            Av1TransformSize.Size16x16,
            bitDepth);

        InlineArray12<byte> configuredRowRange = config.StageRangeRow;
        InlineArray12<byte> configuredColumnRange = config.StageRangeColumn;

        for (int index = 0; index < config.StageNumberRow; index++)
        {
            Assert.Equal(rowRange, configuredRowRange[index]);
        }

        for (int index = 0; index < config.StageNumberColumn; index++)
        {
            Assert.Equal(columnRange, configuredColumnRange[index]);
        }
    }

    [Fact]
    public void ForwardAndInverseOperatorPairsReconstructTheirInput()
    {
        AssertRoundTrip<Av1ForwardTransformer.Dct4Operator, Av1Inverse2dTransformer.Dct4Operator>(Av1TransformType.DctDct, Av1TransformSize.Size4x4, 1, 1);
        AssertRoundTrip<Av1ForwardTransformer.Dct8Operator, Av1Inverse2dTransformer.Dct8Operator>(Av1TransformType.DctDct, Av1TransformSize.Size8x8, 2, 2);
        AssertRoundTrip<Av1ForwardTransformer.Dct16Operator, Av1Inverse2dTransformer.Dct16Operator>(Av1TransformType.DctDct, Av1TransformSize.Size16x16, 3, 3);
        AssertRoundTrip<Av1ForwardTransformer.Dct32Operator, Av1Inverse2dTransformer.Dct32Operator>(Av1TransformType.DctDct, Av1TransformSize.Size32x32, 4, 4);
        AssertRoundTrip<Av1ForwardTransformer.Dct64Operator, Av1Inverse2dTransformer.Dct64Operator>(Av1TransformType.DctDct, Av1TransformSize.Size64x64, 5, 5);
        AssertRoundTrip<Av1ForwardTransformer.Adst4Operator, Av1Inverse2dTransformer.Adst4Operator>(Av1TransformType.AdstAdst, Av1TransformSize.Size4x4, 1, 1);
        AssertRoundTrip<Av1ForwardTransformer.Adst8Operator, Av1Inverse2dTransformer.Adst8Operator>(Av1TransformType.AdstAdst, Av1TransformSize.Size8x8, 2, 2);
        AssertRoundTrip<Av1ForwardTransformer.Adst16Operator, Av1Inverse2dTransformer.Adst16Operator>(Av1TransformType.AdstAdst, Av1TransformSize.Size16x16, 3, 3);
        AssertRoundTrip<Av1ForwardTransformer.Identity4Operator, Av1Inverse2dTransformer.Identity4Operator>(Av1TransformType.Identity, Av1TransformSize.Size4x4, 1, 1);
        AssertRoundTrip<Av1ForwardTransformer.Identity8Operator, Av1Inverse2dTransformer.Identity8Operator>(Av1TransformType.Identity, Av1TransformSize.Size8x8, 2, 1);
        AssertRoundTrip<Av1ForwardTransformer.Identity16Operator, Av1Inverse2dTransformer.Identity16Operator>(Av1TransformType.Identity, Av1TransformSize.Size16x16, 3, 1);
        AssertRoundTrip<Av1ForwardTransformer.Identity32Operator, Av1Inverse2dTransformer.Identity32Operator>(Av1TransformType.Identity, Av1TransformSize.Size32x32, 4, 1);
    }

    /// <summary>
    /// Verifies that every applicable SIMD traversal reconstructs the same samples as the scalar traversal.
    /// </summary>
    /// <param name="transformTypeValue">The integral <see cref="Av1TransformType"/> value.</param>
    /// <param name="transformSizeValue">The integral <see cref="Av1TransformSize"/> value.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    [Theory]
    [MemberData(nameof(Av1ForwardTransformTests.ValidTransformCases), MemberType = typeof(Av1ForwardTransformTests))]
    public void TwoDimensionalKernelsMatchReference(
        int transformTypeValue,
        int transformSizeValue,
        int bitDepth)
    {
        Av1TransformType transformType = (Av1TransformType)transformTypeValue;
        Av1TransformSize transformSize = (Av1TransformSize)transformSizeValue;
        Av1Transform2dFlipConfiguration config = Av1Transform2dFlipConfiguration.CreateInverse(transformType, transformSize, bitDepth);
        DispatchColumn(transformType, transformSize, bitDepth, ref config);
    }

    /// <summary>
    /// Verifies lossless inverse Walsh-Hadamard reconstruction against an independent definition.
    /// </summary>
    [Fact]
    public void LosslessWalshHadamardMatchesReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertLosslessWalshHadamardParity, TransformConfigurations);

    /// <summary>
    /// Exercises DC-only and complete lossless blocks at every supported sample precision.
    /// </summary>
    private static void AssertLosslessWalshHadamardParity()
    {
        const int stride = 7;
        int[] workspace = new int[Av1TransformWorkspace.MaximumLength];
        int[][] coefficientCases =
        [
            [512, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            [-516, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            [320, -192, 64, -448, 128, 256, -320, 96, -224, 160, 384, -128, 448, -64, -256, 192]
        ];

        for (int coefficientCase = 0; coefficientCase < coefficientCases.Length; coefficientCase++)
        {
            int[] coefficients = coefficientCases[coefficientCase];
            int coefficientCount = coefficientCase < 2 ? 1 : coefficients.Length;
            byte[] expectedBytes = new byte[stride * 4];

            Array.Fill(expectedBytes, (byte)233);

            PopulatePrediction(expectedBytes, stride, byte.MaxValue);
            byte[] actualBytes = (byte[])expectedBytes.Clone();

            ApplyWalshHadamardReference(coefficients, expectedBytes, stride, coefficientCount, 8);
            Av1InverseTransformer.Reconstruct8Bit(
                coefficients,
                actualBytes,
                stride,
                Av1TransformSize.Size4x4,
                Av1TransformType.DctDct,
                0,
                coefficientCount,
                true,
                workspace);

            Assert.Equal(expectedBytes, actualBytes);

            foreach (int bitDepth in new[] { 10, 12 })
            {
                int maximum = (1 << bitDepth) - 1;
                short[] expected = new short[stride * 4];

                Array.Fill(expected, (short)-1);

                PopulatePrediction(expected, stride, maximum);
                short[] actual = (short[])expected.Clone();

                ApplyWalshHadamardReference(coefficients, expected, stride, coefficientCount, bitDepth);
                Av1InverseTransformer.ReconstructHighBitDepth(
                    coefficients,
                    actual,
                    stride,
                    Av1TransformSize.Size4x4,
                    Av1TransformType.DctDct,
                    0,
                    coefficientCount,
                    true,
                    bitDepth == 10 ? Av1BitDepth.TenBit : Av1BitDepth.TwelveBit,
                    workspace);

                Assert.Equal(expected, actual);
            }
        }
    }

    [Fact]
    public void ReconstructionDispatchDoesNotAllocatePerBlock()
    {
        const int width = 8;
        int[] coefficients = new int[width * width];
        byte[] reconstruction = new byte[coefficients.Length];
        int[] workspace = new int[Av1TransformWorkspace.MaximumLength];

        Av1InverseTransformer.Reconstruct8Bit(
            coefficients, reconstruction, width, Av1TransformSize.Size8x8, Av1TransformType.DctDct, 0, coefficients.Length, false, workspace);

        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int iteration = 0; iteration < 32; iteration++)
        {
            Av1InverseTransformer.Reconstruct8Bit(
                coefficients, reconstruction, width, Av1TransformSize.Size8x8, Av1TransformType.DctDct, 0, coefficients.Length, false, workspace);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    /// <summary>
    /// Verifies DC-only byte reconstruction against the full transform for every size, signed rounding, clipping, and padded layout.
    /// </summary>
    /// <param name="inPlace">Whether prediction and reconstruction share their storage.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DcOnlyByteReconstructionMatchesFullTransform(bool inPlace)
    {
        ReadOnlySpan<int> dcValues = [-32768, -4095, -1025, -65, -33, -32, -31, -17, -1, 0, 1, 17, 31, 32, 33, 65, 1025, 4095, 32767];
        int[] workspace = new int[Av1TransformWorkspace.MaximumLength];
        for (int size = 0; size < (int)Av1TransformSize.AllSizes; size++)
        {
            Av1TransformSize transformSize = (Av1TransformSize)size;
            Av1Transform2dFlipConfiguration config = Av1Transform2dFlipConfiguration.CreateInverse(Av1TransformType.DctDct, transformSize, 8);
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            int readStride = width + 3;
            int writeStride = inPlace ? readStride : width + 7;
            int[] coefficients = new int[transformSize.GetAdjusted().GetSize2d()];
            byte[] prediction = new byte[readStride * height];
            byte[] expected = new byte[writeStride * height];
            byte[] actual = new byte[expected.Length];
            for (int i = 0; i < prediction.Length; i++)
            {
                prediction[i] = (byte)(i * 47);
            }

            Av1TransformFunctionParameters parameters = new()
            {
                TransformSize = transformSize,
                TransformType = Av1TransformType.DctDct,
                BitDepth = 8,
                EndOfBuffer = 1
            };

            foreach (int dc in dcValues)
            {
                coefficients[0] = dc;
                Array.Fill(expected, byte.MaxValue);
                Array.Fill(actual, byte.MaxValue);
                if (inPlace)
                {
                    prediction.CopyTo(expected, 0);
                    prediction.CopyTo(actual, 0);
                }

                // Bypass the sparse dispatcher for the oracle: the full two-axis transform retains all rounding
                // stages. Comparing the entire padded destination also detects writes beyond each active row.
                Av1Inverse2dTransformer.Transform2dAdd(
                    coefficients, inPlace ? expected : prediction, readStride, expected, writeStride, ref config, workspace);

                Av1InverseTransformerFactory.InverseTransformAdd(
                    coefficients, inPlace ? actual : prediction, readStride, actual, writeStride, parameters, workspace);

                Assert.Equal(expected, actual);
            }
        }
    }

    /// <summary>
    /// Verifies DC-only high-bit-depth reconstruction against the full transform at every supported precision and size.
    /// </summary>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <param name="inPlace">Whether prediction and reconstruction share their storage.</param>
    [Theory]
    [InlineData(8, false)]
    [InlineData(8, true)]
    [InlineData(10, false)]
    [InlineData(10, true)]
    [InlineData(12, false)]
    [InlineData(12, true)]
    public void DcOnlyHighBitDepthReconstructionMatchesFullTransform(int bitDepth, bool inPlace)
    {
        ReadOnlySpan<int> dcValues =
        [
            -524288, -262143, -65535, -4095, -1025, -65, -33, -32, -31, -17, -1,
            0, 1, 17, 31, 32, 33, 65, 1025, 4095, 65535, 262143, 524287
        ];

        int[] workspace = new int[Av1TransformWorkspace.MaximumLength];
        int maximum = (1 << bitDepth) - 1;
        for (int size = 0; size < (int)Av1TransformSize.AllSizes; size++)
        {
            Av1TransformSize transformSize = (Av1TransformSize)size;
            Av1Transform2dFlipConfiguration config = Av1Transform2dFlipConfiguration.CreateInverse(Av1TransformType.DctDct, transformSize, bitDepth);
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            int readStride = width + 3;
            int writeStride = inPlace ? readStride : width + 7;
            int[] coefficients = new int[transformSize.GetAdjusted().GetSize2d()];
            short[] prediction = new short[readStride * height];
            short[] expected = new short[writeStride * height];
            short[] actual = new short[expected.Length];
            for (int i = 0; i < prediction.Length; i++)
            {
                prediction[i] = (short)((i * 47) & maximum);
            }

            Av1TransformFunctionParameters parameters = new()
            {
                TransformSize = transformSize,
                TransformType = Av1TransformType.DctDct,
                BitDepth = bitDepth,
                EndOfBuffer = 1,
                Is16BitPipeline = true
            };

            foreach (int dc in dcValues)
            {
                coefficients[0] = dc;
                Array.Fill(expected, short.MinValue);
                Array.Fill(actual, short.MinValue);
                if (inPlace)
                {
                    prediction.CopyTo(expected, 0);
                    prediction.CopyTo(actual, 0);
                }

                // Include coefficients outside the input clamp as well as signed rounding boundaries. Sparse and
                // full reconstruction must apply the same clamping even when the coded value saturates.
                Av1Inverse2dTransformer.Transform2dAdd(
                    coefficients, inPlace ? expected : prediction, readStride, expected, writeStride, ref config, workspace, bitDepth);

                Av1InverseTransformerFactory.InverseTransformAdd(
                    coefficients, inPlace ? actual : prediction, readStride, actual, writeStride, parameters, workspace);

                Assert.Equal(expected, actual);
            }
        }
    }

    [Theory]
    [InlineData((int)Av1BitDepth.TenBit, 1023)]
    [InlineData((int)Av1BitDepth.TwelveBit, 4095)]
    public void HighBitDepthReconstructionClipsPositiveValues(int bitDepthIndex, short maximum)
    {
        const int width = 4;
        int[] coefficients = new int[width * width];
        coefficients[0] = 64;
        short[] reconstruction = new short[width * width];
        Array.Fill(reconstruction, (short)(maximum - 1));
        int[] workspace = new int[Av1TransformWorkspace.MaximumLength];

        Av1InverseTransformer.ReconstructHighBitDepth(
            coefficients,
            reconstruction,
            width,
            Av1TransformSize.Size4x4,
            Av1TransformType.DctDct,
            0,
            1,
            false,
            (Av1BitDepth)bitDepthIndex,
            workspace);

        Assert.All(reconstruction, value => Assert.Equal(maximum, value));
    }

    [Theory]
    [InlineData((int)Av1BitDepth.TenBit)]
    [InlineData((int)Av1BitDepth.TwelveBit)]
    public void HighBitDepthReconstructionClipsNegativeValues(int bitDepthIndex)
    {
        const int width = 4;
        int[] coefficients = new int[width * width];
        coefficients[0] = -64;
        short[] reconstruction = new short[width * width];
        Array.Fill(reconstruction, (short)1);
        int[] workspace = new int[Av1TransformWorkspace.MaximumLength];

        Av1InverseTransformer.ReconstructHighBitDepth(
            coefficients,
            reconstruction,
            width,
            Av1TransformSize.Size4x4,
            Av1TransformType.DctDct,
            0,
            1,
            false,
            (Av1BitDepth)bitDepthIndex,
            workspace);

        Assert.All(reconstruction, value => Assert.Equal((short)0, value));
    }

    /// <summary>
    /// Populates active eight-bit prediction samples while preserving row-padding sentinels.
    /// </summary>
    private static void PopulatePrediction(Span<byte> prediction, int stride, int maximum)
    {
        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                prediction[(row * stride) + column] = (byte)(((row * 101) + (column * 67) + 19) & maximum);
            }
        }
    }

    /// <summary>
    /// Populates active high-bit-depth prediction samples while preserving row-padding sentinels.
    /// </summary>
    private static void PopulatePrediction(Span<short> prediction, int stride, int maximum)
    {
        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                prediction[(row * stride) + column] = (short)(((row * 911) + (column * 593) + 37) & maximum);
            }
        }
    }

    /// <summary>
    /// Applies the normative inverse Walsh-Hadamard definition to an eight-bit prediction block.
    /// </summary>
    private static void ApplyWalshHadamardReference(ReadOnlySpan<int> coefficients, Span<byte> destination, int stride, int coefficientCount, int bitDepth)
    {
        int[] residuals = CalculateWalshHadamardReference(coefficients, coefficientCount);
        int maximum = (1 << bitDepth) - 1;

        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                int offset = (row * stride) + column;
                destination[offset] = (byte)Math.Clamp(destination[offset] + residuals[(row * 4) + column], 0, maximum);
            }
        }
    }

    /// <summary>
    /// Applies the normative inverse Walsh-Hadamard definition to a high-bit-depth prediction block.
    /// </summary>
    private static void ApplyWalshHadamardReference(ReadOnlySpan<int> coefficients, Span<short> destination, int stride, int coefficientCount, int bitDepth)
    {
        int[] residuals = CalculateWalshHadamardReference(coefficients, coefficientCount);
        int maximum = (1 << bitDepth) - 1;

        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                int offset = (row * stride) + column;
                destination[offset] = (short)Math.Clamp(destination[offset] + residuals[(row * 4) + column], 0, maximum);
            }
        }
    }

    /// <summary>
    /// Calculates the exact four-by-four residual matrix defined by AV1's reversible transform.
    /// </summary>
    private static int[] CalculateWalshHadamardReference(ReadOnlySpan<int> coefficients, int coefficientCount)
    {
        int[] residuals = new int[16];

        if (coefficientCount == 1)
        {
            int first = coefficients[0] >> 2;
            int half = first >> 1;
            int firstIntermediate = first - half;

            for (int column = 0; column < 4; column++)
            {
                int intermediate = column == 0 ? firstIntermediate : half;
                int repeatedResidual = intermediate >> 1;
                residuals[column] = intermediate - repeatedResidual;
                residuals[4 + column] = repeatedResidual;
                residuals[8 + column] = repeatedResidual;
                residuals[12 + column] = repeatedResidual;
            }

            return residuals;
        }

        int[] intermediateValues = new int[16];
        for (int row = 0; row < 4; row++)
        {
            int coefficientOffset = row * 4;
            int a = coefficients[coefficientOffset] >> 2;
            int c = coefficients[coefficientOffset + 1] >> 2;
            int d = coefficients[coefficientOffset + 2] >> 2;
            int b = coefficients[coefficientOffset + 3] >> 2;

            ApplyWalshHadamardReference(ref a, ref b, ref c, ref d);
            intermediateValues[row] = a;
            intermediateValues[4 + row] = b;
            intermediateValues[8 + row] = c;
            intermediateValues[12 + row] = d;
        }

        for (int column = 0; column < 4; column++)
        {
            int offset = column * 4;
            int a = intermediateValues[offset];
            int c = intermediateValues[offset + 1];
            int d = intermediateValues[offset + 2];
            int b = intermediateValues[offset + 3];

            ApplyWalshHadamardReference(ref a, ref b, ref c, ref d);
            residuals[column] = a;
            residuals[4 + column] = b;
            residuals[8 + column] = c;
            residuals[12 + column] = d;
        }

        return residuals;
    }

    /// <summary>
    /// Applies one scalar four-point reversible Walsh-Hadamard dimension for the independent test definition.
    /// </summary>
    private static void ApplyWalshHadamardReference(ref int a, ref int b, ref int c, ref int d)
    {
        a += c;
        d -= b;
        int middle = (a - d) >> 1;
        b = middle - b;
        c = middle - c;
        a -= b;
        d += c;
    }

    /// <summary>
    /// Compares one inverse transform operator across scalar and the supported SIMD lane widths.
    /// </summary>
    /// <typeparam name="TOperator">The inverse transform operator.</typeparam>
    /// <param name="length">The transform length.</param>
    private static void AssertOperatorParity<TOperator>(int length)
        where TOperator : struct, Av1Inverse2dTransformer.IAv1Transform1dOperator
    {
        const int cosBit = 12;
        InlineArray12<byte> stageRange = default;

        for (int index = 0; index < Av1Transform2dFlipConfiguration.MaxStageNumber; index++)
        {
            stageRange[index] = 24;
        }

        Av1TransformVector<Vector128<int>> input128 = default;
        Av1TransformVector<Vector128<int>> output128 = default;
        Av1TransformVector<Vector256<int>> input256 = default;
        Av1TransformVector<Vector256<int>> output256 = default;

        for (int index = 0; index < length; index++)
        {
            input128[index] = Vector128.Create(
                GetInputValue(index, 0),
                GetInputValue(index, 1),
                GetInputValue(index, 2),
                GetInputValue(index, 3));

            input256[index] = Vector256.Create(
                GetInputValue(index, 0),
                GetInputValue(index, 1),
                GetInputValue(index, 2),
                GetInputValue(index, 3),
                GetInputValue(index, 4),
                GetInputValue(index, 5),
                GetInputValue(index, 6),
                GetInputValue(index, 7));
        }

        TOperator.Transform(ref input128, ref output128, ref input128, cosBit, stageRange);
        TOperator.Transform(ref input256, ref output256, ref input256, cosBit, stageRange);

        int[] scalarInput = new int[length];
        int[] scalarOutput = new int[length];

        for (int lane = 0; lane < Vector256<int>.Count; lane++)
        {
            for (int index = 0; index < length; index++)
            {
                scalarInput[index] = GetInputValue(index, lane);
            }

            TOperator.Transform(scalarInput, scalarOutput, scalarInput, cosBit, stageRange);

            for (int index = 0; index < length; index++)
            {
                Assert.Equal(scalarOutput[index], output256[index].GetElement(lane));

                if (lane < Vector128<int>.Count)
                {
                    Assert.Equal(scalarOutput[index], output128[index].GetElement(lane));
                }
            }
        }
    }

    /// <summary>
    /// Verifies that a matching one-dimensional forward and inverse operator pair reconstructs bounded input.
    /// </summary>
    /// <typeparam name="TForwardOperator">The forward transform operator.</typeparam>
    /// <typeparam name="TInverseOperator">The inverse transform operator.</typeparam>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="scaleLog2">The power-of-two scale applied by the operator pair.</param>
    /// <param name="allowedError">The maximum permitted reconstruction error.</param>
    private static void AssertRoundTrip<TForwardOperator, TInverseOperator>(Av1TransformType transformType, Av1TransformSize transformSize, int scaleLog2, int allowedError)
        where TForwardOperator : struct, Av1ForwardTransformer.IAv1ForwardTransform1dOperator
        where TInverseOperator : struct, Av1Inverse2dTransformer.IAv1Transform1dOperator
    {
        const int bitDepth = 10;
        const int testBlockCount = 30;
        Av1Transform2dFlipConfiguration forwardConfig = Av1Transform2dFlipConfiguration.CreateForward(transformType, transformSize, bitDepth);
        Av1Transform2dFlipConfiguration inverseConfig = Av1Transform2dFlipConfiguration.CreateInverse(transformType, transformSize, bitDepth);
        int length = transformSize.GetWidth();
        Random random = new(0);
        int[] input = new int[length];
        int[] forward = new int[length];
        int[] inverse = new int[length];
        int[] step = new int[length];
        Av1TransformVector<int> values = default;
        Av1TransformVector<int> buffer0 = default;
        Av1TransformVector<int> buffer1 = default;

        for (int block = 0; block < testBlockCount; block++)
        {
            for (int index = 0; index < length; index++)
            {
                input[index] = random.Next((1 << bitDepth) - 1);
                values[index] = input[index];
            }

            ref byte valuesBase = ref System.Runtime.CompilerServices.Unsafe.As<Av1TransformVector<int>, byte>(ref values);

            TForwardOperator.Transform(ref valuesBase, sizeof(int), sizeof(int), ref buffer0, ref buffer1, forwardConfig.CosBitColumn);

            for (int index = 0; index < length; index++)
            {
                forward[index] = values[index];
            }

            TInverseOperator.Transform(forward, inverse, step, inverseConfig.CosBitColumn, inverseConfig.StageRangeColumn);

            for (int index = 0; index < length; index++)
            {
                int reconstructed = inverse[index] >> scaleLog2;
                Assert.InRange(Math.Abs(input[index] - reconstructed), 0, allowedError);
            }
        }
    }

    /// <summary>
    /// Closes the static-generic inverse column operator selected by a transform configuration.
    /// </summary>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="config">The inverse transform configuration.</param>
    private static void DispatchColumn(
        Av1TransformType transformType,
        Av1TransformSize transformSize,
        int bitDepth,
        ref Av1Transform2dFlipConfiguration config)
    {
        switch (config.TransformFunctionTypeColumn)
        {
            case Av1TransformFunctionType.Dct4:
                DispatchRow<Av1Inverse2dTransformer.Dct4Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct8:
                DispatchRow<Av1Inverse2dTransformer.Dct8Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct16:
                DispatchRow<Av1Inverse2dTransformer.Dct16Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct32:
                DispatchRow<Av1Inverse2dTransformer.Dct32Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct64:
                DispatchRow<Av1Inverse2dTransformer.Dct64Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Adst4:
                DispatchRow<Av1Inverse2dTransformer.Adst4Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Adst8:
                DispatchRow<Av1Inverse2dTransformer.Adst8Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Adst16:
                DispatchRow<Av1Inverse2dTransformer.Adst16Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity4:
                DispatchRow<Av1Inverse2dTransformer.Identity4Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity8:
                DispatchRow<Av1Inverse2dTransformer.Identity8Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity16:
                DispatchRow<Av1Inverse2dTransformer.Identity16Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity32:
                DispatchRow<Av1Inverse2dTransformer.Identity32Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            default:
                Assert.Fail($"Unexpected column function {config.TransformFunctionTypeColumn} for {transformType} {transformSize}.");
                break;
        }
    }

    /// <summary>
    /// Closes the static-generic inverse row operator after the column operator has been selected.
    /// </summary>
    /// <typeparam name="TColumnOperator">The selected inverse column operator.</typeparam>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="config">The inverse transform configuration.</param>
    private static void DispatchRow<TColumnOperator>(
        Av1TransformType transformType,
        Av1TransformSize transformSize,
        int bitDepth,
        ref Av1Transform2dFlipConfiguration config)
        where TColumnOperator : struct, Av1Inverse2dTransformer.IAv1Transform1dOperator
    {
        switch (config.TransformFunctionTypeRow)
        {
            case Av1TransformFunctionType.Dct4:
                AssertTransform2dParity<TColumnOperator, Av1Inverse2dTransformer.Dct4Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct8:
                AssertTransform2dParity<TColumnOperator, Av1Inverse2dTransformer.Dct8Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct16:
                AssertTransform2dParity<TColumnOperator, Av1Inverse2dTransformer.Dct16Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct32:
                AssertTransform2dParity<TColumnOperator, Av1Inverse2dTransformer.Dct32Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct64:
                AssertTransform2dParity<TColumnOperator, Av1Inverse2dTransformer.Dct64Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Adst4:
                AssertTransform2dParity<TColumnOperator, Av1Inverse2dTransformer.Adst4Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Adst8:
                AssertTransform2dParity<TColumnOperator, Av1Inverse2dTransformer.Adst8Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Adst16:
                AssertTransform2dParity<TColumnOperator, Av1Inverse2dTransformer.Adst16Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity4:
                AssertTransform2dParity<TColumnOperator, Av1Inverse2dTransformer.Identity4Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity8:
                AssertTransform2dParity<TColumnOperator, Av1Inverse2dTransformer.Identity8Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity16:
                AssertTransform2dParity<TColumnOperator, Av1Inverse2dTransformer.Identity16Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity32:
                AssertTransform2dParity<TColumnOperator, Av1Inverse2dTransformer.Identity32Operator>(transformType, transformSize, bitDepth, ref config);
                break;
            default:
                Assert.Fail($"Unexpected row function {config.TransformFunctionTypeRow} for {transformType} {transformSize}.");
                break;
        }
    }

    /// <summary>
    /// Produces bounded conformant coefficients and selects byte or high-bit-depth reconstruction verification.
    /// </summary>
    /// <typeparam name="TColumnOperator">The selected inverse column operator.</typeparam>
    /// <typeparam name="TRowOperator">The selected inverse row operator.</typeparam>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="config">The inverse transform configuration.</param>
    private static void AssertTransform2dParity<TColumnOperator, TRowOperator>(
        Av1TransformType transformType,
        Av1TransformSize transformSize,
        int bitDepth,
        ref Av1Transform2dFlipConfiguration config)
        where TColumnOperator : struct, Av1Inverse2dTransformer.IAv1Transform1dOperator
        where TRowOperator : struct, Av1Inverse2dTransformer.IAv1Transform1dOperator
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int inputStride = width + 5;
        int maximum = (1 << bitDepth) - 1;
        short[] residual = new short[inputStride * height];

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int index = (row * width) + column;
                residual[(row * inputStride) + column] = (short)((index & 3) switch
                {
                    0 => maximum,
                    1 => -maximum,
                    2 => ((index * 73) % ((maximum * 2) + 1)) - maximum,
                    _ => 0,
                });
            }
        }

        // A conformant forward transform supplies coefficient magnitudes at the exact fixed-point bounds expected by
        // the inverse kernels. This is stronger than arbitrary small coefficients and avoids impossible stress inputs.
        int[] coefficients = new int[width * height];
        int[] forwardWorkspace = new int[Av1TransformWorkspace.GetRequiredLength(transformSize)];
        Av1ForwardTransformer.Transform2d(residual, coefficients, (uint)inputStride, transformType, transformSize, bitDepth, forwardWorkspace);

        if (bitDepth == 8)
        {
            AssertByteTransform2dParity<TColumnOperator, TRowOperator>(coefficients, transformSize, ref config);
            return;
        }

        AssertHighBitDepthTransform2dParity<TColumnOperator, TRowOperator>(coefficients, transformSize, bitDepth, ref config);
    }

    /// <summary>
    /// Compares eight-bit scalar and SIMD reconstruction with independently padded read and write rows.
    /// </summary>
    /// <typeparam name="TColumnOperator">The selected inverse column operator.</typeparam>
    /// <typeparam name="TRowOperator">The selected inverse row operator.</typeparam>
    /// <param name="coefficients">The conformant forward-transform coefficients.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="config">The inverse transform configuration.</param>
    private static void AssertByteTransform2dParity<TColumnOperator, TRowOperator>(
        int[] coefficients,
        Av1TransformSize transformSize,
        ref Av1Transform2dFlipConfiguration config)
        where TColumnOperator : struct, Av1Inverse2dTransformer.IAv1Transform1dOperator
        where TRowOperator : struct, Av1Inverse2dTransformer.IAv1Transform1dOperator
    {
        const int bitDepth = 8;
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int readStride = width + 3;
        int writeStride = width + 7;
        int workspaceLength = Av1TransformWorkspace.GetInverseRequiredLength(transformSize);
        byte[] prediction = new byte[readStride * height];

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                prediction[(row * readStride) + column] = (byte)(((row * width) + column) * 29);
            }
        }

        byte[] scalar = new byte[writeStride * height];
        byte[] vector128 = new byte[writeStride * height];
        int[] scalarWorkspace = new int[workspaceLength];
        int[] vector128Workspace = new int[workspaceLength];
        Array.Fill(scalar, byte.MaxValue);
        Array.Fill(vector128, byte.MaxValue);

        Av1Inverse2dTransformer.Transform2dScalar<byte, Av1InverseTransformer.ByteOutputOperator, TColumnOperator, TRowOperator>(
            coefficients, prediction, readStride, scalar, writeStride, ref config, scalarWorkspace, bitDepth);

        Av1Inverse2dTransformer.Transform2dVector128<byte, Av1InverseTransformer.ByteOutputOperator, TColumnOperator, TRowOperator>(
            coefficients, prediction, readStride, vector128, writeStride, ref config, vector128Workspace, bitDepth);

        Assert.Equal(scalar, vector128);

        if (width >= Vector256<int>.Count && height >= Vector256<int>.Count)
        {
            byte[] vector256 = new byte[writeStride * height];
            int[] vector256Workspace = new int[workspaceLength];
            Array.Fill(vector256, byte.MaxValue);

            Av1Inverse2dTransformer.Transform2dVector256<byte, Av1InverseTransformer.ByteOutputOperator, TColumnOperator, TRowOperator>(
                coefficients, prediction, readStride, vector256, writeStride, ref config, vector256Workspace, bitDepth);

            Assert.Equal(scalar, vector256);
        }

        // Keep the complete scalar operator pair as the arithmetic oracle while the factory selects sparse pairs.
        // Prefixes around powers of two exercise sparse support transitions; the separate bound test covers every EOB.
        ReadOnlySpan<short> scan = Av1ScanOrderConstants.GetScanOrder(transformSize, config.TransformType).Scan;
        int[] sparseCoefficients = new int[coefficients.Length];
        byte[] sparseOutput = new byte[writeStride * height];
        int[] sparseWorkspace = new int[workspaceLength];
        Av1TransformFunctionParameters parameters = new()
        {
            TransformType = config.TransformType,
            TransformSize = transformSize,
            BitDepth = bitDepth,
            Is16BitPipeline = false,
        };

        for (int eob = 1; eob <= scan.Length; eob++)
        {
            int raster = scan[eob - 1];
            sparseCoefficients[raster] = coefficients[raster] == 0 ? 1 : coefficients[raster];
            if (eob != 1 && eob != scan.Length &&
                !BitOperations.IsPow2((uint)(eob - 1)) && !BitOperations.IsPow2((uint)eob) && !BitOperations.IsPow2((uint)(eob + 1)))
            {
                continue;
            }

            parameters.EndOfBuffer = eob;
            Array.Fill(scalar, byte.MaxValue);
            Array.Fill(sparseOutput, byte.MaxValue);
            Array.Fill(scalarWorkspace, int.MinValue);
            Array.Fill(sparseWorkspace, int.MaxValue);

            Av1Inverse2dTransformer.Transform2dScalar<byte, Av1InverseTransformer.ByteOutputOperator, TColumnOperator, TRowOperator>(
                sparseCoefficients, prediction, readStride, scalar, writeStride, ref config, scalarWorkspace, bitDepth);

            Av1InverseTransformerFactory.InverseTransformAdd(
                sparseCoefficients, prediction, readStride, sparseOutput, writeStride, parameters, sparseWorkspace);

            Assert.Equal(scalar, sparseOutput);
        }
    }

    /// <summary>
    /// Compares high-bit-depth scalar and SIMD reconstruction with independently padded read and write rows.
    /// </summary>
    /// <typeparam name="TColumnOperator">The selected inverse column operator.</typeparam>
    /// <typeparam name="TRowOperator">The selected inverse row operator.</typeparam>
    /// <param name="coefficients">The conformant forward-transform coefficients.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="config">The inverse transform configuration.</param>
    private static void AssertHighBitDepthTransform2dParity<TColumnOperator, TRowOperator>(
        int[] coefficients,
        Av1TransformSize transformSize,
        int bitDepth,
        ref Av1Transform2dFlipConfiguration config)
        where TColumnOperator : struct, Av1Inverse2dTransformer.IAv1Transform1dOperator
        where TRowOperator : struct, Av1Inverse2dTransformer.IAv1Transform1dOperator
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int readStride = width + 3;
        int writeStride = width + 7;
        int maximum = (1 << bitDepth) - 1;
        int workspaceLength = Av1TransformWorkspace.GetInverseRequiredLength(transformSize);
        short[] prediction = new short[readStride * height];

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                prediction[(row * readStride) + column] = (short)((((row * width) + column) * 47) & maximum);
            }
        }

        short[] scalar = new short[writeStride * height];
        short[] vector128 = new short[writeStride * height];
        int[] scalarWorkspace = new int[workspaceLength];
        int[] vector128Workspace = new int[workspaceLength];
        Array.Fill(scalar, short.MinValue);
        Array.Fill(vector128, short.MinValue);

        Av1Inverse2dTransformer.Transform2dScalar<short, Av1InverseTransformer.HighBitDepthOutputOperator, TColumnOperator, TRowOperator>(
            coefficients, prediction, readStride, scalar, writeStride, ref config, scalarWorkspace, bitDepth);

        Av1Inverse2dTransformer.Transform2dVector128<short, Av1InverseTransformer.HighBitDepthOutputOperator, TColumnOperator, TRowOperator>(
            coefficients, prediction, readStride, vector128, writeStride, ref config, vector128Workspace, bitDepth);

        Assert.Equal(scalar, vector128);

        if (width >= Vector256<int>.Count && height >= Vector256<int>.Count)
        {
            short[] vector256 = new short[writeStride * height];
            int[] vector256Workspace = new int[workspaceLength];
            Array.Fill(vector256, short.MinValue);

            Av1Inverse2dTransformer.Transform2dVector256<short, Av1InverseTransformer.HighBitDepthOutputOperator, TColumnOperator, TRowOperator>(
                coefficients, prediction, readStride, vector256, writeStride, ref config, vector256Workspace, bitDepth);

            Assert.Equal(scalar, vector256);
        }

        // Keep the complete scalar operator pair as the arithmetic oracle while the factory selects sparse pairs.
        // Prefixes around powers of two exercise sparse support transitions; the separate bound test covers every EOB.
        ReadOnlySpan<short> scan = Av1ScanOrderConstants.GetScanOrder(transformSize, config.TransformType).Scan;
        int[] sparseCoefficients = new int[coefficients.Length];
        short[] sparseOutput = new short[writeStride * height];
        int[] sparseWorkspace = new int[workspaceLength];
        Av1TransformFunctionParameters parameters = new()
        {
            TransformType = config.TransformType,
            TransformSize = transformSize,
            BitDepth = bitDepth,
            Is16BitPipeline = true,
        };

        for (int eob = 1; eob <= scan.Length; eob++)
        {
            int raster = scan[eob - 1];
            sparseCoefficients[raster] = coefficients[raster] == 0 ? 1 : coefficients[raster];
            if (eob != 1 && eob != scan.Length &&
                !BitOperations.IsPow2((uint)(eob - 1)) && !BitOperations.IsPow2((uint)eob) && !BitOperations.IsPow2((uint)(eob + 1)))
            {
                continue;
            }

            parameters.EndOfBuffer = eob;
            Array.Fill(scalar, short.MinValue);
            Array.Fill(sparseOutput, short.MinValue);
            Array.Fill(scalarWorkspace, int.MinValue);
            Array.Fill(sparseWorkspace, int.MaxValue);

            Av1Inverse2dTransformer.Transform2dScalar<short, Av1InverseTransformer.HighBitDepthOutputOperator, TColumnOperator, TRowOperator>(
                sparseCoefficients, prediction, readStride, scalar, writeStride, ref config, scalarWorkspace, bitDepth);

            Av1InverseTransformerFactory.InverseTransformAdd(
                sparseCoefficients, prediction, readStride, sparseOutput, writeStride, parameters, sparseWorkspace);

            Assert.Equal(scalar, sparseOutput);
        }
    }

    /// <summary>
    /// Produces deterministic bounded input for one transform position and SIMD lane.
    /// </summary>
    /// <param name="index">The position within the transform.</param>
    /// <param name="lane">The SIMD lane index.</param>
    /// <returns>The input value.</returns>
    private static int GetInputValue(int index, int lane) => (((index * 73) + (lane * 151)) % 1023) - 511;
}
