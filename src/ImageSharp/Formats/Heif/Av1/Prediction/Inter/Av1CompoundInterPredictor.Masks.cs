// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Produces the smooth inter-intra and predictor-difference masks used by compound blending.
/// </content>
internal static partial class Av1CompoundInterPredictor
{
    /// <summary>
    /// Gets libaom's one-dimensional inter-intra alpha curve.
    /// </summary>
    private static ReadOnlySpan<byte> InterIntraWeights =>
    [
        60, 58, 56, 54, 52, 50, 48, 47, 45, 44, 42, 41, 39, 38, 37, 35,
        34, 33, 32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 22, 21, 20,
        19, 19, 18, 18, 17, 16, 16, 15, 15, 14, 14, 13, 13, 12, 12, 12,
        11, 11, 10, 10, 10, 9, 9, 9, 8, 8, 8, 8, 7, 7, 7, 7,
        6, 6, 6, 6, 6, 5, 5, 5, 5, 5, 4, 4, 4, 4, 4, 4,
        4, 4, 4, 4, 3, 3, 3, 3, 3, 3, 3, 3, 3, 2, 2, 2,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
    ];

    /// <summary>
    /// Fills a smooth inter-intra mask for one plane.
    /// </summary>
    public static void FillInterIntraMask(
        Span<byte> mask,
        int maskStride,
        int width,
        int height,
        Av1InterIntraMode mode,
        bool invert)
    {
        int sizeScale = 128 / Math.Max(width, height);
        for (int row = 0; row < height; row++)
        {
            Span<byte> maskRow = mask.Slice(row * maskStride, width);
            for (int column = 0; column < width; column++)
            {
                int alpha = mode switch
                {
                    Av1InterIntraMode.Vertical => InterIntraWeights[row * sizeScale],
                    Av1InterIntraMode.Horizontal => InterIntraWeights[column * sizeScale],
                    Av1InterIntraMode.Smooth => InterIntraWeights[Math.Min(row, column) * sizeScale],
                    _ => 32,
                };

                maskRow[column] = (byte)(invert ? MaximumMaskAlpha - alpha : alpha);
            }
        }
    }

    /// <summary>
    /// Fills an 8-bit difference-weighted compound mask.
    /// </summary>
    public static void FillDifferenceWeightedMask(
        Span<byte> mask,
        int maskStride,
        ReadOnlySpan<byte> first,
        int firstStride,
        ReadOnlySpan<byte> second,
        int secondStride,
        int width,
        int height,
        Av1DifferenceWeightedMaskType maskType)
    {
        bool invert = maskType == Av1DifferenceWeightedMaskType.Type38Inverse;
        for (int row = 0; row < height; row++)
        {
            Span<byte> maskRow = mask.Slice(row * maskStride, width);
            ReadOnlySpan<byte> firstRow = first.Slice(row * firstStride, width);
            ReadOnlySpan<byte> secondRow = second.Slice(row * secondStride, width);
            ref byte maskReference = ref MemoryMarshal.GetReference(maskRow);
            ref byte firstReference = ref MemoryMarshal.GetReference(firstRow);
            ref byte secondReference = ref MemoryMarshal.GetReference(secondRow);
            int column = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector512<byte>.Count;
                for (; column <= vectorEnd; column += Vector512<byte>.Count)
                {
                    Vector512<byte> firstVector = Vector512.LoadUnsafe(ref firstReference, (nuint)column);
                    Vector512<byte> secondVector = Vector512.LoadUnsafe(ref secondReference, (nuint)column);
                    DifferenceWeighted(firstVector, secondVector, 4, invert).StoreUnsafe(ref maskReference, (nuint)column);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector256<byte>.Count;
                for (; column <= vectorEnd; column += Vector256<byte>.Count)
                {
                    Vector256<byte> firstVector = Vector256.LoadUnsafe(ref firstReference, (nuint)column);
                    Vector256<byte> secondVector = Vector256.LoadUnsafe(ref secondReference, (nuint)column);
                    DifferenceWeighted(firstVector, secondVector, 4, invert).StoreUnsafe(ref maskReference, (nuint)column);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector128<byte>.Count;
                for (; column <= vectorEnd; column += Vector128<byte>.Count)
                {
                    Vector128<byte> firstVector = Vector128.LoadUnsafe(ref firstReference, (nuint)column);
                    Vector128<byte> secondVector = Vector128.LoadUnsafe(ref secondReference, (nuint)column);
                    DifferenceWeighted(firstVector, secondVector, 4, invert).StoreUnsafe(ref maskReference, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                int difference = Math.Abs(firstRow[column] - secondRow[column]) >> 4;
                int alpha = Math.Min(MaximumMaskAlpha, 38 + difference);
                maskRow[column] = (byte)(invert ? MaximumMaskAlpha - alpha : alpha);
            }
        }
    }

    /// <summary>
    /// Fills a high-bit-depth difference-weighted compound mask.
    /// </summary>
    public static void FillDifferenceWeightedMask(
        Span<byte> mask,
        int maskStride,
        ReadOnlySpan<ushort> first,
        int firstStride,
        ReadOnlySpan<ushort> second,
        int secondStride,
        int width,
        int height,
        int bitDepth,
        Av1DifferenceWeightedMaskType maskType)
    {
        bool invert = maskType == Av1DifferenceWeightedMaskType.Type38Inverse;
        int differenceShift = bitDepth - 8 + 4;
        for (int row = 0; row < height; row++)
        {
            Span<byte> maskRow = mask.Slice(row * maskStride, width);
            ReadOnlySpan<ushort> firstRow = first.Slice(row * firstStride, width);
            ReadOnlySpan<ushort> secondRow = second.Slice(row * secondStride, width);
            ref byte maskReference = ref MemoryMarshal.GetReference(maskRow);
            ref ushort firstReference = ref MemoryMarshal.GetReference(firstRow);
            ref ushort secondReference = ref MemoryMarshal.GetReference(secondRow);
            int column = 0;

            // Two input vectors narrow to one packed byte mask. This keeps mask construction contiguous and avoids
            // temporary buffers before the following vector blend consumes the complete plane block.
            if (Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector512<byte>.Count;
                for (; column <= vectorEnd; column += Vector512<byte>.Count)
                {
                    Vector512<ushort> first0 = Vector512.LoadUnsafe(ref firstReference, (nuint)column);
                    Vector512<ushort> first1 = Vector512.LoadUnsafe(ref firstReference, (nuint)(column + Vector512<ushort>.Count));
                    Vector512<ushort> second0 = Vector512.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector512<ushort> second1 = Vector512.LoadUnsafe(ref secondReference, (nuint)(column + Vector512<ushort>.Count));
                    DifferenceWeighted(first0, first1, second0, second1, differenceShift, invert)
                        .StoreUnsafe(ref maskReference, (nuint)column);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector256<byte>.Count;
                for (; column <= vectorEnd; column += Vector256<byte>.Count)
                {
                    Vector256<ushort> first0 = Vector256.LoadUnsafe(ref firstReference, (nuint)column);
                    Vector256<ushort> first1 = Vector256.LoadUnsafe(ref firstReference, (nuint)(column + Vector256<ushort>.Count));
                    Vector256<ushort> second0 = Vector256.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector256<ushort> second1 = Vector256.LoadUnsafe(ref secondReference, (nuint)(column + Vector256<ushort>.Count));
                    DifferenceWeighted(first0, first1, second0, second1, differenceShift, invert)
                        .StoreUnsafe(ref maskReference, (nuint)column);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector128<byte>.Count;
                for (; column <= vectorEnd; column += Vector128<byte>.Count)
                {
                    Vector128<ushort> first0 = Vector128.LoadUnsafe(ref firstReference, (nuint)column);
                    Vector128<ushort> first1 = Vector128.LoadUnsafe(ref firstReference, (nuint)(column + Vector128<ushort>.Count));
                    Vector128<ushort> second0 = Vector128.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector128<ushort> second1 = Vector128.LoadUnsafe(ref secondReference, (nuint)(column + Vector128<ushort>.Count));
                    DifferenceWeighted(first0, first1, second0, second1, differenceShift, invert)
                        .StoreUnsafe(ref maskReference, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                int difference = Math.Abs(firstRow[column] - secondRow[column]) >> differenceShift;
                int alpha = Math.Min(MaximumMaskAlpha, 38 + difference);
                maskRow[column] = (byte)(invert ? MaximumMaskAlpha - alpha : alpha);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> DifferenceWeighted(Vector128<byte> first, Vector128<byte> second, int shift, bool invert)
    {
        Vector128<byte> difference = Vector128.Max(first, second) - Vector128.Min(first, second);
        Vector128<ushort> lower = DifferenceWeightedAlpha(Vector128.WidenLower(difference), shift, invert);
        Vector128<ushort> upper = DifferenceWeightedAlpha(Vector128.WidenUpper(difference), shift, invert);
        return Vector128.Narrow(lower, upper);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<byte> DifferenceWeighted(Vector256<byte> first, Vector256<byte> second, int shift, bool invert)
    {
        Vector256<byte> difference = Vector256.Max(first, second) - Vector256.Min(first, second);
        Vector256<ushort> lower = DifferenceWeightedAlpha(Vector256.WidenLower(difference), shift, invert);
        Vector256<ushort> upper = DifferenceWeightedAlpha(Vector256.WidenUpper(difference), shift, invert);
        return Vector256.Narrow(lower, upper);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<byte> DifferenceWeighted(Vector512<byte> first, Vector512<byte> second, int shift, bool invert)
    {
        Vector512<byte> difference = Vector512.Max(first, second) - Vector512.Min(first, second);
        Vector512<ushort> lower = DifferenceWeightedAlpha(Vector512.WidenLower(difference), shift, invert);
        Vector512<ushort> upper = DifferenceWeightedAlpha(Vector512.WidenUpper(difference), shift, invert);
        return Vector512.Narrow(lower, upper);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> DifferenceWeighted(
        Vector128<ushort> first0,
        Vector128<ushort> first1,
        Vector128<ushort> second0,
        Vector128<ushort> second1,
        int shift,
        bool invert)
        => Vector128.Narrow(
            DifferenceWeightedAlpha(Vector128.Max(first0, second0) - Vector128.Min(first0, second0), shift, invert),
            DifferenceWeightedAlpha(Vector128.Max(first1, second1) - Vector128.Min(first1, second1), shift, invert));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<byte> DifferenceWeighted(
        Vector256<ushort> first0,
        Vector256<ushort> first1,
        Vector256<ushort> second0,
        Vector256<ushort> second1,
        int shift,
        bool invert)
        => Vector256.Narrow(
            DifferenceWeightedAlpha(Vector256.Max(first0, second0) - Vector256.Min(first0, second0), shift, invert),
            DifferenceWeightedAlpha(Vector256.Max(first1, second1) - Vector256.Min(first1, second1), shift, invert));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<byte> DifferenceWeighted(
        Vector512<ushort> first0,
        Vector512<ushort> first1,
        Vector512<ushort> second0,
        Vector512<ushort> second1,
        int shift,
        bool invert)
        => Vector512.Narrow(
            DifferenceWeightedAlpha(Vector512.Max(first0, second0) - Vector512.Min(first0, second0), shift, invert),
            DifferenceWeightedAlpha(Vector512.Max(first1, second1) - Vector512.Min(first1, second1), shift, invert));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ushort> DifferenceWeightedAlpha(Vector128<ushort> difference, int shift, bool invert)
    {
        Vector128<ushort> maximum = Vector128.Create((ushort)MaximumMaskAlpha);
        Vector128<ushort> alpha = Vector128.Min(maximum, (difference >> shift) + Vector128.Create((ushort)38));
        return invert ? maximum - alpha : alpha;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ushort> DifferenceWeightedAlpha(Vector256<ushort> difference, int shift, bool invert)
    {
        Vector256<ushort> maximum = Vector256.Create((ushort)MaximumMaskAlpha);
        Vector256<ushort> alpha = Vector256.Min(maximum, (difference >> shift) + Vector256.Create((ushort)38));
        return invert ? maximum - alpha : alpha;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<ushort> DifferenceWeightedAlpha(Vector512<ushort> difference, int shift, bool invert)
    {
        Vector512<ushort> maximum = Vector512.Create((ushort)MaximumMaskAlpha);
        Vector512<ushort> alpha = Vector512.Min(maximum, (difference >> shift) + Vector512.Create((ushort)38));
        return invert ? maximum - alpha : alpha;
    }
}
