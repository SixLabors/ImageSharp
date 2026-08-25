// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1InverseTransformTests
{
    [Fact]
    public void DctOperatorsProduceIdenticalScalarAndSimdResults()
    {
        AssertOperatorParity<Av1Dct4Inverse1dOperator>(4);
        AssertOperatorParity<Av1Dct8Inverse1dOperator>(8);
        AssertOperatorParity<Av1Dct16Inverse1dOperator>(16);
        AssertOperatorParity<Av1Dct32Inverse1dOperator>(32);
        AssertOperatorParity<Av1Dct64Inverse1dOperator>(64);
    }

    [Fact]
    public void AdstOperatorsProduceIdenticalScalarAndSimdResults()
    {
        AssertOperatorParity<Av1Adst4Inverse1dOperator>(4);
        AssertOperatorParity<Av1Adst8Inverse1dOperator>(8);
        AssertOperatorParity<Av1Adst16Inverse1dOperator>(16);
    }

    [Fact]
    public void IdentityOperatorsProduceIdenticalScalarAndSimdResults()
    {
        AssertOperatorParity<Av1Identity4Inverse1dOperator>(4);
        AssertOperatorParity<Av1Identity8Inverse1dOperator>(8);
        AssertOperatorParity<Av1Identity16Inverse1dOperator>(16);
        AssertOperatorParity<Av1Identity32Inverse1dOperator>(32);
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

    [Fact]
    public void Dct8TwoDimensionalByteSimdKernelsMatchScalar()
        => AssertByteTransform2dParity<Av1Dct8Inverse1dOperator, Av1Dct8Inverse1dOperator>(Av1TransformType.DctDct, Av1TransformSize.Size8x8);

    [Fact]
    public void Adst16TwoDimensionalByteSimdKernelsMatchScalarWithBothFlips()
        => AssertByteTransform2dParity<Av1Adst16Inverse1dOperator, Av1Adst16Inverse1dOperator>(Av1TransformType.FlipAdstFlipAdst, Av1TransformSize.Size16x16);

    [Fact]
    public void RectangularDctTwoDimensionalByteSimdKernelsMatchScalar()
        => AssertByteTransform2dParity<Av1Dct8Inverse1dOperator, Av1Dct16Inverse1dOperator>(Av1TransformType.DctDct, Av1TransformSize.Size16x8);

    [Fact]
    public void Identity32TwoDimensionalHighBitDepthSimdKernelsMatchScalar()
        => AssertHighBitDepthTransform2dParity<Av1Identity32Inverse1dOperator, Av1Identity32Inverse1dOperator>(Av1TransformType.Identity, Av1TransformSize.Size32x32);

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

        TOperator.Transform(ref input128, ref output128, ref step128, cosBit, stageRange);
        TOperator.Transform(ref input256, ref output256, ref step256, cosBit, stageRange);

        int[] scalarInput = new int[length];
        int[] scalarOutput = new int[length];
        int[] scalarStep = new int[length];

        for (int lane = 0; lane < Vector256<int>.Count; lane++)
        {
            for (int index = 0; index < length; index++)
            {
                scalarInput[index] = GetInputValue(index, lane);
            }

            TOperator.Transform(scalarInput, scalarOutput, scalarStep, cosBit, stageRange);

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

    private static void AssertByteTransform2dParity<TColumnOperator, TRowOperator>(Av1TransformType transformType, Av1TransformSize transformSize)
        where TColumnOperator : struct, IAv1Transform1dOperator
        where TRowOperator : struct, IAv1Transform1dOperator
    {
        const int bitDepth = 8;
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int[] coefficients = CreateCoefficients(width * height);
        byte[] prediction = new byte[coefficients.Length];

        for (int index = 0; index < prediction.Length; index++)
        {
            prediction[index] = (byte)(64 + ((index * 29) % 128));
        }

        byte[] scalar = new byte[prediction.Length];
        byte[] vector128 = new byte[prediction.Length];
        byte[] vector256 = new byte[prediction.Length];
        int[] scalarWorkspace = new int[Av1TransformWorkspace.MaximumLength];
        int[] vector128Workspace = new int[Av1TransformWorkspace.MaximumLength];
        int[] vector256Workspace = new int[Av1TransformWorkspace.MaximumLength];
        Av1Transform2dFlipConfiguration config = Av1Transform2dFlipConfiguration.CreateInverse(transformType, transformSize, bitDepth);

        Av1Inverse2dTransformer.Transform2dScalar<byte, Av1ByteInverseTransformOutputOperator, TColumnOperator, TRowOperator>(
            coefficients, prediction, width, scalar, width, ref config, scalarWorkspace, bitDepth);

        Av1Inverse2dTransformer.Transform2dVector128<byte, Av1ByteInverseTransformOutputOperator, TColumnOperator, TRowOperator>(
            coefficients, prediction, width, vector128, width, ref config, vector128Workspace, bitDepth);

        Av1Inverse2dTransformer.Transform2dVector256<byte, Av1ByteInverseTransformOutputOperator, TColumnOperator, TRowOperator>(
            coefficients, prediction, width, vector256, width, ref config, vector256Workspace, bitDepth);

        Assert.Equal(scalar, vector128);
        Assert.Equal(scalar, vector256);
    }

    private static void AssertHighBitDepthTransform2dParity<TColumnOperator, TRowOperator>(Av1TransformType transformType, Av1TransformSize transformSize)
        where TColumnOperator : struct, IAv1Transform1dOperator
        where TRowOperator : struct, IAv1Transform1dOperator
    {
        const int bitDepth = 10;
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int[] coefficients = CreateCoefficients(width * height);
        short[] prediction = new short[coefficients.Length];

        for (int index = 0; index < prediction.Length; index++)
        {
            prediction[index] = (short)(256 + ((index * 47) % 512));
        }

        short[] scalar = new short[prediction.Length];
        short[] vector128 = new short[prediction.Length];
        short[] vector256 = new short[prediction.Length];
        int[] scalarWorkspace = new int[Av1TransformWorkspace.MaximumLength];
        int[] vector128Workspace = new int[Av1TransformWorkspace.MaximumLength];
        int[] vector256Workspace = new int[Av1TransformWorkspace.MaximumLength];
        Av1Transform2dFlipConfiguration config = Av1Transform2dFlipConfiguration.CreateInverse(transformType, transformSize, bitDepth);

        Av1Inverse2dTransformer.Transform2dScalar<short, Av1HighBitDepthInverseTransformOutputOperator, TColumnOperator, TRowOperator>(
            coefficients, prediction, width, scalar, width, ref config, scalarWorkspace, bitDepth);

        Av1Inverse2dTransformer.Transform2dVector128<short, Av1HighBitDepthInverseTransformOutputOperator, TColumnOperator, TRowOperator>(
            coefficients, prediction, width, vector128, width, ref config, vector128Workspace, bitDepth);

        Av1Inverse2dTransformer.Transform2dVector256<short, Av1HighBitDepthInverseTransformOutputOperator, TColumnOperator, TRowOperator>(
            coefficients, prediction, width, vector256, width, ref config, vector256Workspace, bitDepth);

        Assert.Equal(scalar, vector128);
        Assert.Equal(scalar, vector256);
    }

    private static int[] CreateCoefficients(int length)
    {
        int[] coefficients = new int[length];

        for (int index = 0; index < coefficients.Length; index++)
        {
            coefficients[index] = ((index * 37) % 129) - 64;
        }

        return coefficients;
    }

    private static int GetInputValue(int index, int lane) => (((index * 73) + (lane * 151)) % 1023) - 511;
}
