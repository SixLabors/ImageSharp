// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1ForwardTransformTests
{
    /// <summary>
    /// The hardware configurations covering every transform vector tier and the scalar fallback.
    /// </summary>
    private const HwIntrinsics TransformConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Gets every normative transform size, type, and bit-depth combination shared with the inverse suite.
    /// </summary>
    public static TheoryData<int, int, int> ValidTransformCases { get; } = CreateValidTransformCases();

    /// <summary>
    /// Verifies every one-dimensional stage network across its scalar and available vector representations.
    /// </summary>
    [Fact]
    public void OneDimensionalOperatorsMatchAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertOneDimensionalOperators, TransformConfigurations);

    /// <summary>
    /// Verifies every permitted size, type, and bit-depth combination against the direct scalar two-axis definition.
    /// </summary>
    [Fact]
    public void TwoDimensionalPipelineMatchesScalarReferenceAcrossHardwareConfigurations()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertTwoDimensionalPipeline, TransformConfigurations);

    /// <summary>
    /// Verifies that the complete transform dispatcher reuses caller-owned workspace.
    /// </summary>
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

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    /// <summary>
    /// Exercises every DCT, ADST, and identity stage network using both Int16 and Int32 lane arithmetic.
    /// </summary>
    private static void AssertOneDimensionalOperators()
    {
        AssertOperator<Av1Dct4Forward1dOperator>(4);
        AssertOperator<Av1Dct8Forward1dOperator>(8);
        AssertOperator<Av1Dct16Forward1dOperator>(16);
        AssertOperator<Av1Dct32Forward1dOperator>(32);
        AssertOperator<Av1Dct64Forward1dOperator>(64);
        AssertOperator<Av1Adst4Forward1dOperator>(4);
        AssertOperator<Av1Adst8Forward1dOperator>(8);
        AssertOperator<Av1Adst16Forward1dOperator>(16);
        AssertOperator<Av1Identity4Forward1dOperator>(4);
        AssertOperator<Av1Identity8Forward1dOperator>(8);
        AssertOperator<Av1Identity16Forward1dOperator>(16);
        AssertOperator<Av1Identity32Forward1dOperator>(32);
    }

    /// <summary>
    /// Compares one stage network across all available scalar and vector representations.
    /// </summary>
    /// <typeparam name="TOperator">The transform operator.</typeparam>
    /// <param name="length">The transform length.</param>
    private static void AssertOperator<TOperator>(int length)
        where TOperator : struct, IAv1ForwardTransform1dOperator
    {
        const int cosBit = 12;

        AssertInt32Operator<TOperator, Vector128<int>>(length, cosBit);

        if (Vector256.IsHardwareAccelerated)
        {
            AssertInt32Operator<TOperator, Vector256<int>>(length, cosBit);
        }

        if (Vector512.IsHardwareAccelerated)
        {
            AssertInt32Operator<TOperator, Vector512<int>>(length, cosBit);
        }

        AssertInt16Operator<TOperator, Vector128<short>>(length, cosBit);

        if (Avx2.IsSupported)
        {
            AssertInt16Operator<TOperator, Vector256<short>>(length, cosBit);
        }

        if (Avx512BW.IsSupported)
        {
            AssertInt16Operator<TOperator, Vector512<short>>(length, cosBit);
        }
    }

    /// <summary>
    /// Compares one Int32 vector representation with the scalar Int32 stage network lane by lane.
    /// </summary>
    private static void AssertInt32Operator<TOperator, TVector>(int length, int cosBit)
        where TOperator : struct, IAv1ForwardTransform1dOperator
        where TVector : struct
    {
        int laneCount = System.Runtime.CompilerServices.Unsafe.SizeOf<TVector>() / sizeof(int);
        Av1TransformVector<TVector> vectorValues = default;
        Av1TransformVector<TVector> vectorBuffer0 = default;
        Av1TransformVector<TVector> vectorBuffer1 = default;

        for (int index = 0; index < length; index++)
        {
            ref int firstLane = ref System.Runtime.CompilerServices.Unsafe.As<TVector, int>(ref vectorValues[index]);

            for (int lane = 0; lane < laneCount; lane++)
            {
                System.Runtime.CompilerServices.Unsafe.Add(ref firstLane, lane) = GetInputValue(index, lane);
            }
        }

        TOperator.Transform(ref vectorValues, ref vectorBuffer0, ref vectorBuffer1, cosBit);

        for (int lane = 0; lane < laneCount; lane++)
        {
            Av1TransformVector<int> scalarValues = default;
            Av1TransformVector<int> scalarBuffer0 = default;
            Av1TransformVector<int> scalarBuffer1 = default;

            for (int index = 0; index < length; index++)
            {
                scalarValues[index] = GetInputValue(index, lane);
            }

            TOperator.Transform(ref scalarValues, ref scalarBuffer0, ref scalarBuffer1, cosBit);

            for (int index = 0; index < length; index++)
            {
                ref int firstLane = ref System.Runtime.CompilerServices.Unsafe.As<TVector, int>(ref vectorBuffer0[index]);
                Assert.Equal(scalarBuffer0[index], System.Runtime.CompilerServices.Unsafe.Add(ref firstLane, lane));
            }
        }
    }

    /// <summary>
    /// Compares one Int16 vector representation with the scalar Int16 stage network lane by lane.
    /// </summary>
    private static void AssertInt16Operator<TOperator, TVector>(int length, int cosBit)
        where TOperator : struct, IAv1ForwardTransform1dOperator
        where TVector : struct
    {
        int laneCount = System.Runtime.CompilerServices.Unsafe.SizeOf<TVector>() / sizeof(short);
        Av1TransformVector<TVector> vectorValues = default;
        Av1TransformVector<TVector> vectorBuffer0 = default;
        Av1TransformVector<TVector> vectorBuffer1 = default;

        for (int index = 0; index < length; index++)
        {
            ref short firstLane = ref System.Runtime.CompilerServices.Unsafe.As<TVector, short>(ref vectorValues[index]);

            for (int lane = 0; lane < laneCount; lane++)
            {
                System.Runtime.CompilerServices.Unsafe.Add(ref firstLane, lane) = GetPackedInputValue(index, lane);
            }
        }

        TOperator.Transform(ref vectorValues, ref vectorBuffer0, ref vectorBuffer1, cosBit);

        for (int lane = 0; lane < laneCount; lane++)
        {
            Av1TransformVector<short> scalarValues = default;
            Av1TransformVector<short> scalarBuffer0 = default;
            Av1TransformVector<short> scalarBuffer1 = default;

            for (int index = 0; index < length; index++)
            {
                scalarValues[index] = GetPackedInputValue(index, lane);
            }

            TOperator.Transform(ref scalarValues, ref scalarBuffer0, ref scalarBuffer1, cosBit);

            for (int index = 0; index < length; index++)
            {
                ref short firstLane = ref System.Runtime.CompilerServices.Unsafe.As<TVector, short>(ref vectorBuffer0[index]);
                Assert.Equal(scalarBuffer0[index], System.Runtime.CompilerServices.Unsafe.Add(ref firstLane, lane));
            }
        }
    }

    /// <summary>
    /// Exercises the complete normative transform matrix for the active hardware configuration.
    /// </summary>
    private static void AssertTwoDimensionalPipeline()
    {
        for (Av1TransformSize transformSize = 0; transformSize < Av1TransformSize.AllSizes; transformSize++)
        {
            for (Av1TransformType transformType = 0; transformType < Av1TransformType.AllTransformTypes; transformType++)
            {
                Av1Transform2dFlipConfiguration config = Av1Transform2dFlipConfiguration.CreateForward(transformType, transformSize, 8);

                if (!config.IsAllowed())
                {
                    continue;
                }

                for (int bitDepth = 8; bitDepth <= 12; bitDepth += 2)
                {
                    AssertTwoDimensionalCase(transformType, transformSize, bitDepth);
                }
            }
        }
    }

    /// <summary>
    /// Compares one complete transform with the direct scalar two-axis definition.
    /// </summary>
    private static void AssertTwoDimensionalCase(Av1TransformType transformType, Av1TransformSize transformSize, int bitDepth)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int inputStride = width + 3;
        Av1TransformSize adjustedSize = transformSize.GetAdjusted();
        int coefficientCount = adjustedSize.GetWidth() * adjustedSize.GetHeight();
        short[] input = new short[inputStride * height];
        int sampleMaximum = (1 << bitDepth) - 1;

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int index = (row * width) + column;
                input[(row * inputStride) + column] = (short)((index & 3) switch
                {
                    0 => sampleMaximum,
                    1 => -sampleMaximum,
                    2 => ((index * 73) % ((2 * sampleMaximum) + 1)) - sampleMaximum,
                    _ => 0,
                });
            }
        }

        int[] expected = new int[coefficientCount];
        int[] actual = new int[coefficientCount];
        int[] workspace = new int[Av1TransformWorkspace.MaximumLength];
        Av1Transform2dFlipConfiguration config = Av1Transform2dFlipConfiguration.CreateForward(transformType, transformSize, bitDepth);

        DispatchReferenceColumn(input, inputStride, expected, ref config);
        Av1ForwardTransformer.Transform2d(input, actual, (uint)inputStride, transformType, transformSize, bitDepth, workspace);
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Selects the scalar reference column operator.
    /// </summary>
    private static void DispatchReferenceColumn(Span<short> input, int stride, Span<int> output, ref Av1Transform2dFlipConfiguration config)
    {
        switch (config.TransformFunctionTypeColumn)
        {
            case Av1TransformFunctionType.Dct4:
                DispatchReferenceRow<Av1Dct4Forward1dOperator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Dct8:
                DispatchReferenceRow<Av1Dct8Forward1dOperator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Dct16:
                DispatchReferenceRow<Av1Dct16Forward1dOperator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Dct32:
                DispatchReferenceRow<Av1Dct32Forward1dOperator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Dct64:
                DispatchReferenceRow<Av1Dct64Forward1dOperator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Adst4:
                DispatchReferenceRow<Av1Adst4Forward1dOperator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Adst8:
                DispatchReferenceRow<Av1Adst8Forward1dOperator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Adst16:
                DispatchReferenceRow<Av1Adst16Forward1dOperator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Identity4:
                DispatchReferenceRow<Av1Identity4Forward1dOperator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Identity8:
                DispatchReferenceRow<Av1Identity8Forward1dOperator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Identity16:
                DispatchReferenceRow<Av1Identity16Forward1dOperator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Identity32:
                DispatchReferenceRow<Av1Identity32Forward1dOperator>(input, stride, output, ref config);
                break;
        }
    }

    /// <summary>
    /// Selects the scalar reference row operator.
    /// </summary>
    private static void DispatchReferenceRow<TColumnOperator>(Span<short> input, int stride, Span<int> output, ref Av1Transform2dFlipConfiguration config)
        where TColumnOperator : struct, IAv1ForwardTransform1dOperator
    {
        switch (config.TransformFunctionTypeRow)
        {
            case Av1TransformFunctionType.Dct4:
                TransformReference<TColumnOperator, Av1Dct4Forward1dOperator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Dct8:
                TransformReference<TColumnOperator, Av1Dct8Forward1dOperator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Dct16:
                TransformReference<TColumnOperator, Av1Dct16Forward1dOperator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Dct32:
                TransformReference<TColumnOperator, Av1Dct32Forward1dOperator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Dct64:
                TransformReference<TColumnOperator, Av1Dct64Forward1dOperator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Adst4:
                TransformReference<TColumnOperator, Av1Adst4Forward1dOperator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Adst8:
                TransformReference<TColumnOperator, Av1Adst8Forward1dOperator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Adst16:
                TransformReference<TColumnOperator, Av1Adst16Forward1dOperator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Identity4:
                TransformReference<TColumnOperator, Av1Identity4Forward1dOperator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Identity8:
                TransformReference<TColumnOperator, Av1Identity8Forward1dOperator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Identity16:
                TransformReference<TColumnOperator, Av1Identity16Forward1dOperator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Identity32:
                TransformReference<TColumnOperator, Av1Identity32Forward1dOperator>(input, stride, output, ref config);
                break;
        }
    }

    /// <summary>
    /// Applies the direct scalar column and row transform definition used as the layout and dispatch oracle.
    /// </summary>
    private static void TransformReference<TColumnOperator, TRowOperator>(
        Span<short> input,
        int stride,
        Span<int> output,
        ref Av1Transform2dFlipConfiguration config)
        where TColumnOperator : struct, IAv1ForwardTransform1dOperator
        where TRowOperator : struct, IAv1ForwardTransform1dOperator
    {
        int width = config.TransformSize.GetWidth();
        int height = config.TransformSize.GetHeight();
        int outputWidth = Math.Min(width, 32);
        int outputHeight = Math.Min(height, 32);
        int[] intermediate = new int[width * height];
        Av1TransformVector<int> values = default;
        Av1TransformVector<int> buffer0 = default;
        Av1TransformVector<int> buffer1 = default;

        for (int column = 0; column < width; column++)
        {
            for (int row = 0; row < height; row++)
            {
                int sourceRow = config.FlipUpsideDown ? height - row - 1 : row;
                values[row] = input[(sourceRow * stride) + column] << config.Shift0;
            }

            TColumnOperator.Transform(ref values, ref buffer0, ref buffer1, config.CosBitColumn);
            int destinationColumn = config.FlipLeftToRight ? width - column - 1 : column;

            for (int row = 0; row < height; row++)
            {
                intermediate[(row * width) + destinationColumn] = Av1Math.RoundShift(buffer0[row], -config.Shift1);
            }
        }

        bool normalizeRectangle = Math.Abs(config.TransformSize.GetRectangleLogRatio()) == 1;

        for (int row = 0; row < outputHeight; row++)
        {
            for (int column = 0; column < width; column++)
            {
                values[column] = intermediate[(row * width) + column];
            }

            TRowOperator.Transform(ref values, ref buffer0, ref buffer1, config.CosBitRow);

            for (int column = 0; column < outputWidth; column++)
            {
                int value = Av1Math.RoundShift(buffer0[column], -config.Shift2);
                output[(row * outputWidth) + column] = normalizeRectangle
                    ? Av1Transform1dMath.HalfButterfly(Av1Transform1dMath.NewSqrt2, value, 0, 0, Av1Transform1dMath.NewSqrt2Bits)
                    : value;
            }
        }
    }

    /// <summary>
    /// Gets a deterministic signed thirty-two-bit transform input.
    /// </summary>
    private static int GetInputValue(int index, int lane)
        => (((index * 73) + (lane * 151)) % 8191) - 4095;

    /// <summary>
    /// Gets a deterministic signed sixteen-bit input including overflow-sensitive edge values.
    /// </summary>
    private static short GetPackedInputValue(int index, int lane)
        => (short)((index + lane) % 5 switch
        {
            0 => short.MaxValue,
            1 => short.MinValue,
            2 => 255,
            3 => -255,
            _ => ((index * 73) + (lane * 151)) % 511 - 255,
        });

    /// <summary>
    /// Creates the complete normative transform matrix shared by the forward and inverse tests.
    /// </summary>
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

                for (int bitDepth = 8; bitDepth <= 12; bitDepth += 2)
                {
                    cases.Add((int)transformType, (int)transformSize, bitDepth);
                }
            }
        }

        return cases;
    }
}
