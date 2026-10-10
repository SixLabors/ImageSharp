// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Counts, ranks and dilates the colors of a 16x16 eight-bit block.
/// </content>
internal static partial class Av1ScreenContentDetector
{
    /// <summary>
    /// Counts the distinct values of a block. The count stops when it exceeds a threshold.
    /// </summary>
    /// <remarks>
    /// Each update depends on the sample value, so the count uses a scalar bitset. A measured vector form that marks one color
    /// per block comparison ran 2.6 times slower on photographic blocks, where the scan stops after a few samples.
    /// </remarks>
    /// <param name="block">The 16x16 eight-bit block samples.</param>
    /// <param name="threshold">The largest count of interest.</param>
    /// <param name="colorCount">Receives the count, which is one above the threshold when the scan stopped early.</param>
    /// <returns><see langword="true"/> when the count does not exceed the threshold.</returns>
    internal static bool CountColorsWithThreshold(ReadOnlySpan<byte> block, int threshold, out int colorCount)
    {
        Span<ulong> seen = stackalloc ulong[4];
        seen.Clear();
        colorCount = 0;
        for (int i = 0; i < block.Length; i++)
        {
            int value = block[i];
            ref ulong word = ref seen[value >> 6];
            ulong mask = 1UL << (value & 63);
            if ((word & mask) == 0)
            {
                word |= mask;
                if (++colorCount > threshold)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Finds the most frequent value of a block. The first value to reach the highest count is the dominant one.
    /// </summary>
    /// <remarks>
    /// Each update depends on the sample value, so the histogram is scalar, like <see cref="CountColorsWithThreshold"/>.
    /// </remarks>
    /// <param name="block">The 16x16 eight-bit block samples.</param>
    /// <returns>The dominant value.</returns>
    internal static byte FindDominantValue(ReadOnlySpan<byte> block)
    {
        Span<int> counts = stackalloc int[256];
        counts.Clear();
        int dominantCount = 0;
        byte dominant = 0;
        for (int i = 0; i < block.Length; i++)
        {
            byte value = block[i];
            if (++counts[value] > dominantCount)
            {
                dominant = value;
                dominantCount = counts[value];
            }
        }

        return dominant;
    }

    /// <summary>
    /// Grows the most frequent value of a block over its eight neighbors.
    /// </summary>
    /// <remarks>
    /// A sample takes the dominant value when it or any neighbor inside the block holds it. One 128-bit lane holds one
    /// block row of sixteen bytes. Thus byte shifts inside each lane give the horizontal neighbors, and the shifts never
    /// carry a neighbor across a row edge. The vertical neighbors come from the rows above and below in a copy of the
    /// marks with one clear row at each end.
    /// </remarks>
    /// <param name="block">The 16x16 eight-bit block samples.</param>
    /// <param name="dilated">The dilated block.</param>
    internal static void DilateBlock(ReadOnlySpan<byte> block, Span<byte> dilated)
    {
        const int length = DetectionBlockArea;
        const int row = DetectionBlockLength;
        byte dominant = FindDominantValue(block);
        Span<byte> spread = stackalloc byte[length + (2 * row)];
        spread[..row].Clear();
        spread[(length + row)..].Clear();
        ref byte blockBase = ref MemoryMarshal.GetReference(block);
        ref byte spreadBase = ref MemoryMarshal.GetReference(spread);
        ref byte dilatedBase = ref MemoryMarshal.GetReference(dilated);

        // The first pass marks each sample whose row holds the dominant value at it or beside it. The marks are 0xFF or 0
        // and start one row into the copy, so the clear rows border the block.
        int i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<byte> target = Vector512.Create(dominant);
            for (; i <= length - Vector512<byte>.Count; i += Vector512<byte>.Count)
            {
                Vector512<byte> equal = Vector512.Equals(Vector512.LoadUnsafe(ref blockBase, (nuint)i), target);
                (equal | Vector512_.ShiftLeftBytesInLane(equal, 1) | Vector512_.ShiftRightBytesInLane(equal, 1))
                    .StoreUnsafe(ref spreadBase, (nuint)(i + row));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<byte> target = Vector256.Create(dominant);
            for (; i <= length - Vector256<byte>.Count; i += Vector256<byte>.Count)
            {
                Vector256<byte> equal = Vector256.Equals(Vector256.LoadUnsafe(ref blockBase, (nuint)i), target);
                (equal | Vector256_.ShiftLeftBytesInLane(equal, 1) | Vector256_.ShiftRightBytesInLane(equal, 1))
                    .StoreUnsafe(ref spreadBase, (nuint)(i + row));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<byte> target = Vector128.Create(dominant);
            for (; i <= length - Vector128<byte>.Count; i += Vector128<byte>.Count)
            {
                Vector128<byte> equal = Vector128.Equals(Vector128.LoadUnsafe(ref blockBase, (nuint)i), target);
                (equal | Vector128_.ShiftLeftBytesInVector(equal, 1) | Vector128_.ShiftRightBytesInVector(equal, 1))
                    .StoreUnsafe(ref spreadBase, (nuint)(i + row));
            }
        }

        for (; i < length; i++)
        {
            int column = i & (row - 1);
            bool marked = block[i] == dominant ||
                (column > 0 && block[i - 1] == dominant) ||
                (column < row - 1 && block[i + 1] == dominant);

            spread[i + row] = marked ? byte.MaxValue : (byte)0;
        }

        // The second pass takes the dominant value wherever the mark above, the mark of the sample or the mark below is set.
        i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<byte> target = Vector512.Create(dominant);
            for (; i <= length - Vector512<byte>.Count; i += Vector512<byte>.Count)
            {
                Vector512<byte> marked = Vector512.LoadUnsafe(ref spreadBase, (nuint)i) |
                    Vector512.LoadUnsafe(ref spreadBase, (nuint)(i + row)) |
                    Vector512.LoadUnsafe(ref spreadBase, (nuint)(i + (2 * row)));

                Vector512.ConditionalSelect(marked, target, Vector512.LoadUnsafe(ref blockBase, (nuint)i))
                    .StoreUnsafe(ref dilatedBase, (nuint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<byte> target = Vector256.Create(dominant);
            for (; i <= length - Vector256<byte>.Count; i += Vector256<byte>.Count)
            {
                Vector256<byte> marked = Vector256.LoadUnsafe(ref spreadBase, (nuint)i) |
                    Vector256.LoadUnsafe(ref spreadBase, (nuint)(i + row)) |
                    Vector256.LoadUnsafe(ref spreadBase, (nuint)(i + (2 * row)));

                Vector256.ConditionalSelect(marked, target, Vector256.LoadUnsafe(ref blockBase, (nuint)i))
                    .StoreUnsafe(ref dilatedBase, (nuint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<byte> target = Vector128.Create(dominant);
            for (; i <= length - Vector128<byte>.Count; i += Vector128<byte>.Count)
            {
                Vector128<byte> marked = Vector128.LoadUnsafe(ref spreadBase, (nuint)i) |
                    Vector128.LoadUnsafe(ref spreadBase, (nuint)(i + row)) |
                    Vector128.LoadUnsafe(ref spreadBase, (nuint)(i + (2 * row)));

                Vector128.ConditionalSelect(marked, target, Vector128.LoadUnsafe(ref blockBase, (nuint)i))
                    .StoreUnsafe(ref dilatedBase, (nuint)i);
            }
        }

        for (; i < length; i++)
        {
            bool marked = (spread[i] | spread[i + row] | spread[i + (2 * row)]) != 0;
            dilated[i] = marked ? dominant : block[i];
        }
    }
}
