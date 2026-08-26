// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1InverseTransformTests
{
    /// <summary>
    /// The hardware configurations covering every transform SIMD tier and the scalar fallback.
    /// </summary>
    private const HwIntrinsics TransformConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

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
    /// Verifies the inverse DCT operators against their scalar implementations.
    /// </summary>
    private static void AssertDctOperatorParity()
    {
        AssertOperatorParity<Av1Dct4Inverse1dOperator>(4);
        AssertOperatorParity<Av1Dct8Inverse1dOperator>(8);
        AssertOperatorParity<Av1Dct16Inverse1dOperator>(16);
        AssertOperatorParity<Av1Dct32Inverse1dOperator>(32);
        AssertOperatorParity<Av1Dct64Inverse1dOperator>(64);
    }

    /// <summary>
    /// Verifies the inverse ADST operators against their scalar implementations.
    /// </summary>
    private static void AssertAdstOperatorParity()
    {
        AssertOperatorParity<Av1Adst4Inverse1dOperator>(4);
        AssertOperatorParity<Av1Adst8Inverse1dOperator>(8);
        AssertOperatorParity<Av1Adst16Inverse1dOperator>(16);
    }

    /// <summary>
    /// Verifies the inverse identity operators against their scalar implementations.
    /// </summary>
    private static void AssertIdentityOperatorParity()
    {
        AssertOperatorParity<Av1Identity4Inverse1dOperator>(4);
        AssertOperatorParity<Av1Identity8Inverse1dOperator>(8);
        AssertOperatorParity<Av1Identity16Inverse1dOperator>(16);
        AssertOperatorParity<Av1Identity32Inverse1dOperator>(32);
    }

    /// <summary>
    /// Verifies the complete sixteen-lane two-dimensional inverse traversal matrix across hardware feature levels.
    /// </summary>
    [Fact]
    public void Vector512KernelsMatchScalarForEveryApplicableConfiguration()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertVector512TransformParity, TransformConfigurations);

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

        Av1TransformStageRange configuredRowRange = config.StageRangeRow;
        Av1TransformStageRange configuredColumnRange = config.StageRangeColumn;

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
        AssertRoundTrip<Av1Dct4Forward1dOperator, Av1Dct4Inverse1dOperator>(Av1TransformType.DctDct, Av1TransformSize.Size4x4, 1, 1);
        AssertRoundTrip<Av1Dct8Forward1dOperator, Av1Dct8Inverse1dOperator>(Av1TransformType.DctDct, Av1TransformSize.Size8x8, 2, 2);
        AssertRoundTrip<Av1Dct16Forward1dOperator, Av1Dct16Inverse1dOperator>(Av1TransformType.DctDct, Av1TransformSize.Size16x16, 3, 3);
        AssertRoundTrip<Av1Dct32Forward1dOperator, Av1Dct32Inverse1dOperator>(Av1TransformType.DctDct, Av1TransformSize.Size32x32, 4, 4);
        AssertRoundTrip<Av1Dct64Forward1dOperator, Av1Dct64Inverse1dOperator>(Av1TransformType.DctDct, Av1TransformSize.Size64x64, 5, 5);
        AssertRoundTrip<Av1Adst4Forward1dOperator, Av1Adst4Inverse1dOperator>(Av1TransformType.AdstAdst, Av1TransformSize.Size4x4, 1, 1);
        AssertRoundTrip<Av1Adst8Forward1dOperator, Av1Adst8Inverse1dOperator>(Av1TransformType.AdstAdst, Av1TransformSize.Size8x8, 2, 2);
        AssertRoundTrip<Av1Adst16Forward1dOperator, Av1Adst16Inverse1dOperator>(Av1TransformType.AdstAdst, Av1TransformSize.Size16x16, 3, 3);
        AssertRoundTrip<Av1Identity4Forward1dOperator, Av1Identity4Inverse1dOperator>(Av1TransformType.Identity, Av1TransformSize.Size4x4, 1, 1);
        AssertRoundTrip<Av1Identity8Forward1dOperator, Av1Identity8Inverse1dOperator>(Av1TransformType.Identity, Av1TransformSize.Size8x8, 2, 1);
        AssertRoundTrip<Av1Identity16Forward1dOperator, Av1Identity16Inverse1dOperator>(Av1TransformType.Identity, Av1TransformSize.Size16x16, 3, 1);
        AssertRoundTrip<Av1Identity32Forward1dOperator, Av1Identity32Inverse1dOperator>(Av1TransformType.Identity, Av1TransformSize.Size32x32, 4, 1);
    }

    /// <summary>
    /// Verifies that every applicable SIMD traversal reconstructs the same samples as the scalar traversal.
    /// </summary>
    /// <param name="transformTypeValue">The integral <see cref="Av1TransformType"/> value.</param>
    /// <param name="transformSizeValue">The integral <see cref="Av1TransformSize"/> value.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    [Theory]
    [MemberData(nameof(Av1ForwardTransformTests.ValidTransformCases), MemberType = typeof(Av1ForwardTransformTests))]
    public void TwoDimensionalSimdKernelsMatchScalarForEveryValidConfiguration(
        int transformTypeValue,
        int transformSizeValue,
        int bitDepth)
    {
        Av1TransformType transformType = (Av1TransformType)transformTypeValue;
        Av1TransformSize transformSize = (Av1TransformSize)transformSizeValue;
        Av1Transform2dFlipConfiguration config = Av1Transform2dFlipConfiguration.CreateInverse(transformType, transformSize, bitDepth);
        DispatchColumn(transformType, transformSize, bitDepth, ref config);
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
    /// Compares one inverse transform operator across scalar and all SIMD lane widths.
    /// </summary>
    /// <typeparam name="TOperator">The inverse transform operator.</typeparam>
    /// <param name="length">The transform length.</param>
    private static void AssertOperatorParity<TOperator>(int length)
        where TOperator : struct, IAv1Transform1dOperator
    {
        const int cosBit = 12;
        Av1TransformStageRange stageRange = default;

        for (int index = 0; index < Av1Transform2dFlipConfiguration.MaxStageNumber; index++)
        {
            stageRange[index] = 24;
        }

        Av1TransformVector<Vector128<int>> input128 = default;
        Av1TransformVector<Vector128<int>> output128 = default;
        Av1TransformVector<Vector128<int>> step128 = default;
        Av1TransformVector<Vector256<int>> input256 = default;
        Av1TransformVector<Vector256<int>> output256 = default;
        Av1TransformVector<Vector256<int>> step256 = default;
        Av1TransformVector<Vector512<int>> input512 = default;
        Av1TransformVector<Vector512<int>> output512 = default;
        Av1TransformVector<Vector512<int>> step512 = default;

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

            input512[index] = Vector512.Create(
                GetInputValue(index, 0),
                GetInputValue(index, 1),
                GetInputValue(index, 2),
                GetInputValue(index, 3),
                GetInputValue(index, 4),
                GetInputValue(index, 5),
                GetInputValue(index, 6),
                GetInputValue(index, 7),
                GetInputValue(index, 8),
                GetInputValue(index, 9),
                GetInputValue(index, 10),
                GetInputValue(index, 11),
                GetInputValue(index, 12),
                GetInputValue(index, 13),
                GetInputValue(index, 14),
                GetInputValue(index, 15));

        }

        TOperator.Transform(ref input128, ref output128, ref step128, cosBit, stageRange);
        TOperator.Transform(ref input256, ref output256, ref step256, cosBit, stageRange);
        TOperator.Transform(ref input512, ref output512, ref step512, cosBit, stageRange);

        int[] scalarInput = new int[length];
        int[] scalarOutput = new int[length];
        int[] scalarStep = new int[length];

        for (int lane = 0; lane < Vector512<int>.Count; lane++)
        {
            for (int index = 0; index < length; index++)
            {
                scalarInput[index] = GetInputValue(index, lane);
            }

            TOperator.Transform(scalarInput, scalarOutput, scalarStep, cosBit, stageRange);

            for (int index = 0; index < length; index++)
            {
                Assert.Equal(scalarOutput[index], output512[index].GetElement(lane));

                if (lane < Vector256<int>.Count)
                {
                    Assert.Equal(scalarOutput[index], output256[index].GetElement(lane));
                }

                if (lane < Vector128<int>.Count)
                {
                    Assert.Equal(scalarOutput[index], output128[index].GetElement(lane));
                }
            }
        }
    }

    /// <summary>
    /// Runs every valid inverse transform configuration capable of filling a sixteen-lane tile.
    /// </summary>
    private static void AssertVector512TransformParity()
    {
        for (Av1TransformSize transformSize = 0; transformSize < Av1TransformSize.AllSizes; transformSize++)
        {
            if (transformSize.GetWidth() < Vector512<int>.Count || transformSize.GetHeight() < Vector512<int>.Count)
            {
                continue;
            }

            for (Av1TransformType transformType = 0; transformType < Av1TransformType.AllTransformTypes; transformType++)
            {
                Av1Transform2dFlipConfiguration allowedConfig = Av1Transform2dFlipConfiguration.CreateInverse(transformType, transformSize, 8);
                if (!allowedConfig.IsAllowed())
                {
                    continue;
                }

                for (int bitDepth = 8; bitDepth <= 12; bitDepth += 2)
                {
                    Av1Transform2dFlipConfiguration config = Av1Transform2dFlipConfiguration.CreateInverse(transformType, transformSize, bitDepth);
                    DispatchColumn(transformType, transformSize, bitDepth, ref config);
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
        where TForwardOperator : struct, IAv1Transform1dOperator
        where TInverseOperator : struct, IAv1Transform1dOperator
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

        for (int block = 0; block < testBlockCount; block++)
        {
            for (int index = 0; index < length; index++)
            {
                input[index] = random.Next((1 << bitDepth) - 1);
            }

            TForwardOperator.Transform(input, forward, step, forwardConfig.CosBitColumn, forwardConfig.StageRangeColumn);
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
                DispatchRow<Av1Dct4Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct8:
                DispatchRow<Av1Dct8Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct16:
                DispatchRow<Av1Dct16Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct32:
                DispatchRow<Av1Dct32Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct64:
                DispatchRow<Av1Dct64Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Adst4:
                DispatchRow<Av1Adst4Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Adst8:
                DispatchRow<Av1Adst8Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Adst16:
                DispatchRow<Av1Adst16Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity4:
                DispatchRow<Av1Identity4Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity8:
                DispatchRow<Av1Identity8Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity16:
                DispatchRow<Av1Identity16Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity32:
                DispatchRow<Av1Identity32Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
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
        where TColumnOperator : struct, IAv1Transform1dOperator
    {
        switch (config.TransformFunctionTypeRow)
        {
            case Av1TransformFunctionType.Dct4:
                AssertTransform2dParity<TColumnOperator, Av1Dct4Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct8:
                AssertTransform2dParity<TColumnOperator, Av1Dct8Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct16:
                AssertTransform2dParity<TColumnOperator, Av1Dct16Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct32:
                AssertTransform2dParity<TColumnOperator, Av1Dct32Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct64:
                AssertTransform2dParity<TColumnOperator, Av1Dct64Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Adst4:
                AssertTransform2dParity<TColumnOperator, Av1Adst4Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Adst8:
                AssertTransform2dParity<TColumnOperator, Av1Adst8Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Adst16:
                AssertTransform2dParity<TColumnOperator, Av1Adst16Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity4:
                AssertTransform2dParity<TColumnOperator, Av1Identity4Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity8:
                AssertTransform2dParity<TColumnOperator, Av1Identity8Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity16:
                AssertTransform2dParity<TColumnOperator, Av1Identity16Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity32:
                AssertTransform2dParity<TColumnOperator, Av1Identity32Inverse1dOperator>(transformType, transformSize, bitDepth, ref config);
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
        where TColumnOperator : struct, IAv1Transform1dOperator
        where TRowOperator : struct, IAv1Transform1dOperator
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
        where TColumnOperator : struct, IAv1Transform1dOperator
        where TRowOperator : struct, IAv1Transform1dOperator
    {
        const int bitDepth = 8;
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int readStride = width + 3;
        int writeStride = width + 7;
        int workspaceLength = Av1TransformWorkspace.GetRequiredLength(transformSize);
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

        Av1Inverse2dTransformer.Transform2dScalar<byte, Av1ByteInverseTransformOutputOperator, TColumnOperator, TRowOperator>(
            coefficients, prediction, readStride, scalar, writeStride, ref config, scalarWorkspace, bitDepth);

        Av1Inverse2dTransformer.Transform2dVector128<byte, Av1ByteInverseTransformOutputOperator, TColumnOperator, TRowOperator>(
            coefficients, prediction, readStride, vector128, writeStride, ref config, vector128Workspace, bitDepth);

        Assert.Equal(scalar, vector128);

        if (width >= Vector256<int>.Count && height >= Vector256<int>.Count)
        {
            byte[] vector256 = new byte[writeStride * height];
            int[] vector256Workspace = new int[workspaceLength];
            Array.Fill(vector256, byte.MaxValue);

            Av1Inverse2dTransformer.Transform2dVector256<byte, Av1ByteInverseTransformOutputOperator, TColumnOperator, TRowOperator>(
                coefficients, prediction, readStride, vector256, writeStride, ref config, vector256Workspace, bitDepth);

            Assert.Equal(scalar, vector256);
        }

        if (width >= Vector512<int>.Count && height >= Vector512<int>.Count)
        {
            byte[] vector512 = new byte[writeStride * height];
            int[] vector512Workspace = new int[workspaceLength];
            Array.Fill(vector512, byte.MaxValue);

            Av1Inverse2dTransformer.Transform2dVector512<byte, Av1ByteInverseTransformOutputOperator, TColumnOperator, TRowOperator>(
                coefficients, prediction, readStride, vector512, writeStride, ref config, vector512Workspace, bitDepth);

            Assert.Equal(scalar, vector512);
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
        where TColumnOperator : struct, IAv1Transform1dOperator
        where TRowOperator : struct, IAv1Transform1dOperator
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int readStride = width + 3;
        int writeStride = width + 7;
        int maximum = (1 << bitDepth) - 1;
        int workspaceLength = Av1TransformWorkspace.GetRequiredLength(transformSize);
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

        Av1Inverse2dTransformer.Transform2dScalar<short, Av1HighBitDepthInverseTransformOutputOperator, TColumnOperator, TRowOperator>(
            coefficients, prediction, readStride, scalar, writeStride, ref config, scalarWorkspace, bitDepth);

        Av1Inverse2dTransformer.Transform2dVector128<short, Av1HighBitDepthInverseTransformOutputOperator, TColumnOperator, TRowOperator>(
            coefficients, prediction, readStride, vector128, writeStride, ref config, vector128Workspace, bitDepth);

        Assert.Equal(scalar, vector128);

        if (width >= Vector256<int>.Count && height >= Vector256<int>.Count)
        {
            short[] vector256 = new short[writeStride * height];
            int[] vector256Workspace = new int[workspaceLength];
            Array.Fill(vector256, short.MinValue);

            Av1Inverse2dTransformer.Transform2dVector256<short, Av1HighBitDepthInverseTransformOutputOperator, TColumnOperator, TRowOperator>(
                coefficients, prediction, readStride, vector256, writeStride, ref config, vector256Workspace, bitDepth);

            Assert.Equal(scalar, vector256);
        }

        if (width >= Vector512<int>.Count && height >= Vector512<int>.Count)
        {
            short[] vector512 = new short[writeStride * height];
            int[] vector512Workspace = new int[workspaceLength];
            Array.Fill(vector512, short.MinValue);

            Av1Inverse2dTransformer.Transform2dVector512<short, Av1HighBitDepthInverseTransformOutputOperator, TColumnOperator, TRowOperator>(
                coefficients, prediction, readStride, vector512, writeStride, ref config, vector512Workspace, bitDepth);

            Assert.Equal(scalar, vector512);
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
