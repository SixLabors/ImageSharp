// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
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
    /// Verifies the one-dimensional stage networks, every permitted two-dimensional size, type, and bit-depth
    /// combination, the reversible transform, and the Hadamard screening costs against their scalar definitions at
    /// every hardware tier.
    /// </summary>
    [Fact]
    public void TransformsMatchReferenceAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertTransforms, TransformConfigurations);

    private static void AssertTransforms()
    {
        AssertOneDimensionalOperators();
        AssertTwoDimensionalPipeline();
        AssertLosslessTransform();
        foreach (int bitDepth in new[] { 8, 10, 12 })
        {
            AssertHadamardScreeningCost((1 << bitDepth) - 1);
            foreach (int size in new[] { 4, 8, 16, 32 })
            {
                AssertQuickHadamardCost(size, bitDepth);
            }

            AssertModeEstimationHadamard(8, bitDepth);
            AssertModeEstimationHadamard(16, bitDepth);
        }

        AssertModeEstimationDct();
        AssertModeEstimationRows();
    }

    /// <summary>
    /// Verifies the 4x4 mode-estimation DCT against a scalar transcription of aom_fdct4x4_lp_c() for eight-bit
    /// residuals, where libaom's C and x86 forms agree.
    /// </summary>
    private static void AssertModeEstimationDct()
    {
        const int stride = 7;
        short[] residual = new short[stride * 4];
        int[] coefficients = new int[16];
        int[] expected = new int[16];
        int[] workspace = new int[Av1TransformWorkspace.MaximumLength];
        for (int pattern = 0; pattern < 6; pattern++)
        {
            for (int i = 0; i < residual.Length; i++)
            {
                residual[i] = (short)(pattern switch
                {
                    0 => 255,
                    1 => i == 0 ? 0 : -255,
                    2 => (i & 1) == 0 ? 255 : -255,
                    3 => (((i / stride) + (i % stride)) & 1) == 0 ? 255 : -255,
                    4 => i == 9 ? 1 : 0,
                    _ => ((i * 7919) % 511) - 255
                });
            }

            ReferenceForwardDct4x4(residual, stride, expected);
            Av1ForwardTransformer.TransformForModeEstimation(residual, stride, 4, coefficients, workspace, false);
            Assert.Equal(expected, coefficients);
        }
    }

    /// <summary>
    /// Verifies that transforming a row of adjacent blocks at once gives each block's single-block coefficients,
    /// for every row length that reaches the four-, two- and one-block steps.
    /// </summary>
    private static void AssertModeEstimationRows()
    {
        foreach (int size in new[] { 4, 8, 16 })
        {
            const int maximumBlocks = 7;
            int stride = (size * maximumBlocks) + 5;
            short[] residual = new short[stride * size];
            for (int i = 0; i < residual.Length; i++)
            {
                residual[i] = (short)(((i * 7919) % 511) - 255);
            }

            int blockLength = size * size;
            int[] workspace = new int[Av1TransformWorkspace.MaximumLength];
            int[] expected = new int[blockLength];
            for (int count = 1; count <= maximumBlocks; count++)
            {
                int[] row = new int[count * blockLength];
                Av1ForwardTransformer.TransformRowForModeEstimation(residual, stride, size, count, row, workspace, false);
                for (int block = 0; block < count; block++)
                {
                    Av1ForwardTransformer.TransformForModeEstimation(residual.AsSpan(block * size), stride, size, expected, workspace, false);
                    Assert.Equal(expected, row.AsSpan(block * blockLength, blockLength).ToArray());
                }
            }
        }
    }

    /// <summary>
    /// Computes the 4x4 forward DCT with the arithmetic of aom_fdct4x4_lp_c().
    /// </summary>
    /// <param name="input">The residual samples.</param>
    /// <param name="stride">The residual row stride.</param>
    /// <param name="output">Receives the sixteen coefficients.</param>
    private static void ReferenceForwardDct4x4(short[] input, int stride, int[] output)
    {
        short[] intermediate = new short[16];
        short[] result = new short[16];
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < 4; i++)
            {
                int in0;
                int in1;
                int in2;
                int in3;
                if (pass == 0)
                {
                    in0 = input[i] * 16;
                    in1 = input[stride + i] * 16;
                    in2 = input[(2 * stride) + i] * 16;
                    in3 = input[(3 * stride) + i] * 16;
                    if (i == 0 && in0 != 0)
                    {
                        in0++;
                    }
                }
                else
                {
                    in0 = intermediate[i];
                    in1 = intermediate[4 + i];
                    in2 = intermediate[8 + i];
                    in3 = intermediate[12 + i];
                }

                int step0 = in0 + in3;
                int step1 = in1 + in2;
                int step2 = in1 - in2;
                int step3 = in0 - in3;
                short temp0 = (short)ReferenceRoundShift((step0 + step1) * 11585L);
                short temp2 = (short)ReferenceRoundShift((step0 - step1) * 11585L);
                short temp1 = (short)ReferenceRoundShift((step2 * 6270L) + (step3 * 15137L));
                short temp3 = (short)ReferenceRoundShift((-step2 * 15137L) + (step3 * 6270L));
                if (pass == 0)
                {
                    intermediate[(i * 4) + 0] = temp0;
                    intermediate[(i * 4) + 1] = temp1;
                    intermediate[(i * 4) + 2] = temp2;
                    intermediate[(i * 4) + 3] = temp3;
                }
                else
                {
                    result[i] = temp0;
                    result[4 + i] = temp1;
                    result[8 + i] = temp2;
                    result[12 + i] = temp3;
                }
            }
        }

        for (int i = 0; i < 16; i++)
        {
            output[i] = (short)((result[i] + 1) >> 2);
        }
    }

    /// <summary>
    /// Rounds a Q14 product to an integer, as fdct_round_shift() does.
    /// </summary>
    /// <param name="value">The Q14 product.</param>
    /// <returns>The rounded integer.</returns>
    private static long ReferenceRoundShift(long value) => (value + 8192) >> 14;

    /// <summary>
    /// Verifies the mode-estimation Hadamard coefficients, in order, against a scalar transcription of
    /// aom_hadamard_lp_8x8_c() and the sixteen-bit 16x16 combine, including residuals that wrap sixteen-bit lanes.
    /// </summary>
    /// <param name="size">The square transform width, eight or sixteen.</param>
    /// <param name="bitDepth">The coded sample precision.</param>
    private static void AssertModeEstimationHadamard(int size, int bitDepth)
    {
        int maximum = (1 << bitDepth) - 1;
        int stride = size + 3;
        short[] residual = new short[stride * size];
        int[] coefficients = new int[size * size];
        int[] expected = new int[size * size];
        int[] workspace = new int[Av1TransformWorkspace.MaximumLength];
        for (int pattern = 0; pattern < 5; pattern++)
        {
            for (int i = 0; i < residual.Length; i++)
            {
                int row = i / stride;
                int column = i % stride;
                residual[i] = (short)(pattern switch
                {
                    0 => maximum,
                    1 => i == 19 ? -maximum : 0,
                    2 => (column & 1) == 0 ? maximum : -maximum,
                    3 => ((row + column) & 1) == 0 ? maximum : -maximum,
                    _ => ((i * 7919) % ((2 * maximum) + 1)) - maximum
                });
            }

            ReferenceModeEstimationHadamard(residual, stride, size, bitDepth > 8, expected);
            Av1ForwardTransformer.TransformForModeEstimation(residual, stride, size, coefficients, workspace, bitDepth > 8);
            Assert.Equal(expected, coefficients);
        }
    }

    /// <summary>
    /// Computes the mode-estimation Hadamard coefficients with sixteen-bit scalar arithmetic.
    /// </summary>
    /// <param name="residual">The residual samples.</param>
    /// <param name="stride">The residual row stride.</param>
    /// <param name="size">The square transform width, eight or sixteen.</param>
    /// <param name="highBitDepth">Whether the high-bit-depth coefficient layout applies.</param>
    /// <param name="coefficients">Receives the coefficients.</param>
    private static void ReferenceModeEstimationHadamard(short[] residual, int stride, int size, bool highBitDepth, int[] coefficients)
    {
        short[] packed = new short[size * size];
        int blockCount = size == 8 ? 1 : 4;
        for (int block = 0; block < blockCount; block++)
        {
            int offset = ((block >> 1) * 8 * stride) + ((block & 1) * 8);
            short[] buffer = new short[64];
            short[] buffer2 = new short[64];
            for (int column = 0; column < 8; column++)
            {
                ReferenceHadamardColumn8(residual, offset + column, stride, buffer, 8 * column);
            }

            for (int column = 0; column < 8; column++)
            {
                ReferenceHadamardColumn8(buffer, column, 8, buffer2, 8 * column);
            }

            // aom_hadamard_lp_8x8_c() transposes its output to match the SSE2 layout.
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    packed[(block * 64) + (i * 8) + j] = buffer2[(j * 8) + i];
                }
            }
        }

        if (size == 16)
        {
            for (int i = 0; i < 64; i++)
            {
                short b0 = (short)((short)(packed[i] + packed[64 + i]) >> 1);
                short b1 = (short)((short)(packed[i] - packed[64 + i]) >> 1);
                short b2 = (short)((short)(packed[128 + i] + packed[192 + i]) >> 1);
                short b3 = (short)((short)(packed[128 + i] - packed[192 + i]) >> 1);
                packed[i] = (short)(b0 + b2);
                packed[64 + i] = (short)(b1 + b3);
                packed[128 + i] = (short)(b0 - b2);
                packed[192 + i] = (short)(b1 - b3);
            }
        }

        for (int i = 0; i < packed.Length; i++)
        {
            coefficients[i] = packed[i];
        }

        if (size == 16 && highBitDepth)
        {
            for (int row = 0; row < 16; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    int first = (row * 16) + 4 + column;
                    (coefficients[first], coefficients[first + 4]) = (coefficients[first + 4], coefficients[first]);
                }
            }
        }
    }

    /// <summary>
    /// Transforms one column with the sixteen-bit butterflies of hadamard_col8().
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="offset">The column's first sample.</param>
    /// <param name="stride">The source row stride.</param>
    /// <param name="destination">The destination samples.</param>
    /// <param name="destinationOffset">The first destination sample.</param>
    private static void ReferenceHadamardColumn8(short[] source, int offset, int stride, short[] destination, int destinationOffset)
    {
        short b0 = (short)(source[offset] + source[offset + stride]);
        short b1 = (short)(source[offset] - source[offset + stride]);
        short b2 = (short)(source[offset + (2 * stride)] + source[offset + (3 * stride)]);
        short b3 = (short)(source[offset + (2 * stride)] - source[offset + (3 * stride)]);
        short b4 = (short)(source[offset + (4 * stride)] + source[offset + (5 * stride)]);
        short b5 = (short)(source[offset + (4 * stride)] - source[offset + (5 * stride)]);
        short b6 = (short)(source[offset + (6 * stride)] + source[offset + (7 * stride)]);
        short b7 = (short)(source[offset + (6 * stride)] - source[offset + (7 * stride)]);
        short c0 = (short)(b0 + b2);
        short c1 = (short)(b1 + b3);
        short c2 = (short)(b0 - b2);
        short c3 = (short)(b1 - b3);
        short c4 = (short)(b4 + b6);
        short c5 = (short)(b5 + b7);
        short c6 = (short)(b4 - b6);
        short c7 = (short)(b5 - b7);
        destination[destinationOffset] = (short)(c0 + c4);
        destination[destinationOffset + 7] = (short)(c1 + c5);
        destination[destinationOffset + 3] = (short)(c2 + c6);
        destination[destinationOffset + 4] = (short)(c3 + c7);
        destination[destinationOffset + 2] = (short)(c0 - c4);
        destination[destinationOffset + 6] = (short)(c1 - c5);
        destination[destinationOffset + 1] = (short)(c2 - c6);
        destination[destinationOffset + 5] = (short)(c3 - c7);
    }

    /// <summary>
    /// Verifies the fast screening transform against the independent Hadamard matrix definition.
    /// </summary>
    /// <param name="maximum">The largest residual magnitude for the coded sample precision.</param>
    private static void AssertHadamardScreeningCost(int maximum)
    {
        const int width = 8;
        short[] residual = new short[width * width];
        int[] workspace = new int[Av1TransformWorkspace.MaximumLength];
        for (int pattern = 0; pattern < 5; pattern++)
        {
            for (int i = 0; i < residual.Length; i++)
            {
                residual[i] = (short)(pattern switch
                {
                    0 => maximum,
                    1 => i == 19 ? -maximum : 0,
                    2 => (i & 1) == 0 ? maximum : -maximum,
                    3 => (((i / width) + (i % width)) & 1) == 0 ? maximum : -maximum,
                    _ => ((i * 7919) % ((2 * maximum) + 1)) - maximum
                });
            }

            int expected = 0;
            for (int v = 0; v < width; v++)
            {
                for (int u = 0; u < width; u++)
                {
                    int coefficient = 0;
                    for (int y = 0; y < width; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            // H[u,x] = (-1)^popcount(u & x). Direct matrix multiplication is independent of the
                            // production butterfly stages, transpose tiles, and vectorized magnitude reduction.
                            int parity = (BitOperations.PopCount((uint)(u & x)) + BitOperations.PopCount((uint)(v & y))) & 1;
                            coefficient += (parity == 0 ? 1 : -1) * residual[(y * width) + x];
                        }
                    }

                    expected += Math.Abs(coefficient);
                }
            }

            Assert.Equal(expected, Av1ForwardTransformer.GetHadamard8x8Cost(residual, workspace));
        }
    }

    /// <summary>
    /// Verifies one quick Hadamard size against the scalar libaom transform hierarchy.
    /// </summary>
    /// <param name="size">The square transform width.</param>
    /// <param name="bitDepth">The coded sample precision.</param>
    private static void AssertQuickHadamardCost(int size, int bitDepth)
    {
        int maximum = (1 << bitDepth) - 1;
        short[] residual = new short[size * size];
        int[] coefficients = new int[size * size];
        int[] expectedCoefficients = new int[size * size];
        int[] workspace = new int[Av1TransformWorkspace.MaximumLength];
        for (int pattern = 0; pattern < 5; pattern++)
        {
            for (int i = 0; i < residual.Length; i++)
            {
                residual[i] = (short)(pattern switch
                {
                    0 => maximum,
                    1 => i == 19 % residual.Length ? -maximum : 0,
                    2 => (i & 1) == 0 ? maximum : -maximum,
                    3 => (((i / size) + (i % size)) & 1) == 0 ? maximum : -maximum,
                    _ => ((i * 7919) % ((2 * maximum) + 1)) - maximum
                });
            }

            ReferenceHadamard(residual, size, size, bitDepth > 8, expectedCoefficients);
            long expected = expectedCoefficients.Sum(value => Math.Abs((long)value));
            long actual = Av1ForwardTransformer.GetHadamardCost(
                residual,
                size,
                size,
                bitDepth > 8,
                coefficients,
                workspace);

            Assert.Equal(expected, actual);
        }
    }

    private static void AssertLosslessTransform()
    {
        const int stride = 7;
        short[] input = new short[stride * 4];
        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                input[(row * stride) + column] = (short)((row * 1003) - (column * 499) + (row * column * 71) - 1024);
            }
        }

        int[] expected = new int[16];
        int[] actual = new int[16];
        TransformLosslessReference(input, expected, stride);
        Av1ForwardTransformer.TransformLossless4x4(input, actual, stride);

        Assert.Equal(expected, actual);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 32; iteration++)
        {
            Av1ForwardTransformer.TransformLossless4x4(input, actual, stride);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static void TransformLosslessReference(ReadOnlySpan<short> input, Span<int> output, int stride)
    {
        // The first pass traverses columns and writes their four transformed values contiguously. The second
        // pass consumes that transposed layout in place, matching the normative reversible operation order.
        for (int column = 0; column < 4; column++)
        {
            int a = input[column];
            int b = input[stride + column];
            int c = input[(2 * stride) + column];
            int d = input[(3 * stride) + column];

            a += b;
            d -= c;
            int e = (a - d) >> 1;
            b = e - b;
            c = e - c;
            a -= c;
            d += b;

            int offset = column * 4;
            output[offset] = a;
            output[offset + 1] = c;
            output[offset + 2] = d;
            output[offset + 3] = b;
        }

        for (int column = 0; column < 4; column++)
        {
            int a = output[column];
            int b = output[4 + column];
            int c = output[8 + column];
            int d = output[12 + column];

            a += b;
            d -= c;
            int e = (a - d) >> 1;
            b = e - b;
            c = e - c;
            a -= c;
            d += b;

            output[column] = a * 4;
            output[4 + column] = c * 4;
            output[8 + column] = d * 4;
            output[12 + column] = b * 4;
        }

        // Encoder coefficient storage is row-major, so exchange the reference walk's final frequency axes.
        (output[1], output[4]) = (output[4], output[1]);
        (output[2], output[8]) = (output[8], output[2]);
        (output[3], output[12]) = (output[12], output[3]);
        (output[6], output[9]) = (output[9], output[6]);
        (output[7], output[13]) = (output[13], output[7]);
        (output[11], output[14]) = (output[14], output[11]);
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
    private static void DispatchReferenceColumn(ReadOnlySpan<short> input, int stride, Span<int> output, ref Av1Transform2dFlipConfiguration config)
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
    private static void DispatchReferenceRow<TColumnOperator>(ReadOnlySpan<short> input, int stride, Span<int> output, ref Av1Transform2dFlipConfiguration config)
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
        ReadOnlySpan<short> input,
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
            _ => (((index * 73) + (lane * 151)) % 511) - 255,
        });

    private static void ReferenceHadamard(
        ReadOnlySpan<short> residual,
        int stride,
        int size,
        bool highBitDepth,
        Span<int> coefficients)
    {
        if (size is 4 or 8)
        {
            int[] intermediate = new int[size * size];
            ReferenceHadamardPass(residual, stride, size, size == 4 || !highBitDepth, intermediate);
            ReferenceHadamardPass(intermediate, size, size, size == 4 || !highBitDepth, coefficients);
            return;
        }

        int half = size >> 1;
        int quadrantLength = half * half;
        for (int quadrant = 0; quadrant < 4; quadrant++)
        {
            int rowOffset = (quadrant >> 1) * half;
            int columnOffset = (quadrant & 1) * half;
            ReferenceHadamard(
                residual[((rowOffset * stride) + columnOffset)..],
                stride,
                half,
                highBitDepth,
                coefficients.Slice(quadrant * quadrantLength, quadrantLength));
        }

        int shift = size == 32 ? 2 : 1;
        for (int index = 0; index < quadrantLength; index++)
        {
            int a0 = coefficients[index];
            int a1 = coefficients[quadrantLength + index];
            int a2 = coefficients[(2 * quadrantLength) + index];
            int a3 = coefficients[(3 * quadrantLength) + index];
            int b0 = (a0 + a1) >> shift;
            int b1 = (a0 - a1) >> shift;
            int b2 = (a2 + a3) >> shift;
            int b3 = (a2 - a3) >> shift;
            coefficients[index] = highBitDepth ? b0 + b2 : (short)(b0 + b2);
            coefficients[quadrantLength + index] = highBitDepth ? b1 + b3 : (short)(b1 + b3);
            coefficients[(2 * quadrantLength) + index] = highBitDepth ? b0 - b2 : (short)(b0 - b2);
            coefficients[(3 * quadrantLength) + index] = highBitDepth ? b1 - b3 : (short)(b1 - b3);
        }
    }

    private static void ReferenceHadamardPass<T>(
        ReadOnlySpan<T> source,
        int stride,
        int size,
        bool narrow,
        Span<int> destination)
        where T : unmanaged, INumber<T>
    {
        Span<int> values = stackalloc int[8];
        for (int column = 0; column < size; column++)
        {
            for (int row = 0; row < size; row++)
            {
                values[row] = int.CreateChecked(source[(row * stride) + column]);
            }

            if (size == 4)
            {
                for (int pair = 0; pair < 4; pair += 2)
                {
                    int first = values[pair];
                    int second = values[pair + 1];
                    values[pair] = (first + second) >> 1;
                    values[pair + 1] = (first - second) >> 1;
                }
            }

            for (int half = size == 4 ? 2 : 1; half < size; half *= 2)
            {
                for (int start = 0; start < size; start += 2 * half)
                {
                    for (int index = start; index < start + half; index++)
                    {
                        int first = values[index];
                        int second = values[index + half];
                        values[index] = narrow ? (short)(first + second) : first + second;
                        values[index + half] = narrow ? (short)(first - second) : first - second;
                    }
                }
            }

            for (int row = 0; row < size; row++)
            {
                destination[(column * size) + row] = values[row];
            }
        }
    }

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
