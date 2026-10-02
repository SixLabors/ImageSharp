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
/// <remarks>
/// The samples load through <see cref="Av1MotionVectorStatistics.ITextureOperator{TSample}"/>, which widens them to
/// sixteen-bit lanes. The weighted 3x3 sum is at most sixteen times 4095, or 65520, so it fits those lanes at every bit
/// depth. The difference from the smoothed value is signed, so it is formed after widening to 32-bit lanes.
/// </remarks>
internal static class Av1VarianceStatistic
{
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
        where TOperator : struct, Av1MotionVectorStatistics.ITextureOperator<TSample>
    {
        ref TSample origin = ref MemoryMarshal.GetReference(source);
        long total = 0;
        for (int y = 0; y < height; y++)
        {
            // The first and last rows repeat themselves beyond the block edge, so their neighbor rows clamp.
            ref TSample above = ref Unsafe.Add(ref origin, Math.Max(y - 1, 0) * stride);
            ref TSample current = ref Unsafe.Add(ref origin, y * stride);
            ref TSample below = ref Unsafe.Add(ref origin, Math.Min(y + 1, height - 1) * stride);

            // The first and last columns repeat their own sample beyond the edge, so they run in the scalar member.
            // The columns between read both neighbors in place: a vector at column x reads columns x - 1 through
            // x + Count, and the loop bound keeps x + Count at most the last column. A squared difference is at most
            // 4095 squared, and a 32-bit lane of a 128-sample row takes at most 32 of them, so the lanes fold into the
            // total once per row.
            total += GetSquaredDifference<TSample, TOperator>(ref above, ref current, ref below, 0, width);
            int x = 1;
            int end = width - 1;
            if (Vector512.IsHardwareAccelerated)
            {
                Vector512<int> sum = Vector512<int>.Zero;
                for (; x + Vector512<ushort>.Count <= end; x += Vector512<ushort>.Count)
                {
                    sum = AccumulateSquaredDifferences<TSample, TOperator>(ref above, ref current, ref below, (nuint)x, sum);
                }

                total += Vector512.Sum(sum);
            }

            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<int> sum = Vector256<int>.Zero;
                for (; x + Vector256<ushort>.Count <= end; x += Vector256<ushort>.Count)
                {
                    sum = AccumulateSquaredDifferences<TSample, TOperator>(ref above, ref current, ref below, (nuint)x, sum);
                }

                total += Vector256.Sum(sum);
            }

            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<int> sum = Vector128<int>.Zero;
                for (; x + Vector128<ushort>.Count <= end; x += Vector128<ushort>.Count)
                {
                    sum = AccumulateSquaredDifferences<TSample, TOperator>(ref above, ref current, ref below, (nuint)x, sum);
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
        where TOperator : struct, Av1MotionVectorStatistics.ITextureOperator<TSample>
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
                int above = TOperator.Load(ref column, (nuint)(Math.Max(y - 1, 0) * stride));
                int center = TOperator.Load(ref column, (nuint)(y * stride));
                int below = TOperator.Load(ref column, (nuint)(Math.Min(y + 1, visibleHeight - 1) * stride));
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
                int left = TOperator.Load(ref row, (nuint)Math.Max(x - 1, 0));
                int center = TOperator.Load(ref row, (nuint)x);
                int right = TOperator.Load(ref row, (nuint)Math.Min(x + 1, visibleWidth - 1));
                long difference = center - ((left + (center << 1) + right) >> 2);
                rowTotal += difference * difference;
            }

            total += (rowTotal * repeatedRows) << 4;
        }

        return total;
    }

    /// <summary>
    /// Adds the squared differences of eight columns from their smoothed values to the lane totals. Every column has
    /// both neighbors inside the row.
    /// </summary>
    /// <typeparam name="TSample">The sample type.</typeparam>
    /// <typeparam name="TOperator">The sample loads.</typeparam>
    /// <param name="above">The first sample of the row above.</param>
    /// <param name="current">The first sample of the row.</param>
    /// <param name="below">The first sample of the row below.</param>
    /// <param name="x">The first column.</param>
    /// <param name="sum">The lane totals so far.</param>
    /// <returns>The lane totals with the eight columns added.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> AccumulateSquaredDifferences<TSample, TOperator>(ref TSample above, ref TSample current, ref TSample below, nuint x, Vector128<int> sum)
        where TSample : unmanaged
        where TOperator : struct, Av1MotionVectorStatistics.ITextureOperator<TSample>
    {
        // Each row's 1-2-1 sum, then the rows' 1-2-1 sum, all in sixteen-bit lanes, and the shift that divides by 16.
        Vector128<ushort> lanes = default;
        Vector128<ushort> center = TOperator.Load(ref current, x, lanes);
        Vector128<ushort> top = TOperator.Load(ref above, x - 1, lanes) + (TOperator.Load(ref above, x, lanes) << 1) + TOperator.Load(ref above, x + 1, lanes);
        Vector128<ushort> middle = TOperator.Load(ref current, x - 1, lanes) + (center << 1) + TOperator.Load(ref current, x + 1, lanes);
        Vector128<ushort> bottom = TOperator.Load(ref below, x - 1, lanes) + (TOperator.Load(ref below, x, lanes) << 1) + TOperator.Load(ref below, x + 1, lanes);
        Vector128<ushort> smoothed = (top + (middle << 1) + bottom) >>> 4;

        // The signed differences of the lower and upper four columns, squared in 32-bit lanes.
        Vector128<int> lower = Vector128.WidenLower(center).AsInt32() - Vector128.WidenLower(smoothed).AsInt32();
        Vector128<int> upper = Vector128.WidenUpper(center).AsInt32() - Vector128.WidenUpper(smoothed).AsInt32();
        return sum + (lower * lower) + (upper * upper);
    }

    /// <summary>
    /// Adds the squared differences of sixteen columns from their smoothed values to the lane totals. Every column has
    /// both neighbors inside the row.
    /// </summary>
    /// <typeparam name="TSample">The sample type.</typeparam>
    /// <typeparam name="TOperator">The sample loads.</typeparam>
    /// <param name="above">The first sample of the row above.</param>
    /// <param name="current">The first sample of the row.</param>
    /// <param name="below">The first sample of the row below.</param>
    /// <param name="x">The first column.</param>
    /// <param name="sum">The lane totals so far.</param>
    /// <returns>The lane totals with the sixteen columns added.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> AccumulateSquaredDifferences<TSample, TOperator>(ref TSample above, ref TSample current, ref TSample below, nuint x, Vector256<int> sum)
        where TSample : unmanaged
        where TOperator : struct, Av1MotionVectorStatistics.ITextureOperator<TSample>
    {
        // Each row's 1-2-1 sum, then the rows' 1-2-1 sum, all in sixteen-bit lanes, and the shift that divides by 16.
        Vector256<ushort> lanes = default;
        Vector256<ushort> center = TOperator.Load(ref current, x, lanes);
        Vector256<ushort> top = TOperator.Load(ref above, x - 1, lanes) + (TOperator.Load(ref above, x, lanes) << 1) + TOperator.Load(ref above, x + 1, lanes);
        Vector256<ushort> middle = TOperator.Load(ref current, x - 1, lanes) + (center << 1) + TOperator.Load(ref current, x + 1, lanes);
        Vector256<ushort> bottom = TOperator.Load(ref below, x - 1, lanes) + (TOperator.Load(ref below, x, lanes) << 1) + TOperator.Load(ref below, x + 1, lanes);
        Vector256<ushort> smoothed = (top + (middle << 1) + bottom) >>> 4;

        // The signed differences of the lower and upper eight columns, squared in 32-bit lanes.
        Vector256<int> lower = Vector256.WidenLower(center).AsInt32() - Vector256.WidenLower(smoothed).AsInt32();
        Vector256<int> upper = Vector256.WidenUpper(center).AsInt32() - Vector256.WidenUpper(smoothed).AsInt32();
        return sum + (lower * lower) + (upper * upper);
    }

    /// <summary>
    /// Adds the squared differences of thirty-two columns from their smoothed values to the lane totals. Every column
    /// has both neighbors inside the row.
    /// </summary>
    /// <typeparam name="TSample">The sample type.</typeparam>
    /// <typeparam name="TOperator">The sample loads.</typeparam>
    /// <param name="above">The first sample of the row above.</param>
    /// <param name="current">The first sample of the row.</param>
    /// <param name="below">The first sample of the row below.</param>
    /// <param name="x">The first column.</param>
    /// <param name="sum">The lane totals so far.</param>
    /// <returns>The lane totals with the thirty-two columns added.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> AccumulateSquaredDifferences<TSample, TOperator>(ref TSample above, ref TSample current, ref TSample below, nuint x, Vector512<int> sum)
        where TSample : unmanaged
        where TOperator : struct, Av1MotionVectorStatistics.ITextureOperator<TSample>
    {
        // Each row's 1-2-1 sum, then the rows' 1-2-1 sum, all in sixteen-bit lanes, and the shift that divides by 16.
        Vector512<ushort> lanes = default;
        Vector512<ushort> center = TOperator.Load(ref current, x, lanes);
        Vector512<ushort> top = TOperator.Load(ref above, x - 1, lanes) + (TOperator.Load(ref above, x, lanes) << 1) + TOperator.Load(ref above, x + 1, lanes);
        Vector512<ushort> middle = TOperator.Load(ref current, x - 1, lanes) + (center << 1) + TOperator.Load(ref current, x + 1, lanes);
        Vector512<ushort> bottom = TOperator.Load(ref below, x - 1, lanes) + (TOperator.Load(ref below, x, lanes) << 1) + TOperator.Load(ref below, x + 1, lanes);
        Vector512<ushort> smoothed = (top + (middle << 1) + bottom) >>> 4;

        // The signed differences of the lower and upper sixteen columns, squared in 32-bit lanes.
        Vector512<int> lower = Vector512.WidenLower(center).AsInt32() - Vector512.WidenLower(smoothed).AsInt32();
        Vector512<int> upper = Vector512.WidenUpper(center).AsInt32() - Vector512.WidenUpper(smoothed).AsInt32();
        return sum + (lower * lower) + (upper * upper);
    }

    /// <summary>
    /// Returns the squared difference of one column from its smoothed value, repeating the edge sample beyond the
    /// row.
    /// </summary>
    /// <typeparam name="TSample">The sample type.</typeparam>
    /// <typeparam name="TOperator">The sample loads.</typeparam>
    /// <param name="above">The first sample of the row above.</param>
    /// <param name="current">The first sample of the row.</param>
    /// <param name="below">The first sample of the row below.</param>
    /// <param name="x">The column.</param>
    /// <param name="width">The row width.</param>
    /// <returns>The squared difference.</returns>
    private static long GetSquaredDifference<TSample, TOperator>(ref TSample above, ref TSample current, ref TSample below, int x, int width)
        where TSample : unmanaged
        where TOperator : struct, Av1MotionVectorStatistics.ITextureOperator<TSample>
    {
        // The neighbor columns clamp to the row, which repeats the edge sample as the reference's padded copy does.
        nuint left = (nuint)Math.Max(x - 1, 0);
        nuint column = (nuint)x;
        nuint right = (nuint)Math.Min(x + 1, width - 1);
        int top = TOperator.Load(ref above, left) + (TOperator.Load(ref above, column) << 1) + TOperator.Load(ref above, right);
        int center = TOperator.Load(ref current, column);
        int middle = TOperator.Load(ref current, left) + (center << 1) + TOperator.Load(ref current, right);
        int bottom = TOperator.Load(ref below, left) + (TOperator.Load(ref below, column) << 1) + TOperator.Load(ref below, right);
        long difference = center - ((top + (middle << 1) + bottom) >> 4);
        return difference * difference;
    }
}
