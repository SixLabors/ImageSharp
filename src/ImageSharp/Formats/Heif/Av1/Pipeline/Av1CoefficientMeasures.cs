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
}
