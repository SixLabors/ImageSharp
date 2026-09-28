// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Measures the magnitudes of transform coefficients with the arithmetic of an
/// <see cref="ICoefficientMeasureOperator"/>.
/// </summary>
/// <remarks>
/// Each traversal walks the widest available register first and finishes in the scalar overload.
/// </remarks>
internal static partial class Av1CoefficientMeasures
{
    /// <summary>
    /// Returns the sum of the magnitudes of the coefficients. Reference: aom_satd.
    /// </summary>
    /// <param name="coefficients">The coefficients.</param>
    /// <returns>The exact sum of the magnitudes.</returns>
    public static long SumAbsolute(ReadOnlySpan<int> coefficients)
        => SumAbsolute<CoefficientMeasureOperator>(coefficients);

    /// <summary>
    /// Returns the largest magnitude of the coefficients. Reference: the threshold scan of
    /// predict_skip_txfm(), which fails on the first coefficient at or above its threshold.
    /// </summary>
    /// <param name="coefficients">The coefficients.</param>
    /// <returns>The largest magnitude, or zero for an empty span.</returns>
    public static int GetMaximumAbsolute(ReadOnlySpan<int> coefficients)
        => GetMaximumAbsolute<CoefficientMeasureOperator>(coefficients);

    /// <summary>
    /// Returns the one-based scan position of the last nonzero coefficient. Reference: the eob search of
    /// av1_quantize_fp_avx2, which takes the largest inverse-scan index of a nonzero lane.
    /// </summary>
    /// <param name="quantized">The raster-order quantized coefficients.</param>
    /// <param name="inverseScan">The scan position of each raster-order coefficient.</param>
    /// <returns>The end position in scan order, or zero for an empty block.</returns>
    public static ushort GetEndOfBlock(ReadOnlySpan<int> quantized, ReadOnlySpan<short> inverseScan)
        => GetEndOfBlock<CoefficientMeasureOperator>(quantized, inverseScan);

    /// <summary>
    /// Returns the squared error between original and reconstructed coefficients. Reference: av1_block_error_lp,
    /// which holds the reconstruction as int16, and av1_highbd_block_error.
    /// </summary>
    /// <param name="coefficients">The original coefficients.</param>
    /// <param name="reconstructed">The reconstructed coefficients.</param>
    /// <param name="reconstructedIsInt16">Whether the reconstruction is kept in int16 storage, which wraps it.</param>
    /// <returns>The exact sum of the squared differences.</returns>
    public static long SumSquaredDifferences(ReadOnlySpan<int> coefficients, ReadOnlySpan<int> reconstructed, bool reconstructedIsInt16)
        => SumSquaredDifferences<CoefficientMeasureOperator>(coefficients, reconstructed, reconstructedIsInt16);

    /// <summary>
    /// Scales a square residual block by eight into coefficients. Reference: scale_square_buf_vals().
    /// </summary>
    /// <param name="destination">Receives the coefficients, packed at the block width.</param>
    /// <param name="residual">The residual samples at the block origin.</param>
    /// <param name="stride">The residual row stride.</param>
    /// <param name="width">The block width and height.</param>
    public static void ScaleResidual(Span<int> destination, ReadOnlySpan<short> residual, int stride, int width)
        => ScaleResidual<CoefficientMeasureOperator>(destination, residual, stride, width);

    /// <summary>
    /// Traverses <see cref="SumAbsolute(ReadOnlySpan{int})"/> at descending register widths.
    /// </summary>
    private static long SumAbsolute<TOperator>(ReadOnlySpan<int> coefficients)
        where TOperator : struct, ICoefficientMeasureOperator
    {
        ref int coefficientBase = ref MemoryMarshal.GetReference(coefficients);
        int length = coefficients.Length;
        Vector512<long> total512 = Vector512<long>.Zero;
        Vector256<long> total256 = Vector256<long>.Zero;
        Vector128<long> total128 = Vector128<long>.Zero;
        long total = 0;
        int i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; i <= length - Vector512<int>.Count; i += Vector512<int>.Count)
            {
                total512 = TOperator.AccumulateAbsolute(Vector512.LoadUnsafe(ref coefficientBase, (nuint)i), total512);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; i <= length - Vector256<int>.Count; i += Vector256<int>.Count)
            {
                total256 = TOperator.AccumulateAbsolute(Vector256.LoadUnsafe(ref coefficientBase, (nuint)i), total256);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; i <= length - Vector128<int>.Count; i += Vector128<int>.Count)
            {
                total128 = TOperator.AccumulateAbsolute(Vector128.LoadUnsafe(ref coefficientBase, (nuint)i), total128);
            }
        }

        for (; i < length; i++)
        {
            total = TOperator.AccumulateAbsolute(Unsafe.Add(ref coefficientBase, i), total);
        }

        total256 += total512.GetLower() + total512.GetUpper();
        total128 += total256.GetLower() + total256.GetUpper();
        return total + Vector128.Sum(total128);
    }

    /// <summary>
    /// Traverses <see cref="GetMaximumAbsolute(ReadOnlySpan{int})"/> at descending register widths.
    /// </summary>
    private static int GetMaximumAbsolute<TOperator>(ReadOnlySpan<int> coefficients)
        where TOperator : struct, ICoefficientMeasureOperator
    {
        ref int coefficientBase = ref MemoryMarshal.GetReference(coefficients);
        int length = coefficients.Length;
        Vector512<int> maximum512 = Vector512<int>.Zero;
        Vector256<int> maximum256 = Vector256<int>.Zero;
        Vector128<int> maximum128 = Vector128<int>.Zero;
        int maximum = 0;
        int i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; i <= length - Vector512<int>.Count; i += Vector512<int>.Count)
            {
                maximum512 = TOperator.AccumulateMaximumAbsolute(Vector512.LoadUnsafe(ref coefficientBase, (nuint)i), maximum512);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; i <= length - Vector256<int>.Count; i += Vector256<int>.Count)
            {
                maximum256 = TOperator.AccumulateMaximumAbsolute(Vector256.LoadUnsafe(ref coefficientBase, (nuint)i), maximum256);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; i <= length - Vector128<int>.Count; i += Vector128<int>.Count)
            {
                maximum128 = TOperator.AccumulateMaximumAbsolute(Vector128.LoadUnsafe(ref coefficientBase, (nuint)i), maximum128);
            }
        }

        for (; i < length; i++)
        {
            maximum = TOperator.AccumulateMaximumAbsolute(Unsafe.Add(ref coefficientBase, i), maximum);
        }

        // A width the hardware lacks holds zeros, which never exceed a magnitude.
        maximum256 = Vector256.Max(maximum256, Vector256.Max(maximum512.GetLower(), maximum512.GetUpper()));
        maximum128 = Vector128.Max(maximum128, Vector128.Max(maximum256.GetLower(), maximum256.GetUpper()));
        for (int lane = 0; lane < Vector128<int>.Count; lane++)
        {
            maximum = Math.Max(maximum, maximum128[lane]);
        }

        return maximum;
    }

    /// <summary>
    /// Traverses <see cref="GetEndOfBlock(ReadOnlySpan{int}, ReadOnlySpan{short})"/> at descending register widths.
    /// </summary>
    private static ushort GetEndOfBlock<TOperator>(ReadOnlySpan<int> quantized, ReadOnlySpan<short> inverseScan)
        where TOperator : struct, ICoefficientMeasureOperator
    {
        int length = quantized.Length;
        ref int quantizedBase = ref MemoryMarshal.GetReference(quantized);
        ref short inverseScanBase = ref MemoryMarshal.GetReference(inverseScan[..length]);
        Vector512<int> maximum512 = Vector512<int>.Zero;
        Vector256<int> maximum256 = Vector256<int>.Zero;
        Vector128<int> maximum128 = Vector128<int>.Zero;
        int endOfBlock = 0;
        int i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; i <= length - Vector512<int>.Count; i += Vector512<int>.Count)
            {
                maximum512 = TOperator.AccumulateEndOfBlock(Vector512.LoadUnsafe(ref quantizedBase, (nuint)i), ref inverseScanBase, (nuint)i, maximum512);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; i <= length - Vector256<int>.Count; i += Vector256<int>.Count)
            {
                maximum256 = TOperator.AccumulateEndOfBlock(Vector256.LoadUnsafe(ref quantizedBase, (nuint)i), ref inverseScanBase, (nuint)i, maximum256);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; i <= length - Vector128<int>.Count; i += Vector128<int>.Count)
            {
                maximum128 = TOperator.AccumulateEndOfBlock(Vector128.LoadUnsafe(ref quantizedBase, (nuint)i), ref inverseScanBase, (nuint)i, maximum128);
            }
        }

        for (; i < length; i++)
        {
            endOfBlock = TOperator.AccumulateEndOfBlock(Unsafe.Add(ref quantizedBase, i), Unsafe.Add(ref inverseScanBase, i), endOfBlock);
        }

        maximum256 = Vector256.Max(maximum256, Vector256.Max(maximum512.GetLower(), maximum512.GetUpper()));
        maximum128 = Vector128.Max(maximum128, Vector128.Max(maximum256.GetLower(), maximum256.GetUpper()));
        for (int lane = 0; lane < Vector128<int>.Count; lane++)
        {
            endOfBlock = Math.Max(endOfBlock, maximum128[lane]);
        }

        return (ushort)endOfBlock;
    }

    /// <summary>
    /// Traverses <see cref="SumSquaredDifferences(ReadOnlySpan{int}, ReadOnlySpan{int}, bool)"/> at descending register widths.
    /// </summary>
    private static long SumSquaredDifferences<TOperator>(ReadOnlySpan<int> coefficients, ReadOnlySpan<int> reconstructed, bool reconstructedIsInt16)
        where TOperator : struct, ICoefficientMeasureOperator
    {
        int length = coefficients.Length;
        ref int coefficientBase = ref MemoryMarshal.GetReference(coefficients);
        ref int reconstructedBase = ref MemoryMarshal.GetReference(reconstructed[..length]);
        Vector512<long> total512 = Vector512<long>.Zero;
        Vector256<long> total256 = Vector256<long>.Zero;
        Vector128<long> total128 = Vector128<long>.Zero;
        long total = 0;
        int i = 0;

        // The storage choice is the same for every coefficient of the block, so its branch always predicts.
        if (Vector512.IsHardwareAccelerated)
        {
            for (; i <= length - Vector512<int>.Count; i += Vector512<int>.Count)
            {
                Vector512<int> values = Vector512.LoadUnsafe(ref reconstructedBase, (nuint)i);
                values = reconstructedIsInt16 ? TOperator.TruncateToInt16(values) : values;
                total512 = TOperator.AccumulateSquaredDifferences(Vector512.LoadUnsafe(ref coefficientBase, (nuint)i), values, total512);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; i <= length - Vector256<int>.Count; i += Vector256<int>.Count)
            {
                Vector256<int> values = Vector256.LoadUnsafe(ref reconstructedBase, (nuint)i);
                values = reconstructedIsInt16 ? TOperator.TruncateToInt16(values) : values;
                total256 = TOperator.AccumulateSquaredDifferences(Vector256.LoadUnsafe(ref coefficientBase, (nuint)i), values, total256);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; i <= length - Vector128<int>.Count; i += Vector128<int>.Count)
            {
                Vector128<int> values = Vector128.LoadUnsafe(ref reconstructedBase, (nuint)i);
                values = reconstructedIsInt16 ? TOperator.TruncateToInt16(values) : values;
                total128 = TOperator.AccumulateSquaredDifferences(Vector128.LoadUnsafe(ref coefficientBase, (nuint)i), values, total128);
            }
        }

        for (; i < length; i++)
        {
            int value = Unsafe.Add(ref reconstructedBase, i);
            total = TOperator.AccumulateSquaredDifferences(Unsafe.Add(ref coefficientBase, i), reconstructedIsInt16 ? (short)value : value, total);
        }

        total256 += total512.GetLower() + total512.GetUpper();
        total128 += total256.GetLower() + total256.GetUpper();
        return total + Vector128.Sum(total128);
    }

    /// <summary>
    /// Traverses <see cref="ScaleResidual(Span{int}, ReadOnlySpan{short}, int, int)"/> at descending register widths.
    /// </summary>
    private static void ScaleResidual<TOperator>(Span<int> destination, ReadOnlySpan<short> residual, int stride, int width)
        where TOperator : struct, ICoefficientMeasureOperator
    {
        ref int destinationBase = ref MemoryMarshal.GetReference(destination[..(width * width)]);
        ref short residualBase = ref MemoryMarshal.GetReference(residual[..(((width - 1) * stride) + width)]);
        for (int y = 0; y < width; y++)
        {
            ref short row = ref Unsafe.Add(ref residualBase, y * stride);
            ref int destinationRow = ref Unsafe.Add(ref destinationBase, y * width);
            int x = 0;
            if (Vector512.IsHardwareAccelerated)
            {
                for (; x <= width - Vector512<short>.Count; x += Vector512<short>.Count)
                {
                    TOperator.StoreScaledResidual(Vector512.LoadUnsafe(ref row, (nuint)x), ref Unsafe.Add(ref destinationRow, x));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x <= width - Vector256<short>.Count; x += Vector256<short>.Count)
                {
                    TOperator.StoreScaledResidual(Vector256.LoadUnsafe(ref row, (nuint)x), ref Unsafe.Add(ref destinationRow, x));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x <= width - Vector128<short>.Count; x += Vector128<short>.Count)
                {
                    TOperator.StoreScaledResidual(Vector128.LoadUnsafe(ref row, (nuint)x), ref Unsafe.Add(ref destinationRow, x));
                }
            }

            for (; x < width; x++)
            {
                Unsafe.Add(ref destinationRow, x) = TOperator.ScaleResidual(Unsafe.Add(ref row, x));
            }
        }
    }
}
