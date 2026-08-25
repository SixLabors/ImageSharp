// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1ForwardTransformTests
{
    [Fact]
    public void DctOperatorsProduceIdenticalScalarAndSimdResults()
    {
        AssertOperatorParity<Av1Dct4Forward1dOperator>(4);
        AssertOperatorParity<Av1Dct8Forward1dOperator>(8);
        AssertOperatorParity<Av1Dct16Forward1dOperator>(16);
        AssertOperatorParity<Av1Dct32Forward1dOperator>(32);
        AssertOperatorParity<Av1Dct64Forward1dOperator>(64);
    }

    [Fact]
    public void AdstOperatorsProduceIdenticalScalarAndSimdResults()
    {
        AssertOperatorParity<Av1Adst4Forward1dOperator>(4);
        AssertOperatorParity<Av1Adst8Forward1dOperator>(8);
        AssertOperatorParity<Av1Adst16Forward1dOperator>(16);
    }

    [Fact]
    public void IdentityOperatorsProduceIdenticalScalarAndSimdResults()
    {
        AssertOperatorParity<Av1Identity4Forward1dOperator>(4);
        AssertOperatorParity<Av1Identity8Forward1dOperator>(8);
        AssertOperatorParity<Av1Identity16Forward1dOperator>(16);
        AssertOperatorParity<Av1Identity32Forward1dOperator>(32);
    }

    [Fact]
    public void Dct8TwoDimensionalSimdKernelsMatchScalar()
        => AssertTransform2dParity<Av1Dct8Forward1dOperator, Av1Dct8Forward1dOperator>(Av1TransformType.DctDct, Av1TransformSize.Size8x8);

    [Fact]
    public void Adst16TwoDimensionalSimdKernelsMatchScalarWithBothFlips()
        => AssertTransform2dParity<Av1Adst16Forward1dOperator, Av1Adst16Forward1dOperator>(Av1TransformType.FlipAdstFlipAdst, Av1TransformSize.Size16x16);

    [Fact]
    public void RectangularDctTwoDimensionalSimdKernelsMatchScalar()
        => AssertTransform2dParity<Av1Dct8Forward1dOperator, Av1Dct16Forward1dOperator>(Av1TransformType.DctDct, Av1TransformSize.Size16x8);

    [Fact]
    public void Identity32TwoDimensionalSimdKernelsMatchScalar()
        => AssertTransform2dParity<Av1Identity32Forward1dOperator, Av1Identity32Forward1dOperator>(Av1TransformType.Identity, Av1TransformSize.Size32x32);

    [Fact]
    public void TransformDispatchDoesNotAllocatePerBlock()
    {
        const int width = 8;
        short[] input = new short[width * width];
        int[] output = new int[input.Length];
        int[] workspace = new int[Av1TransformWorkspace.MaximumLength];

        Av1ForwardTransformer.Transform2d(input, output, width, Av1TransformType.DctDct, Av1TransformSize.Size8x8, 8, workspace);
        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int iteration = 0; iteration < 32; iteration++)
        {
            Av1ForwardTransformer.Transform2d(input, output, width, Av1TransformType.DctDct, Av1TransformSize.Size8x8, 8, workspace);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
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

    private static void AssertTransform2dParity<TColumnOperator, TRowOperator>(Av1TransformType transformType, Av1TransformSize transformSize)
        where TColumnOperator : struct, IAv1Transform1dOperator
        where TRowOperator : struct, IAv1Transform1dOperator
    {
        const int bitDepth = 10;
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        short[] input = new short[width * height];

        for (int index = 0; index < input.Length; index++)
        {
            input[index] = (short)(((index * 73) % 1023) - 511);
        }

        Av1Transform2dFlipConfiguration config = Av1Transform2dFlipConfiguration.CreateForward(transformType, transformSize, bitDepth);
        int[] scalar = new int[input.Length];
        int[] vector128 = new int[input.Length];
        int[] vector256 = new int[input.Length];
        int[] scalarWorkspace = new int[Av1TransformWorkspace.MaximumLength];
        int[] vector128Workspace = new int[Av1TransformWorkspace.MaximumLength];
        int[] vector256Workspace = new int[Av1TransformWorkspace.MaximumLength];

        Av1ForwardTransformer.Transform2dScalar<TColumnOperator, TRowOperator>(input, scalar, (uint)width, ref config, scalarWorkspace);
        Av1ForwardTransformer.Transform2dVector128<TColumnOperator, TRowOperator>(input, vector128, (uint)width, ref config, vector128Workspace);
        Av1ForwardTransformer.Transform2dVector256<TColumnOperator, TRowOperator>(input, vector256, (uint)width, ref config, vector256Workspace);

        Assert.Equal(scalar, vector128);
        Assert.Equal(scalar, vector256);
    }

    private static int GetInputValue(int index, int lane) => (((index * 73) + (lane * 151)) % 1023) - 511;
}
