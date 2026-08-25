// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Reconstructs HEVC intra-prediction blocks from prepared neighboring samples.
/// </summary>
internal static partial class HevcIntraPredictor
{
    /// <summary>
    /// The HEVC planar prediction mode.
    /// </summary>
    private const int PlanarMode = 0;

    /// <summary>
    /// The HEVC DC prediction mode.
    /// </summary>
    private const int DcMode = 1;

    /// <summary>
    /// The HEVC horizontal prediction mode.
    /// </summary>
    private const int HorizontalMode = 10;

    /// <summary>
    /// The first prediction mode whose main reference is the top row.
    /// </summary>
    private const int FirstVerticalMode = 18;

    /// <summary>
    /// The HEVC vertical prediction mode.
    /// </summary>
    private const int VerticalMode = 26;

    /// <summary>
    /// The largest transform-block side supported by HEVC intra prediction.
    /// </summary>
    private const int MaximumBlockSize = 32;

    /// <summary>
    /// Defines one closed intra-prediction operation selected by the decoded mode.
    /// </summary>
    /// <typeparam name="TOperator">The implementing operator type.</typeparam>
    private interface IHevcIntraPredictionOperator<TOperator>
        where TOperator : struct, IHevcIntraPredictionOperator<TOperator>
    {
        /// <summary>
        /// Reconstructs one square prediction block.
        /// </summary>
        /// <param name="top">The top-left, top, and top-right reference samples.</param>
        /// <param name="left">The top-left, left, and below-left reference samples.</param>
        /// <param name="destination">The destination buffer beginning at the block origin.</param>
        /// <param name="destinationStride">The destination row stride in samples.</param>
        /// <param name="size">The square block side in samples.</param>
        /// <param name="mode">The decoded prediction mode.</param>
        /// <param name="bitDepth">The reconstructed component precision.</param>
        /// <param name="filterPredictionEdges">Whether the luma edge filter applies to the selected block.</param>
        /// <param name="scratch">The caller-owned block and extended-reference scratch space.</param>
        public static abstract void Predict(
            ReadOnlySpan<ushort> top,
            ReadOnlySpan<ushort> left,
            Span<ushort> destination,
            int destinationStride,
            int size,
            int mode,
            int bitDepth,
            bool filterPredictionEdges,
            Span<ushort> scratch);
    }

    /// <summary>
    /// Gets the angle selected by each absolute angular-mode displacement.
    /// </summary>
    private static ReadOnlySpan<int> PredictionAngles => [0, 2, 5, 9, 13, 17, 21, 26, 32];

    /// <summary>
    /// Gets the reciprocal angle used to extend the main reference for negative directions.
    /// </summary>
    private static ReadOnlySpan<int> InversePredictionAngles => [0, 4096, 1638, 910, 630, 482, 390, 315, 256];

    /// <summary>
    /// Gets the scratch length required to predict a block of the specified size.
    /// </summary>
    /// <param name="log2Size">The base-two logarithm of the square block side.</param>
    /// <returns>The required number of <see cref="ushort"/> elements.</returns>
    public static int GetScratchLength(int log2Size)
    {
        DebugGuard.MustBeBetweenOrEqualTo(log2Size, 2, 5, nameof(log2Size));
        int size = 1 << log2Size;
        return (size * size) + (4 * size) + 1;
    }

    /// <summary>
    /// Reconstructs one square intra-prediction block using a closed operator selected by the decoded mode.
    /// </summary>
    /// <param name="top">The top-left, top, and top-right reference samples.</param>
    /// <param name="left">The top-left, left, and below-left reference samples.</param>
    /// <param name="destination">The destination buffer beginning at the block origin.</param>
    /// <param name="destinationStride">The destination row stride in samples.</param>
    /// <param name="log2Size">The base-two logarithm of the square block side.</param>
    /// <param name="mode">The decoded prediction mode in the inclusive range zero through thirty-four.</param>
    /// <param name="bitDepth">The reconstructed component precision.</param>
    /// <param name="filterPredictionEdges">Whether the luma edge filter applies to the selected block.</param>
    /// <param name="scratch">The caller-owned scratch returned by <see cref="GetScratchLength(int)"/>.</param>
    public static void Predict(
        ReadOnlySpan<ushort> top,
        ReadOnlySpan<ushort> left,
        Span<ushort> destination,
        int destinationStride,
        int log2Size,
        int mode,
        int bitDepth,
        bool filterPredictionEdges,
        Span<ushort> scratch)
    {
        DebugGuard.MustBeBetweenOrEqualTo(log2Size, 2, 5, nameof(log2Size));
        DebugGuard.MustBeBetweenOrEqualTo(mode, PlanarMode, 34, nameof(mode));
        int size = 1 << log2Size;

        switch (mode)
        {
            case PlanarMode:
                Predict<PlanarPredictionOperator>(
                    top,
                    left,
                    destination,
                    destinationStride,
                    size,
                    mode,
                    bitDepth,
                    filterPredictionEdges,
                    scratch);
                break;
            case DcMode:
                Predict<DcPredictionOperator>(
                    top,
                    left,
                    destination,
                    destinationStride,
                    size,
                    mode,
                    bitDepth,
                    filterPredictionEdges,
                    scratch);
                break;
            default:
                Predict<AngularPredictionOperator>(
                    top,
                    left,
                    destination,
                    destinationStride,
                    size,
                    mode,
                    bitDepth,
                    filterPredictionEdges,
                    scratch);
                break;
        }
    }

    /// <summary>
    /// Filters prepared reference samples using the normative three-tap or strong bilinear filter.
    /// </summary>
    /// <param name="top">The unfiltered top-left, top, and top-right samples.</param>
    /// <param name="left">The unfiltered top-left, left, and below-left samples.</param>
    /// <param name="filteredTop">The destination top reference.</param>
    /// <param name="filteredLeft">The destination left reference.</param>
    /// <param name="log2Size">The base-two logarithm of the square prediction-block side.</param>
    /// <param name="bitDepth">The reconstructed luma precision.</param>
    /// <param name="strongIntraSmoothingEnabled">Whether the sequence permits strong intra smoothing.</param>
    public static void FilterReferenceSamples(
        ReadOnlySpan<ushort> top,
        ReadOnlySpan<ushort> left,
        Span<ushort> filteredTop,
        Span<ushort> filteredLeft,
        int log2Size,
        int bitDepth,
        bool strongIntraSmoothingEnabled)
    {
        DebugGuard.MustBeBetweenOrEqualTo(log2Size, 2, 5, nameof(log2Size));
        int size = 1 << log2Size;
        int referenceLength = (size * 2) + 1;
        bool useStrongSmoothing = strongIntraSmoothingEnabled && size == MaximumBlockSize;
        if (useStrongSmoothing)
        {
            int threshold = 1 << (bitDepth - 5);
            int last = referenceLength - 1;
            bool leftIsBilinear = Math.Abs((left[last] + left[0]) - (2 * left[size])) < threshold;
            bool topIsBilinear = Math.Abs((top[0] + top[last]) - (2 * top[size])) < threshold;
            useStrongSmoothing = leftIsBilinear && topIsBilinear;
        }

        if (useStrongSmoothing)
        {
            FilterReferenceBilinear(top[..referenceLength], filteredTop, size);
            FilterReferenceBilinear(left[..referenceLength], filteredLeft, size);
            return;
        }

        // The corner belongs to both references. Filtering it once from the first samples on both sides keeps the
        // two logical arrays identical at index zero before their independent one-dimensional filters continue.
        ushort filteredCorner = (ushort)((left[1] + (2 * top[0]) + top[1] + 2) >> 2);
        filteredTop[0] = filteredCorner;
        filteredLeft[0] = filteredCorner;
        FilterReferenceThreeTap(top[..referenceLength], filteredTop);
        FilterReferenceThreeTap(left[..referenceLength], filteredLeft);
    }

    /// <summary>
    /// Invokes one statically selected prediction operator without interface dispatch in the block loop.
    /// </summary>
    /// <typeparam name="TOperator">The selected prediction operator.</typeparam>
    /// <param name="top">The prepared top reference.</param>
    /// <param name="left">The prepared left reference.</param>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride in samples.</param>
    /// <param name="size">The square block side in samples.</param>
    /// <param name="mode">The decoded prediction mode.</param>
    /// <param name="bitDepth">The reconstructed component precision.</param>
    /// <param name="filterPredictionEdges">Whether the luma edge filter applies.</param>
    /// <param name="scratch">The caller-owned prediction scratch.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Predict<TOperator>(
        ReadOnlySpan<ushort> top,
        ReadOnlySpan<ushort> left,
        Span<ushort> destination,
        int destinationStride,
        int size,
        int mode,
        int bitDepth,
        bool filterPredictionEdges,
        Span<ushort> scratch)
        where TOperator : struct, IHevcIntraPredictionOperator<TOperator>
        => TOperator.Predict(
            top,
            left,
            destination,
            destinationStride,
            size,
            mode,
            bitDepth,
            filterPredictionEdges,
            scratch);

    /// <summary>
    /// Applies the strong bilinear filter between the reference endpoints.
    /// </summary>
    /// <param name="source">The complete unfiltered reference.</param>
    /// <param name="destination">The complete filtered reference.</param>
    /// <param name="size">The prediction-block side in samples.</param>
    private static void FilterReferenceBilinear(ReadOnlySpan<ushort> source, Span<ushort> destination, int size)
    {
        int last = source.Length - 1;
        destination[0] = source[0];
        destination[last] = source[last];
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        uint first = source[0];
        uint final = source[last];
        int shift = BitOperations.Log2((uint)(size * 2));
        uint rounding = (uint)size;
        int i = 1;

        // Each widened lane represents one reference coordinate. The weights sum to 2N, so narrowing is exact
        // after the rounded shift for every supported 8, 10, and 12-bit sample.
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<uint> indices = CreateIndicesVector512();
            int oneVectorFromEnd = last - Vector512<ushort>.Count;
            for (; i <= oneVectorFromEnd; i += Vector512<ushort>.Count)
            {
                Vector512<uint> lowerIndices = indices + Vector512.Create((uint)i);
                Vector512<uint> upperIndices = lowerIndices + Vector512.Create((uint)Vector512<uint>.Count);
                Vector512<uint> lower = (((Vector512.Create((uint)last) - lowerIndices) * first) + (lowerIndices * final) + Vector512.Create(rounding)) >> shift;
                Vector512<uint> upper = (((Vector512.Create((uint)last) - upperIndices) * first) + (upperIndices * final) + Vector512.Create(rounding)) >> shift;
                Vector512.Narrow(lower, upper).StoreUnsafe(ref Unsafe.Add(ref destinationBase, i));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<uint> indices = CreateIndicesVector256();
            int oneVectorFromEnd = last - Vector256<ushort>.Count;
            for (; i <= oneVectorFromEnd; i += Vector256<ushort>.Count)
            {
                Vector256<uint> lowerIndices = indices + Vector256.Create((uint)i);
                Vector256<uint> upperIndices = lowerIndices + Vector256.Create((uint)Vector256<uint>.Count);
                Vector256<uint> lower = (((Vector256.Create((uint)last) - lowerIndices) * first) + (lowerIndices * final) + Vector256.Create(rounding)) >> shift;
                Vector256<uint> upper = (((Vector256.Create((uint)last) - upperIndices) * first) + (upperIndices * final) + Vector256.Create(rounding)) >> shift;
                Vector256.Narrow(lower, upper).StoreUnsafe(ref Unsafe.Add(ref destinationBase, i));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<uint> indices = CreateIndicesVector128();
            int oneVectorFromEnd = last - Vector128<ushort>.Count;
            for (; i <= oneVectorFromEnd; i += Vector128<ushort>.Count)
            {
                Vector128<uint> lowerIndices = indices + Vector128.Create((uint)i);
                Vector128<uint> upperIndices = lowerIndices + Vector128.Create((uint)Vector128<uint>.Count);
                Vector128<uint> lower = (((Vector128.Create((uint)last) - lowerIndices) * first) + (lowerIndices * final) + Vector128.Create(rounding)) >> shift;
                Vector128<uint> upper = (((Vector128.Create((uint)last) - upperIndices) * first) + (upperIndices * final) + Vector128.Create(rounding)) >> shift;
                Vector128.Narrow(lower, upper).StoreUnsafe(ref Unsafe.Add(ref destinationBase, i));
            }
        }

        for (; i < last; i++)
        {
            Unsafe.Add(ref destinationBase, i) = (ushort)((((last - i) * first) + (i * final) + rounding) >> shift);
        }
    }

    /// <summary>
    /// Applies the normal three-tap reference filter to every non-endpoint sample.
    /// </summary>
    /// <param name="source">The complete unfiltered reference.</param>
    /// <param name="destination">The complete filtered reference with its corner already initialized.</param>
    private static void FilterReferenceThreeTap(ReadOnlySpan<ushort> source, Span<ushort> destination)
    {
        int last = source.Length - 1;
        destination[last] = source[last];
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        int i = 1;

        if (Vector512.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = last - Vector512<ushort>.Count;
            for (; i <= oneVectorFromEnd; i += Vector512<ushort>.Count)
            {
                Vector512<ushort> previous = Vector512.LoadUnsafe(ref sourceBase, (nuint)(i - 1));
                Vector512<ushort> current = Vector512.LoadUnsafe(ref sourceBase, (nuint)i);
                Vector512<ushort> next = Vector512.LoadUnsafe(ref sourceBase, (nuint)(i + 1));
                (Vector512<uint> previousLow, Vector512<uint> previousHigh) = Vector512.Widen(previous);
                (Vector512<uint> currentLow, Vector512<uint> currentHigh) = Vector512.Widen(current);
                (Vector512<uint> nextLow, Vector512<uint> nextHigh) = Vector512.Widen(next);
                Vector512<uint> low = (previousLow + (currentLow << 1) + nextLow + Vector512.Create(2U)) >> 2;
                Vector512<uint> high = (previousHigh + (currentHigh << 1) + nextHigh + Vector512.Create(2U)) >> 2;
                Vector512.Narrow(low, high).StoreUnsafe(ref Unsafe.Add(ref destinationBase, i));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = last - Vector256<ushort>.Count;
            for (; i <= oneVectorFromEnd; i += Vector256<ushort>.Count)
            {
                Vector256<ushort> previous = Vector256.LoadUnsafe(ref sourceBase, (nuint)(i - 1));
                Vector256<ushort> current = Vector256.LoadUnsafe(ref sourceBase, (nuint)i);
                Vector256<ushort> next = Vector256.LoadUnsafe(ref sourceBase, (nuint)(i + 1));
                (Vector256<uint> previousLow, Vector256<uint> previousHigh) = Vector256.Widen(previous);
                (Vector256<uint> currentLow, Vector256<uint> currentHigh) = Vector256.Widen(current);
                (Vector256<uint> nextLow, Vector256<uint> nextHigh) = Vector256.Widen(next);
                Vector256<uint> low = (previousLow + (currentLow << 1) + nextLow + Vector256.Create(2U)) >> 2;
                Vector256<uint> high = (previousHigh + (currentHigh << 1) + nextHigh + Vector256.Create(2U)) >> 2;
                Vector256.Narrow(low, high).StoreUnsafe(ref Unsafe.Add(ref destinationBase, i));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = last - Vector128<ushort>.Count;
            for (; i <= oneVectorFromEnd; i += Vector128<ushort>.Count)
            {
                Vector128<ushort> previous = Vector128.LoadUnsafe(ref sourceBase, (nuint)(i - 1));
                Vector128<ushort> current = Vector128.LoadUnsafe(ref sourceBase, (nuint)i);
                Vector128<ushort> next = Vector128.LoadUnsafe(ref sourceBase, (nuint)(i + 1));
                (Vector128<uint> previousLow, Vector128<uint> previousHigh) = Vector128.Widen(previous);
                (Vector128<uint> currentLow, Vector128<uint> currentHigh) = Vector128.Widen(current);
                (Vector128<uint> nextLow, Vector128<uint> nextHigh) = Vector128.Widen(next);
                Vector128<uint> low = (previousLow + (currentLow << 1) + nextLow + Vector128.Create(2U)) >> 2;
                Vector128<uint> high = (previousHigh + (currentHigh << 1) + nextHigh + Vector128.Create(2U)) >> 2;
                Vector128.Narrow(low, high).StoreUnsafe(ref Unsafe.Add(ref destinationBase, i));
            }
        }

        for (; i < last; i++)
        {
            Unsafe.Add(ref destinationBase, i) = (ushort)((source[i - 1] + (2 * source[i]) + source[i + 1] + 2) >> 2);
        }
    }

    /// <summary>
    /// Creates the zero-through-fifteen lane indices used by 512-bit weighted interpolation.
    /// </summary>
    /// <returns>The ordered lane indices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<uint> CreateIndicesVector512()
        => Vector512.Create(0U, 1U, 2U, 3U, 4U, 5U, 6U, 7U, 8U, 9U, 10U, 11U, 12U, 13U, 14U, 15U);

    /// <summary>
    /// Creates the zero-through-seven lane indices used by 256-bit weighted interpolation.
    /// </summary>
    /// <returns>The ordered lane indices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<uint> CreateIndicesVector256() => Vector256.Create(0U, 1U, 2U, 3U, 4U, 5U, 6U, 7U);

    /// <summary>
    /// Creates the zero-through-three lane indices used by 128-bit weighted interpolation.
    /// </summary>
    /// <returns>The ordered lane indices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> CreateIndicesVector128() => Vector128.Create(0U, 1U, 2U, 3U);
}
