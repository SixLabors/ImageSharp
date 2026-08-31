// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 forward transform arithmetic, dispatch, layout, and allocation behavior.
/// </summary>
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
    /// Verifies every one-dimensional stage network against the independent analytical transform definition.
    /// </summary>
    [Fact]
    public void OneDimensionalOperatorsMatchAnalyticalReference()
    {
        AssertOperatorAccuracy<Av1ForwardTransformer.Dct4Operator>(Av1TransformType1d.Dct, 4);
        AssertOperatorAccuracy<Av1ForwardTransformer.Dct8Operator>(Av1TransformType1d.Dct, 8);
        AssertOperatorAccuracy<Av1ForwardTransformer.Dct16Operator>(Av1TransformType1d.Dct, 16);
        AssertOperatorAccuracy<Av1ForwardTransformer.Dct32Operator>(Av1TransformType1d.Dct, 32);
        AssertOperatorAccuracy<Av1ForwardTransformer.Dct64Operator>(Av1TransformType1d.Dct, 64);
        AssertOperatorAccuracy<Av1ForwardTransformer.Adst4Operator>(Av1TransformType1d.Adst, 4);
        AssertOperatorAccuracy<Av1ForwardTransformer.Adst8Operator>(Av1TransformType1d.Adst, 8);
        AssertOperatorAccuracy<Av1ForwardTransformer.Adst16Operator>(Av1TransformType1d.Adst, 16);
        AssertOperatorAccuracy<Av1ForwardTransformer.Identity4Operator>(Av1TransformType1d.Identity, 4);
        AssertOperatorAccuracy<Av1ForwardTransformer.Identity8Operator>(Av1TransformType1d.Identity, 8);
        AssertOperatorAccuracy<Av1ForwardTransformer.Identity16Operator>(Av1TransformType1d.Identity, 16);
        AssertOperatorAccuracy<Av1ForwardTransformer.Identity32Operator>(Av1TransformType1d.Identity, 32);
    }

    /// <summary>
    /// Verifies every permitted size, type, and bit-depth combination against the direct scalar two-axis definition.
    /// </summary>
    [Fact]
    public void TwoDimensionalPipelineMatchesReference()
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
        AssertOperator<Av1ForwardTransformer.Dct4Operator>(4);
        AssertOperator<Av1ForwardTransformer.Dct8Operator>(8);
        AssertOperator<Av1ForwardTransformer.Dct16Operator>(16);
        AssertOperator<Av1ForwardTransformer.Dct32Operator>(32);
        AssertOperator<Av1ForwardTransformer.Dct64Operator>(64);
        AssertOperator<Av1ForwardTransformer.Adst4Operator>(4);
        AssertOperator<Av1ForwardTransformer.Adst8Operator>(8);
        AssertOperator<Av1ForwardTransformer.Adst16Operator>(16);
        AssertOperator<Av1ForwardTransformer.Identity4Operator>(4);
        AssertOperator<Av1ForwardTransformer.Identity8Operator>(8);
        AssertOperator<Av1ForwardTransformer.Identity16Operator>(16);
        AssertOperator<Av1ForwardTransformer.Identity32Operator>(32);
    }

    /// <summary>
    /// Compares one stage network across all available scalar and vector representations.
    /// </summary>
    /// <typeparam name="TOperator">The transform operator.</typeparam>
    /// <param name="length">The transform length.</param>
    private static void AssertOperator<TOperator>(int length)
        where TOperator : struct, Av1ForwardTransformer.IAv1ForwardTransform1dOperator
    {
        const int cosBit = 12;

        AssertInt32Vector128Operator<TOperator>(length, cosBit);

        if (Vector256.IsHardwareAccelerated)
        {
            AssertInt32Vector256Operator<TOperator>(length, cosBit);
        }

        if (Vector512.IsHardwareAccelerated)
        {
            AssertInt32Vector512Operator<TOperator>(length, cosBit);
        }

        AssertInt16Vector128Operator<TOperator>(length, cosBit);

        if (Avx2.IsSupported)
        {
            AssertInt16Vector256Operator<TOperator>(length, cosBit);
        }

        if (Avx512BW.IsSupported)
        {
            AssertInt16Vector512Operator<TOperator>(length, cosBit);
        }
    }

    /// <summary>
    /// Compares one integer stage network with the analytical reference transform.
    /// </summary>
    /// <typeparam name="TOperator">The transform operator.</typeparam>
    /// <param name="transformType">The analytical transform definition.</param>
    /// <param name="length">The transform length.</param>
    private static void AssertOperatorAccuracy<TOperator>(Av1TransformType1d transformType, int length)
        where TOperator : struct, Av1ForwardTransformer.IAv1ForwardTransform1dOperator
    {
        const int cosBit = 13;
        const int testBlockCount = 500;
        const int maximumCoefficientError = 7;
        Random random = new(0);
        double[] referenceInput = new double[length];
        double[] referenceOutput = new double[length];
        Av1TransformVector<int> values = default;
        Av1TransformVector<int> buffer0 = default;
        Av1TransformVector<int> buffer1 = default;

        for (int block = 0; block < testBlockCount; block++)
        {
            for (int index = 0; index < length; index++)
            {
                int input = random.Next(1024) - random.Next(1024);
                values[index] = input;
                referenceInput[index] = input;
            }

            ref byte valuesBase = ref System.Runtime.CompilerServices.Unsafe.As<Av1TransformVector<int>, byte>(ref values);

            TOperator.Transform(ref valuesBase, sizeof(int), sizeof(int), ref buffer0, ref buffer1, cosBit);
            Av1ReferenceTransform.ReferenceTransform1d(transformType, referenceInput, referenceOutput, length);

            // the reference decoder permits seven integer coefficient units because each fixed-point butterfly rounds independently.
            for (int index = 0; index < length; index++)
            {
                int expected = (int)Math.Round(referenceOutput[index], MidpointRounding.AwayFromZero);
                int error = Math.Abs(values[index] - expected);

                Assert.True(
                    error <= maximumCoefficientError,
                    $"{typeof(TOperator).Name} coefficient {index}: expected {expected}, actual {values[index]}, error {error}.");
            }
        }
    }

    /// <summary>
    /// Compares the Vector128 Int32 representation with the scalar stage network lane by lane.
    /// </summary>
    /// <typeparam name="TOperator">The transform operator.</typeparam>
    /// <param name="length">The transform length.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    private static void AssertInt32Vector128Operator<TOperator>(int length, int cosBit)
        where TOperator : struct, Av1ForwardTransformer.IAv1ForwardTransform1dOperator
    {
        int laneCount = System.Runtime.CompilerServices.Unsafe.SizeOf<Vector128<int>>() / sizeof(int);
        Av1TransformVector<Vector128<int>> vectorValues = default;
        Av1TransformVector<Vector128<int>> vectorBuffer0 = default;
        Av1TransformVector<Vector128<int>> vectorBuffer1 = default;

        for (int index = 0; index < length; index++)
        {
            ref int firstLane = ref System.Runtime.CompilerServices.Unsafe.As<Vector128<int>, int>(ref vectorValues[index]);

            for (int lane = 0; lane < laneCount; lane++)
            {
                System.Runtime.CompilerServices.Unsafe.Add(ref firstLane, lane) = GetInputValue(index, lane);
            }
        }

        ref byte vectorValuesBase = ref System.Runtime.CompilerServices.Unsafe.As<Av1TransformVector<Vector128<int>>, byte>(ref vectorValues);
        nint vectorStride = System.Runtime.CompilerServices.Unsafe.SizeOf<Vector128<int>>();

        TOperator.Transform(ref vectorValuesBase, vectorStride, vectorStride, ref vectorBuffer0, ref vectorBuffer1, cosBit);

        for (int lane = 0; lane < laneCount; lane++)
        {
            Av1TransformVector<int> scalarValues = default;
            Av1TransformVector<int> scalarBuffer0 = default;
            Av1TransformVector<int> scalarBuffer1 = default;

            for (int index = 0; index < length; index++)
            {
                scalarValues[index] = GetInputValue(index, lane);
            }

            ref byte scalarValuesBase = ref System.Runtime.CompilerServices.Unsafe.As<Av1TransformVector<int>, byte>(ref scalarValues);

            TOperator.Transform(ref scalarValuesBase, sizeof(int), sizeof(int), ref scalarBuffer0, ref scalarBuffer1, cosBit);

            for (int index = 0; index < length; index++)
            {
                ref int firstLane = ref System.Runtime.CompilerServices.Unsafe.As<Vector128<int>, int>(ref vectorValues[index]);
                Assert.Equal(scalarValues[index], System.Runtime.CompilerServices.Unsafe.Add(ref firstLane, lane));
            }
        }
    }

    /// <summary>
    /// Compares the Vector256 Int32 representation with the scalar stage network lane by lane.
    /// </summary>
    /// <typeparam name="TOperator">The transform operator.</typeparam>
    /// <param name="length">The transform length.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    private static void AssertInt32Vector256Operator<TOperator>(int length, int cosBit)
        where TOperator : struct, Av1ForwardTransformer.IAv1ForwardTransform1dOperator
    {
        int laneCount = System.Runtime.CompilerServices.Unsafe.SizeOf<Vector256<int>>() / sizeof(int);
        Av1TransformVector<Vector256<int>> vectorValues = default;
        Av1TransformVector<Vector256<int>> vectorBuffer0 = default;
        Av1TransformVector<Vector256<int>> vectorBuffer1 = default;

        for (int index = 0; index < length; index++)
        {
            ref int firstLane = ref System.Runtime.CompilerServices.Unsafe.As<Vector256<int>, int>(ref vectorValues[index]);

            for (int lane = 0; lane < laneCount; lane++)
            {
                System.Runtime.CompilerServices.Unsafe.Add(ref firstLane, lane) = GetInputValue(index, lane);
            }
        }

        ref byte vectorValuesBase = ref System.Runtime.CompilerServices.Unsafe.As<Av1TransformVector<Vector256<int>>, byte>(ref vectorValues);
        nint vectorStride = System.Runtime.CompilerServices.Unsafe.SizeOf<Vector256<int>>();

        TOperator.Transform(ref vectorValuesBase, vectorStride, vectorStride, ref vectorBuffer0, ref vectorBuffer1, cosBit);

        for (int lane = 0; lane < laneCount; lane++)
        {
            Av1TransformVector<int> scalarValues = default;
            Av1TransformVector<int> scalarBuffer0 = default;
            Av1TransformVector<int> scalarBuffer1 = default;

            for (int index = 0; index < length; index++)
            {
                scalarValues[index] = GetInputValue(index, lane);
            }

            ref byte scalarValuesBase = ref System.Runtime.CompilerServices.Unsafe.As<Av1TransformVector<int>, byte>(ref scalarValues);

            TOperator.Transform(ref scalarValuesBase, sizeof(int), sizeof(int), ref scalarBuffer0, ref scalarBuffer1, cosBit);

            for (int index = 0; index < length; index++)
            {
                ref int firstLane = ref System.Runtime.CompilerServices.Unsafe.As<Vector256<int>, int>(ref vectorValues[index]);
                Assert.Equal(scalarValues[index], System.Runtime.CompilerServices.Unsafe.Add(ref firstLane, lane));
            }
        }
    }

    /// <summary>
    /// Compares the Vector512 Int32 representation with the scalar stage network lane by lane.
    /// </summary>
    /// <typeparam name="TOperator">The transform operator.</typeparam>
    /// <param name="length">The transform length.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    private static void AssertInt32Vector512Operator<TOperator>(int length, int cosBit)
        where TOperator : struct, Av1ForwardTransformer.IAv1ForwardTransform1dOperator
    {
        int laneCount = System.Runtime.CompilerServices.Unsafe.SizeOf<Vector512<int>>() / sizeof(int);
        Av1TransformVector<Vector512<int>> vectorValues = default;
        Av1TransformVector<Vector512<int>> vectorBuffer0 = default;
        Av1TransformVector<Vector512<int>> vectorBuffer1 = default;

        for (int index = 0; index < length; index++)
        {
            ref int firstLane = ref System.Runtime.CompilerServices.Unsafe.As<Vector512<int>, int>(ref vectorValues[index]);

            for (int lane = 0; lane < laneCount; lane++)
            {
                System.Runtime.CompilerServices.Unsafe.Add(ref firstLane, lane) = GetInputValue(index, lane);
            }
        }

        ref byte vectorValuesBase = ref System.Runtime.CompilerServices.Unsafe.As<Av1TransformVector<Vector512<int>>, byte>(ref vectorValues);
        nint vectorStride = System.Runtime.CompilerServices.Unsafe.SizeOf<Vector512<int>>();

        TOperator.Transform(ref vectorValuesBase, vectorStride, vectorStride, ref vectorBuffer0, ref vectorBuffer1, cosBit);

        for (int lane = 0; lane < laneCount; lane++)
        {
            Av1TransformVector<int> scalarValues = default;
            Av1TransformVector<int> scalarBuffer0 = default;
            Av1TransformVector<int> scalarBuffer1 = default;

            for (int index = 0; index < length; index++)
            {
                scalarValues[index] = GetInputValue(index, lane);
            }

            ref byte scalarValuesBase = ref System.Runtime.CompilerServices.Unsafe.As<Av1TransformVector<int>, byte>(ref scalarValues);

            TOperator.Transform(ref scalarValuesBase, sizeof(int), sizeof(int), ref scalarBuffer0, ref scalarBuffer1, cosBit);

            for (int index = 0; index < length; index++)
            {
                ref int firstLane = ref System.Runtime.CompilerServices.Unsafe.As<Vector512<int>, int>(ref vectorValues[index]);
                Assert.Equal(scalarValues[index], System.Runtime.CompilerServices.Unsafe.Add(ref firstLane, lane));
            }
        }
    }

    /// <summary>
    /// Compares the Vector128 Int16 representation with the scalar stage network lane by lane.
    /// </summary>
    /// <typeparam name="TOperator">The transform operator.</typeparam>
    /// <param name="length">The transform length.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    private static void AssertInt16Vector128Operator<TOperator>(int length, int cosBit)
        where TOperator : struct, Av1ForwardTransformer.IAv1ForwardTransform1dOperator
    {
        int laneCount = System.Runtime.CompilerServices.Unsafe.SizeOf<Vector128<short>>() / sizeof(short);
        Av1TransformVector<Vector128<short>> vectorValues = default;
        Av1TransformVector<Vector128<short>> vectorBuffer0 = default;
        Av1TransformVector<Vector128<short>> vectorBuffer1 = default;

        for (int index = 0; index < length; index++)
        {
            ref short firstLane = ref System.Runtime.CompilerServices.Unsafe.As<Vector128<short>, short>(ref vectorValues[index]);

            for (int lane = 0; lane < laneCount; lane++)
            {
                System.Runtime.CompilerServices.Unsafe.Add(ref firstLane, lane) = GetPackedInputValue(index, lane);
            }
        }

        ref byte vectorValuesBase = ref System.Runtime.CompilerServices.Unsafe.As<Av1TransformVector<Vector128<short>>, byte>(ref vectorValues);
        nint vectorStride = System.Runtime.CompilerServices.Unsafe.SizeOf<Vector128<short>>();

        TOperator.Transform(ref vectorValuesBase, vectorStride, vectorStride, ref vectorBuffer0, ref vectorBuffer1, cosBit);

        for (int lane = 0; lane < laneCount; lane++)
        {
            Av1TransformVector<short> scalarValues = default;
            Av1TransformVector<short> scalarBuffer0 = default;
            Av1TransformVector<short> scalarBuffer1 = default;

            for (int index = 0; index < length; index++)
            {
                scalarValues[index] = GetPackedInputValue(index, lane);
            }

            ref byte scalarValuesBase = ref System.Runtime.CompilerServices.Unsafe.As<Av1TransformVector<short>, byte>(ref scalarValues);

            TOperator.Transform(ref scalarValuesBase, sizeof(short), sizeof(short), ref scalarBuffer0, ref scalarBuffer1, cosBit);

            for (int index = 0; index < length; index++)
            {
                ref short firstLane = ref System.Runtime.CompilerServices.Unsafe.As<Vector128<short>, short>(ref vectorValues[index]);
                Assert.Equal(scalarValues[index], System.Runtime.CompilerServices.Unsafe.Add(ref firstLane, lane));
            }
        }
    }

    /// <summary>
    /// Compares the Vector256 Int16 representation with the scalar stage network lane by lane.
    /// </summary>
    /// <typeparam name="TOperator">The transform operator.</typeparam>
    /// <param name="length">The transform length.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    private static void AssertInt16Vector256Operator<TOperator>(int length, int cosBit)
        where TOperator : struct, Av1ForwardTransformer.IAv1ForwardTransform1dOperator
    {
        int laneCount = System.Runtime.CompilerServices.Unsafe.SizeOf<Vector256<short>>() / sizeof(short);
        Av1TransformVector<Vector256<short>> vectorValues = default;
        Av1TransformVector<Vector256<short>> vectorBuffer0 = default;
        Av1TransformVector<Vector256<short>> vectorBuffer1 = default;

        for (int index = 0; index < length; index++)
        {
            ref short firstLane = ref System.Runtime.CompilerServices.Unsafe.As<Vector256<short>, short>(ref vectorValues[index]);

            for (int lane = 0; lane < laneCount; lane++)
            {
                System.Runtime.CompilerServices.Unsafe.Add(ref firstLane, lane) = GetPackedInputValue(index, lane);
            }
        }

        ref byte vectorValuesBase = ref System.Runtime.CompilerServices.Unsafe.As<Av1TransformVector<Vector256<short>>, byte>(ref vectorValues);
        nint vectorStride = System.Runtime.CompilerServices.Unsafe.SizeOf<Vector256<short>>();

        TOperator.Transform(ref vectorValuesBase, vectorStride, vectorStride, ref vectorBuffer0, ref vectorBuffer1, cosBit);

        for (int lane = 0; lane < laneCount; lane++)
        {
            Av1TransformVector<short> scalarValues = default;
            Av1TransformVector<short> scalarBuffer0 = default;
            Av1TransformVector<short> scalarBuffer1 = default;

            for (int index = 0; index < length; index++)
            {
                scalarValues[index] = GetPackedInputValue(index, lane);
            }

            ref byte scalarValuesBase = ref System.Runtime.CompilerServices.Unsafe.As<Av1TransformVector<short>, byte>(ref scalarValues);

            TOperator.Transform(ref scalarValuesBase, sizeof(short), sizeof(short), ref scalarBuffer0, ref scalarBuffer1, cosBit);

            for (int index = 0; index < length; index++)
            {
                ref short firstLane = ref System.Runtime.CompilerServices.Unsafe.As<Vector256<short>, short>(ref vectorValues[index]);
                Assert.Equal(scalarValues[index], System.Runtime.CompilerServices.Unsafe.Add(ref firstLane, lane));
            }
        }
    }

    /// <summary>
    /// Compares the Vector512 Int16 representation with the scalar stage network lane by lane.
    /// </summary>
    /// <typeparam name="TOperator">The transform operator.</typeparam>
    /// <param name="length">The transform length.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    private static void AssertInt16Vector512Operator<TOperator>(int length, int cosBit)
        where TOperator : struct, Av1ForwardTransformer.IAv1ForwardTransform1dOperator
    {
        int laneCount = System.Runtime.CompilerServices.Unsafe.SizeOf<Vector512<short>>() / sizeof(short);
        Av1TransformVector<Vector512<short>> vectorValues = default;
        Av1TransformVector<Vector512<short>> vectorBuffer0 = default;
        Av1TransformVector<Vector512<short>> vectorBuffer1 = default;

        for (int index = 0; index < length; index++)
        {
            ref short firstLane = ref System.Runtime.CompilerServices.Unsafe.As<Vector512<short>, short>(ref vectorValues[index]);

            for (int lane = 0; lane < laneCount; lane++)
            {
                System.Runtime.CompilerServices.Unsafe.Add(ref firstLane, lane) = GetPackedInputValue(index, lane);
            }
        }

        ref byte vectorValuesBase = ref System.Runtime.CompilerServices.Unsafe.As<Av1TransformVector<Vector512<short>>, byte>(ref vectorValues);
        nint vectorStride = System.Runtime.CompilerServices.Unsafe.SizeOf<Vector512<short>>();

        TOperator.Transform(ref vectorValuesBase, vectorStride, vectorStride, ref vectorBuffer0, ref vectorBuffer1, cosBit);

        for (int lane = 0; lane < laneCount; lane++)
        {
            Av1TransformVector<short> scalarValues = default;
            Av1TransformVector<short> scalarBuffer0 = default;
            Av1TransformVector<short> scalarBuffer1 = default;

            for (int index = 0; index < length; index++)
            {
                scalarValues[index] = GetPackedInputValue(index, lane);
            }

            ref byte scalarValuesBase = ref System.Runtime.CompilerServices.Unsafe.As<Av1TransformVector<short>, byte>(ref scalarValues);

            TOperator.Transform(ref scalarValuesBase, sizeof(short), sizeof(short), ref scalarBuffer0, ref scalarBuffer1, cosBit);

            for (int index = 0; index < length; index++)
            {
                ref short firstLane = ref System.Runtime.CompilerServices.Unsafe.As<Vector512<short>, short>(ref vectorValues[index]);
                Assert.Equal(scalarValues[index], System.Runtime.CompilerServices.Unsafe.Add(ref firstLane, lane));
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
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="bitDepth">The source sample bit depth.</param>
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
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="output">The destination reference coefficients.</param>
    /// <param name="config">The resolved transform functions, shifts, and axis orientation.</param>
    private static void DispatchReferenceColumn(Span<short> input, int stride, Span<int> output, ref Av1Transform2dFlipConfiguration config)
    {
        switch (config.TransformFunctionTypeColumn)
        {
            case Av1TransformFunctionType.Dct4:
                DispatchReferenceRow<Av1ForwardTransformer.Dct4Operator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Dct8:
                DispatchReferenceRow<Av1ForwardTransformer.Dct8Operator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Dct16:
                DispatchReferenceRow<Av1ForwardTransformer.Dct16Operator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Dct32:
                DispatchReferenceRow<Av1ForwardTransformer.Dct32Operator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Dct64:
                DispatchReferenceRow<Av1ForwardTransformer.Dct64Operator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Adst4:
                DispatchReferenceRow<Av1ForwardTransformer.Adst4Operator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Adst8:
                DispatchReferenceRow<Av1ForwardTransformer.Adst8Operator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Adst16:
                DispatchReferenceRow<Av1ForwardTransformer.Adst16Operator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Identity4:
                DispatchReferenceRow<Av1ForwardTransformer.Identity4Operator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Identity8:
                DispatchReferenceRow<Av1ForwardTransformer.Identity8Operator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Identity16:
                DispatchReferenceRow<Av1ForwardTransformer.Identity16Operator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Identity32:
                DispatchReferenceRow<Av1ForwardTransformer.Identity32Operator>(input, stride, output, ref config);
                break;
        }
    }

    /// <summary>
    /// Selects the scalar reference row operator.
    /// </summary>
    /// <typeparam name="TColumnOperator">The column transform operator.</typeparam>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="output">The destination reference coefficients.</param>
    /// <param name="config">The resolved transform functions, shifts, and axis orientation.</param>
    private static void DispatchReferenceRow<TColumnOperator>(Span<short> input, int stride, Span<int> output, ref Av1Transform2dFlipConfiguration config)
        where TColumnOperator : struct, Av1ForwardTransformer.IAv1ForwardTransform1dOperator
    {
        switch (config.TransformFunctionTypeRow)
        {
            case Av1TransformFunctionType.Dct4:
                TransformReference<TColumnOperator, Av1ForwardTransformer.Dct4Operator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Dct8:
                TransformReference<TColumnOperator, Av1ForwardTransformer.Dct8Operator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Dct16:
                TransformReference<TColumnOperator, Av1ForwardTransformer.Dct16Operator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Dct32:
                TransformReference<TColumnOperator, Av1ForwardTransformer.Dct32Operator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Dct64:
                TransformReference<TColumnOperator, Av1ForwardTransformer.Dct64Operator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Adst4:
                TransformReference<TColumnOperator, Av1ForwardTransformer.Adst4Operator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Adst8:
                TransformReference<TColumnOperator, Av1ForwardTransformer.Adst8Operator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Adst16:
                TransformReference<TColumnOperator, Av1ForwardTransformer.Adst16Operator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Identity4:
                TransformReference<TColumnOperator, Av1ForwardTransformer.Identity4Operator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Identity8:
                TransformReference<TColumnOperator, Av1ForwardTransformer.Identity8Operator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Identity16:
                TransformReference<TColumnOperator, Av1ForwardTransformer.Identity16Operator>(input, stride, output, ref config);
                break;
            case Av1TransformFunctionType.Identity32:
                TransformReference<TColumnOperator, Av1ForwardTransformer.Identity32Operator>(input, stride, output, ref config);
                break;
        }
    }

    /// <summary>
    /// Applies the direct scalar column and row transform definition used as the layout and dispatch oracle.
    /// </summary>
    /// <typeparam name="TColumnOperator">The column transform operator.</typeparam>
    /// <typeparam name="TRowOperator">The row transform operator.</typeparam>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="output">The destination reference coefficients.</param>
    /// <param name="config">The resolved transform functions, shifts, and axis orientation.</param>
    private static void TransformReference<TColumnOperator, TRowOperator>(
        Span<short> input,
        int stride,
        Span<int> output,
        ref Av1Transform2dFlipConfiguration config)
        where TColumnOperator : struct, Av1ForwardTransformer.IAv1ForwardTransform1dOperator
        where TRowOperator : struct, Av1ForwardTransformer.IAv1ForwardTransform1dOperator
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

            ref byte valuesBase = ref System.Runtime.CompilerServices.Unsafe.As<Av1TransformVector<int>, byte>(ref values);

            TColumnOperator.Transform(ref valuesBase, sizeof(int), sizeof(int), ref buffer0, ref buffer1, config.CosBitColumn);
            int destinationColumn = config.FlipLeftToRight ? width - column - 1 : column;

            for (int row = 0; row < height; row++)
            {
                intermediate[(row * width) + destinationColumn] = Av1Math.RoundShift(values[row], -config.Shift1);
            }
        }

        bool normalizeRectangle = Math.Abs(config.TransformSize.GetRectangleLogRatio()) == 1;

        for (int row = 0; row < outputHeight; row++)
        {
            for (int column = 0; column < width; column++)
            {
                values[column] = intermediate[(row * width) + column];
            }

            ref byte valuesBase = ref System.Runtime.CompilerServices.Unsafe.As<Av1TransformVector<int>, byte>(ref values);

            TRowOperator.Transform(ref valuesBase, sizeof(int), sizeof(int), ref buffer0, ref buffer1, config.CosBitRow);

            for (int column = 0; column < outputWidth; column++)
            {
                int value = Av1Math.RoundShift(values[column], -config.Shift2);
                output[(row * outputWidth) + column] = normalizeRectangle
                    ? Av1Transform1dMath.HalfButterfly(Av1Transform1dMath.NewSqrt2, value, 0, 0, Av1Transform1dMath.NewSqrt2Bits)
                    : value;
            }
        }
    }

    /// <summary>
    /// Gets a deterministic signed thirty-two-bit transform input.
    /// </summary>
    /// <param name="index">The transform position.</param>
    /// <param name="lane">The independent SIMD lane.</param>
    /// <returns>The deterministic input value.</returns>
    private static int GetInputValue(int index, int lane)
        => (((index * 73) + (lane * 151)) % 8191) - 4095;

    /// <summary>
    /// Gets a deterministic signed sixteen-bit input including overflow-sensitive edge values.
    /// </summary>
    /// <param name="index">The transform position.</param>
    /// <param name="lane">The independent SIMD lane.</param>
    /// <returns>The deterministic packed input value.</returns>
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
    /// <returns>Every permitted transform type, transform size, and AV1 image bit depth.</returns>
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
