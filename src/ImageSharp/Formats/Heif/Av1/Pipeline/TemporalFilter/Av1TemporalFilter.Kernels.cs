// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.TemporalFilter;

/// <content>
/// Defines the per-sample traversals of the temporal filter. Each walks the widest accelerated operator overload first
/// and finishes in the scalar overload.
/// </content>
internal static partial class Av1TemporalFilter
{
    /// <summary>
    /// The reciprocal normalization of the combined error: 1 / ((window balance weight 5 + 1) * search error weight 20).
    /// </summary>
    internal const double InverseErrorNormalization = 1.0 / ((5 + 1) * 20);

    /// <summary>
    /// The weight of the window error in the combined error: the window balance weight 5 times <see cref="InverseErrorNormalization"/>.
    /// </summary>
    internal const double CombinedWindowWeight = 5.0 * InverseErrorNormalization;

    /// <summary>
    /// Stores the squared differences between a block of the frame to filter and its prediction.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="frame">The frame samples at the block origin.</param>
    /// <param name="frameStride">The frame row stride.</param>
    /// <param name="prediction">The prediction, packed at the block width.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="errors">The squared differences to write, packed at the block width.</param>
    internal static void BuildSquaredErrors<TSample, TOperator>(
        ReadOnlySpan<TSample> frame,
        int frameStride,
        ReadOnlySpan<TSample> prediction,
        int width,
        int height,
        Span<uint> errors)
        where TSample : unmanaged
        where TOperator : struct, ITemporalFilterOperator<TSample>
    {
        // The slices check every bound once, so the walk below can read by reference.
        ref TSample frameBase = ref MemoryMarshal.GetReference(frame[..(((height - 1) * frameStride) + width)]);
        ref TSample predictionBase = ref MemoryMarshal.GetReference(prediction[..(width * height)]);
        ref uint errorBase = ref MemoryMarshal.GetReference(errors[..(width * height)]);
        for (int row = 0; row < height; row++)
        {
            ref TSample frameRow = ref Unsafe.Add(ref frameBase, row * frameStride);
            ref TSample predictionRow = ref Unsafe.Add(ref predictionBase, row * width);
            ref uint errorRow = ref Unsafe.Add(ref errorBase, row * width);
            int column = 0;
            if (Vector512.IsHardwareAccelerated)
            {
                for (; column <= width - Vector512<uint>.Count; column += Vector512<uint>.Count)
                {
                    TOperator.StoreSquaredErrors(
                        ref Unsafe.Add(ref frameRow, column), ref Unsafe.Add(ref predictionRow, column), ref Unsafe.Add(ref errorRow, column), default(Vector512<uint>));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; column <= width - Vector256<uint>.Count; column += Vector256<uint>.Count)
                {
                    TOperator.StoreSquaredErrors(
                        ref Unsafe.Add(ref frameRow, column), ref Unsafe.Add(ref predictionRow, column), ref Unsafe.Add(ref errorRow, column), default(Vector256<uint>));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; column <= width - Vector128<uint>.Count; column += Vector128<uint>.Count)
                {
                    TOperator.StoreSquaredErrors(
                        ref Unsafe.Add(ref frameRow, column), ref Unsafe.Add(ref predictionRow, column), ref Unsafe.Add(ref errorRow, column), default(Vector128<uint>));
                }
            }

            for (; column < width; column++)
            {
                TOperator.StoreSquaredErrors(Unsafe.Add(ref frameRow, column), Unsafe.Add(ref predictionRow, column), ref Unsafe.Add(ref errorRow, column));
            }
        }
    }

    /// <summary>
    /// Sums the luma squared differences that each chroma sample covers.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="lumaErrors">The luma squared differences, packed at the luma block width.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift. It is one only when <paramref name="subsamplingX"/> is also one.</param>
    /// <param name="width">The chroma block width.</param>
    /// <param name="height">The chroma block height.</param>
    /// <param name="destination">The luma sums to write, packed at the chroma block width.</param>
    internal static void SumLumaErrors<TSample, TOperator>(
        ReadOnlySpan<uint> lumaErrors,
        int subsamplingX,
        int subsamplingY,
        int width,
        int height,
        Span<uint> destination)
        where TSample : unmanaged
        where TOperator : struct, ITemporalFilterOperator<TSample>
    {
        if (subsamplingX == 0)
        {
            // AV1 has no vertical-only subsampling, so an unsubsampled chroma plane reads its luma error directly.
            lumaErrors[..(width * height)].CopyTo(destination);
            return;
        }

        int lumaWidth = width << 1;
        ref uint lumaBase = ref MemoryMarshal.GetReference(lumaErrors[..((lumaWidth * height) << subsamplingY)]);
        ref uint destinationBase = ref MemoryMarshal.GetReference(destination[..(width * height)]);
        for (int row = 0; row < height; row++)
        {
            ref uint upper = ref Unsafe.Add(ref lumaBase, (row << subsamplingY) * lumaWidth);
            ref uint lower = ref Unsafe.Add(ref upper, lumaWidth);
            ref uint destinationRow = ref Unsafe.Add(ref destinationBase, row * width);
            int column = 0;
            if (subsamplingY == 0)
            {
                if (Vector512.IsHardwareAccelerated)
                {
                    for (; column <= width - Vector512<uint>.Count; column += Vector512<uint>.Count)
                    {
                        TOperator.SumLumaPairs(ref Unsafe.Add(ref upper, column << 1), ref Unsafe.Add(ref destinationRow, column), default(Vector512<uint>));
                    }
                }

                if (Vector256.IsHardwareAccelerated)
                {
                    for (; column <= width - Vector256<uint>.Count; column += Vector256<uint>.Count)
                    {
                        TOperator.SumLumaPairs(ref Unsafe.Add(ref upper, column << 1), ref Unsafe.Add(ref destinationRow, column), default(Vector256<uint>));
                    }
                }

                if (Vector128.IsHardwareAccelerated)
                {
                    for (; column <= width - Vector128<uint>.Count; column += Vector128<uint>.Count)
                    {
                        TOperator.SumLumaPairs(ref Unsafe.Add(ref upper, column << 1), ref Unsafe.Add(ref destinationRow, column), default(Vector128<uint>));
                    }
                }

                for (; column < width; column++)
                {
                    TOperator.SumLumaPairs(ref Unsafe.Add(ref upper, column << 1), ref Unsafe.Add(ref destinationRow, column));
                }

                continue;
            }

            if (Vector512.IsHardwareAccelerated)
            {
                for (; column <= width - Vector512<uint>.Count; column += Vector512<uint>.Count)
                {
                    TOperator.SumLumaQuads(
                        ref Unsafe.Add(ref upper, column << 1), ref Unsafe.Add(ref lower, column << 1), ref Unsafe.Add(ref destinationRow, column), default(Vector512<uint>));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; column <= width - Vector256<uint>.Count; column += Vector256<uint>.Count)
                {
                    TOperator.SumLumaQuads(
                        ref Unsafe.Add(ref upper, column << 1), ref Unsafe.Add(ref lower, column << 1), ref Unsafe.Add(ref destinationRow, column), default(Vector256<uint>));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; column <= width - Vector128<uint>.Count; column += Vector128<uint>.Count)
                {
                    TOperator.SumLumaQuads(
                        ref Unsafe.Add(ref upper, column << 1), ref Unsafe.Add(ref lower, column << 1), ref Unsafe.Add(ref destinationRow, column), default(Vector128<uint>));
                }
            }

            for (; column < width; column++)
            {
                TOperator.SumLumaQuads(ref Unsafe.Add(ref upper, column << 1), ref Unsafe.Add(ref lower, column << 1), ref Unsafe.Add(ref destinationRow, column));
            }
        }
    }

    /// <summary>
    /// Stores the window error of every sample: the sum of the squared differences in the clamped five-by-five
    /// window, plus the covered luma squared differences on chroma, scaled down to the eight-bit domain.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="errors">The squared differences of the plane, packed at the block width.</param>
    /// <param name="lumaErrors">The luma sums per sample, packed at the block width. The luma plane passes zeros.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="shift">The high-bit-depth shift, 2 * (bit depth - 8).</param>
    /// <param name="columns">Scratch for one edge-padded row of column sums, at least <paramref name="width"/> + 4 values.</param>
    /// <param name="destination">The window errors to write, packed at the block width.</param>
    internal static void BuildWindowErrors<TSample, TOperator>(
        ReadOnlySpan<uint> errors,
        ReadOnlySpan<uint> lumaErrors,
        int width,
        int height,
        int shift,
        Span<uint> columns,
        Span<uint> destination)
        where TSample : unmanaged
        where TOperator : struct, ITemporalFilterOperator<TSample>
    {
        const int HalfWindow = 2;
        ref uint errorBase = ref MemoryMarshal.GetReference(errors[..(width * height)]);
        ref uint lumaBase = ref MemoryMarshal.GetReference(lumaErrors[..(width * height)]);
        ref uint columnBase = ref MemoryMarshal.GetReference(columns[..(width + (2 * HalfWindow))]);
        ref uint destinationBase = ref MemoryMarshal.GetReference(destination[..(width * height)]);
        ref uint columnSums = ref Unsafe.Add(ref columnBase, HalfWindow);
        for (int row = 0; row < height; row++)
        {
            // The window clamps its rows to the block. The two rows above the block repeat the first row, and the two rows
            // below the block repeat the last row.
            ref uint row0 = ref Unsafe.Add(ref errorBase, Math.Max(row - 2, 0) * width);
            ref uint row1 = ref Unsafe.Add(ref errorBase, Math.Max(row - 1, 0) * width);
            ref uint row2 = ref Unsafe.Add(ref errorBase, row * width);
            ref uint row3 = ref Unsafe.Add(ref errorBase, Math.Min(row + 1, height - 1) * width);
            ref uint row4 = ref Unsafe.Add(ref errorBase, Math.Min(row + 2, height - 1) * width);
            int column = 0;
            if (Vector512.IsHardwareAccelerated)
            {
                for (; column <= width - Vector512<uint>.Count; column += Vector512<uint>.Count)
                {
                    TOperator.SumRows(
                        ref Unsafe.Add(ref row0, column),
                        ref Unsafe.Add(ref row1, column),
                        ref Unsafe.Add(ref row2, column),
                        ref Unsafe.Add(ref row3, column),
                        ref Unsafe.Add(ref row4, column),
                        ref Unsafe.Add(ref columnSums, column),
                        default(Vector512<uint>));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; column <= width - Vector256<uint>.Count; column += Vector256<uint>.Count)
                {
                    TOperator.SumRows(
                        ref Unsafe.Add(ref row0, column),
                        ref Unsafe.Add(ref row1, column),
                        ref Unsafe.Add(ref row2, column),
                        ref Unsafe.Add(ref row3, column),
                        ref Unsafe.Add(ref row4, column),
                        ref Unsafe.Add(ref columnSums, column),
                        default(Vector256<uint>));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; column <= width - Vector128<uint>.Count; column += Vector128<uint>.Count)
                {
                    TOperator.SumRows(
                        ref Unsafe.Add(ref row0, column),
                        ref Unsafe.Add(ref row1, column),
                        ref Unsafe.Add(ref row2, column),
                        ref Unsafe.Add(ref row3, column),
                        ref Unsafe.Add(ref row4, column),
                        ref Unsafe.Add(ref columnSums, column),
                        default(Vector128<uint>));
                }
            }

            for (; column < width; column++)
            {
                TOperator.SumRows(
                    Unsafe.Add(ref row0, column),
                    Unsafe.Add(ref row1, column),
                    Unsafe.Add(ref row2, column),
                    Unsafe.Add(ref row3, column),
                    Unsafe.Add(ref row4, column),
                    ref Unsafe.Add(ref columnSums, column));
            }

            // Two copies of each outer column sum on each side clamp the window columns to the block, in the same way as
            // the rows. Index 0 of the padded row is then the window start of column 0.
            columnBase = columnSums;
            Unsafe.Add(ref columnBase, 1) = columnSums;
            Unsafe.Add(ref columnSums, width) = Unsafe.Add(ref columnSums, width - 1);
            Unsafe.Add(ref columnSums, width + 1) = Unsafe.Add(ref columnSums, width - 1);

            ref uint lumaRow = ref Unsafe.Add(ref lumaBase, row * width);
            ref uint destinationRow = ref Unsafe.Add(ref destinationBase, row * width);
            column = 0;
            if (Vector512.IsHardwareAccelerated)
            {
                for (; column <= width - Vector512<uint>.Count; column += Vector512<uint>.Count)
                {
                    TOperator.SumWindow(
                        ref Unsafe.Add(ref columnBase, column), ref Unsafe.Add(ref lumaRow, column), shift, ref Unsafe.Add(ref destinationRow, column), default(Vector512<uint>));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; column <= width - Vector256<uint>.Count; column += Vector256<uint>.Count)
                {
                    TOperator.SumWindow(
                        ref Unsafe.Add(ref columnBase, column), ref Unsafe.Add(ref lumaRow, column), shift, ref Unsafe.Add(ref destinationRow, column), default(Vector256<uint>));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; column <= width - Vector128<uint>.Count; column += Vector128<uint>.Count)
                {
                    TOperator.SumWindow(
                        ref Unsafe.Add(ref columnBase, column), ref Unsafe.Add(ref lumaRow, column), shift, ref Unsafe.Add(ref destinationRow, column), default(Vector128<uint>));
                }
            }

            for (; column < width; column++)
            {
                TOperator.SumWindow(ref Unsafe.Add(ref columnBase, column), Unsafe.Add(ref lumaRow, column), shift, ref Unsafe.Add(ref destinationRow, column));
            }
        }
    }

    /// <summary>
    /// Weighs a predicted block and adds it to the accumulators.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="windowErrors">The window errors, packed at the block width.</param>
    /// <param name="prediction">The prediction, packed at the block width.</param>
    /// <param name="accumulator">The weighted sums of the plane, packed at the block width.</param>
    /// <param name="count">The weight totals of the plane, packed at the block width.</param>
    /// <param name="width">The block width, a multiple of 32.</param>
    /// <param name="height">The block height, a multiple of 32.</param>
    /// <param name="inverseReferenceCount">The reciprocal of the number of squared errors in a window.</param>
    /// <param name="blockErrors">The sixteen sub-block errors multiplied by the normalization factor.</param>
    /// <param name="firstFactors">The sixteen first multipliers of the combined error.</param>
    /// <param name="secondFactor">The second multiplier of the combined error.</param>
    /// <param name="level">The weight calculation level.</param>
    internal static void AccumulateWeights<TSample, TOperator>(
        ReadOnlySpan<uint> windowErrors,
        ReadOnlySpan<TSample> prediction,
        Span<uint> accumulator,
        Span<ushort> count,
        int width,
        int height,
        double inverseReferenceCount,
        ReadOnlySpan<double> blockErrors,
        ReadOnlySpan<double> firstFactors,
        double secondFactor,
        int level)
        where TSample : unmanaged
        where TOperator : struct, ITemporalFilterOperator<TSample>
    {
        ref uint errorBase = ref MemoryMarshal.GetReference(windowErrors[..(width * height)]);
        ref TSample predictionBase = ref MemoryMarshal.GetReference(prediction[..(width * height)]);
        ref uint accumulatorBase = ref MemoryMarshal.GetReference(accumulator[..(width * height)]);
        ref ushort countBase = ref MemoryMarshal.GetReference(count[..(width * height)]);
        ReadOnlySpan<double> errors = blockErrors[..16];
        ReadOnlySpan<double> factors = firstFactors[..16];
        int segmentWidth = width >> 2;
        int halfHeight = height >> 1;
        int quarterHeight = height >> 2;
        for (int row = 0; row < height; row++)
        {
            // Sub-block index (y32 * 2 + x32) * 4 + (y16 * 2 + x16): the 64x64 block holds four 32x32 blocks in
            // raster order, each holding four 16x16 blocks in raster order, scaled to the plane dimensions.
            int rowIndex = ((row / halfHeight) << 3) + (((row % halfHeight) / quarterHeight) << 1);
            int rowOffset = row * width;
            for (int segment = 0; segment < 4; segment++)
            {
                int index = rowIndex + ((segment >> 1) << 2) + (segment & 1);
                WeightTerms terms = new(inverseReferenceCount, errors[index], factors[index], secondFactor, level);
                int column = rowOffset + (segment * segmentWidth);
                int end = column + segmentWidth;

                // Level zero uses the exact exponential. No vector form rounds the same way as the scalar exponential,
                // so level zero runs only the scalar overload.
                if (level != 0)
                {
                    if (Vector512.IsHardwareAccelerated)
                    {
                        for (; column <= end - Vector512<double>.Count; column += Vector512<double>.Count)
                        {
                            TOperator.AccumulateWeights(
                                ref Unsafe.Add(ref errorBase, column),
                                ref Unsafe.Add(ref predictionBase, column),
                                ref Unsafe.Add(ref accumulatorBase, column),
                                ref Unsafe.Add(ref countBase, column),
                                in terms,
                                default(Vector512<double>));
                        }
                    }

                    if (Vector256.IsHardwareAccelerated)
                    {
                        for (; column <= end - Vector256<double>.Count; column += Vector256<double>.Count)
                        {
                            TOperator.AccumulateWeights(
                                ref Unsafe.Add(ref errorBase, column),
                                ref Unsafe.Add(ref predictionBase, column),
                                ref Unsafe.Add(ref accumulatorBase, column),
                                ref Unsafe.Add(ref countBase, column),
                                in terms,
                                default(Vector256<double>));
                        }
                    }

                    if (Vector128.IsHardwareAccelerated)
                    {
                        // The 128-bit overload weighs four samples with two double registers.
                        for (; column <= end - Vector128<uint>.Count; column += Vector128<uint>.Count)
                        {
                            TOperator.AccumulateWeights(
                                ref Unsafe.Add(ref errorBase, column),
                                ref Unsafe.Add(ref predictionBase, column),
                                ref Unsafe.Add(ref accumulatorBase, column),
                                ref Unsafe.Add(ref countBase, column),
                                in terms,
                                default(Vector128<double>));
                        }
                    }
                }

                for (; column < end; column++)
                {
                    TOperator.AccumulateWeights(
                        Unsafe.Add(ref errorBase, column),
                        Unsafe.Add(ref predictionBase, column),
                        ref Unsafe.Add(ref accumulatorBase, column),
                        ref Unsafe.Add(ref countBase, column),
                        in terms);
                }
            }
        }
    }

    /// <summary>
    /// Adds a block of the frame to filter to the accumulators at the full weight.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="frame">The frame samples at the block origin.</param>
    /// <param name="frameStride">The frame row stride.</param>
    /// <param name="accumulator">The weighted sums of the plane, packed at the block width.</param>
    /// <param name="count">The weight totals of the plane, packed at the block width.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    internal static void AccumulateSelf<TSample, TOperator>(
        ReadOnlySpan<TSample> frame,
        int frameStride,
        Span<uint> accumulator,
        Span<ushort> count,
        int width,
        int height)
        where TSample : unmanaged
        where TOperator : struct, ITemporalFilterOperator<TSample>
    {
        ref TSample frameBase = ref MemoryMarshal.GetReference(frame[..(((height - 1) * frameStride) + width)]);
        ref uint accumulatorBase = ref MemoryMarshal.GetReference(accumulator[..(width * height)]);
        ref ushort countBase = ref MemoryMarshal.GetReference(count[..(width * height)]);
        for (int row = 0; row < height; row++)
        {
            ref TSample frameRow = ref Unsafe.Add(ref frameBase, row * frameStride);
            ref uint accumulatorRow = ref Unsafe.Add(ref accumulatorBase, row * width);
            ref ushort countRow = ref Unsafe.Add(ref countBase, row * width);
            int column = 0;
            if (Vector512.IsHardwareAccelerated)
            {
                for (; column <= width - Vector512<uint>.Count; column += Vector512<uint>.Count)
                {
                    TOperator.AccumulateSelf(
                        ref Unsafe.Add(ref frameRow, column), ref Unsafe.Add(ref accumulatorRow, column), ref Unsafe.Add(ref countRow, column), default(Vector512<uint>));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; column <= width - Vector256<uint>.Count; column += Vector256<uint>.Count)
                {
                    TOperator.AccumulateSelf(
                        ref Unsafe.Add(ref frameRow, column), ref Unsafe.Add(ref accumulatorRow, column), ref Unsafe.Add(ref countRow, column), default(Vector256<uint>));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; column <= width - Vector128<uint>.Count; column += Vector128<uint>.Count)
                {
                    TOperator.AccumulateSelf(
                        ref Unsafe.Add(ref frameRow, column), ref Unsafe.Add(ref accumulatorRow, column), ref Unsafe.Add(ref countRow, column), default(Vector128<uint>));
                }
            }

            for (; column < width; column++)
            {
                TOperator.AccumulateSelf(Unsafe.Add(ref frameRow, column), ref Unsafe.Add(ref accumulatorRow, column), ref Unsafe.Add(ref countRow, column));
            }
        }
    }

    /// <summary>
    /// Divides the accumulators of one plane block by their weight totals and writes the filtered samples.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="accumulator">The weighted sums of the plane, packed at the block width.</param>
    /// <param name="count">The weight totals of the plane, packed at the block width. Every total is nonzero.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="destination">The filtered frame samples at the block origin.</param>
    /// <param name="destinationStride">The filtered frame row stride.</param>
    internal static void Normalize<TSample, TOperator>(
        ReadOnlySpan<uint> accumulator,
        ReadOnlySpan<ushort> count,
        int width,
        int height,
        Span<TSample> destination,
        int destinationStride)
        where TSample : unmanaged
        where TOperator : struct, ITemporalFilterOperator<TSample>
    {
        ref uint accumulatorBase = ref MemoryMarshal.GetReference(accumulator[..(width * height)]);
        ref ushort countBase = ref MemoryMarshal.GetReference(count[..(width * height)]);
        ref TSample destinationBase = ref MemoryMarshal.GetReference(destination[..(((height - 1) * destinationStride) + width)]);
        for (int row = 0; row < height; row++)
        {
            ref uint accumulatorRow = ref Unsafe.Add(ref accumulatorBase, row * width);
            ref ushort countRow = ref Unsafe.Add(ref countBase, row * width);
            ref TSample destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int column = 0;
            if (Vector512.IsHardwareAccelerated)
            {
                for (; column <= width - Vector512<double>.Count; column += Vector512<double>.Count)
                {
                    TOperator.Normalize(
                        ref Unsafe.Add(ref accumulatorRow, column), ref Unsafe.Add(ref countRow, column), ref Unsafe.Add(ref destinationRow, column), default(Vector512<double>));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; column <= width - Vector256<double>.Count; column += Vector256<double>.Count)
                {
                    TOperator.Normalize(
                        ref Unsafe.Add(ref accumulatorRow, column), ref Unsafe.Add(ref countRow, column), ref Unsafe.Add(ref destinationRow, column), default(Vector256<double>));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                // The 128-bit overload divides four accumulators with two double registers.
                for (; column <= width - Vector128<uint>.Count; column += Vector128<uint>.Count)
                {
                    TOperator.Normalize(
                        ref Unsafe.Add(ref accumulatorRow, column), ref Unsafe.Add(ref countRow, column), ref Unsafe.Add(ref destinationRow, column), default(Vector128<double>));
                }
            }

            for (; column < width; column++)
            {
                TOperator.Normalize(Unsafe.Add(ref accumulatorRow, column), Unsafe.Add(ref countRow, column), ref Unsafe.Add(ref destinationRow, column));
            }
        }
    }

    /// <summary>
    /// Estimates the noise level of one plane from the Laplacian of its smooth samples.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="plane">The plane samples, starting at the top-left visible sample.</param>
    /// <param name="stride">The plane row stride.</param>
    /// <param name="width">The visible plane width.</param>
    /// <param name="height">The visible plane height.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="edgeThreshold">The gradient magnitude below which a sample counts as smooth.</param>
    /// <returns>The noise level, or -1 when fewer than sixteen samples are smooth.</returns>
    internal static double EstimateNoise<TSample, TOperator>(
        ReadOnlySpan<TSample> plane,
        int stride,
        int width,
        int height,
        int bitDepth,
        int edgeThreshold)
        where TSample : unmanaged
        where TOperator : struct, ITemporalFilterOperator<TSample>
    {
        const double SquareRootHalfPi = 1.25331413732;
        NoiseTerms terms = new(edgeThreshold, bitDepth);
        ref TSample planeBase = ref MemoryMarshal.GetReference(plane[..(((height - 1) * stride) + width)]);

        // For eight-bit planes, the columns 1 to vectorEnd sum into one 32-bit value that wraps modulo 2^32. The remaining
        // columns sum into a 64-bit value. Other AV1 encoders split the sum in this way, so the noise estimate stays the same.
        // Integer wrapping does not depend on the lane assignment, so a wrapping int sum of the same columns gives the same
        // result as any vector width. High-bit-depth planes sum all columns into the 64-bit value.
        int vectorEnd = bitDepth == 8 ? (width - 1) & ~31 : 0;
        int vectorSum = 0;
        int vectorCount = 0;
        long scalarSum = 0;
        int scalarCount = 0;
        for (int row = 1; row < height - 1; row++)
        {
            ref TSample rowBase = ref Unsafe.Add(ref planeBase, row * stride);
            if (vectorEnd > 0)
            {
                (int sum, int count) = AccumulateNoiseRow<TSample, TOperator>(ref rowBase, stride, 1, vectorEnd + 1, in terms);
                vectorSum = unchecked(vectorSum + sum);
                vectorCount = unchecked(vectorCount + count);
            }

            (int tailSum, int tailCount) = AccumulateNoiseRow<TSample, TOperator>(ref rowBase, stride, vectorEnd + 1, width - 1, in terms);
            scalarSum += tailSum;
            scalarCount += tailCount;
        }

        long accumulated = scalarSum + vectorSum;
        int smoothCount = scalarCount + vectorCount;
        return smoothCount < 16 ? -1.0 : (double)accumulated / (6 * smoothCount) * SquareRootHalfPi;
    }

    /// <summary>
    /// Returns the Laplacian total and the smooth-sample count of one row between two columns.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="row">The first sample of the row.</param>
    /// <param name="stride">The plane row stride.</param>
    /// <param name="start">The first center column.</param>
    /// <param name="end">The column after the last center column.</param>
    /// <param name="terms">The edge threshold and the bit-depth rounding.</param>
    /// <returns>The Laplacian total and the number of smooth samples.</returns>
    private static (int Sum, int Count) AccumulateNoiseRow<TSample, TOperator>(ref TSample row, int stride, int start, int end, in NoiseTerms terms)
        where TSample : unmanaged
        where TOperator : struct, ITemporalFilterOperator<TSample>
    {
        // A lane holds at most one magnitude below 2^12 per call, so a row of any practical width cannot overflow
        // a lane before the single reduction at the end of the row.
        int column = start;
        int sum = 0;
        int count = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<int> sum512 = Vector512<int>.Zero;
            Vector512<int> count512 = Vector512<int>.Zero;
            for (; column <= end - Vector512<short>.Count; column += Vector512<short>.Count)
            {
                TOperator.AccumulateNoise(ref Unsafe.Add(ref row, column), stride, in terms, ref sum512, ref count512);
            }

            sum += Vector512.Sum(sum512);
            count += Vector512.Sum(count512);
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<int> sum256 = Vector256<int>.Zero;
            Vector256<int> count256 = Vector256<int>.Zero;
            for (; column <= end - Vector256<short>.Count; column += Vector256<short>.Count)
            {
                TOperator.AccumulateNoise(ref Unsafe.Add(ref row, column), stride, in terms, ref sum256, ref count256);
            }

            sum += Vector256.Sum(sum256);
            count += Vector256.Sum(count256);
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<int> sum128 = Vector128<int>.Zero;
            Vector128<int> count128 = Vector128<int>.Zero;
            for (; column <= end - Vector128<short>.Count; column += Vector128<short>.Count)
            {
                TOperator.AccumulateNoise(ref Unsafe.Add(ref row, column), stride, in terms, ref sum128, ref count128);
            }

            sum += Vector128.Sum(sum128);
            count += Vector128.Sum(count128);
        }

        for (; column < end; column++)
        {
            TOperator.AccumulateNoise(ref Unsafe.Add(ref row, column), stride, in terms, ref sum, ref count);
        }

        return (sum, count);
    }
}
