// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Measures OBMC motion search candidates and builds the OBMC search target with the arithmetic of an
/// <see cref="IObmcOperator{TSample}"/>.
/// </summary>
/// <remarks>
/// The weighted source and the mask are packed at the block width, one thirty-two-bit value per luma
/// sample. Each row walks the widest available register first and finishes in the scalar overload.
/// OBMC blocks are at least eight samples wide and every overlap is at least four, so the scalar
/// overload runs only on hardware without vector support.
/// </remarks>
internal static partial class Av1ObmcSearch
{
    /// <summary>
    /// Returns the OBMC sum of absolute differences of a prediction. Reference: aom_obmc_sad and
    /// aom_highbd_obmc_sad, with the traversal of obmc_sad_w8n().
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="prediction">The prediction samples at the block origin.</param>
    /// <param name="predictionStride">The prediction row stride.</param>
    /// <param name="weightedSource">The weighted source, packed at the block width.</param>
    /// <param name="mask">The prediction weights, packed at the block width.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <returns>The sum of the rounded absolute differences.</returns>
    public static int SumAbsoluteDifferences<TSample, TOperator>(
        ReadOnlySpan<TSample> prediction,
        int predictionStride,
        ReadOnlySpan<int> weightedSource,
        ReadOnlySpan<int> mask,
        int width,
        int height)
        where TSample : unmanaged
        where TOperator : struct, IObmcOperator<TSample>
    {
        // The slices check every bound once, so the walk below can read by reference.
        ref TSample predictionBase = ref MemoryMarshal.GetReference(prediction[..(((height - 1) * predictionStride) + width)]);
        ref int sourceBase = ref MemoryMarshal.GetReference(weightedSource[..(width * height)]);
        ref int maskBase = ref MemoryMarshal.GetReference(mask[..(width * height)]);

        // A rounded difference is below 2^12 and a block has at most 2^14 samples, so no lane of any
        // total can overflow before the single reduction at the end.
        Vector512<uint> total512 = Vector512<uint>.Zero;
        Vector256<uint> total256 = Vector256<uint>.Zero;
        Vector128<uint> total128 = Vector128<uint>.Zero;
        uint total = 0;
        for (int row = 0; row < height; row++)
        {
            ref TSample predictionRow = ref Unsafe.Add(ref predictionBase, row * predictionStride);
            ref int sourceRow = ref Unsafe.Add(ref sourceBase, row * width);
            ref int maskRow = ref Unsafe.Add(ref maskBase, row * width);
            int column = 0;
            if (Vector512.IsHardwareAccelerated)
            {
                for (; column <= width - Vector512<int>.Count; column += Vector512<int>.Count)
                {
                    total512 = TOperator.AccumulateAbsoluteDifferences(
                        ref Unsafe.Add(ref predictionRow, column), ref Unsafe.Add(ref sourceRow, column), ref Unsafe.Add(ref maskRow, column), total512);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; column <= width - Vector256<int>.Count; column += Vector256<int>.Count)
                {
                    total256 = TOperator.AccumulateAbsoluteDifferences(
                        ref Unsafe.Add(ref predictionRow, column), ref Unsafe.Add(ref sourceRow, column), ref Unsafe.Add(ref maskRow, column), total256);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; column <= width - Vector128<int>.Count; column += Vector128<int>.Count)
                {
                    total128 = TOperator.AccumulateAbsoluteDifferences(
                        ref Unsafe.Add(ref predictionRow, column), ref Unsafe.Add(ref sourceRow, column), ref Unsafe.Add(ref maskRow, column), total128);
                }
            }

            for (; column < width; column++)
            {
                total = TOperator.AccumulateAbsoluteDifferences(
                    Unsafe.Add(ref predictionRow, column), Unsafe.Add(ref sourceRow, column), Unsafe.Add(ref maskRow, column), total);
            }
        }

        if (Vector512.IsHardwareAccelerated)
        {
            total += Vector512.Sum(total512);
        }

        if (Vector256.IsHardwareAccelerated)
        {
            total += Vector256.Sum(total256);
        }

        if (Vector128.IsHardwareAccelerated)
        {
            total += Vector128.Sum(total128);
        }

        return (int)total;
    }

    /// <summary>
    /// Returns the signed sum and the squared sum of the rounded OBMC differences of a prediction.
    /// Reference: obmc_variance_w8n() and hbd_obmc_variance_w8n().
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="prediction">The prediction samples at the block origin.</param>
    /// <param name="predictionStride">The prediction row stride.</param>
    /// <param name="weightedSource">The weighted source, packed at the block width.</param>
    /// <param name="mask">The prediction weights, packed at the block width.</param>
    /// <param name="width">The block width, at most 128.</param>
    /// <param name="height">The block height.</param>
    /// <param name="sum">Receives the signed sum of the rounded differences.</param>
    /// <param name="squares">Receives the sum of their squares.</param>
    public static void GetMoments<TSample, TOperator>(
        ReadOnlySpan<TSample> prediction,
        int predictionStride,
        ReadOnlySpan<int> weightedSource,
        ReadOnlySpan<int> mask,
        int width,
        int height,
        out int sum,
        out ulong squares)
        where TSample : unmanaged
        where TOperator : struct, IObmcOperator<TSample>
    {
        ref TSample predictionBase = ref MemoryMarshal.GetReference(prediction[..(((height - 1) * predictionStride) + width)]);
        ref int sourceBase = ref MemoryMarshal.GetReference(weightedSource[..(width * height)]);
        ref int maskBase = ref MemoryMarshal.GetReference(mask[..(width * height)]);

        // A rounded difference is below 2^12 in magnitude, so the signed total of a block of at most
        // 2^14 samples fits every lane. A square is below 2^24, and a row of at most 128 samples puts
        // at most 32 squares in a lane of the narrowest register, which stays below 2^29. The squares
        // of each row therefore collect in thirty-two-bit lanes and fold into sixty-four-bit lanes at
        // the end of the row. libaom bounds its thirty-two-bit lanes the same way, with row groups.
        Vector512<int> sum512 = Vector512<int>.Zero;
        Vector256<int> sum256 = Vector256<int>.Zero;
        Vector128<int> sum128 = Vector128<int>.Zero;
        Vector512<ulong> squares512 = Vector512<ulong>.Zero;
        Vector256<ulong> squares256 = Vector256<ulong>.Zero;
        Vector128<ulong> squares128 = Vector128<ulong>.Zero;
        sum = 0;
        squares = 0;
        for (int row = 0; row < height; row++)
        {
            ref TSample predictionRow = ref Unsafe.Add(ref predictionBase, row * predictionStride);
            ref int sourceRow = ref Unsafe.Add(ref sourceBase, row * width);
            ref int maskRow = ref Unsafe.Add(ref maskBase, row * width);
            int column = 0;
            if (Vector512.IsHardwareAccelerated && column <= width - Vector512<int>.Count)
            {
                Vector512<uint> rowSquares = Vector512<uint>.Zero;
                for (; column <= width - Vector512<int>.Count; column += Vector512<int>.Count)
                {
                    TOperator.AccumulateMoments(
                        ref Unsafe.Add(ref predictionRow, column), ref Unsafe.Add(ref sourceRow, column), ref Unsafe.Add(ref maskRow, column), ref sum512, ref rowSquares);
                }

                (Vector512<ulong> lower, Vector512<ulong> upper) = Vector512.Widen(rowSquares);
                squares512 += lower + upper;
            }

            if (Vector256.IsHardwareAccelerated && column <= width - Vector256<int>.Count)
            {
                Vector256<uint> rowSquares = Vector256<uint>.Zero;
                for (; column <= width - Vector256<int>.Count; column += Vector256<int>.Count)
                {
                    TOperator.AccumulateMoments(
                        ref Unsafe.Add(ref predictionRow, column), ref Unsafe.Add(ref sourceRow, column), ref Unsafe.Add(ref maskRow, column), ref sum256, ref rowSquares);
                }

                (Vector256<ulong> lower, Vector256<ulong> upper) = Vector256.Widen(rowSquares);
                squares256 += lower + upper;
            }

            if (Vector128.IsHardwareAccelerated && column <= width - Vector128<int>.Count)
            {
                Vector128<uint> rowSquares = Vector128<uint>.Zero;
                for (; column <= width - Vector128<int>.Count; column += Vector128<int>.Count)
                {
                    TOperator.AccumulateMoments(
                        ref Unsafe.Add(ref predictionRow, column), ref Unsafe.Add(ref sourceRow, column), ref Unsafe.Add(ref maskRow, column), ref sum128, ref rowSquares);
                }

                (Vector128<ulong> lower, Vector128<ulong> upper) = Vector128.Widen(rowSquares);
                squares128 += lower + upper;
            }

            for (; column < width; column++)
            {
                TOperator.AccumulateMoments(
                    Unsafe.Add(ref predictionRow, column), Unsafe.Add(ref sourceRow, column), Unsafe.Add(ref maskRow, column), ref sum, ref squares);
            }
        }

        if (Vector512.IsHardwareAccelerated)
        {
            sum += Vector512.Sum(sum512);
            squares += Vector512.Sum(squares512);
        }

        if (Vector256.IsHardwareAccelerated)
        {
            sum += Vector256.Sum(sum256);
            squares += Vector256.Sum(squares256);
        }

        if (Vector128.IsHardwareAccelerated)
        {
            sum += Vector128.Sum(sum128);
            squares += Vector128.Sum(squares128);
        }
    }

    /// <summary>
    /// Writes one row of the above-neighbor term of the OBMC search target. Reference: the row loop of
    /// calc_target_weighted_pred_above().
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="prediction">The above-neighbor prediction row.</param>
    /// <param name="weight">The weight of the block's own prediction on this row.</param>
    /// <param name="weightedSource">The weighted source row to write.</param>
    /// <param name="mask">The prediction weight row to write.</param>
    /// <param name="width">The number of samples to write.</param>
    public static void WeightAbove<TSample, TOperator>(
        ReadOnlySpan<TSample> prediction,
        int weight,
        Span<int> weightedSource,
        Span<int> mask,
        int width)
        where TSample : unmanaged
        where TOperator : struct, IObmcOperator<TSample>
    {
        ref TSample predictionRow = ref MemoryMarshal.GetReference(prediction[..width]);
        ref int sourceRow = ref MemoryMarshal.GetReference(weightedSource[..width]);
        ref int maskRow = ref MemoryMarshal.GetReference(mask[..width]);
        int column = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<int> weights = Vector512.Create(weight);
            for (; column <= width - Vector512<int>.Count; column += Vector512<int>.Count)
            {
                TOperator.WeightAbove(ref Unsafe.Add(ref predictionRow, column), weights, ref Unsafe.Add(ref sourceRow, column), ref Unsafe.Add(ref maskRow, column));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<int> weights = Vector256.Create(weight);
            for (; column <= width - Vector256<int>.Count; column += Vector256<int>.Count)
            {
                TOperator.WeightAbove(ref Unsafe.Add(ref predictionRow, column), weights, ref Unsafe.Add(ref sourceRow, column), ref Unsafe.Add(ref maskRow, column));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<int> weights = Vector128.Create(weight);
            for (; column <= width - Vector128<int>.Count; column += Vector128<int>.Count)
            {
                TOperator.WeightAbove(ref Unsafe.Add(ref predictionRow, column), weights, ref Unsafe.Add(ref sourceRow, column), ref Unsafe.Add(ref maskRow, column));
            }
        }

        for (; column < width; column++)
        {
            TOperator.WeightAbove(Unsafe.Add(ref predictionRow, column), weight, ref Unsafe.Add(ref sourceRow, column), ref Unsafe.Add(ref maskRow, column));
        }
    }

    /// <summary>
    /// Blends one row of the left-neighbor term into the OBMC search target. Reference: the column
    /// loop of calc_target_weighted_pred_left().
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="prediction">The left-neighbor prediction row.</param>
    /// <param name="weights">The column weights of the block's own prediction, one per overlapped column.</param>
    /// <param name="weightedSource">The weighted source row to update.</param>
    /// <param name="mask">The prediction weight row to update.</param>
    public static void WeightLeft<TSample, TOperator>(
        ReadOnlySpan<TSample> prediction,
        ReadOnlySpan<byte> weights,
        Span<int> weightedSource,
        Span<int> mask)
        where TSample : unmanaged
        where TOperator : struct, IObmcOperator<TSample>
    {
        int width = weights.Length;
        ref TSample predictionRow = ref MemoryMarshal.GetReference(prediction[..width]);
        ref byte weightRow = ref MemoryMarshal.GetReference(weights);
        ref int sourceRow = ref MemoryMarshal.GetReference(weightedSource[..width]);
        ref int maskRow = ref MemoryMarshal.GetReference(mask[..width]);
        int column = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; column <= width - Vector512<int>.Count; column += Vector512<int>.Count)
            {
                TOperator.WeightLeft(
                    ref Unsafe.Add(ref predictionRow, column),
                    ref Unsafe.Add(ref weightRow, column),
                    ref Unsafe.Add(ref sourceRow, column),
                    ref Unsafe.Add(ref maskRow, column),
                    default(Vector512<int>));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; column <= width - Vector256<int>.Count; column += Vector256<int>.Count)
            {
                TOperator.WeightLeft(
                    ref Unsafe.Add(ref predictionRow, column),
                    ref Unsafe.Add(ref weightRow, column),
                    ref Unsafe.Add(ref sourceRow, column),
                    ref Unsafe.Add(ref maskRow, column),
                    default(Vector256<int>));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; column <= width - Vector128<int>.Count; column += Vector128<int>.Count)
            {
                TOperator.WeightLeft(
                    ref Unsafe.Add(ref predictionRow, column),
                    ref Unsafe.Add(ref weightRow, column),
                    ref Unsafe.Add(ref sourceRow, column),
                    ref Unsafe.Add(ref maskRow, column),
                    default(Vector128<int>));
            }
        }

        for (; column < width; column++)
        {
            TOperator.WeightLeft(Unsafe.Add(ref predictionRow, column), Unsafe.Add(ref weightRow, column), ref Unsafe.Add(ref sourceRow, column), ref Unsafe.Add(ref maskRow, column));
        }
    }

    /// <summary>
    /// Scales the whole OBMC search target by the maximum blend weight between the above and the left
    /// neighbor passes. Reference: the scale loop of calc_target_weighted_pred().
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="weightedSource">The weighted source to update.</param>
    /// <param name="mask">The prediction weights to update.</param>
    public static void Scale<TSample, TOperator>(Span<int> weightedSource, Span<int> mask)
        where TSample : unmanaged
        where TOperator : struct, IObmcOperator<TSample>
    {
        int length = weightedSource.Length;
        ref int sourceBase = ref MemoryMarshal.GetReference(weightedSource);
        ref int maskBase = ref MemoryMarshal.GetReference(mask[..length]);
        int index = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; index <= length - Vector512<int>.Count; index += Vector512<int>.Count)
            {
                TOperator.Scale(ref Unsafe.Add(ref sourceBase, index), ref Unsafe.Add(ref maskBase, index), default(Vector512<int>));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; index <= length - Vector256<int>.Count; index += Vector256<int>.Count)
            {
                TOperator.Scale(ref Unsafe.Add(ref sourceBase, index), ref Unsafe.Add(ref maskBase, index), default(Vector256<int>));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; index <= length - Vector128<int>.Count; index += Vector128<int>.Count)
            {
                TOperator.Scale(ref Unsafe.Add(ref sourceBase, index), ref Unsafe.Add(ref maskBase, index), default(Vector128<int>));
            }
        }

        for (; index < length; index++)
        {
            TOperator.Scale(ref Unsafe.Add(ref sourceBase, index), ref Unsafe.Add(ref maskBase, index));
        }
    }

    /// <summary>
    /// Replaces one row of neighbor terms with the source scaled by 64 * 64 minus the term. Reference:
    /// the source loop of calc_target_weighted_pred().
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="source">The source row.</param>
    /// <param name="weightedSource">The weighted source row to update.</param>
    public static void SubtractFromSource<TSample, TOperator>(ReadOnlySpan<TSample> source, Span<int> weightedSource)
        where TSample : unmanaged
        where TOperator : struct, IObmcOperator<TSample>
    {
        int width = weightedSource.Length;
        ref TSample sourceRow = ref MemoryMarshal.GetReference(source[..width]);
        ref int targetRow = ref MemoryMarshal.GetReference(weightedSource);
        int column = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; column <= width - Vector512<int>.Count; column += Vector512<int>.Count)
            {
                TOperator.SubtractFromSource(ref Unsafe.Add(ref sourceRow, column), ref Unsafe.Add(ref targetRow, column), default(Vector512<int>));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; column <= width - Vector256<int>.Count; column += Vector256<int>.Count)
            {
                TOperator.SubtractFromSource(ref Unsafe.Add(ref sourceRow, column), ref Unsafe.Add(ref targetRow, column), default(Vector256<int>));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; column <= width - Vector128<int>.Count; column += Vector128<int>.Count)
            {
                TOperator.SubtractFromSource(ref Unsafe.Add(ref sourceRow, column), ref Unsafe.Add(ref targetRow, column), default(Vector128<int>));
            }
        }

        for (; column < width; column++)
        {
            TOperator.SubtractFromSource(Unsafe.Add(ref sourceRow, column), ref Unsafe.Add(ref targetRow, column));
        }
    }
}
