// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Builds signed AV1 residual planes and measures sample-domain error.
/// </summary>
internal static partial class Av1ResidualBuilder
{
    /// <summary>
    /// The width and height of the encoder's fixed motion-search block, in samples.
    /// </summary>
    private const int SearchBlockDimension = 8;

    /// <summary>
    /// Measures absolute prediction error over a rectangular block, optionally sampling alternate rows.
    /// </summary>
    /// <param name="source">Source samples beginning at the block origin.</param>
    /// <param name="sourceStride">The source row stride, in samples.</param>
    /// <param name="prediction">Prediction samples beginning at the block origin.</param>
    /// <param name="predictionStride">The prediction row stride, in samples.</param>
    /// <param name="width">The block width, in samples.</param>
    /// <param name="height">The block height, in samples.</param>
    /// <param name="rowStep">One for every row, or two for alternating rows with doubled error.</param>
    /// <returns>The unnormalized absolute difference over the block.</returns>
    public static int SumAbsoluteDifferences(
        ReadOnlySpan<byte> source,
        int sourceStride,
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        int width,
        int height,
        int rowStep)
        => SumAbsoluteDifferences<byte, ByteOperator>(source, sourceStride, prediction, predictionStride, width, height, rowStep);

    /// <summary>
    /// Measures signed residual sum and squared error over a rectangular prediction block.
    /// </summary>
    /// <param name="source">Source samples beginning at the block origin.</param>
    /// <param name="sourceStride">The source row stride, in samples.</param>
    /// <param name="prediction">Prediction samples beginning at the block origin.</param>
    /// <param name="predictionStride">The prediction row stride, in samples.</param>
    /// <param name="width">The block width, in samples.</param>
    /// <param name="height">The block height, in samples.</param>
    /// <param name="sum">The unnormalized signed residual sum.</param>
    /// <param name="sumOfSquares">The unnormalized squared residual sum.</param>
    public static void GetMoments(
        ReadOnlySpan<byte> source,
        int sourceStride,
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        int width,
        int height,
        out int sum,
        out long sumOfSquares)
        => GetMoments<byte, ByteOperator>(source, sourceStride, prediction, predictionStride, width, height, out sum, out sumOfSquares);

    /// <summary>
    /// Measures absolute prediction error over a rectangular block, optionally sampling alternate rows.
    /// </summary>
    /// <param name="source">Source samples beginning at the block origin.</param>
    /// <param name="sourceStride">The source row stride, in samples.</param>
    /// <param name="prediction">Prediction samples beginning at the block origin.</param>
    /// <param name="predictionStride">The prediction row stride, in samples.</param>
    /// <param name="width">The block width, in samples.</param>
    /// <param name="height">The block height, in samples.</param>
    /// <param name="rowStep">One for every row, or two for alternating rows with doubled error.</param>
    /// <returns>The unnormalized absolute difference over the block.</returns>
    public static int SumAbsoluteDifferences(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        ReadOnlySpan<ushort> prediction,
        int predictionStride,
        int width,
        int height,
        int rowStep)
        => SumAbsoluteDifferences<ushort, UInt16Operator>(source, sourceStride, prediction, predictionStride, width, height, rowStep);

    /// <summary>
    /// Measures signed residual sum and squared error over a rectangular prediction block.
    /// </summary>
    /// <param name="source">Source samples beginning at the block origin.</param>
    /// <param name="sourceStride">The source row stride, in samples.</param>
    /// <param name="prediction">Prediction samples beginning at the block origin.</param>
    /// <param name="predictionStride">The prediction row stride, in samples.</param>
    /// <param name="width">The block width, in samples.</param>
    /// <param name="height">The block height, in samples.</param>
    /// <param name="sum">The unnormalized signed residual sum.</param>
    /// <param name="sumOfSquares">The unnormalized squared residual sum.</param>
    public static void GetMoments(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        ReadOnlySpan<ushort> prediction,
        int predictionStride,
        int width,
        int height,
        out int sum,
        out long sumOfSquares)
        => GetMoments<ushort, UInt16Operator>(source, sourceStride, prediction, predictionStride, width, height, out sum, out sumOfSquares);

    /// <summary>
    /// Traverses rectangular SAD candidates using the selected sample operator and descending vector widths.
    /// </summary>
    private static int SumAbsoluteDifferences<TSample, TOperator>(
        ReadOnlySpan<TSample> source,
        int sourceStride,
        ReadOnlySpan<TSample> prediction,
        int predictionStride,
        int width,
        int height,
        int rowStep)
        where TSample : unmanaged
        where TOperator : struct, IResidualOperator<TSample>
    {
        // The running total stays in lanes for the whole block and is reduced once at the end.
        // One 32-bit lane takes a twelve-bit difference more than a million times, and the largest
        // AV1 block holds 16384 samples, so the block needs no intermediate fold.
        Vector512<uint> total512 = Vector512<uint>.Zero;
        Vector256<uint> total256 = Vector256<uint>.Zero;
        Vector128<uint> total128 = Vector128<uint>.Zero;
        int sum = 0;

        // Wider blocks use complete native loads. The eight-sample tail retains the existing compact load,
        // which reads eight bytes or eight words without crossing a short row's boundary.
        for (int y = 0; y < height; y += rowStep)
        {
            ReadOnlySpan<TSample> sourceRow = source.Slice(y * sourceStride, width);
            ReadOnlySpan<TSample> predictionRow = prediction.Slice(y * predictionStride, width);
            ref TSample sourceBase = ref MemoryMarshal.GetReference(sourceRow);
            ref TSample predictionBase = ref MemoryMarshal.GetReference(predictionRow);
            int x = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                for (; x <= width - Vector512<TSample>.Count; x += Vector512<TSample>.Count)
                {
                    total512 = TOperator.AccumulateAbsoluteDifferences(
                        Vector512.LoadUnsafe(ref sourceBase, (nuint)x),
                        Vector512.LoadUnsafe(ref predictionBase, (nuint)x),
                        total512);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x <= width - Vector256<TSample>.Count; x += Vector256<TSample>.Count)
                {
                    total256 = TOperator.AccumulateAbsoluteDifferences(
                        Vector256.LoadUnsafe(ref sourceBase, (nuint)x),
                        Vector256.LoadUnsafe(ref predictionBase, (nuint)x),
                        total256);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x <= width - Vector128<TSample>.Count; x += Vector128<TSample>.Count)
                {
                    total128 = TOperator.AccumulateAbsoluteDifferences(
                        Vector128.LoadUnsafe(ref sourceBase, (nuint)x),
                        Vector128.LoadUnsafe(ref predictionBase, (nuint)x),
                        total128);
                }
            }

            // A row of eight bytes fills half of a byte vector, so the compact load pads it with
            // zeros. The padding is identical on both sides, so its difference is zero and it adds
            // nothing to the total.
            if (Vector128.IsHardwareAccelerated && x <= width - SearchBlockDimension)
            {
                total128 = TOperator.AccumulateAbsoluteDifferences(
                    LoadSearchRow(sourceRow[x..]), LoadSearchRow(predictionRow[x..]), total128);

                x += SearchBlockDimension;
            }

            for (; x < width; x++)
            {
                sum += TOperator.SumAbsoluteDifferences(sourceRow[x], predictionRow[x]);
            }
        }

        // Folding the wide totals down costs four adds and no branch. A width the hardware does not
        // have contributes a zero vector, so the unused stages drop out of the result on their own.
        total256 += total512.GetLower() + total512.GetUpper();
        total128 += total256.GetLower() + total256.GetUpper();
        sum += (int)Vector128.Sum(total128);

        // Alternate-row search represents the complete even-height block by doubling the sampled row total.
        // Precision normalization follows this scaling so fractional error units are truncated only once.
        return sum * rowStep;
    }

    /// <summary>
    /// Accumulates rectangular residual moments without storing an intermediate residual plane.
    /// </summary>
    private static void GetMoments<TSample, TOperator>(
        ReadOnlySpan<TSample> source,
        int sourceStride,
        ReadOnlySpan<TSample> prediction,
        int predictionStride,
        int width,
        int height,
        out int sum,
        out long sumOfSquares)
        where TSample : unmanaged
        where TOperator : struct, IResidualOperator<TSample>
    {
        sum = 0;
        sumOfSquares = 0;

        // The signed total stays in lanes for the whole block. One lane takes two differences per
        // vector, so a lane holds at most 8190 per vector and cannot overflow inside any AV1 block.
        Vector512<int> sum512 = Vector512<int>.Zero;
        Vector256<int> sum256 = Vector256<int>.Zero;
        Vector128<int> sum128 = Vector128<int>.Zero;

        // Wider blocks use complete native loads. The eight-sample tail retains the existing compact load,
        // which reads eight bytes or eight words without crossing a short row's boundary.
        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<TSample> sourceRow = source.Slice(y * sourceStride, width);
            ReadOnlySpan<TSample> predictionRow = prediction.Slice(y * predictionStride, width);
            ref TSample sourceBase = ref MemoryMarshal.GetReference(sourceRow);
            ref TSample predictionBase = ref MemoryMarshal.GetReference(predictionRow);
            int x = 0;

            // The squared total folds at the end of each row. A lane takes two squares per vector, so
            // it holds at most 33538050 per vector and overflows after 64 of them. A row of the widest
            // AV1 block is 16 vectors at the narrowest width, which keeps a wide margin.
            Vector512<int> squares512 = Vector512<int>.Zero;
            Vector256<int> squares256 = Vector256<int>.Zero;
            Vector128<int> squares128 = Vector128<int>.Zero;

            if (Vector512.IsHardwareAccelerated)
            {
                for (; x <= width - Vector512<TSample>.Count; x += Vector512<TSample>.Count)
                {
                    TOperator.AccumulateMoments(
                        Vector512.LoadUnsafe(ref sourceBase, (nuint)x),
                        Vector512.LoadUnsafe(ref predictionBase, (nuint)x),
                        ref sum512,
                        ref squares512);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x <= width - Vector256<TSample>.Count; x += Vector256<TSample>.Count)
                {
                    TOperator.AccumulateMoments(
                        Vector256.LoadUnsafe(ref sourceBase, (nuint)x),
                        Vector256.LoadUnsafe(ref predictionBase, (nuint)x),
                        ref sum256,
                        ref squares256);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x <= width - Vector128<TSample>.Count; x += Vector128<TSample>.Count)
                {
                    TOperator.AccumulateMoments(
                        Vector128.LoadUnsafe(ref sourceBase, (nuint)x),
                        Vector128.LoadUnsafe(ref predictionBase, (nuint)x),
                        ref sum128,
                        ref squares128);
                }
            }

            // The padding the compact load adds is identical on both sides, so it contributes a zero
            // difference and a zero square.
            if (Vector128.IsHardwareAccelerated && x <= width - SearchBlockDimension)
            {
                TOperator.AccumulateMoments(
                    LoadSearchRow(sourceRow[x..]), LoadSearchRow(predictionRow[x..]), ref sum128, ref squares128);

                x += SearchBlockDimension;
            }

            squares256 += squares512.GetLower() + squares512.GetUpper();
            squares128 += squares256.GetLower() + squares256.GetUpper();
            sumOfSquares += Vector128.Sum(squares128);

            for (; x < width; x++)
            {
                int difference = TOperator.Subtract(sourceRow[x], predictionRow[x]);
                sum += difference;
                sumOfSquares += difference * difference;
            }
        }

        sum256 += sum512.GetLower() + sum512.GetUpper();
        sum128 += sum256.GetLower() + sum256.GetUpper();
        sum += Vector128.Sum(sum128);
    }

    /// <summary>
    /// Calculates the sum of absolute differences for an 8x8 block.
    /// </summary>
    /// <param name="source">The source samples starting at the block origin.</param>
    /// <param name="sourceStride">The source row stride, in samples.</param>
    /// <param name="prediction">The prediction samples starting at the block origin.</param>
    /// <param name="predictionStride">The prediction row stride, in samples.</param>
    /// <returns>The sum of absolute sample differences.</returns>
    public static int SumAbsoluteDifferences8x8(ReadOnlySpan<byte> source, int sourceStride, ReadOnlySpan<byte> prediction, int predictionStride)
        => SumAbsoluteDifferences8x8<byte, ByteOperator>(source, sourceStride, prediction, predictionStride);

    /// <summary>
    /// Measures four horizontally adjacent 8x8 predictions against one source block.
    /// </summary>
    /// <param name="source">The source samples starting at the block origin.</param>
    /// <param name="sourceStride">The source row stride, in samples.</param>
    /// <param name="prediction">The first prediction, with three additional samples available at the right of each row.</param>
    /// <param name="predictionStride">The prediction row stride, in samples.</param>
    /// <param name="sums">The four results in increasing horizontal-offset order.</param>
    public static void SumFourAbsoluteDifferences8x8(
        ReadOnlySpan<byte> source, int sourceStride, ReadOnlySpan<byte> prediction, int predictionStride, Span<int> sums)
        => SumFourAbsoluteDifferences8x8<byte, ByteOperator>(source, sourceStride, prediction, predictionStride, sums);

    /// <summary>
    /// Calculates the signed sum and squared sum of differences for an 8x8 block.
    /// </summary>
    /// <param name="source">The source samples starting at the block origin.</param>
    /// <param name="sourceStride">The source row stride, in samples.</param>
    /// <param name="prediction">The prediction samples starting at the block origin.</param>
    /// <param name="predictionStride">The prediction row stride, in samples.</param>
    /// <param name="sum">The signed sum of sample differences.</param>
    /// <param name="sumOfSquares">The sum of squared sample differences.</param>
    public static void GetMoments8x8(
        ReadOnlySpan<byte> source, int sourceStride, ReadOnlySpan<byte> prediction, int predictionStride, out int sum, out int sumOfSquares)
        => GetMoments8x8<byte, ByteOperator>(source, sourceStride, prediction, predictionStride, out sum, out sumOfSquares);

    /// <summary>
    /// Calculates the sum of absolute differences for an 8x8 block.
    /// </summary>
    /// <param name="source">The source samples starting at the block origin.</param>
    /// <param name="sourceStride">The source row stride, in samples.</param>
    /// <param name="prediction">The prediction samples starting at the block origin.</param>
    /// <param name="predictionStride">The prediction row stride, in samples.</param>
    /// <returns>The sum of absolute sample differences.</returns>
    public static int SumAbsoluteDifferences8x8(ReadOnlySpan<ushort> source, int sourceStride, ReadOnlySpan<ushort> prediction, int predictionStride)
        => SumAbsoluteDifferences8x8<ushort, UInt16Operator>(source, sourceStride, prediction, predictionStride);

    /// <summary>
    /// Measures four horizontally adjacent 8x8 predictions against one source block.
    /// </summary>
    /// <param name="source">The source samples starting at the block origin.</param>
    /// <param name="sourceStride">The source row stride, in samples.</param>
    /// <param name="prediction">The first prediction, with three additional samples available at the right of each row.</param>
    /// <param name="predictionStride">The prediction row stride, in samples.</param>
    /// <param name="sums">The four results in increasing horizontal-offset order.</param>
    public static void SumFourAbsoluteDifferences8x8(
        ReadOnlySpan<ushort> source, int sourceStride, ReadOnlySpan<ushort> prediction, int predictionStride, Span<int> sums)
        => SumFourAbsoluteDifferences8x8<ushort, UInt16Operator>(source, sourceStride, prediction, predictionStride, sums);

    /// <summary>
    /// Calculates the signed sum and squared sum of differences for an 8x8 block.
    /// </summary>
    /// <param name="source">The source samples starting at the block origin.</param>
    /// <param name="sourceStride">The source row stride, in samples.</param>
    /// <param name="prediction">The prediction samples starting at the block origin.</param>
    /// <param name="predictionStride">The prediction row stride, in samples.</param>
    /// <param name="sum">The signed sum of sample differences.</param>
    /// <param name="sumOfSquares">The sum of squared sample differences.</param>
    public static void GetMoments8x8(
        ReadOnlySpan<ushort> source, int sourceStride, ReadOnlySpan<ushort> prediction, int predictionStride, out int sum, out int sumOfSquares)
        => GetMoments8x8<ushort, UInt16Operator>(source, sourceStride, prediction, predictionStride, out sum, out sumOfSquares);

    /// <summary>
    /// Traverses an 8x8 block while its closed operator calculates scalar or eight-sample row costs.
    /// </summary>
    private static int SumAbsoluteDifferences8x8<TSample, TOperator>(
        ReadOnlySpan<TSample> source, int sourceStride, ReadOnlySpan<TSample> prediction, int predictionStride)
        where TSample : unmanaged
        where TOperator : struct, IResidualOperator<TSample>
    {
        int sum = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            // Eight widened AV1 samples exactly fill 128 bits. Wider loads would cross the row boundary;
            // byte storage is loaded as eight bytes and ushort storage as eight native-order words.
            // The total stays in lanes across all eight rows, so the block reduces once. Eight rows of
            // eight bytes reach 16320 in one lane, which is far inside the 32-bit range.
            Vector128<uint> total = Vector128<uint>.Zero;
            for (int row = 0; row < SearchBlockDimension; row++)
            {
                total = TOperator.AccumulateAbsoluteDifferences(
                    LoadSearchRow(source[(row * sourceStride)..]),
                    LoadSearchRow(prediction[(row * predictionStride)..]),
                    total);
            }

            sum = (int)Vector128.Sum(total);
        }
        else
        {
            for (int row = 0; row < SearchBlockDimension; row++)
            {
                ReadOnlySpan<TSample> sourceRow = source.Slice(row * sourceStride, SearchBlockDimension);
                ReadOnlySpan<TSample> predictionRow = prediction.Slice(row * predictionStride, SearchBlockDimension);
                for (int column = 0; column < SearchBlockDimension; column++)
                {
                    sum += TOperator.SumAbsoluteDifferences(sourceRow[column], predictionRow[column]);
                }
            }
        }

        return sum;
    }

    /// <summary>
    /// Traverses four adjacent candidates together, retaining one source load per row or scalar sample.
    /// </summary>
    private static void SumFourAbsoluteDifferences8x8<TSample, TOperator>(
        ReadOnlySpan<TSample> source, int sourceStride, ReadOnlySpan<TSample> prediction, int predictionStride, Span<int> sums)
        where TSample : unmanaged
        where TOperator : struct, IResidualOperator<TSample>
    {
        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<int> totals = Vector128<int>.Zero;
            for (int row = 0; row < SearchBlockDimension; row++)
            {
                ReadOnlySpan<TSample> predictionRow = prediction[(row * predictionStride)..];
                Vector128<TSample> sourceRow = LoadSearchRow(source[(row * sourceStride)..]);

                // The four prediction windows overlap, but each candidate owns one result lane. The operator
                // widens the source only once and reuses it for all four independent absolute-difference sums.
                totals += TOperator.SumFourAbsoluteDifferences(
                    sourceRow,
                    LoadSearchRow(predictionRow),
                    LoadSearchRow(predictionRow[1..]),
                    LoadSearchRow(predictionRow[2..]),
                    LoadSearchRow(predictionRow[3..]));
            }

            totals.CopyTo(sums);
        }
        else
        {
            int sum0 = 0;
            int sum1 = 0;
            int sum2 = 0;
            int sum3 = 0;
            for (int row = 0; row < SearchBlockDimension; row++)
            {
                ReadOnlySpan<TSample> sourceRow = source.Slice(row * sourceStride, SearchBlockDimension);
                ReadOnlySpan<TSample> predictionRow = prediction.Slice(row * predictionStride, SearchBlockDimension + 3);
                for (int column = 0; column < SearchBlockDimension; column++)
                {
                    TSample sample = sourceRow[column];
                    sum0 += TOperator.SumAbsoluteDifferences(sample, predictionRow[column]);
                    sum1 += TOperator.SumAbsoluteDifferences(sample, predictionRow[column + 1]);
                    sum2 += TOperator.SumAbsoluteDifferences(sample, predictionRow[column + 2]);
                    sum3 += TOperator.SumAbsoluteDifferences(sample, predictionRow[column + 3]);
                }
            }

            sums[0] = sum0;
            sums[1] = sum1;
            sums[2] = sum2;
            sums[3] = sum3;
        }
    }

    /// <summary>
    /// Accumulates both residual moments in one traversal without materializing a residual buffer.
    /// </summary>
    private static void GetMoments8x8<TSample, TOperator>(
        ReadOnlySpan<TSample> source, int sourceStride, ReadOnlySpan<TSample> prediction, int predictionStride, out int sum, out int sumOfSquares)
        where TSample : unmanaged
        where TOperator : struct, IResidualOperator<TSample>
    {
        sum = 0;
        sumOfSquares = 0;

        // Even 64 maximum twelve-bit residual squares fit in a signed int. Preserve the unnormalized
        // moments here; the caller applies the frame's precision-dependent rounding before deriving variance.
        if (Vector128.IsHardwareAccelerated)
        {
            // Sixty-four squared twelve-bit residuals reach 1073217600, so both totals stay in lanes
            // for the whole block and reduce once rather than once per row.
            Vector128<int> sums = Vector128<int>.Zero;
            Vector128<int> squares = Vector128<int>.Zero;
            for (int row = 0; row < SearchBlockDimension; row++)
            {
                TOperator.AccumulateMoments(
                    LoadSearchRow(source[(row * sourceStride)..]),
                    LoadSearchRow(prediction[(row * predictionStride)..]),
                    ref sums,
                    ref squares);
            }

            sum = Vector128.Sum(sums);
            sumOfSquares = Vector128.Sum(squares);
        }
        else
        {
            for (int row = 0; row < SearchBlockDimension; row++)
            {
                ReadOnlySpan<TSample> sourceRow = source.Slice(row * sourceStride, SearchBlockDimension);
                ReadOnlySpan<TSample> predictionRow = prediction.Slice(row * predictionStride, SearchBlockDimension);
                for (int column = 0; column < SearchBlockDimension; column++)
                {
                    sumOfSquares += TOperator.SumSquaredDifferences(sourceRow[column], predictionRow[column], out int difference);
                    sum += difference;
                }
            }
        }
    }

    /// <summary>
    /// Loads exactly eight native-order samples; byte rows occupy the lower half of the returned vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<TSample> LoadSearchRow<TSample>(ReadOnlySpan<TSample> source)
        where TSample : unmanaged
    {
        // The closed byte/ushort instantiation removes this storage-width choice. The eight-byte load
        // never consumes padding or a following row; the operator widens only its eight populated lanes.
        return Vector128<TSample>.Count == SearchBlockDimension
            ? Vector128.Create(source)
            : Vector128.Create(Vector64.Create(source), Vector64<TSample>.Zero);
    }

    /// <summary>
    /// Subtracts an 8-bit prediction plane from its source plane.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="prediction">The prediction samples.</param>
    /// <param name="predictionStride">The prediction row stride.</param>
    /// <param name="residual">The destination residual samples.</param>
    /// <param name="residualStride">The residual row stride.</param>
    /// <param name="width">The number of samples per row.</param>
    /// <param name="height">The number of rows.</param>
    public static void Subtract(
        ReadOnlySpan<byte> source,
        int sourceStride,
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        Span<short> residual,
        int residualStride,
        int width,
        int height)
    {
        Subtract<byte, ByteOperator>(source, sourceStride, prediction, predictionStride, residual, residualStride, width, height);
    }

    /// <summary>
    /// Subtracts a high-bit-depth prediction plane from its source plane.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="prediction">The prediction samples.</param>
    /// <param name="predictionStride">The prediction row stride.</param>
    /// <param name="residual">The destination residual samples.</param>
    /// <param name="residualStride">The residual row stride.</param>
    /// <param name="width">The number of samples per row.</param>
    /// <param name="height">The number of rows.</param>
    public static void Subtract(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        ReadOnlySpan<ushort> prediction,
        int predictionStride,
        Span<short> residual,
        int residualStride,
        int width,
        int height)
        => Subtract<ushort, UInt16Operator>(source, sourceStride, prediction, predictionStride, residual, residualStride, width, height);

    /// <summary>
    /// Replaces the residual of a transform block outside the frame with values derived from its visible part, so
    /// the transform codes no edge that the frame does not show. A two-dimensional transform takes the mean of the
    /// visible residual, and a one-dimensional transform takes the mean of each visible row or column along its
    /// identity direction. Reference: fill_residue_outside_frame().
    /// </summary>
    /// <param name="residual">The residual block.</param>
    /// <param name="residualStride">The residual row stride.</param>
    /// <param name="columns">The transform width.</param>
    /// <param name="rows">The transform height.</param>
    /// <param name="visibleColumns">The number of columns inside the frame.</param>
    /// <param name="visibleRows">The number of rows inside the frame.</param>
    /// <param name="transformType">The transform type that will code the block.</param>
    public static void FillResidueOutsideFrame(
        Span<short> residual,
        int residualStride,
        int columns,
        int rows,
        int visibleColumns,
        int visibleRows,
        Av1TransformType transformType)
    {
        bool completeBlockOutside = visibleColumns == 0 || visibleRows == 0;
        int rightPixels = columns - visibleColumns;
        if (transformType <= Av1TransformType.Identity)
        {
            short average = 0;
            if (transformType != Av1TransformType.Identity && !completeBlockOutside)
            {
                int sum = 0;
                for (int row = 0; row < visibleRows; row++)
                {
                    foreach (short value in residual.Slice(row * residualStride, visibleColumns))
                    {
                        sum += value;
                    }
                }

                average = (short)DivideAndRoundSigned(sum, visibleColumns * visibleRows);
            }

            for (int row = 0; row < rows; row++)
            {
                residual.Slice((row * residualStride) + visibleColumns, rightPixels).Fill(average);
            }

            for (int row = visibleRows; row < rows; row++)
            {
                residual.Slice(row * residualStride, visibleColumns).Fill(average);
            }

            return;
        }

        if (IsHorizontalIdentity(transformType))
        {
            // The rows are coded by the identity, so each hidden row repeats the mean of its visible column.
            if (visibleRows < rows)
            {
                for (int column = 0; column < visibleColumns; column++)
                {
                    short average = 0;
                    if (!completeBlockOutside)
                    {
                        int sum = 0;
                        for (int row = 0; row < visibleRows; row++)
                        {
                            sum += residual[(row * residualStride) + column];
                        }

                        average = (short)DivideAndRoundSigned(sum, visibleRows);
                    }

                    for (int row = visibleRows; row < rows; row++)
                    {
                        residual[(row * residualStride) + column] = average;
                    }
                }
            }

            if (rightPixels != 0)
            {
                for (int row = 0; row < rows; row++)
                {
                    residual.Slice((row * residualStride) + visibleColumns, rightPixels).Clear();
                }
            }

            return;
        }

        // The columns are coded by the identity, so each hidden column repeats the mean of its visible row.
        if (rightPixels != 0)
        {
            for (int row = 0; row < visibleRows; row++)
            {
                short average = 0;
                if (!completeBlockOutside)
                {
                    int sum = 0;
                    foreach (short value in residual.Slice(row * residualStride, visibleColumns))
                    {
                        sum += value;
                    }

                    average = (short)DivideAndRoundSigned(sum, visibleColumns);
                }

                residual.Slice((row * residualStride) + visibleColumns, rightPixels).Fill(average);
            }
        }

        for (int row = visibleRows; row < rows; row++)
        {
            residual.Slice(row * residualStride, columns).Clear();
        }
    }

    /// <summary>
    /// Divides and rounds half away from zero. Reference: DIVIDE_AND_ROUND_SIGNED.
    /// </summary>
    private static int DivideAndRoundSigned(int numerator, int denominator)
        => numerator < 0 ? (numerator - (denominator / 2)) / denominator : (numerator + (denominator / 2)) / denominator;

    /// <summary>
    /// Gets whether the horizontal one-dimensional transform of a type is the identity. Reference: htx_tab.
    /// </summary>
    private static bool IsHorizontalIdentity(Av1TransformType transformType)
        => transformType is Av1TransformType.VerticalDct or Av1TransformType.VerticalAdst or Av1TransformType.VerticalFlipAdst;

    /// <summary>
    /// Calculates the exact squared error between strided 8-bit sample planes.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="prediction">The prediction or reconstruction samples.</param>
    /// <param name="predictionStride">The prediction row stride.</param>
    /// <param name="width">The number of samples per row.</param>
    /// <param name="height">The number of rows.</param>
    /// <returns>The sum of squared sample differences.</returns>
    public static long SumSquaredError(
        ReadOnlySpan<byte> source,
        int sourceStride,
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        int width,
        int height)
    {
        return SumSquaredError<byte, ByteOperator>(source, sourceStride, prediction, predictionStride, width, height);
    }

    /// <summary>
    /// Calculates the exact squared error between strided high-bit-depth sample planes.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="prediction">The prediction or reconstruction samples.</param>
    /// <param name="predictionStride">The prediction row stride.</param>
    /// <param name="width">The number of samples per row.</param>
    /// <param name="height">The number of rows.</param>
    /// <returns>The sum of squared sample differences.</returns>
    public static long SumSquaredError(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        ReadOnlySpan<ushort> prediction,
        int predictionStride,
        int width,
        int height)
        => SumSquaredError<ushort, UInt16Operator>(source, sourceStride, prediction, predictionStride, width, height);

    /// <summary>
    /// Sums the squares of a contiguous signed residual block. Reference: aom_sum_squares_i16.
    /// </summary>
    /// <param name="residual">The residual samples.</param>
    /// <returns>The exact sum of squared sample differences.</returns>
    public static long SumSquares(ReadOnlySpan<short> residual)
        => SumSquares<ResidualSquaresOperator>(residual, residual.Length, residual.Length, 1);

    /// <summary>
    /// Sums the squares of a rectangle of a signed residual plane. Reference: aom_sum_squares_2d_i16.
    /// </summary>
    /// <param name="residual">The residual samples at the rectangle origin.</param>
    /// <param name="stride">The residual row stride.</param>
    /// <param name="width">The rectangle width.</param>
    /// <param name="height">The rectangle height.</param>
    /// <returns>The exact sum of squared sample differences.</returns>
    public static long SumSquares(ReadOnlySpan<short> residual, int stride, int width, int height)
        => SumSquares<ResidualSquaresOperator>(residual, stride, width, height);

    /// <summary>
    /// Sums a contiguous signed residual block and the squares of its samples.
    /// </summary>
    /// <remarks>
    /// This is <c>aom_sum_sse_2d_i16</c>, which <c>pixel_diff_stats</c> uses to derive the mean and the
    /// variance of one transform block's residual without a second pass over it.
    /// </remarks>
    /// <param name="residual">The residual samples.</param>
    /// <param name="sum">The exact sum of the samples.</param>
    /// <returns>The exact sum of squared samples.</returns>
    public static long SumAndSumSquares(ReadOnlySpan<short> residual, out long sum)
        => SumAndSumSquares<ResidualSquaresOperator>(residual, residual.Length, residual.Length, 1, out sum);

    /// <summary>
    /// Sums a rectangle of a signed residual plane and the squares of its samples. Reference:
    /// aom_get_blk_sse_sum.
    /// </summary>
    /// <param name="residual">The residual samples at the rectangle origin.</param>
    /// <param name="stride">The residual row stride.</param>
    /// <param name="width">The rectangle width.</param>
    /// <param name="height">The rectangle height.</param>
    /// <param name="sum">The exact sum of the samples.</param>
    /// <returns>The exact sum of squared samples.</returns>
    public static long SumAndSumSquares(ReadOnlySpan<short> residual, int stride, int width, int height, out long sum)
        => SumAndSumSquares<ResidualSquaresOperator>(residual, stride, width, height, out sum);

    /// <summary>
    /// Traverses the square sum of a residual rectangle at descending register widths.
    /// </summary>
    private static long SumSquares<TOperator>(ReadOnlySpan<short> residual, int stride, int width, int height)
        where TOperator : struct, IResidualSquaresOperator
    {
        // The slice checks every bound once, so the rows below can load by reference.
        ref short residualBase = ref MemoryMarshal.GetReference(residual[..(height == 0 ? 0 : ((height - 1) * stride) + width)]);

        // The squares accumulate in 64-bit lanes and reduce once at the end. A horizontal sum inside
        // the loop costs a chain of shuffles and adds, which is more than the lane work it reduces,
        // and a 32-bit lane would overflow after 64 vectors of twelve-bit residuals.
        Vector512<long> total512 = Vector512<long>.Zero;
        Vector256<long> total256 = Vector256<long>.Zero;
        Vector128<long> total128 = Vector128<long>.Zero;
        long sum = 0;
        for (int y = 0; y < height; y++)
        {
            ref short rowBase = ref Unsafe.Add(ref residualBase, y * stride);
            int x = 0;
            if (Vector512.IsHardwareAccelerated)
            {
                for (; x <= width - Vector512<short>.Count; x += Vector512<short>.Count)
                {
                    total512 = TOperator.AccumulateSquares(Vector512.LoadUnsafe(ref rowBase, (nuint)x), total512);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x <= width - Vector256<short>.Count; x += Vector256<short>.Count)
                {
                    total256 = TOperator.AccumulateSquares(Vector256.LoadUnsafe(ref rowBase, (nuint)x), total256);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x <= width - Vector128<short>.Count; x += Vector128<short>.Count)
                {
                    total128 = TOperator.AccumulateSquares(Vector128.LoadUnsafe(ref rowBase, (nuint)x), total128);
                }
            }

            for (; x < width; x++)
            {
                sum = TOperator.AccumulateSquares(Unsafe.Add(ref rowBase, x), sum);
            }
        }

        total256 += total512.GetLower() + total512.GetUpper();
        total128 += total256.GetLower() + total256.GetUpper();
        return sum + Vector128.Sum(total128);
    }

    /// <summary>
    /// Traverses the sum and the square sum of a residual rectangle at descending register widths.
    /// </summary>
    private static long SumAndSumSquares<TOperator>(ReadOnlySpan<short> residual, int stride, int width, int height, out long sum)
        where TOperator : struct, IResidualSquaresOperator
    {
        ref short residualBase = ref MemoryMarshal.GetReference(residual[..(height == 0 ? 0 : ((height - 1) * stride) + width)]);

        // Both totals accumulate in 64-bit lanes and reduce once at the end, for the reason the
        // square sum gives: a reduction belongs outside the loop, and a 32-bit lane is too narrow
        // for a complete encoder block.
        Vector512<long> squares512 = Vector512<long>.Zero;
        Vector256<long> squares256 = Vector256<long>.Zero;
        Vector128<long> squares128 = Vector128<long>.Zero;
        Vector512<long> sum512 = Vector512<long>.Zero;
        Vector256<long> sum256 = Vector256<long>.Zero;
        Vector128<long> sum128 = Vector128<long>.Zero;
        long sumOfSquares = 0;
        long total = 0;
        for (int y = 0; y < height; y++)
        {
            ref short rowBase = ref Unsafe.Add(ref residualBase, y * stride);
            int x = 0;
            if (Vector512.IsHardwareAccelerated)
            {
                for (; x <= width - Vector512<short>.Count; x += Vector512<short>.Count)
                {
                    Vector512<short> values = Vector512.LoadUnsafe(ref rowBase, (nuint)x);
                    sum512 = TOperator.AccumulateSum(values, sum512);
                    squares512 = TOperator.AccumulateSquares(values, squares512);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x <= width - Vector256<short>.Count; x += Vector256<short>.Count)
                {
                    Vector256<short> values = Vector256.LoadUnsafe(ref rowBase, (nuint)x);
                    sum256 = TOperator.AccumulateSum(values, sum256);
                    squares256 = TOperator.AccumulateSquares(values, squares256);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x <= width - Vector128<short>.Count; x += Vector128<short>.Count)
                {
                    Vector128<short> values = Vector128.LoadUnsafe(ref rowBase, (nuint)x);
                    sum128 = TOperator.AccumulateSum(values, sum128);
                    squares128 = TOperator.AccumulateSquares(values, squares128);
                }
            }

            for (; x < width; x++)
            {
                short value = Unsafe.Add(ref rowBase, x);
                total = TOperator.AccumulateSum(value, total);
                sumOfSquares = TOperator.AccumulateSquares(value, sumOfSquares);
            }
        }

        sum256 += sum512.GetLower() + sum512.GetUpper();
        sum128 += sum256.GetLower() + sum256.GetUpper();
        squares256 += squares512.GetLower() + squares512.GetUpper();
        squares128 += squares256.GetLower() + squares256.GetUpper();
        sum = total + Vector128.Sum(sum128);
        return sumOfSquares + Vector128.Sum(squares128);
    }

    private static long SumSquaredError<TSample, TOperator>(
        ReadOnlySpan<TSample> source,
        int sourceStride,
        ReadOnlySpan<TSample> prediction,
        int predictionStride,
        int width,
        int height)
        where TSample : unmanaged
        where TOperator : struct, IResidualOperator<TSample>
    {
        ref TSample sourceBase = ref MemoryMarshal.GetReference(source);
        ref TSample predictionBase = ref MemoryMarshal.GetReference(prediction);

        // Every row of a block has the same width, so the stage boundaries are taken once rather
        // than per row.
        int end512 = width - Vector512<short>.Count;
        int end256 = width - Vector256<short>.Count;
        int end128 = width - Vector128<short>.Count;
        bool use512 = Vector512.IsHardwareAccelerated && end512 >= 0;
        bool use256 = Vector256.IsHardwareAccelerated && end256 >= 0;
        bool use128 = Vector128.IsHardwareAccelerated && end128 >= 0;
        long total = 0;

        for (int y = 0; y < height; y++)
        {
            // The rows are addressed by offset rather than sliced. A transform block is small, so
            // constructing two spans and recomputing their vector counts for every row costs more
            // than the row of arithmetic it guards.
            ref TSample sourceRow = ref Unsafe.Add(ref sourceBase, y * sourceStride);
            ref TSample predictionRow = ref Unsafe.Add(ref predictionBase, y * predictionStride);
            int x = 0;

            // The squares accumulate in lanes and fold once per row. A row holds at most 64 samples
            // and a squared difference of twelve-bit samples reaches 16769025, so a row total stays
            // inside a 32-bit lane at either sample depth. Carrying the lanes across the whole block
            // was measured and made no difference, so the simpler bound stands.
            if (use512)
            {
                Vector512<int> squares = Vector512<int>.Zero;
                for (; x <= end512; x += Vector512<short>.Count)
                {
                    Vector512<short> difference = TOperator.LoadDifference(ref sourceRow, ref predictionRow, x, Vector512<short>.Zero);
                    squares += Vector512_.MultiplyAddAdjacent(difference, difference);
                }

                total += Vector512.Sum(squares);
            }

            if (use256 && x <= end256)
            {
                Vector256<int> squares = Vector256<int>.Zero;
                for (; x <= end256; x += Vector256<short>.Count)
                {
                    Vector256<short> difference = TOperator.LoadDifference(ref sourceRow, ref predictionRow, x, Vector256<short>.Zero);
                    squares += Vector256_.MultiplyAddAdjacent(difference, difference);
                }

                total += Vector256.Sum(squares);
            }

            if (use128 && x <= end128)
            {
                Vector128<int> squares = Vector128<int>.Zero;
                for (; x <= end128; x += Vector128<short>.Count)
                {
                    Vector128<short> difference = TOperator.LoadDifference(ref sourceRow, ref predictionRow, x, Vector128<short>.Zero);
                    squares += Vector128_.MultiplyAddAdjacent(difference, difference);
                }

                total += Vector128.Sum(squares);
            }

            for (; x < width; x++)
            {
                int difference = TOperator.Subtract(Unsafe.Add(ref sourceRow, x), Unsafe.Add(ref predictionRow, x));
                total += difference * difference;
            }
        }

        return total;
    }

    private static void Subtract<TSample, TOperator>(
        ReadOnlySpan<TSample> source,
        int sourceStride,
        ReadOnlySpan<TSample> prediction,
        int predictionStride,
        Span<short> residual,
        int residualStride,
        int width,
        int height)
        where TSample : unmanaged
        where TOperator : struct, IResidualOperator<TSample>
    {
        ref TSample sourceBase = ref MemoryMarshal.GetReference(source);
        ref TSample predictionBase = ref MemoryMarshal.GetReference(prediction);
        ref short residualBase = ref MemoryMarshal.GetReference(residual);

        // Every row of a block has the same width, so which stages run, and where each one stops,
        // are settled once rather than per row. One vector of sixteen-bit lanes is one vector of
        // residuals whatever the sample depth, so a row of eight samples fills a vector and no
        // transform width falls to the scalar loop on its own.
        int end512 = width - Vector512<short>.Count;
        int end256 = width - Vector256<short>.Count;
        int end128 = width - Vector128<short>.Count;
        bool use512 = Vector512.IsHardwareAccelerated && end512 >= 0;
        bool use256 = Vector256.IsHardwareAccelerated && end256 >= 0;
        bool use128 = Vector128.IsHardwareAccelerated && end128 >= 0;

        for (int y = 0; y < height; y++)
        {
            // The rows are addressed by offset for the same reason the squared error addresses
            // them that way: a transform row is short, so per-row span work dominates.
            ref TSample sourceRow = ref Unsafe.Add(ref sourceBase, y * sourceStride);
            ref TSample predictionRow = ref Unsafe.Add(ref predictionBase, y * predictionStride);
            ref short residualRow = ref Unsafe.Add(ref residualBase, y * residualStride);
            int x = 0;

            if (use512)
            {
                for (; x <= end512; x += Vector512<short>.Count)
                {
                    TOperator.LoadDifference(ref sourceRow, ref predictionRow, x, Vector512<short>.Zero)
                        .StoreUnsafe(ref residualRow, (nuint)x);
                }
            }

            if (use256 && x <= end256)
            {
                for (; x <= end256; x += Vector256<short>.Count)
                {
                    TOperator.LoadDifference(ref sourceRow, ref predictionRow, x, Vector256<short>.Zero)
                        .StoreUnsafe(ref residualRow, (nuint)x);
                }
            }

            if (use128 && x <= end128)
            {
                for (; x <= end128; x += Vector128<short>.Count)
                {
                    TOperator.LoadDifference(ref sourceRow, ref predictionRow, x, Vector128<short>.Zero)
                        .StoreUnsafe(ref residualRow, (nuint)x);
                }
            }

            for (; x < width; x++)
            {
                Unsafe.Add(ref residualRow, x) = TOperator.Subtract(Unsafe.Add(ref sourceRow, x), Unsafe.Add(ref predictionRow, x));
            }
        }
    }
}
