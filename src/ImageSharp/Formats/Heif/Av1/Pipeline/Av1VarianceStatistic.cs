// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Measures how far a block departs from its own 3x3 Gaussian smoothing: sixteen times the sum of the squared
/// differences between each sample and its smoothed value. Samples beyond the block repeat its edge. Reference:
/// aom_calc_variance_stat() and aom_highbd_calc_variance_stat().
/// </summary>
internal static class Av1VarianceStatistic
{
    /// <summary>
    /// Defines the sample-width-specific loads of the measure across hardware widths.
    /// </summary>
    /// <typeparam name="TSample">The sample type.</typeparam>
    public interface IVarianceStatisticOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Converts one sample.
        /// </summary>
        /// <param name="sample">The sample.</param>
        /// <returns>The sample value.</returns>
        public static abstract int ToInt32(TSample sample);

        /// <summary>
        /// Loads four samples and widens them to 32-bit lanes.
        /// </summary>
        /// <param name="row">The first sample of the row.</param>
        /// <param name="offset">The column of the first loaded sample.</param>
        /// <param name="width">The overload-selection value.</param>
        /// <returns>The four samples in increasing column order.</returns>
        public static abstract Vector128<int> LoadWidened(ref TSample row, nuint offset, Vector128<int> width);

        /// <summary>
        /// Loads eight samples and widens them to 32-bit lanes.
        /// </summary>
        /// <param name="row">The first sample of the row.</param>
        /// <param name="offset">The column of the first loaded sample.</param>
        /// <param name="width">The overload-selection value.</param>
        /// <returns>The eight samples in increasing column order.</returns>
        public static abstract Vector256<int> LoadWidened(ref TSample row, nuint offset, Vector256<int> width);

        /// <summary>
        /// Loads sixteen samples and widens them to 32-bit lanes.
        /// </summary>
        /// <param name="row">The first sample of the row.</param>
        /// <param name="offset">The column of the first loaded sample.</param>
        /// <param name="width">The overload-selection value.</param>
        /// <returns>The sixteen samples in increasing column order.</returns>
        public static abstract Vector512<int> LoadWidened(ref TSample row, nuint offset, Vector512<int> width);
    }

    /// <summary>
    /// Measures one block.
    /// </summary>
    /// <typeparam name="TSample">The sample type.</typeparam>
    /// <typeparam name="TOperator">The sample loads.</typeparam>
    /// <param name="source">The samples, starting at the block origin.</param>
    /// <param name="stride">The row stride.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <returns>The measure.</returns>
    public static long Calculate<TSample, TOperator>(ReadOnlySpan<TSample> source, int stride, int width, int height)
        where TSample : unmanaged
        where TOperator : struct, IVarianceStatisticOperator<TSample>
    {
        ref TSample origin = ref MemoryMarshal.GetReference(source);
        long total = 0;
        for (int y = 0; y < height; y++)
        {
            ref TSample above = ref Unsafe.Add(ref origin, Math.Max(y - 1, 0) * stride);
            ref TSample current = ref Unsafe.Add(ref origin, y * stride);
            ref TSample below = ref Unsafe.Add(ref origin, Math.Min(y + 1, height - 1) * stride);

            // The first and last columns repeat their own sample beyond the edge, so they run in the scalar member.
            // The columns between read both neighbors in place. A squared difference is at most 4095 squared, so a
            // 32-bit lane holds the at most 32 vectors of a 128-sample row, and each row folds into the total.
            total += GetSquaredDifference<TSample, TOperator>(ref above, ref current, ref below, 0, width);
            int x = 1;
            int end = width - 1;
            if (Vector512.IsHardwareAccelerated)
            {
                Vector512<int> sum = Vector512<int>.Zero;
                for (; x + Vector512<int>.Count <= end; x += Vector512<int>.Count)
                {
                    Vector512<int> difference = GetDifference<TSample, TOperator>(ref above, ref current, ref below, (nuint)x, default(Vector512<int>));
                    sum += difference * difference;
                }

                total += Vector512.Sum(sum);
            }

            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<int> sum = Vector256<int>.Zero;
                for (; x + Vector256<int>.Count <= end; x += Vector256<int>.Count)
                {
                    Vector256<int> difference = GetDifference<TSample, TOperator>(ref above, ref current, ref below, (nuint)x, default(Vector256<int>));
                    sum += difference * difference;
                }

                total += Vector256.Sum(sum);
            }

            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<int> sum = Vector128<int>.Zero;
                for (; x + Vector128<int>.Count <= end; x += Vector128<int>.Count)
                {
                    Vector128<int> difference = GetDifference<TSample, TOperator>(ref above, ref current, ref below, (nuint)x, default(Vector128<int>));
                    sum += difference * difference;
                }

                total += Vector128.Sum(sum);
            }

            for (; x < end; x++)
            {
                total += GetSquaredDifference<TSample, TOperator>(ref above, ref current, ref below, x, width);
            }

            if (width > 1)
            {
                total += GetSquaredDifference<TSample, TOperator>(ref above, ref current, ref below, width - 1, width);
            }
        }

        return total << 4;
    }

    /// <summary>
    /// Measures one block that may extend past the visible frame, where the samples repeat the last visible row and
    /// column as the reference's extended source border does. The visible part measures as a block of its own,
    /// because its last row and column already see themselves beyond the edge. Every column past the edge repeats the
    /// last visible column, so it smooths vertically only. Every row past the edge repeats the last visible row, so
    /// it smooths horizontally only. A sample past both edges equals all of its neighbors and adds nothing.
    /// Reference: aom_calc_variance_stat() over a source extended by av1_copy_and_extend_frame().
    /// </summary>
    /// <typeparam name="TSample">The sample type.</typeparam>
    /// <typeparam name="TOperator">The sample loads.</typeparam>
    /// <param name="source">The samples, starting at the block origin.</param>
    /// <param name="stride">The row stride.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="visibleWidth">The number of columns inside the frame, at least one.</param>
    /// <param name="visibleHeight">The number of rows inside the frame, at least one.</param>
    /// <returns>The measure.</returns>
    public static long CalculateWithBorder<TSample, TOperator>(
        ReadOnlySpan<TSample> source,
        int stride,
        int width,
        int height,
        int visibleWidth,
        int visibleHeight)
        where TSample : unmanaged
        where TOperator : struct, IVarianceStatisticOperator<TSample>
    {
        long total = Calculate<TSample, TOperator>(source, stride, visibleWidth, visibleHeight);
        ref TSample origin = ref MemoryMarshal.GetReference(source);

        // Each column past the edge holds the last visible column, whose difference from the 1-2-1 vertical smoothing
        // of itself counts once per repeated column.
        int repeatedColumns = width - visibleWidth;
        if (repeatedColumns > 0)
        {
            ref TSample column = ref Unsafe.Add(ref origin, visibleWidth - 1);
            long columnTotal = 0;
            for (int y = 0; y < visibleHeight; y++)
            {
                int above = TOperator.ToInt32(Unsafe.Add(ref column, Math.Max(y - 1, 0) * stride));
                int center = TOperator.ToInt32(Unsafe.Add(ref column, y * stride));
                int below = TOperator.ToInt32(Unsafe.Add(ref column, Math.Min(y + 1, visibleHeight - 1) * stride));
                long difference = center - ((above + (center << 1) + below) >> 2);
                columnTotal += difference * difference;
            }

            total += (columnTotal * repeatedColumns) << 4;
        }

        // Each row past the edge holds the last visible row, whose difference from the 1-2-1 horizontal smoothing of
        // itself counts once per repeated row.
        int repeatedRows = height - visibleHeight;
        if (repeatedRows > 0)
        {
            ref TSample row = ref Unsafe.Add(ref origin, (visibleHeight - 1) * stride);
            long rowTotal = 0;
            for (int x = 0; x < visibleWidth; x++)
            {
                int left = TOperator.ToInt32(Unsafe.Add(ref row, Math.Max(x - 1, 0)));
                int center = TOperator.ToInt32(Unsafe.Add(ref row, x));
                int right = TOperator.ToInt32(Unsafe.Add(ref row, Math.Min(x + 1, visibleWidth - 1)));
                long difference = center - ((left + (center << 1) + right) >> 2);
                rowTotal += difference * difference;
            }

            total += (rowTotal * repeatedRows) << 4;
        }

        return total;
    }

    /// <summary>
    /// Returns the differences of four columns from their smoothed values. Every column has both neighbors inside
    /// the row.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> GetDifference<TSample, TOperator>(ref TSample above, ref TSample current, ref TSample below, nuint x, Vector128<int> width)
        where TSample : unmanaged
        where TOperator : struct, IVarianceStatisticOperator<TSample>
    {
        Vector128<int> center = TOperator.LoadWidened(ref current, x, width);
        Vector128<int> top = TOperator.LoadWidened(ref above, x - 1, width) + (TOperator.LoadWidened(ref above, x, width) << 1) + TOperator.LoadWidened(ref above, x + 1, width);
        Vector128<int> middle = TOperator.LoadWidened(ref current, x - 1, width) + (center << 1) + TOperator.LoadWidened(ref current, x + 1, width);
        Vector128<int> bottom = TOperator.LoadWidened(ref below, x - 1, width) + (TOperator.LoadWidened(ref below, x, width) << 1) + TOperator.LoadWidened(ref below, x + 1, width);
        return center - ((top + (middle << 1) + bottom) >> 4);
    }

    /// <summary>
    /// Returns the differences of eight columns from their smoothed values. Every column has both neighbors inside
    /// the row.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> GetDifference<TSample, TOperator>(ref TSample above, ref TSample current, ref TSample below, nuint x, Vector256<int> width)
        where TSample : unmanaged
        where TOperator : struct, IVarianceStatisticOperator<TSample>
    {
        Vector256<int> center = TOperator.LoadWidened(ref current, x, width);
        Vector256<int> top = TOperator.LoadWidened(ref above, x - 1, width) + (TOperator.LoadWidened(ref above, x, width) << 1) + TOperator.LoadWidened(ref above, x + 1, width);
        Vector256<int> middle = TOperator.LoadWidened(ref current, x - 1, width) + (center << 1) + TOperator.LoadWidened(ref current, x + 1, width);
        Vector256<int> bottom = TOperator.LoadWidened(ref below, x - 1, width) + (TOperator.LoadWidened(ref below, x, width) << 1) + TOperator.LoadWidened(ref below, x + 1, width);
        return center - ((top + (middle << 1) + bottom) >> 4);
    }

    /// <summary>
    /// Returns the differences of sixteen columns from their smoothed values. Every column has both neighbors
    /// inside the row.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> GetDifference<TSample, TOperator>(ref TSample above, ref TSample current, ref TSample below, nuint x, Vector512<int> width)
        where TSample : unmanaged
        where TOperator : struct, IVarianceStatisticOperator<TSample>
    {
        Vector512<int> center = TOperator.LoadWidened(ref current, x, width);
        Vector512<int> top = TOperator.LoadWidened(ref above, x - 1, width) + (TOperator.LoadWidened(ref above, x, width) << 1) + TOperator.LoadWidened(ref above, x + 1, width);
        Vector512<int> middle = TOperator.LoadWidened(ref current, x - 1, width) + (center << 1) + TOperator.LoadWidened(ref current, x + 1, width);
        Vector512<int> bottom = TOperator.LoadWidened(ref below, x - 1, width) + (TOperator.LoadWidened(ref below, x, width) << 1) + TOperator.LoadWidened(ref below, x + 1, width);
        return center - ((top + (middle << 1) + bottom) >> 4);
    }

    /// <summary>
    /// Returns the squared difference of one column from its smoothed value, repeating the edge sample beyond the
    /// row.
    /// </summary>
    private static long GetSquaredDifference<TSample, TOperator>(ref TSample above, ref TSample current, ref TSample below, int x, int width)
        where TSample : unmanaged
        where TOperator : struct, IVarianceStatisticOperator<TSample>
    {
        int left = Math.Max(x - 1, 0);
        int right = Math.Min(x + 1, width - 1);
        int top = TOperator.ToInt32(Unsafe.Add(ref above, left)) + (TOperator.ToInt32(Unsafe.Add(ref above, x)) << 1) + TOperator.ToInt32(Unsafe.Add(ref above, right));
        int center = TOperator.ToInt32(Unsafe.Add(ref current, x));
        int middle = TOperator.ToInt32(Unsafe.Add(ref current, left)) + (center << 1) + TOperator.ToInt32(Unsafe.Add(ref current, right));
        int bottom = TOperator.ToInt32(Unsafe.Add(ref below, left)) + (TOperator.ToInt32(Unsafe.Add(ref below, x)) << 1) + TOperator.ToInt32(Unsafe.Add(ref below, right));
        long difference = center - ((top + (middle << 1) + bottom) >> 4);
        return difference * difference;
    }

    /// <summary>
    /// Loads eight-bit samples.
    /// </summary>
    public readonly struct ByteOperator : IVarianceStatisticOperator<byte>
    {
        /// <inheritdoc/>
        public static int ToInt32(byte sample) => sample;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> LoadWidened(ref byte row, nuint offset, Vector128<int> width)
        {
            Vector128<byte> samples = Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref row, offset))).AsByte();
            return Vector128.WidenLower(Vector128.WidenLower(samples)).AsInt32();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> LoadWidened(ref byte row, nuint offset, Vector256<int> width)
            => Vector256.WidenLower(Vector128.WidenLower(Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref row, offset))).AsByte()).ToVector256Unsafe()).AsInt32();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> LoadWidened(ref byte row, nuint offset, Vector512<int> width)
            => Vector512.WidenLower(Vector256.WidenLower(Vector128.LoadUnsafe(ref row, offset).ToVector256Unsafe()).ToVector512Unsafe()).AsInt32();
    }

    /// <summary>
    /// Loads high bit depth samples.
    /// </summary>
    public readonly struct UInt16Operator : IVarianceStatisticOperator<ushort>
    {
        /// <inheritdoc/>
        public static int ToInt32(ushort sample) => sample;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> LoadWidened(ref ushort row, nuint offset, Vector128<int> width)
            => Vector128.WidenLower(Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref row, offset)))).AsUInt16()).AsInt32();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> LoadWidened(ref ushort row, nuint offset, Vector256<int> width)
            => Vector256.WidenLower(Vector128.LoadUnsafe(ref row, offset).ToVector256Unsafe()).AsInt32();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> LoadWidened(ref ushort row, nuint offset, Vector512<int> width)
            => Vector512.WidenLower(Vector256.LoadUnsafe(ref row, offset).ToVector512Unsafe()).AsInt32();
    }
}
