// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Computes the Sobel gradients and histogram bins of the intra-mode gradient histogram.
/// </summary>
internal static class Av1GradientHistogram
{
    /// <summary>
    /// The bin reported for a sample whose horizontal gradient is zero, which splits between the two end bins.
    /// </summary>
    public const int VerticalBin = -1;

    /// <summary>
    /// Gets the upper ratio of each histogram bin, in Q16. The last bin is unbounded.
    /// </summary>
    private static ReadOnlySpan<int> Thresholds =>
    [
        -1334015, -441798, -261605, -183158, -138560, -109331, -88359, -72303,
        -59392, -48579, -39272, -30982, -23445, -16400, -9715, -3194,
        3227, 9748, 16433, 23478, 31015, 39305, 48611, 59425,
        72336, 88392, 109364, 138593, 183191, 261638, 441831
    ];

    /// <summary>
    /// Computes the gradient magnitude and histogram bin of every interior sample of one row.
    /// </summary>
    /// <param name="block">The block samples, row by row.</param>
    /// <param name="stride">The block row stride, which is also its width.</param>
    /// <param name="row">The interior row, at least one and at most two below the block height.</param>
    /// <param name="magnitudes">Receives |dx| + |dy| of each of the width minus two interior samples.</param>
    /// <param name="bins">Receives the bin of each interior sample, or <see cref="VerticalBin"/> when dx is zero.</param>
    /// <param name="horizontal">Receives the horizontal gradient of each interior sample.</param>
    /// <param name="vertical">Receives the vertical gradient of each interior sample.</param>
    public static void ComputeRow(
        ReadOnlySpan<short> block,
        int stride,
        int row,
        Span<short> magnitudes,
        Span<int> bins,
        Span<short> horizontal,
        Span<short> vertical)
        => ComputeRow(
            block.Slice((row - 1) * stride, stride),
            block.Slice(row * stride, stride),
            block.Slice((row + 1) * stride, stride),
            stride,
            magnitudes,
            bins,
            horizontal,
            vertical);

    /// <summary>
    /// Computes the gradient magnitude and histogram bin of every interior sample of one row from the row and the rows above and below it.
    /// The three rows do not have to be adjacent in memory.
    /// </summary>
    /// <param name="aboveRow">The row above.</param>
    /// <param name="currentRow">The row whose interior samples are measured.</param>
    /// <param name="belowRow">The row below.</param>
    /// <param name="width">The number of samples in each row.</param>
    /// <param name="magnitudes">Receives |dx| + |dy| of each of the width minus two interior samples.</param>
    /// <param name="bins">Receives the bin of each interior sample, or <see cref="VerticalBin"/> when dx is zero.</param>
    /// <param name="horizontal">Receives the horizontal gradient of each interior sample.</param>
    /// <param name="vertical">Receives the vertical gradient of each interior sample.</param>
    public static void ComputeRow(
        ReadOnlySpan<short> aboveRow,
        ReadOnlySpan<short> currentRow,
        ReadOnlySpan<short> belowRow,
        int width,
        Span<short> magnitudes,
        Span<int> bins,
        Span<short> horizontal,
        Span<short> vertical)
    {
        int count = width - 2;
        ref short above = ref MemoryMarshal.GetReference(aboveRow);
        ref short current = ref MemoryMarshal.GetReference(currentRow);
        ref short below = ref MemoryMarshal.GetReference(belowRow);
        ref short magnitudeBase = ref MemoryMarshal.GetReference(magnitudes);
        ref short horizontalBase = ref MemoryMarshal.GetReference(horizontal);
        ref short verticalBase = ref MemoryMarshal.GetReference(vertical);

        // A Sobel sum is at most four 12-bit samples and a magnitude is two such sums, so both fit in 16-bit lanes.
        // Lane i holds interior column i + 1. Its left, center and right neighbors start at i, i + 1 and i + 2.
        int column = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; column <= count - Vector512<short>.Count; column += Vector512<short>.Count)
            {
                nuint i = (nuint)column;
                Vector512<short> dx = Vector512.LoadUnsafe(ref above, i + 2) + (Vector512.LoadUnsafe(ref current, i + 2) << 1) + Vector512.LoadUnsafe(ref below, i + 2)
                    - Vector512.LoadUnsafe(ref above, i) - (Vector512.LoadUnsafe(ref current, i) << 1) - Vector512.LoadUnsafe(ref below, i);

                Vector512<short> dy = Vector512.LoadUnsafe(ref below, i) + (Vector512.LoadUnsafe(ref below, i + 1) << 1) + Vector512.LoadUnsafe(ref below, i + 2)
                    - Vector512.LoadUnsafe(ref above, i) - (Vector512.LoadUnsafe(ref above, i + 1) << 1) - Vector512.LoadUnsafe(ref above, i + 2);

                dx.StoreUnsafe(ref horizontalBase, i);
                dy.StoreUnsafe(ref verticalBase, i);
                (Vector512.Abs(dx) + Vector512.Abs(dy)).StoreUnsafe(ref magnitudeBase, i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; column <= count - Vector256<short>.Count; column += Vector256<short>.Count)
            {
                nuint i = (nuint)column;
                Vector256<short> dx = Vector256.LoadUnsafe(ref above, i + 2) + (Vector256.LoadUnsafe(ref current, i + 2) << 1) + Vector256.LoadUnsafe(ref below, i + 2)
                    - Vector256.LoadUnsafe(ref above, i) - (Vector256.LoadUnsafe(ref current, i) << 1) - Vector256.LoadUnsafe(ref below, i);

                Vector256<short> dy = Vector256.LoadUnsafe(ref below, i) + (Vector256.LoadUnsafe(ref below, i + 1) << 1) + Vector256.LoadUnsafe(ref below, i + 2)
                    - Vector256.LoadUnsafe(ref above, i) - (Vector256.LoadUnsafe(ref above, i + 1) << 1) - Vector256.LoadUnsafe(ref above, i + 2);

                dx.StoreUnsafe(ref horizontalBase, i);
                dy.StoreUnsafe(ref verticalBase, i);
                (Vector256.Abs(dx) + Vector256.Abs(dy)).StoreUnsafe(ref magnitudeBase, i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; column <= count - Vector128<short>.Count; column += Vector128<short>.Count)
            {
                nuint i = (nuint)column;
                Vector128<short> dx = Vector128.LoadUnsafe(ref above, i + 2) + (Vector128.LoadUnsafe(ref current, i + 2) << 1) + Vector128.LoadUnsafe(ref below, i + 2)
                    - Vector128.LoadUnsafe(ref above, i) - (Vector128.LoadUnsafe(ref current, i) << 1) - Vector128.LoadUnsafe(ref below, i);

                Vector128<short> dy = Vector128.LoadUnsafe(ref below, i) + (Vector128.LoadUnsafe(ref below, i + 1) << 1) + Vector128.LoadUnsafe(ref below, i + 2)
                    - Vector128.LoadUnsafe(ref above, i) - (Vector128.LoadUnsafe(ref above, i + 1) << 1) - Vector128.LoadUnsafe(ref above, i + 2);

                dx.StoreUnsafe(ref horizontalBase, i);
                dy.StoreUnsafe(ref verticalBase, i);
                (Vector128.Abs(dx) + Vector128.Abs(dy)).StoreUnsafe(ref magnitudeBase, i);
            }
        }

        for (; column < count; column++)
        {
            int dx = Unsafe.Add(ref above, column + 2) + (2 * Unsafe.Add(ref current, column + 2)) + Unsafe.Add(ref below, column + 2)
                - Unsafe.Add(ref above, column) - (2 * Unsafe.Add(ref current, column)) - Unsafe.Add(ref below, column);

            int dy = Unsafe.Add(ref below, column) + (2 * Unsafe.Add(ref below, column + 1)) + Unsafe.Add(ref below, column + 2)
                - Unsafe.Add(ref above, column) - (2 * Unsafe.Add(ref above, column + 1)) - Unsafe.Add(ref above, column + 2);

            Unsafe.Add(ref horizontalBase, column) = (short)dx;
            Unsafe.Add(ref verticalBase, column) = (short)dy;
            Unsafe.Add(ref magnitudeBase, column) = (short)(Math.Abs(dx) + Math.Abs(dy));
        }

        ComputeBins(horizontal[..count], vertical[..count], bins[..count]);
    }

    /// <summary>
    /// Finds the histogram bin of each gradient: the first bin whose threshold is at least the Q16 ratio dy / dx.
    /// </summary>
    /// <remarks>
    /// The ratio is the truncated integer quotient (dy &lt;&lt; 16) / dx. Both operands are exact in a double.
    /// The divisor is below 2^15 and the quotient is below 2^31. As a result, the correctly rounded double quotient truncates to the integer quotient.
    /// The bin is then the number of thresholds below the ratio.
    /// </remarks>
    /// <param name="horizontal">The horizontal gradients.</param>
    /// <param name="vertical">The vertical gradients.</param>
    /// <param name="bins">Receives each bin, or <see cref="VerticalBin"/> when the horizontal gradient is zero.</param>
    private static void ComputeBins(ReadOnlySpan<short> horizontal, ReadOnlySpan<short> vertical, Span<int> bins)
    {
        ref short horizontalBase = ref MemoryMarshal.GetReference(horizontal);
        ref short verticalBase = ref MemoryMarshal.GetReference(vertical);
        ref int binBase = ref MemoryMarshal.GetReference(bins);
        ReadOnlySpan<int> thresholds = Thresholds;
        int count = bins.Length;
        int index = 0;

        // Each 64-bit lane holds one sample. GreaterThan sets a lane to -1 where the ratio is above the threshold, so subtracting
        // the mask counts the thresholds below the ratio. A zero dx gives an undefined ratio, and the select replaces that lane
        // with VerticalBin. Every bin fits in 32 bits, so the narrowing to int keeps the value.
        if (Vector512.IsHardwareAccelerated)
        {
            for (; index <= count - Vector512<long>.Count; index += Vector512<long>.Count)
            {
                Vector512<long> dx = WidenToInt64(Vector128.LoadUnsafe(ref horizontalBase, (nuint)index));
                Vector512<long> dy = WidenToInt64(Vector128.LoadUnsafe(ref verticalBase, (nuint)index));
                Vector512<long> ratio = Vector512.ConvertToInt64(Vector512.ConvertToDouble(dy << 16) / Vector512.ConvertToDouble(dx));
                Vector512<long> bin = Vector512<long>.Zero;
                foreach (int threshold in thresholds)
                {
                    bin -= Vector512.GreaterThan(ratio, Vector512.Create((long)threshold));
                }

                bin = Vector512.ConditionalSelect(Vector512.Equals(dx, Vector512<long>.Zero), Vector512.Create((long)VerticalBin), bin);
                Vector256.Narrow(bin.GetLower(), bin.GetUpper()).StoreUnsafe(ref binBase, (nuint)index);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; index <= count - Vector256<long>.Count; index += Vector256<long>.Count)
            {
                Vector256<long> dx = WidenToInt64(Unsafe.ReadUnaligned<long>(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref horizontalBase, index))));
                Vector256<long> dy = WidenToInt64(Unsafe.ReadUnaligned<long>(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref verticalBase, index))));
                Vector256<long> ratio = Vector256.ConvertToInt64(Vector256.ConvertToDouble(dy << 16) / Vector256.ConvertToDouble(dx));
                Vector256<long> bin = Vector256<long>.Zero;
                foreach (int threshold in thresholds)
                {
                    bin -= Vector256.GreaterThan(ratio, Vector256.Create((long)threshold));
                }

                bin = Vector256.ConditionalSelect(Vector256.Equals(dx, Vector256<long>.Zero), Vector256.Create((long)VerticalBin), bin);
                Vector128.Narrow(bin.GetLower(), bin.GetUpper()).StoreUnsafe(ref binBase, (nuint)index);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; index <= count - Vector128<long>.Count; index += Vector128<long>.Count)
            {
                Vector128<long> dx = Vector128.Create(Unsafe.Add(ref horizontalBase, index), (long)Unsafe.Add(ref horizontalBase, index + 1));
                Vector128<long> dy = Vector128.Create(Unsafe.Add(ref verticalBase, index), (long)Unsafe.Add(ref verticalBase, index + 1));
                Vector128<long> ratio = Vector128.ConvertToInt64(Vector128.ConvertToDouble(dy << 16) / Vector128.ConvertToDouble(dx));
                Vector128<long> bin = Vector128<long>.Zero;
                foreach (int threshold in thresholds)
                {
                    bin -= Vector128.GreaterThan(ratio, Vector128.Create((long)threshold));
                }

                bin = Vector128.ConditionalSelect(Vector128.Equals(dx, Vector128<long>.Zero), Vector128.Create((long)VerticalBin), bin);
                Unsafe.Add(ref binBase, index) = (int)bin.ToScalar();
                Unsafe.Add(ref binBase, index + 1) = (int)bin.GetElement(1);
            }
        }

        for (; index < count; index++)
        {
            int dx = Unsafe.Add(ref horizontalBase, index);
            if (dx == 0)
            {
                Unsafe.Add(ref binBase, index) = VerticalBin;
                continue;
            }

            int ratio = (Unsafe.Add(ref verticalBase, index) << 16) / dx;
            int bin = 0;
            foreach (int threshold in thresholds)
            {
                bin += ratio > threshold ? 1 : 0;
            }

            Unsafe.Add(ref binBase, index) = bin;
        }
    }

    /// <summary>
    /// Widens eight sixteen-bit values to 64 bits.
    /// </summary>
    /// <param name="value">The values.</param>
    /// <returns>The widened values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<long> WidenToInt64(Vector128<short> value)
    {
        Vector256<int> widened = Vector256_.Widen(value);
        (Vector256<long> lower, Vector256<long> upper) = Vector256.Widen(widened);
        return Vector512.Create(lower, upper);
    }

    /// <summary>
    /// Widens four sixteen-bit values, packed in one 64-bit value, to 64 bits.
    /// </summary>
    /// <param name="packed">The four values.</param>
    /// <returns>The widened values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<long> WidenToInt64(long packed)
    {
        Vector128<int> widened = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsInt16());
        (Vector128<long> lower, Vector128<long> upper) = Vector128.Widen(widened);
        return Vector256.Create(lower, upper);
    }
}
