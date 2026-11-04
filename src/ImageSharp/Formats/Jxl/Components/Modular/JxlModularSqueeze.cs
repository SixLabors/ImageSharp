// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Modular;

/// <summary>
/// Reconstructs integer sample pairs from modular averages and squeeze residuals.
/// </summary>
/// <remarks>
/// The decoder owns the three regions and validates their dimensions. Destination must not overlap either input.
/// Horizontal reconstruction depends on the preceding output pair. Vertical reconstruction has the same dependency between rows.
/// </remarks>
internal static class JxlModularSqueeze
{
    // Eight average columns and their lookahead fit sixteen independent rows in the existing inline-array types.
    private const int TileColumns = 8;

    /// <summary>
    /// Reverses one horizontal or vertical squeeze without allocating sample storage.
    /// </summary>
    /// <param name="averages">The ceiling-sized half of each sample pair.</param>
    /// <param name="residuals">The floor-sized residual plane.</param>
    /// <param name="destination">The complete reconstructed plane.</param>
    /// <param name="horizontal">Whether pairs occupy adjacent columns instead of adjacent rows.</param>
    /// <param name="cancellationToken">The token checked before each row or row batch.</param>
    public static void Apply(
        Buffer2DRegion<int> averages,
        Buffer2DRegion<int> residuals,
        Buffer2DRegion<int> destination,
        bool horizontal,
        CancellationToken cancellationToken)
        => Apply<int>(averages, residuals, destination, horizontal, cancellationToken);

    /// <summary>
    /// Reverses a squeeze using the common signed storage selected for its channels.
    /// </summary>
    /// <param name="averages">The ceiling-sized average channel.</param>
    /// <param name="residuals">The floor-sized residual channel.</param>
    /// <param name="destination">The separate complete output channel with the same storage width.</param>
    /// <param name="horizontal">Whether pairs occupy adjacent columns instead of adjacent rows.</param>
    /// <param name="cancellationToken">The token checked before each row or row batch.</param>
    public static void Apply(
        JxlModularChannel averages,
        JxlModularChannel residuals,
        JxlModularChannel destination,
        bool horizontal,
        CancellationToken cancellationToken)
    {
        // The transform owner allocates outputs in the same storage representation as their inputs.
        // Select that representation once; arithmetic and tiling remain shared by short and int samples.
        if (averages.Uses16BitStorage)
        {
            Apply<short>(averages.Region16, residuals.Region16, destination.Region16, horizontal, cancellationToken);
        }
        else
        {
            Apply<int>(averages.Region, residuals.Region, destination.Region, horizontal, cancellationToken);
        }
    }

    /// <summary>
    /// Reverses one squeeze with wide arithmetic and the stream's selected signed storage.
    /// </summary>
    /// <typeparam name="TSample">The selected short or int storage carrier.</typeparam>
    /// <param name="averages">The ceiling-sized average plane.</param>
    /// <param name="residuals">The floor-sized residual plane.</param>
    /// <param name="destination">The separate complete output plane.</param>
    /// <param name="horizontal">Whether pairs occupy adjacent columns instead of adjacent rows.</param>
    /// <param name="cancellationToken">The token checked before each row or row batch.</param>
    public static void Apply<TSample>(
        Buffer2DRegion<TSample> averages,
        Buffer2DRegion<TSample> residuals,
        Buffer2DRegion<TSample> destination,
        bool horizontal,
        CancellationToken cancellationToken)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        // Squeezing a one-sample dimension produces only an average. The empty residual channel has no backing buffer.
        // Copy the unpaired samples without borrowing a row from that empty channel.
        if (residuals.Width == 0 || residuals.Height == 0)
        {
            for (int y = 0; y < destination.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                averages.DangerousGetRowSpan(y).CopyTo(destination.DangerousGetRowSpan(y));
            }

            return;
        }

        if (horizontal)
        {
            ApplyHorizontal(averages, residuals, destination, cancellationToken);
        }
        else
        {
            ApplyVertical(averages, residuals, destination, cancellationToken);
        }
    }

    /// <summary>
    /// Reconstructs vertical pairs with independent columns in SIMD lanes.
    /// </summary>
    /// <typeparam name="TSample">The selected short or int storage carrier.</typeparam>
    /// <param name="averages">The average plane.</param>
    /// <param name="residuals">The residual plane.</param>
    /// <param name="destination">The nonoverlapping output plane.</param>
    /// <param name="cancellationToken">The token checked before each reconstructed row pair.</param>
    private static void ApplyVertical<TSample>(
        Buffer2DRegion<TSample> averages,
        Buffer2DRegion<TSample> residuals,
        Buffer2DRegion<TSample> destination,
        CancellationToken cancellationToken)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        for (int y = 0; y < residuals.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Span<TSample> average = averages.DangerousGetRowSpan(y);
            Span<TSample> next = averages.DangerousGetRowSpan(Math.Min(y + 1, averages.Height - 1));
            Span<TSample> residual = residuals.DangerousGetRowSpan(y);
            Span<TSample> first = destination.DangerousGetRowSpan(2 * y);
            Span<TSample> second = destination.DangerousGetRowSpan((2 * y) + 1);
            Span<TSample> previous = y == 0 ? average : destination.DangerousGetRowSpan((2 * y) - 1);
            int x = 0;

            // A lane follows one column. Every vector reads the preceding reconstructed row, not an earlier lane.
            // Unaligned loads accept arbitrary frame-group origins. Descending widths leave only the scalar tail.
            if (Vector512.IsHardwareAccelerated)
            {
                nuint count = (average.Length - x).Vector512Count<int>();
                for (nuint block = 0; block < count; block++, x += Vector512<int>.Count)
                {
                    SqueezeOperator.Apply(
                        JxlModularChannel.Load512<TSample>(average, x),
                        JxlModularChannel.Load512<TSample>(residual, x),
                        JxlModularChannel.Load512<TSample>(next, x),
                        JxlModularChannel.Load512<TSample>(previous, x),
                        out Vector512<int> a,
                        out Vector512<int> b);

                    JxlModularChannel.Store512(first, x, a);
                    JxlModularChannel.Store512(second, x, b);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint count = (average.Length - x).Vector256Count<int>();
                for (nuint block = 0; block < count; block++, x += Vector256<int>.Count)
                {
                    SqueezeOperator.Apply(
                        JxlModularChannel.Load256<TSample>(average, x),
                        JxlModularChannel.Load256<TSample>(residual, x),
                        JxlModularChannel.Load256<TSample>(next, x),
                        JxlModularChannel.Load256<TSample>(previous, x),
                        out Vector256<int> a,
                        out Vector256<int> b);

                    JxlModularChannel.Store256(first, x, a);
                    JxlModularChannel.Store256(second, x, b);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint count = (average.Length - x).Vector128Count<int>();
                for (nuint block = 0; block < count; block++, x += Vector128<int>.Count)
                {
                    SqueezeOperator.Apply(
                        JxlModularChannel.Load128<TSample>(average, x),
                        JxlModularChannel.Load128<TSample>(residual, x),
                        JxlModularChannel.Load128<TSample>(next, x),
                        JxlModularChannel.Load128<TSample>(previous, x),
                        out Vector128<int> a,
                        out Vector128<int> b);

                    JxlModularChannel.Store128(first, x, a);
                    JxlModularChannel.Store128(second, x, b);
                }
            }

            for (; x < average.Length; x++)
            {
                SqueezeOperator.Apply(
                    int.CreateTruncating(average[x]),
                    int.CreateTruncating(residual[x]),
                    int.CreateTruncating(next[x]),
                    int.CreateTruncating(previous[x]),
                    out int a,
                    out int b);

                first[x] = TSample.CreateTruncating(a);
                second[x] = TSample.CreateTruncating(b);
            }
        }

        // An odd height leaves one unpaired average. No residual or tendency applies to that sample.
        if ((destination.Height & 1) != 0)
        {
            averages.DangerousGetRowSpan(averages.Height - 1).CopyTo(destination.DangerousGetRowSpan(destination.Height - 1));
        }
    }

    /// <summary>
    /// Reconstructs horizontal pairs with independent rows in SIMD lanes.
    /// </summary>
    /// <typeparam name="TSample">The selected short or int storage carrier.</typeparam>
    /// <param name="averages">The average plane.</param>
    /// <param name="residuals">The residual plane.</param>
    /// <param name="destination">The nonoverlapping output plane.</param>
    /// <param name="cancellationToken">The token checked before each reconstructed row batch.</param>
    private static void ApplyHorizontal<TSample>(
        Buffer2DRegion<TSample> averages,
        Buffer2DRegion<TSample> residuals,
        Buffer2DRegion<TSample> destination,
        CancellationToken cancellationToken)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        // Rows can belong to separate allocator segments. Stage only a bounded tile, then use the shared transpose kernel.
        // These four inline buffers occupy 4 KiB on the stack. They are reused across the entire plane.
        InlineArray256<int> tileStorage = default;
        InlineArray256<int> averageStorage = default;
        InlineArray256<int> residualStorage = default;
        InlineArray256<int> outputStorage = default;
        InlineArray16<int> previousStorage = default;
        Span<int> tile = tileStorage;
        Span<int> averageTile = averageStorage;
        Span<int> residualTile = residualStorage;
        Span<int> outputTile = outputStorage;
        Span<int> previous = previousStorage;
        int y = 0;

        while (y < averages.Height)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int remaining = averages.Height - y;
            int rows = Vector512.IsHardwareAccelerated && remaining >= Vector512<int>.Count ? Vector512<int>.Count
                : Vector256.IsHardwareAccelerated && remaining >= Vector256<int>.Count ? Vector256<int>.Count
                : Vector128.IsHardwareAccelerated && remaining >= Vector128<int>.Count ? Vector128<int>.Count : 1;

            if (rows == 1)
            {
                Span<TSample> average = averages.DangerousGetRowSpan(y);
                Span<TSample> residual = residuals.DangerousGetRowSpan(y);
                Span<TSample> output = destination.DangerousGetRowSpan(y);
                int left = int.CreateTruncating(average[0]);

                for (int x = 0; x < residual.Length; x++)
                {
                    SqueezeOperator.Apply(
                        int.CreateTruncating(average[x]),
                        int.CreateTruncating(residual[x]),
                        int.CreateTruncating(average[Math.Min(x + 1, average.Length - 1)]),
                        left,
                        out int a,
                        out int b);

                    output[2 * x] = TSample.CreateTruncating(a);
                    output[(2 * x) + 1] = TSample.CreateTruncating(b);

                    // Retain the stored short value for the next pair without reading the output row again.
                    left = typeof(TSample) == typeof(short) ? (short)b : b;
                }

                if ((destination.Width & 1) != 0)
                {
                    output[^1] = average[^1];
                }

                y++;
                continue;
            }

            for (int row = 0; row < rows; row++)
            {
                previous[row] = int.CreateTruncating(averages.DangerousGetRowSpan(y + row)[0]);
            }

            for (int x = 0; x < residuals.Width; x += TileColumns)
            {
                int columns = Math.Min(TileColumns, residuals.Width - x);
                int averageColumns = columns + 1;

                // Include one lookahead average. The last pair repeats its average when the source width is even.
                for (int row = 0; row < rows; row++)
                {
                    Span<TSample> average = averages.DangerousGetRowSpan(y + row);
                    Span<int> target = tile.Slice(row * averageColumns, averageColumns);
                    CopyToInt32<TSample>(average.Slice(x, columns), target);
                    target[columns] = int.CreateTruncating(average[Math.Min(x + columns, average.Length - 1)]);
                }

                SimdUtils.Transpose(tile, averageColumns, averageTile, rows, rows, averageColumns);

                for (int row = 0; row < rows; row++)
                {
                    CopyToInt32<TSample>(residuals.DangerousGetRowSpan(y + row).Slice(x, columns), tile.Slice(row * columns, columns));
                }

                SimdUtils.Transpose(tile, columns, residualTile, rows, rows, columns);

                // Each transposed column now holds one sample from each independent row. Keep the last odd output
                // across tiles because the next column's tendency depends on it. Dispatch once for the complete tile.
                if (rows == Vector512<int>.Count)
                {
                    ReconstructColumns512<TSample>(averageTile, residualTile, outputTile, previous, columns);
                }
                else if (rows == Vector256<int>.Count)
                {
                    ReconstructColumns256<TSample>(averageTile, residualTile, outputTile, previous, columns);
                }
                else
                {
                    ReconstructColumns128<TSample>(averageTile, residualTile, outputTile, previous, columns);
                }

                SimdUtils.Transpose(outputTile, rows, tile, 2 * columns, 2 * columns, rows);

                for (int row = 0; row < rows; row++)
                {
                    CopyFromInt32<TSample>(tile.Slice(row * 2 * columns, 2 * columns), destination.DangerousGetRowSpan(y + row).Slice(2 * x));
                }
            }

            if ((destination.Width & 1) != 0)
            {
                for (int row = 0; row < rows; row++)
                {
                    destination.DangerousGetRowSpan(y + row)[destination.Width - 1] = averages.DangerousGetRowSpan(y + row)[averages.Width - 1];
                }
            }

            y += rows;
        }
    }

    /// <summary>
    /// Restores dependent columns for 4 independent rows and retains the final odd outputs for the next tile.
    /// </summary>
    /// <typeparam name="TSample">The selected short or int storage carrier.</typeparam>
    /// <param name="averages">Column-major average lanes, including one lookahead column.</param>
    /// <param name="residuals">Column-major residual lanes.</param>
    /// <param name="output">The alternating first and second output columns.</param>
    /// <param name="previous">The preceding odd output from each row.</param>
    /// <param name="columns">The number of pairs in the tile.</param>
    private static void ReconstructColumns128<TSample>(
        ReadOnlySpan<int> averages,
        ReadOnlySpan<int> residuals,
        Span<int> output,
        Span<int> previous,
        int columns)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        ref int averageRef = ref MemoryMarshal.GetReference(averages);
        ref int residualRef = ref MemoryMarshal.GetReference(residuals);
        ref int outputRef = ref MemoryMarshal.GetReference(output);
        ref int previousRef = ref MemoryMarshal.GetReference(previous);
        Vector128<int> left = Vector128.LoadUnsafe(ref previousRef);

        for (int x = 0; x < columns; x++)
        {
            nuint offset = (nuint)(x * Vector128<int>.Count);
            SqueezeOperator.Apply(
                Vector128.LoadUnsafe(ref averageRef, offset),
                Vector128.LoadUnsafe(ref residualRef, offset),
                Vector128.LoadUnsafe(ref averageRef, offset + (nuint)Vector128<int>.Count),
                left,
                out Vector128<int> a,
                out Vector128<int> b);

            // The following transpose restores rows. Keep pair members in separate contiguous column vectors here.
            a.StoreUnsafe(ref outputRef, 2 * offset);
            b.StoreUnsafe(ref outputRef, (2 * offset) + (nuint)Vector128<int>.Count);

            // The next pair sees the value stored in the short plane, not an unbounded int temporary.
            left = typeof(TSample) == typeof(short) ? (b << 16) >> 16 : b;
        }

        left.StoreUnsafe(ref previousRef);
    }

    /// <summary>
    /// Restores dependent columns for 8 independent rows and retains the final odd outputs for the next tile.
    /// </summary>
    /// <typeparam name="TSample">The selected short or int storage carrier.</typeparam>
    /// <param name="averages">Column-major average lanes, including one lookahead column.</param>
    /// <param name="residuals">Column-major residual lanes.</param>
    /// <param name="output">The alternating first and second output columns.</param>
    /// <param name="previous">The preceding odd output from each row.</param>
    /// <param name="columns">The number of pairs in the tile.</param>
    private static void ReconstructColumns256<TSample>(
        ReadOnlySpan<int> averages,
        ReadOnlySpan<int> residuals,
        Span<int> output,
        Span<int> previous,
        int columns)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        ref int averageRef = ref MemoryMarshal.GetReference(averages);
        ref int residualRef = ref MemoryMarshal.GetReference(residuals);
        ref int outputRef = ref MemoryMarshal.GetReference(output);
        ref int previousRef = ref MemoryMarshal.GetReference(previous);
        Vector256<int> left = Vector256.LoadUnsafe(ref previousRef);

        for (int x = 0; x < columns; x++)
        {
            nuint offset = (nuint)(x * Vector256<int>.Count);
            SqueezeOperator.Apply(
                Vector256.LoadUnsafe(ref averageRef, offset),
                Vector256.LoadUnsafe(ref residualRef, offset),
                Vector256.LoadUnsafe(ref averageRef, offset + (nuint)Vector256<int>.Count),
                left,
                out Vector256<int> a,
                out Vector256<int> b);

            // The following transpose restores rows. Keep pair members in separate contiguous column vectors here.
            a.StoreUnsafe(ref outputRef, 2 * offset);
            b.StoreUnsafe(ref outputRef, (2 * offset) + (nuint)Vector256<int>.Count);

            // The next pair sees the value stored in the short plane, not an unbounded int temporary.
            left = typeof(TSample) == typeof(short) ? (b << 16) >> 16 : b;
        }

        left.StoreUnsafe(ref previousRef);
    }

    /// <summary>
    /// Restores dependent columns for 16 independent rows and retains the final odd outputs for the next tile.
    /// </summary>
    /// <typeparam name="TSample">The selected short or int storage carrier.</typeparam>
    /// <param name="averages">Column-major average lanes, including one lookahead column.</param>
    /// <param name="residuals">Column-major residual lanes.</param>
    /// <param name="output">The alternating first and second output columns.</param>
    /// <param name="previous">The preceding odd output from each row.</param>
    /// <param name="columns">The number of pairs in the tile.</param>
    private static void ReconstructColumns512<TSample>(
        ReadOnlySpan<int> averages,
        ReadOnlySpan<int> residuals,
        Span<int> output,
        Span<int> previous,
        int columns)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        ref int averageRef = ref MemoryMarshal.GetReference(averages);
        ref int residualRef = ref MemoryMarshal.GetReference(residuals);
        ref int outputRef = ref MemoryMarshal.GetReference(output);
        ref int previousRef = ref MemoryMarshal.GetReference(previous);
        Vector512<int> left = Vector512.LoadUnsafe(ref previousRef);

        for (int x = 0; x < columns; x++)
        {
            nuint offset = (nuint)(x * Vector512<int>.Count);
            SqueezeOperator.Apply(
                Vector512.LoadUnsafe(ref averageRef, offset),
                Vector512.LoadUnsafe(ref residualRef, offset),
                Vector512.LoadUnsafe(ref averageRef, offset + (nuint)Vector512<int>.Count),
                left,
                out Vector512<int> a,
                out Vector512<int> b);

            // The following transpose restores rows. Keep pair members in separate contiguous column vectors here.
            a.StoreUnsafe(ref outputRef, 2 * offset);
            b.StoreUnsafe(ref outputRef, (2 * offset) + (nuint)Vector512<int>.Count);

            // The next pair sees the value stored in the short plane, not an unbounded int temporary.
            left = typeof(TSample) == typeof(short) ? (b << 16) >> 16 : b;
        }

        left.StoreUnsafe(ref previousRef);
    }

    /// <summary>
    /// Widens one bounded input tile row into the existing integer transpose storage.
    /// </summary>
    /// <typeparam name="TSample">The selected short or int storage carrier.</typeparam>
    /// <param name="source">At most eight samples from one stored input row.</param>
    /// <param name="destination">The corresponding integer tile slice, which can include a following lookahead slot.</param>
    private static void CopyToInt32<TSample>(ReadOnlySpan<TSample> source, Span<int> destination)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        if (typeof(TSample) == typeof(int))
        {
            MemoryMarshal.Cast<TSample, int>(source).CopyTo(destination);
            return;
        }

        // Input tiles contain at most eight columns. One 256-bit int result widens the whole ordinary tile;
        // 128-bit and scalar tails handle partial tiles without reading an unpopulated lookahead sample.
        ref int target = ref MemoryMarshal.GetReference(destination);
        int x = 0;
        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (source.Length - x).Vector256Count<int>();
            for (nuint block = 0; block < count; block++, x += Vector256<int>.Count)
            {
                JxlModularChannel.Load256(source, x).StoreUnsafe(ref target, (nuint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (source.Length - x).Vector128Count<int>();
            for (nuint block = 0; block < count; block++, x += Vector128<int>.Count)
            {
                JxlModularChannel.Load128(source, x).StoreUnsafe(ref target, (nuint)x);
            }
        }

        for (; x < source.Length; x++)
        {
            destination[x] = int.CreateTruncating(source[x]);
        }
    }

    /// <summary>
    /// Stores one reconstructed tile row in its selected integer representation.
    /// </summary>
    /// <typeparam name="TSample">The selected short or int storage carrier.</typeparam>
    /// <param name="source">At most sixteen widened reconstructed samples.</param>
    /// <param name="destination">The corresponding stored output slice.</param>
    private static void CopyFromInt32<TSample>(ReadOnlySpan<int> source, Span<TSample> destination)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        if (typeof(TSample) == typeof(int))
        {
            source.CopyTo(MemoryMarshal.Cast<TSample, int>(destination));
            return;
        }

        // Short stores retain the low sixteen bits. This is storage wrapping, not saturation.
        // The selected width writes only the samples in this tile; partial image edges need no padding.
        ref int first = ref MemoryMarshal.GetReference(source);
        int x = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (source.Length - x).Vector512Count<int>();
            for (nuint block = 0; block < count; block++, x += Vector512<int>.Count)
            {
                JxlModularChannel.Store512(destination, x, Vector512.LoadUnsafe(ref first, (nuint)x));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (source.Length - x).Vector256Count<int>();
            for (nuint block = 0; block < count; block++, x += Vector256<int>.Count)
            {
                JxlModularChannel.Store256(destination, x, Vector256.LoadUnsafe(ref first, (nuint)x));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (source.Length - x).Vector128Count<int>();
            for (nuint block = 0; block < count; block++, x += Vector128<int>.Count)
            {
                JxlModularChannel.Store128(destination, x, Vector128.LoadUnsafe(ref first, (nuint)x));
            }
        }

        for (; x < source.Length; x++)
        {
            destination[x] = TSample.CreateTruncating(source[x]);
        }
    }

    /// <summary>
    /// Defines reversible pair arithmetic independently of horizontal or vertical traversal.
    /// </summary>
    private readonly struct SqueezeOperator
    {
        // For magnitudes at most UInt32.MaxValue, (magnitude * 0xAAAAAAAB) >> 33 equals integer division by three.
        // The unsigned product fits in 64 bits. This avoids lane-wise integer division in every SIMD width.
        private const ulong DivideByThreeMultiplier = 0xAAAAAAAB;
        private const int DivideByThreeShift = 33;

        /// <summary>
        /// Restores one sample pair using wide intermediate arithmetic and division toward zero.
        /// </summary>
        /// <param name="average">The pair average.</param>
        /// <param name="residual">The difference with its smooth tendency removed.</param>
        /// <param name="next">The following pair average, or the current average at the edge.</param>
        /// <param name="previous">The preceding odd sample, or the current average at the edge.</param>
        /// <param name="first">The reconstructed even sample.</param>
        /// <param name="second">The reconstructed odd sample.</param>
        public static void Apply(int average, int residual, int next, int previous, out int first, out int second)
        {
            long before = (long)previous - average;
            long after = (long)average - next;
            long tendency = 0;

            // A tendency is meaningful only along a monotonic sequence. Limit its parity-adjusted magnitude so
            // zero residual does not introduce a new extremum between the preceding sample and next average.
            if ((before >= 0 && after >= 0) || (before <= 0 && after <= 0))
            {
                long magnitude = (2 + Math.Abs((long)previous - next) + (Math.Abs(before) / 3)) >> 2;
                long beforeLimit = 2 * Math.Abs(before);
                long afterLimit = 2 * Math.Abs(after);
                magnitude = magnitude > beforeLimit + (magnitude & 1) ? beforeLimit + 1 : magnitude;
                magnitude = magnitude + (magnitude & 1) > afterLimit ? afterLimit : magnitude;
                tendency = previous < next ? -magnitude : magnitude;
            }

            long difference = residual + tendency;
            first = (int)(average + (difference / 2));
            second = (int)(average + (difference / 2) - difference);
        }

        /// <summary>
        /// Restores 4 independent pairs. Widen before subtraction so extreme stored samples match the scalar arithmetic.
        /// </summary>
        /// <param name="average">The pair-average lanes.</param>
        /// <param name="residual">The residual lanes.</param>
        /// <param name="next">The following-average lanes.</param>
        /// <param name="previous">The preceding-odd-sample lanes.</param>
        /// <param name="first">The reconstructed even lanes.</param>
        /// <param name="second">The reconstructed odd lanes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(
            Vector128<int> average,
            Vector128<int> residual,
            Vector128<int> next,
            Vector128<int> previous,
            out Vector128<int> first,
            out Vector128<int> second)
        {
            ApplyWide(
                Vector128.WidenLower(average),
                Vector128.WidenLower(residual),
                Vector128.WidenLower(next),
                Vector128.WidenLower(previous),
                out Vector128<long> a0,
                out Vector128<long> b0);

            ApplyWide(
                Vector128.WidenUpper(average),
                Vector128.WidenUpper(residual),
                Vector128.WidenUpper(next),
                Vector128.WidenUpper(previous),
                out Vector128<long> a1,
                out Vector128<long> b1);

            first = Vector128.Narrow(a0, a1);
            second = Vector128.Narrow(b0, b1);
        }

        /// <summary>
        /// Evaluates tendency and signed half-differences in wide lanes without intermediate overflow.
        /// </summary>
        /// <param name="average">The widened average lanes.</param>
        /// <param name="residual">The widened residual lanes.</param>
        /// <param name="next">The widened following-average lanes.</param>
        /// <param name="previous">The widened preceding-sample lanes.</param>
        /// <param name="first">The wide even outputs, narrowed only after the complete calculation.</param>
        /// <param name="second">The wide odd outputs.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyWide(
            Vector128<long> average,
            Vector128<long> residual,
            Vector128<long> next,
            Vector128<long> previous,
            out Vector128<long> first,
            out Vector128<long> second)
        {
            Vector128<long> before = previous - average;
            Vector128<long> after = average - next;
            Vector128<long> beforeMagnitude = Vector128.Abs(before);
            Vector128<long> afterMagnitude = Vector128.Abs(after);
            Vector128<long> one = Vector128.Create(1L);
            Vector128<long> third = ((beforeMagnitude.AsUInt64() * Vector128.Create(DivideByThreeMultiplier)) >>> DivideByThreeShift).AsInt64();
            Vector128<long> magnitude = (Vector128.Create(2L) + Vector128.Abs(previous - next) + third) >> 2;
            Vector128<long> beforeLimit = beforeMagnitude << 1;
            Vector128<long> afterLimit = afterMagnitude << 1;
            magnitude = Vector128.ConditionalSelect(Vector128.GreaterThan(magnitude, beforeLimit + (magnitude & one)), beforeLimit + one, magnitude);
            magnitude = Vector128.ConditionalSelect(Vector128.GreaterThan(magnitude + (magnitude & one), afterLimit), afterLimit, magnitude);

            // Equal adjacent values remain monotonic with either sign. Their tendency can be one unit because
            // the limits account for odd pair differences; testing only the XOR sign would discard that case.
            Vector128<long> increasing = Vector128.LessThanOrEqual(before, Vector128<long>.Zero)
                & Vector128.LessThanOrEqual(after, Vector128<long>.Zero);
            Vector128<long> decreasing = Vector128.GreaterThanOrEqual(before, Vector128<long>.Zero)
                & Vector128.GreaterThanOrEqual(after, Vector128<long>.Zero);
            Vector128<long> monotonic = increasing | decreasing;
            Vector128<long> tendency = Vector128.ConditionalSelect(Vector128.LessThan(previous, next), -magnitude, magnitude) & monotonic;
            Vector128<long> difference = residual + tendency;

            // Arithmetic shift rounds negative odd values down. Add the sign bit first to obtain truncation toward zero.
            Vector128<long> halfDifference = (difference + (difference.AsUInt64() >>> 63).AsInt64()) >> 1;
            first = average + halfDifference;
            second = first - difference;
        }

        /// <summary>
        /// Restores 8 independent pairs. Widen before subtraction so extreme stored samples match the scalar arithmetic.
        /// </summary>
        /// <param name="average">The pair-average lanes.</param>
        /// <param name="residual">The residual lanes.</param>
        /// <param name="next">The following-average lanes.</param>
        /// <param name="previous">The preceding-odd-sample lanes.</param>
        /// <param name="first">The reconstructed even lanes.</param>
        /// <param name="second">The reconstructed odd lanes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(
            Vector256<int> average,
            Vector256<int> residual,
            Vector256<int> next,
            Vector256<int> previous,
            out Vector256<int> first,
            out Vector256<int> second)
        {
            ApplyWide(
                Vector256.WidenLower(average),
                Vector256.WidenLower(residual),
                Vector256.WidenLower(next),
                Vector256.WidenLower(previous),
                out Vector256<long> a0,
                out Vector256<long> b0);

            ApplyWide(
                Vector256.WidenUpper(average),
                Vector256.WidenUpper(residual),
                Vector256.WidenUpper(next),
                Vector256.WidenUpper(previous),
                out Vector256<long> a1,
                out Vector256<long> b1);

            first = Vector256.Narrow(a0, a1);
            second = Vector256.Narrow(b0, b1);
        }

        /// <summary>
        /// Evaluates tendency and signed half-differences in wide lanes without intermediate overflow.
        /// </summary>
        /// <param name="average">The widened average lanes.</param>
        /// <param name="residual">The widened residual lanes.</param>
        /// <param name="next">The widened following-average lanes.</param>
        /// <param name="previous">The widened preceding-sample lanes.</param>
        /// <param name="first">The wide even outputs, narrowed only after the complete calculation.</param>
        /// <param name="second">The wide odd outputs.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyWide(
            Vector256<long> average,
            Vector256<long> residual,
            Vector256<long> next,
            Vector256<long> previous,
            out Vector256<long> first,
            out Vector256<long> second)
        {
            Vector256<long> before = previous - average;
            Vector256<long> after = average - next;
            Vector256<long> beforeMagnitude = Vector256.Abs(before);
            Vector256<long> afterMagnitude = Vector256.Abs(after);
            Vector256<long> one = Vector256.Create(1L);
            Vector256<long> third = ((beforeMagnitude.AsUInt64() * Vector256.Create(DivideByThreeMultiplier)) >>> DivideByThreeShift).AsInt64();
            Vector256<long> magnitude = (Vector256.Create(2L) + Vector256.Abs(previous - next) + third) >> 2;
            Vector256<long> beforeLimit = beforeMagnitude << 1;
            Vector256<long> afterLimit = afterMagnitude << 1;
            magnitude = Vector256.ConditionalSelect(Vector256.GreaterThan(magnitude, beforeLimit + (magnitude & one)), beforeLimit + one, magnitude);
            magnitude = Vector256.ConditionalSelect(Vector256.GreaterThan(magnitude + (magnitude & one), afterLimit), afterLimit, magnitude);

            // Equal adjacent values remain monotonic with either sign. Their tendency can be one unit because
            // the limits account for odd pair differences; testing only the XOR sign would discard that case.
            Vector256<long> increasing = Vector256.LessThanOrEqual(before, Vector256<long>.Zero)
                & Vector256.LessThanOrEqual(after, Vector256<long>.Zero);
            Vector256<long> decreasing = Vector256.GreaterThanOrEqual(before, Vector256<long>.Zero)
                & Vector256.GreaterThanOrEqual(after, Vector256<long>.Zero);
            Vector256<long> monotonic = increasing | decreasing;
            Vector256<long> tendency = Vector256.ConditionalSelect(Vector256.LessThan(previous, next), -magnitude, magnitude) & monotonic;
            Vector256<long> difference = residual + tendency;

            // Arithmetic shift rounds negative odd values down. Add the sign bit first to obtain truncation toward zero.
            Vector256<long> halfDifference = (difference + (difference.AsUInt64() >>> 63).AsInt64()) >> 1;
            first = average + halfDifference;
            second = first - difference;
        }

        /// <summary>
        /// Restores 16 independent pairs. Widen before subtraction so extreme stored samples match the scalar arithmetic.
        /// </summary>
        /// <param name="average">The pair-average lanes.</param>
        /// <param name="residual">The residual lanes.</param>
        /// <param name="next">The following-average lanes.</param>
        /// <param name="previous">The preceding-odd-sample lanes.</param>
        /// <param name="first">The reconstructed even lanes.</param>
        /// <param name="second">The reconstructed odd lanes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(
            Vector512<int> average,
            Vector512<int> residual,
            Vector512<int> next,
            Vector512<int> previous,
            out Vector512<int> first,
            out Vector512<int> second)
        {
            ApplyWide(
                Vector512.WidenLower(average),
                Vector512.WidenLower(residual),
                Vector512.WidenLower(next),
                Vector512.WidenLower(previous),
                out Vector512<long> a0,
                out Vector512<long> b0);

            ApplyWide(
                Vector512.WidenUpper(average),
                Vector512.WidenUpper(residual),
                Vector512.WidenUpper(next),
                Vector512.WidenUpper(previous),
                out Vector512<long> a1,
                out Vector512<long> b1);

            first = Vector512.Narrow(a0, a1);
            second = Vector512.Narrow(b0, b1);
        }

        /// <summary>
        /// Evaluates tendency and signed half-differences in wide lanes without intermediate overflow.
        /// </summary>
        /// <param name="average">The widened average lanes.</param>
        /// <param name="residual">The widened residual lanes.</param>
        /// <param name="next">The widened following-average lanes.</param>
        /// <param name="previous">The widened preceding-sample lanes.</param>
        /// <param name="first">The wide even outputs, narrowed only after the complete calculation.</param>
        /// <param name="second">The wide odd outputs.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyWide(
            Vector512<long> average,
            Vector512<long> residual,
            Vector512<long> next,
            Vector512<long> previous,
            out Vector512<long> first,
            out Vector512<long> second)
        {
            Vector512<long> before = previous - average;
            Vector512<long> after = average - next;
            Vector512<long> beforeMagnitude = Vector512.Abs(before);
            Vector512<long> afterMagnitude = Vector512.Abs(after);
            Vector512<long> one = Vector512.Create(1L);
            Vector512<long> third = ((beforeMagnitude.AsUInt64() * Vector512.Create(DivideByThreeMultiplier)) >>> DivideByThreeShift).AsInt64();
            Vector512<long> magnitude = (Vector512.Create(2L) + Vector512.Abs(previous - next) + third) >> 2;
            Vector512<long> beforeLimit = beforeMagnitude << 1;
            Vector512<long> afterLimit = afterMagnitude << 1;
            magnitude = Vector512.ConditionalSelect(Vector512.GreaterThan(magnitude, beforeLimit + (magnitude & one)), beforeLimit + one, magnitude);
            magnitude = Vector512.ConditionalSelect(Vector512.GreaterThan(magnitude + (magnitude & one), afterLimit), afterLimit, magnitude);

            // Equal adjacent values remain monotonic with either sign. Their tendency can be one unit because
            // the limits account for odd pair differences; testing only the XOR sign would discard that case.
            Vector512<long> increasing = Vector512.LessThanOrEqual(before, Vector512<long>.Zero)
                & Vector512.LessThanOrEqual(after, Vector512<long>.Zero);
            Vector512<long> decreasing = Vector512.GreaterThanOrEqual(before, Vector512<long>.Zero)
                & Vector512.GreaterThanOrEqual(after, Vector512<long>.Zero);
            Vector512<long> monotonic = increasing | decreasing;
            Vector512<long> tendency = Vector512.ConditionalSelect(Vector512.LessThan(previous, next), -magnitude, magnitude) & monotonic;
            Vector512<long> difference = residual + tendency;

            // Arithmetic shift rounds negative odd values down. Add the sign bit first to obtain truncation toward zero.
            Vector512<long> halfDifference = (difference + (difference.AsUInt64() >>> 63).AsInt64()) >> 1;
            first = average + halfDifference;
            second = first - difference;
        }
    }
}
