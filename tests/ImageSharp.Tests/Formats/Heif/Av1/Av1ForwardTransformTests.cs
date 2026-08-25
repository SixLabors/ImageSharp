// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1ForwardTransformTests
{
    /// <summary>
    /// Gets every normative transform size, type, and bit-depth combination exercised by the forward and inverse suites.
    /// </summary>
    public static TheoryData<int, int, int> ValidTransformCases { get; } = CreateValidTransformCases();

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

    /// <summary>
    /// Verifies that every applicable SIMD traversal produces the same coefficients as the scalar traversal.
    /// </summary>
    /// <param name="transformTypeValue">The integral <see cref="Av1TransformType"/> value.</param>
    /// <param name="transformSizeValue">The integral <see cref="Av1TransformSize"/> value.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    [Theory]
    [MemberData(nameof(ValidTransformCases))]
    public void TwoDimensionalSimdKernelsMatchScalarForEveryValidConfiguration(
        int transformTypeValue,
        int transformSizeValue,
        int bitDepth)
    {
        Av1TransformType transformType = (Av1TransformType)transformTypeValue;
        Av1TransformSize transformSize = (Av1TransformSize)transformSizeValue;
        Av1Transform2dFlipConfiguration config = Av1Transform2dFlipConfiguration.CreateForward(transformType, transformSize, bitDepth);
        DispatchColumn(transformType, transformSize, bitDepth, ref config);
    }

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

    /// <summary>
    /// Creates the complete normative transform matrix shared by the forward and inverse parity tests.
    /// </summary>
    /// <returns>The transform type, size, and bit-depth cases.</returns>
    private static TheoryData<int, int, int> CreateValidTransformCases()
    {
        TheoryData<int, int, int> cases = [];

        for (Av1TransformSize transformSize = 0; transformSize < Av1TransformSize.AllSizes; transformSize++)
        {
            for (Av1TransformType transformType = 0; transformType < Av1TransformType.AllTransformTypes; transformType++)
            {
                Av1Transform2dFlipConfiguration config = Av1Transform2dFlipConfiguration.CreateForward(transformType, transformSize, 8);

                if (!config.IsAllowed())
                {
                    continue;
                }

                // libaom verifies the low-bit-depth kernel separately from its 10- and 12-bit kernels. Keeping each
                // depth as a distinct case makes any fixed-point range failure identify the exact configuration.
                for (int bitDepth = 8; bitDepth <= 12; bitDepth += 2)
                {
                    cases.Add((int)transformType, (int)transformSize, bitDepth);
                }
            }
        }

        return cases;
    }

    /// <summary>
    /// Closes the static-generic column operator selected by a transform configuration.
    /// </summary>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="config">The forward transform configuration.</param>
    private static void DispatchColumn(
        Av1TransformType transformType,
        Av1TransformSize transformSize,
        int bitDepth,
        ref Av1Transform2dFlipConfiguration config)
    {
        switch (config.TransformFunctionTypeColumn)
        {
            case Av1TransformFunctionType.Dct4:
                DispatchRow<Av1Dct4Forward1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct8:
                DispatchRow<Av1Dct8Forward1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct16:
                DispatchRow<Av1Dct16Forward1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct32:
                DispatchRow<Av1Dct32Forward1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct64:
                DispatchRow<Av1Dct64Forward1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Adst4:
                DispatchRow<Av1Adst4Forward1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Adst8:
                DispatchRow<Av1Adst8Forward1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Adst16:
                DispatchRow<Av1Adst16Forward1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity4:
                DispatchRow<Av1Identity4Forward1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity8:
                DispatchRow<Av1Identity8Forward1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity16:
                DispatchRow<Av1Identity16Forward1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity32:
                DispatchRow<Av1Identity32Forward1dOperator>(transformType, transformSize, bitDepth, ref config);
                break;
            default:
                Assert.Fail($"Unexpected column function {config.TransformFunctionTypeColumn} for {transformType} {transformSize}.");
                break;
        }
    }

    /// <summary>
    /// Closes the static-generic row operator after the column operator has been selected.
    /// </summary>
    /// <typeparam name="TColumnOperator">The selected column operator.</typeparam>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="config">The forward transform configuration.</param>
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
                AssertTransform2dParity<TColumnOperator, Av1Dct4Forward1dOperator>(transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct8:
                AssertTransform2dParity<TColumnOperator, Av1Dct8Forward1dOperator>(transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct16:
                AssertTransform2dParity<TColumnOperator, Av1Dct16Forward1dOperator>(transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct32:
                AssertTransform2dParity<TColumnOperator, Av1Dct32Forward1dOperator>(transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Dct64:
                AssertTransform2dParity<TColumnOperator, Av1Dct64Forward1dOperator>(transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Adst4:
                AssertTransform2dParity<TColumnOperator, Av1Adst4Forward1dOperator>(transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Adst8:
                AssertTransform2dParity<TColumnOperator, Av1Adst8Forward1dOperator>(transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Adst16:
                AssertTransform2dParity<TColumnOperator, Av1Adst16Forward1dOperator>(transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity4:
                AssertTransform2dParity<TColumnOperator, Av1Identity4Forward1dOperator>(transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity8:
                AssertTransform2dParity<TColumnOperator, Av1Identity8Forward1dOperator>(transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity16:
                AssertTransform2dParity<TColumnOperator, Av1Identity16Forward1dOperator>(transformSize, bitDepth, ref config);
                break;
            case Av1TransformFunctionType.Identity32:
                AssertTransform2dParity<TColumnOperator, Av1Identity32Forward1dOperator>(transformSize, bitDepth, ref config);
                break;
            default:
                Assert.Fail($"Unexpected row function {config.TransformFunctionTypeRow} for {transformType} {transformSize}.");
                break;
        }
    }

    /// <summary>
    /// Compares scalar and SIMD forward traversals using padded rows and bounded extreme residuals.
    /// </summary>
    /// <typeparam name="TColumnOperator">The selected column operator.</typeparam>
    /// <typeparam name="TRowOperator">The selected row operator.</typeparam>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="config">The forward transform configuration.</param>
    private static void AssertTransform2dParity<TColumnOperator, TRowOperator>(
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
        short[] input = new short[inputStride * height];

        // Padded rows exercise the same edge-block layout used by the encoder. The alternating extrema are the
        // bounded residual limits used by libaom's SIMD match tests and expose wrapping errors in fixed-point stages.
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int index = (row * width) + column;
                input[(row * inputStride) + column] = (short)((index & 3) switch
                {
                    0 => maximum,
                    1 => -maximum,
                    2 => ((index * 73) % ((maximum * 2) + 1)) - maximum,
                    _ => 0,
                });
            }
        }

        int coefficientCount = width * height;
        int workspaceLength = Av1TransformWorkspace.GetRequiredLength(transformSize);
        int[] scalar = new int[coefficientCount];
        int[] vector128 = new int[coefficientCount];
        int[] scalarWorkspace = new int[workspaceLength];
        int[] vector128Workspace = new int[workspaceLength];

        Av1ForwardTransformer.Transform2dScalar<TColumnOperator, TRowOperator>(input, scalar, (uint)inputStride, ref config, scalarWorkspace);
        Av1ForwardTransformer.Transform2dVector128<TColumnOperator, TRowOperator>(input, vector128, (uint)inputStride, ref config, vector128Workspace);

        Assert.Equal(scalar, vector128);

        // The production dispatcher uses 256-bit lanes only when both axes contain a complete eight-lane tile.
        if (width >= Vector256<int>.Count && height >= Vector256<int>.Count)
        {
            int[] vector256 = new int[coefficientCount];
            int[] vector256Workspace = new int[workspaceLength];
            Av1ForwardTransformer.Transform2dVector256<TColumnOperator, TRowOperator>(input, vector256, (uint)inputStride, ref config, vector256Workspace);

            Assert.Equal(scalar, vector256);
        }
    }

    private static int GetInputValue(int index, int lane) => (((index * 73) + (lane * 151)) % 1023) - 511;
}
