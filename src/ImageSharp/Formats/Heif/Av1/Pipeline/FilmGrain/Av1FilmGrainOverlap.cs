// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.FilmGrain;

/// <summary>
/// Blends grain samples across adjacent synthesis blocks.
/// </summary>
/// <remarks>
/// Vertical boundaries contain only one or two strided columns and use the fixed scalar kernels. Horizontal boundaries
/// are contiguous and progress from the runtime's preferred native width through smaller vector widths before the
/// scalar tail. Every lane applies the same Q5 overlap weights, rounding offset, and signed grain clamp.
/// </remarks>
internal static class Av1FilmGrainOverlap
{
    /// <summary>
    /// Blends the two grain columns on a vertical block boundary.
    /// </summary>
    /// <param name="left">The saved grain columns from the block on the left.</param>
    /// <param name="leftStride">The saved-column row stride.</param>
    /// <param name="right">The grain columns selected for the block on the right.</param>
    /// <param name="rightStride">The right-block row stride.</param>
    /// <param name="destination">The overlap destination.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="width">The one- or two-sample overlap width.</param>
    /// <param name="height">The overlap height.</param>
    /// <param name="minimum">The minimum grain value.</param>
    /// <param name="maximum">The maximum grain value.</param>
    public static void Vertical(
        ReadOnlySpan<int> left,
        int leftStride,
        ReadOnlySpan<int> right,
        int rightStride,
        Span<int> destination,
        int destinationStride,
        int width,
        int height,
        int minimum,
        int maximum)
    {
        // Each row contributes only one or two strided samples. Gather plus scalar scatter would do more work than
        // the fixed scalar kernel, while the horizontally contiguous boundary below benefits directly from SIMD.
        if (width == 1)
        {
            for (int row = 0; row < height; row++)
            {
                int leftOffset = row * leftStride;
                int rightOffset = row * rightStride;
                int destinationOffset = row * destinationStride;

                // A subsampled one-column boundary uses the dedicated 23:22 overlap weights.
                destination[destinationOffset] = Av1Math.Clamp(
                    ((left[leftOffset] * 23) + (right[rightOffset] * 22) + 16) >> 5,
                    minimum,
                    maximum);
            }

            return;
        }

        for (int row = 0; row < height; row++)
        {
            int leftOffset = row * leftStride;
            int rightOffset = row * rightStride;
            int destinationOffset = row * destinationStride;

            // The two-column kernel biases the outer samples toward their originating block and crosses the 27:17
            // weights for the inner samples. These fixed weights are part of AV1 grain synthesis.
            destination[destinationOffset] = Av1Math.Clamp(
                ((left[leftOffset] * 27) + (right[rightOffset] * 17) + 16) >> 5,
                minimum,
                maximum);

            destination[destinationOffset + 1] = Av1Math.Clamp(
                ((left[leftOffset + 1] * 17) + (right[rightOffset + 1] * 27) + 16) >> 5,
                minimum,
                maximum);
        }
    }

    /// <summary>
    /// Blends the one or two grain rows on a horizontal block boundary.
    /// </summary>
    /// <param name="top">The saved grain rows from the block above.</param>
    /// <param name="topStride">The saved-row stride.</param>
    /// <param name="bottom">The grain rows selected for the block below.</param>
    /// <param name="bottomStride">The lower-block row stride.</param>
    /// <param name="destination">The overlap destination.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="width">The overlap width.</param>
    /// <param name="height">The one- or two-sample overlap height.</param>
    /// <param name="minimum">The minimum grain value.</param>
    /// <param name="maximum">The maximum grain value.</param>
    public static void Horizontal(
        ReadOnlySpan<int> top,
        int topStride,
        ReadOnlySpan<int> bottom,
        int bottomStride,
        Span<int> destination,
        int destinationStride,
        int width,
        int height,
        int minimum,
        int maximum)
    {
        if (height == 1)
        {
            // Vertically subsampled chroma collapses the overlap to the single-row 23:22 kernel.
            BlendRow(top, bottom, destination, width, 23, 22, minimum, maximum);
            return;
        }

        // Luma and full-height chroma use the crossed two-row 27:17 overlap kernel.
        BlendRow(top, bottom, destination, width, 27, 17, minimum, maximum);
        BlendRow(
            top[topStride..],
            bottom[bottomStride..],
            destination[destinationStride..],
            width,
            17,
            27,
            minimum,
            maximum);
    }

    /// <summary>
    /// Blends one contiguous overlap row using the widest useful vector width and a scalar remainder.
    /// </summary>
    /// <param name="left">The samples from the preceding block.</param>
    /// <param name="right">The samples from the following block.</param>
    /// <param name="destination">The blended samples.</param>
    /// <param name="width">The number of samples to blend.</param>
    /// <param name="leftWeight">The preceding-block weight.</param>
    /// <param name="rightWeight">The following-block weight.</param>
    /// <param name="minimum">The minimum grain value.</param>
    /// <param name="maximum">The maximum grain value.</param>
    private static void BlendRow(
        ReadOnlySpan<int> left,
        ReadOnlySpan<int> right,
        Span<int> destination,
        int width,
        int leftWeight,
        int rightWeight,
        int minimum,
        int maximum)
    {
        int column = 0;

        // Vector<T> exposes the runtime's preferred native width. This avoids selecting split 512-bit operations on
        // machines whose execution resources are 256 bits wide while retaining a native 512-bit traversal elsewhere.
        if (Vector512.IsHardwareAccelerated && Vector<int>.Count == Vector512<int>.Count)
        {
            column = Blend(left, right, destination, width, column, leftWeight, rightWeight, minimum, maximum, Vector512<int>.Zero);
        }

        if (Vector256.IsHardwareAccelerated)
        {
            column = Blend(left, right, destination, width, column, leftWeight, rightWeight, minimum, maximum, Vector256<int>.Zero);
        }

        if (Vector128.IsHardwareAccelerated)
        {
            column = Blend(left, right, destination, width, column, leftWeight, rightWeight, minimum, maximum, Vector128<int>.Zero);
        }

        for (; column < width; column++)
        {
            int value = ((left[column] * leftWeight) + (right[column] * rightWeight) + 16) >> 5;
            destination[column] = Av1Math.Clamp(value, minimum, maximum);
        }
    }

    /// <summary>
    /// Blends complete 512-bit groups from one overlap row.
    /// </summary>
    /// <param name="left">The samples from the preceding block.</param>
    /// <param name="right">The samples from the following block.</param>
    /// <param name="destination">The blended samples.</param>
    /// <param name="width">The number of samples to blend.</param>
    /// <param name="column">The first unprocessed sample.</param>
    /// <param name="leftWeight">The preceding-block weight.</param>
    /// <param name="rightWeight">The following-block weight.</param>
    /// <param name="minimum">The minimum grain value.</param>
    /// <param name="maximum">The maximum grain value.</param>
    /// <param name="vector">The overload-selection value.</param>
    /// <returns>The first sample not processed by this vector width.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Blend(
        ReadOnlySpan<int> left,
        ReadOnlySpan<int> right,
        Span<int> destination,
        int width,
        int column,
        int leftWeight,
        int rightWeight,
        int minimum,
        int maximum,
        Vector512<int> vector)
    {
        ref int leftBase = ref MemoryMarshal.GetReference(left);
        ref int rightBase = ref MemoryMarshal.GetReference(right);
        ref int destinationBase = ref MemoryMarshal.GetReference(destination);
        Vector512<int> leftWeights = Vector512.Create(leftWeight);
        Vector512<int> rightWeights = Vector512.Create(rightWeight);
        Vector512<int> rounding = Vector512.Create(16);
        Vector512<int> minima = Vector512.Create(minimum);
        Vector512<int> maxima = Vector512.Create(maximum);
        int vectorEnd = width - Vector512<int>.Count;
        for (; column <= vectorEnd; column += Vector512<int>.Count)
        {
            Vector512<int> leftValues = Vector512.LoadUnsafe(ref leftBase, (nuint)column);
            Vector512<int> rightValues = Vector512.LoadUnsafe(ref rightBase, (nuint)column);
            Vector512<int> result = ((leftValues * leftWeights) + (rightValues * rightWeights) + rounding) >> 5;
            Vector512.Min(Vector512.Max(result, minima), maxima).StoreUnsafe(ref destinationBase, (nuint)column);
        }

        return column;
    }

    /// <summary>
    /// Blends complete 256-bit groups from one overlap row.
    /// </summary>
    /// <param name="left">The samples from the preceding block.</param>
    /// <param name="right">The samples from the following block.</param>
    /// <param name="destination">The blended samples.</param>
    /// <param name="width">The number of samples to blend.</param>
    /// <param name="column">The first unprocessed sample.</param>
    /// <param name="leftWeight">The preceding-block weight.</param>
    /// <param name="rightWeight">The following-block weight.</param>
    /// <param name="minimum">The minimum grain value.</param>
    /// <param name="maximum">The maximum grain value.</param>
    /// <param name="vector">The overload-selection value.</param>
    /// <returns>The first sample not processed by this vector width.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Blend(
        ReadOnlySpan<int> left,
        ReadOnlySpan<int> right,
        Span<int> destination,
        int width,
        int column,
        int leftWeight,
        int rightWeight,
        int minimum,
        int maximum,
        Vector256<int> vector)
    {
        ref int leftBase = ref MemoryMarshal.GetReference(left);
        ref int rightBase = ref MemoryMarshal.GetReference(right);
        ref int destinationBase = ref MemoryMarshal.GetReference(destination);
        Vector256<int> leftWeights = Vector256.Create(leftWeight);
        Vector256<int> rightWeights = Vector256.Create(rightWeight);
        Vector256<int> rounding = Vector256.Create(16);
        Vector256<int> minima = Vector256.Create(minimum);
        Vector256<int> maxima = Vector256.Create(maximum);
        int vectorEnd = width - Vector256<int>.Count;
        for (; column <= vectorEnd; column += Vector256<int>.Count)
        {
            Vector256<int> leftValues = Vector256.LoadUnsafe(ref leftBase, (nuint)column);
            Vector256<int> rightValues = Vector256.LoadUnsafe(ref rightBase, (nuint)column);
            Vector256<int> result = ((leftValues * leftWeights) + (rightValues * rightWeights) + rounding) >> 5;
            Vector256.Min(Vector256.Max(result, minima), maxima).StoreUnsafe(ref destinationBase, (nuint)column);
        }

        return column;
    }

    /// <summary>
    /// Blends complete 128-bit groups from one overlap row.
    /// </summary>
    /// <param name="left">The samples from the preceding block.</param>
    /// <param name="right">The samples from the following block.</param>
    /// <param name="destination">The blended samples.</param>
    /// <param name="width">The number of samples to blend.</param>
    /// <param name="column">The first unprocessed sample.</param>
    /// <param name="leftWeight">The preceding-block weight.</param>
    /// <param name="rightWeight">The following-block weight.</param>
    /// <param name="minimum">The minimum grain value.</param>
    /// <param name="maximum">The maximum grain value.</param>
    /// <param name="vector">The overload-selection value.</param>
    /// <returns>The first sample not processed by this vector width.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Blend(
        ReadOnlySpan<int> left,
        ReadOnlySpan<int> right,
        Span<int> destination,
        int width,
        int column,
        int leftWeight,
        int rightWeight,
        int minimum,
        int maximum,
        Vector128<int> vector)
    {
        ref int leftBase = ref MemoryMarshal.GetReference(left);
        ref int rightBase = ref MemoryMarshal.GetReference(right);
        ref int destinationBase = ref MemoryMarshal.GetReference(destination);
        Vector128<int> leftWeights = Vector128.Create(leftWeight);
        Vector128<int> rightWeights = Vector128.Create(rightWeight);
        Vector128<int> rounding = Vector128.Create(16);
        Vector128<int> minima = Vector128.Create(minimum);
        Vector128<int> maxima = Vector128.Create(maximum);
        int vectorEnd = width - Vector128<int>.Count;
        for (; column <= vectorEnd; column += Vector128<int>.Count)
        {
            Vector128<int> leftValues = Vector128.LoadUnsafe(ref leftBase, (nuint)column);
            Vector128<int> rightValues = Vector128.LoadUnsafe(ref rightBase, (nuint)column);
            Vector128<int> result = ((leftValues * leftWeights) + (rightValues * rightWeights) + rounding) >> 5;
            Vector128.Min(Vector128.Max(result, minima), maxima).StoreUnsafe(ref destinationBase, (nuint)column);
        }

        return column;
    }
}
