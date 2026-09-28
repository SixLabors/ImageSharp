// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Measures wedge mask candidates from the residuals of two single-reference predictions, with the arithmetic
/// of an <see cref="IWedgeOperator"/>.
/// </summary>
/// <remarks>
/// Every array is packed at the block width, so one traversal walks the whole block as one row. Each
/// traversal walks the widest available register first and finishes in the scalar overload.
/// </remarks>
internal static partial class Av1WedgeSearch
{
    /// <summary>
    /// The largest mask weight, MAX_MASK_VALUE.
    /// </summary>
    private const int MaximumMaskValue = 1 << MaximumMaskBits;

    /// <summary>
    /// The precision of a mask weight, WEDGE_WEIGHT_BITS.
    /// </summary>
    private const int MaximumMaskBits = 6;

    /// <summary>
    /// Replaces each sample of a first residual with the saturated difference of the squares of the two
    /// residuals. Reference: av1_wedge_compute_delta_squares().
    /// </summary>
    /// <param name="destination">Receives the saturated differences; it can be <paramref name="first"/>.</param>
    /// <param name="first">The residuals of the first prediction.</param>
    /// <param name="second">The residuals of the second prediction.</param>
    public static void ComputeDeltaSquares(Span<short> destination, ReadOnlySpan<short> first, ReadOnlySpan<short> second)
        => ComputeDeltaSquares<WedgeOperator>(destination, first, second);

    /// <summary>
    /// Returns whether the wedge mask with the given weights must be inverted. Reference:
    /// av1_wedge_sign_from_residuals().
    /// </summary>
    /// <param name="deltaSquares">The saturated differences of squares of the two residuals.</param>
    /// <param name="mask">The weights of the non-inverted mask.</param>
    /// <param name="limit">The sign limit: 32 times the energy of the first residual minus that of the second.</param>
    /// <returns><see langword="true"/> when the weighted differences exceed the limit.</returns>
    public static bool GetSign(ReadOnlySpan<short> deltaSquares, ReadOnlySpan<byte> mask, long limit)
        => GetWeightedDelta<WedgeOperator>(deltaSquares, mask) > limit;

    /// <summary>
    /// Returns the rounded squared error of a wedge blend from the residual of the second prediction and the
    /// difference of the predictions. Reference: av1_wedge_sse_from_residuals().
    /// </summary>
    /// <param name="residual">The residuals of the second prediction.</param>
    /// <param name="difference">The second prediction minus the first.</param>
    /// <param name="mask">The weights of the first prediction.</param>
    /// <returns>The squared error, rounded by 2 * WEDGE_WEIGHT_BITS.</returns>
    public static ulong SumSquaredErrors(ReadOnlySpan<short> residual, ReadOnlySpan<short> difference, ReadOnlySpan<byte> mask)
        => SumSquaredErrors<WedgeOperator>(residual, difference, mask);

    /// <summary>
    /// Traverses <see cref="ComputeDeltaSquares(Span{short}, ReadOnlySpan{short}, ReadOnlySpan{short})"/> at descending register widths.
    /// </summary>
    private static void ComputeDeltaSquares<TOperator>(Span<short> destination, ReadOnlySpan<short> first, ReadOnlySpan<short> second)
        where TOperator : struct, IWedgeOperator
    {
        int count = destination.Length;
        ref short destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short firstBase = ref MemoryMarshal.GetReference(first[..count]);
        ref short secondBase = ref MemoryMarshal.GetReference(second[..count]);
        int i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; i <= count - Vector512<short>.Count; i += Vector512<short>.Count)
            {
                TOperator.DeltaSquares(Vector512.LoadUnsafe(ref firstBase, (nuint)i), Vector512.LoadUnsafe(ref secondBase, (nuint)i))
                    .StoreUnsafe(ref destinationBase, (nuint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; i <= count - Vector256<short>.Count; i += Vector256<short>.Count)
            {
                TOperator.DeltaSquares(Vector256.LoadUnsafe(ref firstBase, (nuint)i), Vector256.LoadUnsafe(ref secondBase, (nuint)i))
                    .StoreUnsafe(ref destinationBase, (nuint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; i <= count - Vector128<short>.Count; i += Vector128<short>.Count)
            {
                TOperator.DeltaSquares(Vector128.LoadUnsafe(ref firstBase, (nuint)i), Vector128.LoadUnsafe(ref secondBase, (nuint)i))
                    .StoreUnsafe(ref destinationBase, (nuint)i);
            }
        }

        for (; i < count; i++)
        {
            Unsafe.Add(ref destinationBase, i) = TOperator.DeltaSquares(Unsafe.Add(ref firstBase, i), Unsafe.Add(ref secondBase, i));
        }
    }

    /// <summary>
    /// Returns the sum of the mask-weighted differences of squares, walking descending register widths.
    /// </summary>
    private static long GetWeightedDelta<TOperator>(ReadOnlySpan<short> deltaSquares, ReadOnlySpan<byte> mask)
        where TOperator : struct, IWedgeOperator
    {
        int count = deltaSquares.Length;
        ref short deltaBase = ref MemoryMarshal.GetReference(deltaSquares);
        ref byte maskBase = ref MemoryMarshal.GetReference(mask[..count]);
        Vector512<long> total512 = Vector512<long>.Zero;
        Vector256<long> total256 = Vector256<long>.Zero;
        Vector128<long> total128 = Vector128<long>.Zero;
        long total = 0;
        int i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; i <= count - Vector512<short>.Count; i += Vector512<short>.Count)
            {
                total512 = TOperator.AccumulateWeightedDelta(
                    Vector512.LoadUnsafe(ref deltaBase, (nuint)i), TOperator.LoadWeights(ref maskBase, (nuint)i, default(Vector512<short>)), total512);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; i <= count - Vector256<short>.Count; i += Vector256<short>.Count)
            {
                total256 = TOperator.AccumulateWeightedDelta(
                    Vector256.LoadUnsafe(ref deltaBase, (nuint)i), TOperator.LoadWeights(ref maskBase, (nuint)i, default(Vector256<short>)), total256);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; i <= count - Vector128<short>.Count; i += Vector128<short>.Count)
            {
                total128 = TOperator.AccumulateWeightedDelta(
                    Vector128.LoadUnsafe(ref deltaBase, (nuint)i), TOperator.LoadWeights(ref maskBase, (nuint)i, default(Vector128<short>)), total128);
            }
        }

        for (; i < count; i++)
        {
            total = TOperator.AccumulateWeightedDelta(Unsafe.Add(ref deltaBase, i), Unsafe.Add(ref maskBase, i), total);
        }

        total256 += total512.GetLower() + total512.GetUpper();
        total128 += total256.GetLower() + total256.GetUpper();
        return total + Vector128.Sum(total128);
    }

    /// <summary>
    /// Traverses <see cref="SumSquaredErrors(ReadOnlySpan{short}, ReadOnlySpan{short}, ReadOnlySpan{byte})"/> at descending register widths.
    /// </summary>
    private static ulong SumSquaredErrors<TOperator>(ReadOnlySpan<short> residual, ReadOnlySpan<short> difference, ReadOnlySpan<byte> mask)
        where TOperator : struct, IWedgeOperator
    {
        int count = residual.Length;
        ref short residualBase = ref MemoryMarshal.GetReference(residual);
        ref short differenceBase = ref MemoryMarshal.GetReference(difference[..count]);
        ref byte maskBase = ref MemoryMarshal.GetReference(mask[..count]);
        Vector512<ulong> total512 = Vector512<ulong>.Zero;
        Vector256<ulong> total256 = Vector256<ulong>.Zero;
        Vector128<ulong> total128 = Vector128<ulong>.Zero;
        ulong total = 0;
        int i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; i <= count - Vector512<short>.Count; i += Vector512<short>.Count)
            {
                total512 = TOperator.AccumulateSquaredErrors(
                    Vector512.LoadUnsafe(ref residualBase, (nuint)i),
                    Vector512.LoadUnsafe(ref differenceBase, (nuint)i),
                    TOperator.LoadWeights(ref maskBase, (nuint)i, default(Vector512<short>)),
                    total512);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; i <= count - Vector256<short>.Count; i += Vector256<short>.Count)
            {
                total256 = TOperator.AccumulateSquaredErrors(
                    Vector256.LoadUnsafe(ref residualBase, (nuint)i),
                    Vector256.LoadUnsafe(ref differenceBase, (nuint)i),
                    TOperator.LoadWeights(ref maskBase, (nuint)i, default(Vector256<short>)),
                    total256);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; i <= count - Vector128<short>.Count; i += Vector128<short>.Count)
            {
                total128 = TOperator.AccumulateSquaredErrors(
                    Vector128.LoadUnsafe(ref residualBase, (nuint)i),
                    Vector128.LoadUnsafe(ref differenceBase, (nuint)i),
                    TOperator.LoadWeights(ref maskBase, (nuint)i, default(Vector128<short>)),
                    total128);
            }
        }

        for (; i < count; i++)
        {
            total = TOperator.AccumulateSquaredErrors(Unsafe.Add(ref residualBase, i), Unsafe.Add(ref differenceBase, i), Unsafe.Add(ref maskBase, i), total);
        }

        total256 += total512.GetLower() + total512.GetUpper();
        total128 += total256.GetLower() + total256.GetUpper();
        total += Vector128.Sum(total128);

        // ROUND_POWER_OF_TWO(csse, 2 * WEDGE_WEIGHT_BITS): the blend carries two factors of 64.
        const int Shift = 2 * MaximumMaskBits;
        return (total + (1UL << (Shift - 1))) >> Shift;
    }
}
