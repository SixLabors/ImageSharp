// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

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
                    Vector512<TSample> sourceVector = Vector512.LoadUnsafe(ref sourceBase, (nuint)x);
                    Vector512<TSample> predictionVector = Vector512.LoadUnsafe(ref predictionBase, (nuint)x);
                    Vector512<short> lower = TOperator.Subtract(sourceVector, predictionVector, out Vector512<short> upper);

                    // Absolute residuals fit short, but their horizontal sum may not. Widen before reducing.
                    Vector512<short> absolute = Vector512.Abs(lower);
                    sum += Vector512.Sum(Vector512.WidenLower(absolute)) + Vector512.Sum(Vector512.WidenUpper(absolute));
                    if (Vector512<TSample>.Count != Vector512<short>.Count)
                    {
                        // Byte subtraction produces two widened halves; word subtraction has only the lower half.
                        absolute = Vector512.Abs(upper);
                        sum += Vector512.Sum(Vector512.WidenLower(absolute)) + Vector512.Sum(Vector512.WidenUpper(absolute));
                    }
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x <= width - Vector256<TSample>.Count; x += Vector256<TSample>.Count)
                {
                    Vector256<TSample> sourceVector = Vector256.LoadUnsafe(ref sourceBase, (nuint)x);
                    Vector256<TSample> predictionVector = Vector256.LoadUnsafe(ref predictionBase, (nuint)x);
                    Vector256<short> lower = TOperator.Subtract(sourceVector, predictionVector, out Vector256<short> upper);

                    // Absolute residuals fit short, but their horizontal sum may not. Widen before reducing.
                    Vector256<short> absolute = Vector256.Abs(lower);
                    sum += Vector256.Sum(Vector256.WidenLower(absolute)) + Vector256.Sum(Vector256.WidenUpper(absolute));
                    if (Vector256<TSample>.Count != Vector256<short>.Count)
                    {
                        // Byte subtraction produces two widened halves; word subtraction has only the lower half.
                        absolute = Vector256.Abs(upper);
                        sum += Vector256.Sum(Vector256.WidenLower(absolute)) + Vector256.Sum(Vector256.WidenUpper(absolute));
                    }
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x <= width - Vector128<TSample>.Count; x += Vector128<TSample>.Count)
                {
                    Vector128<TSample> sourceVector = Vector128.LoadUnsafe(ref sourceBase, (nuint)x);
                    Vector128<TSample> predictionVector = Vector128.LoadUnsafe(ref predictionBase, (nuint)x);
                    Vector128<short> lower = TOperator.Subtract(sourceVector, predictionVector, out Vector128<short> upper);

                    // Absolute residuals fit short, but their horizontal sum may not. Widen before reducing.
                    Vector128<short> absolute = Vector128.Abs(lower);
                    sum += Vector128.Sum(Vector128.WidenLower(absolute)) + Vector128.Sum(Vector128.WidenUpper(absolute));
                    if (Vector128<TSample>.Count != Vector128<short>.Count)
                    {
                        // Byte subtraction produces two widened halves; word subtraction has only the lower half.
                        absolute = Vector128.Abs(upper);
                        sum += Vector128.Sum(Vector128.WidenLower(absolute)) + Vector128.Sum(Vector128.WidenUpper(absolute));
                    }
                }
            }

            if (Vector128.IsHardwareAccelerated && x <= width - SearchBlockDimension)
            {
                Vector128<TSample> sourceVector = LoadSearchRow(sourceRow[x..]);
                Vector128<TSample> predictionVector = LoadSearchRow(predictionRow[x..]);
                sum += TOperator.SumAbsoluteDifferences(sourceVector, predictionVector);
                x += SearchBlockDimension;
            }

            for (; x < width; x++)
            {
                sum += TOperator.SumAbsoluteDifferences(sourceRow[x], predictionRow[x]);
            }
        }

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

        // Wider blocks use complete native loads. The eight-sample tail retains the existing compact load,
        // which reads eight bytes or eight words without crossing a short row's boundary.
        for (int y = 0; y < height; y++)
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
                    Vector512<TSample> sourceVector = Vector512.LoadUnsafe(ref sourceBase, (nuint)x);
                    Vector512<TSample> predictionVector = Vector512.LoadUnsafe(ref predictionBase, (nuint)x);
                    Vector512<short> lower = TOperator.Subtract(sourceVector, predictionVector, out Vector512<short> upper);

                    // Widen signed lanes before summing: a vector of twelve-bit residuals can exceed short.
                    // Each vector's squared sum fits int; the block total needs long for large twelve-bit blocks.
                    sum += Vector512.Sum(Vector512.WidenLower(lower)) + Vector512.Sum(Vector512.WidenUpper(lower));
                    sumOfSquares += SumSquares(lower);
                    if (Vector512<TSample>.Count != Vector512<short>.Count)
                    {
                        // Byte subtraction produces two widened halves; word subtraction has only the lower half.
                        sum += Vector512.Sum(Vector512.WidenLower(upper)) + Vector512.Sum(Vector512.WidenUpper(upper));
                        sumOfSquares += SumSquares(upper);
                    }
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x <= width - Vector256<TSample>.Count; x += Vector256<TSample>.Count)
                {
                    Vector256<TSample> sourceVector = Vector256.LoadUnsafe(ref sourceBase, (nuint)x);
                    Vector256<TSample> predictionVector = Vector256.LoadUnsafe(ref predictionBase, (nuint)x);
                    Vector256<short> lower = TOperator.Subtract(sourceVector, predictionVector, out Vector256<short> upper);

                    // Widen signed lanes before summing: a vector of twelve-bit residuals can exceed short.
                    // Each vector's squared sum fits int; the block total needs long for large twelve-bit blocks.
                    sum += Vector256.Sum(Vector256.WidenLower(lower)) + Vector256.Sum(Vector256.WidenUpper(lower));
                    sumOfSquares += SumSquares(lower);
                    if (Vector256<TSample>.Count != Vector256<short>.Count)
                    {
                        // Byte subtraction produces two widened halves; word subtraction has only the lower half.
                        sum += Vector256.Sum(Vector256.WidenLower(upper)) + Vector256.Sum(Vector256.WidenUpper(upper));
                        sumOfSquares += SumSquares(upper);
                    }
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x <= width - Vector128<TSample>.Count; x += Vector128<TSample>.Count)
                {
                    Vector128<TSample> sourceVector = Vector128.LoadUnsafe(ref sourceBase, (nuint)x);
                    Vector128<TSample> predictionVector = Vector128.LoadUnsafe(ref predictionBase, (nuint)x);
                    Vector128<short> lower = TOperator.Subtract(sourceVector, predictionVector, out Vector128<short> upper);

                    // Widen signed lanes before summing: a vector of twelve-bit residuals can exceed short.
                    // Each vector's squared sum fits int; the block total needs long for large twelve-bit blocks.
                    sum += Vector128.Sum(Vector128.WidenLower(lower)) + Vector128.Sum(Vector128.WidenUpper(lower));
                    sumOfSquares += SumSquares(lower);
                    if (Vector128<TSample>.Count != Vector128<short>.Count)
                    {
                        // Byte subtraction produces two widened halves; word subtraction has only the lower half.
                        sum += Vector128.Sum(Vector128.WidenLower(upper)) + Vector128.Sum(Vector128.WidenUpper(upper));
                        sumOfSquares += SumSquares(upper);
                    }
                }
            }

            if (Vector128.IsHardwareAccelerated && x <= width - SearchBlockDimension)
            {
                Vector128<TSample> sourceVector = LoadSearchRow(sourceRow[x..]);
                Vector128<TSample> predictionVector = LoadSearchRow(predictionRow[x..]);
                sumOfSquares += TOperator.SumSquaredDifferences(sourceVector, predictionVector, out int tailSum);
                sum += tailSum;
                x += SearchBlockDimension;
            }

            for (; x < width; x++)
            {
                int difference = TOperator.Subtract(sourceRow[x], predictionRow[x]);
                sum += difference;
                sumOfSquares += difference * difference;
            }
        }
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
            for (int row = 0; row < SearchBlockDimension; row++)
            {
                Vector128<TSample> sourceRow = LoadSearchRow(source[(row * sourceStride)..]);
                Vector128<TSample> predictionRow = LoadSearchRow(prediction[(row * predictionStride)..]);
                sum += TOperator.SumAbsoluteDifferences(sourceRow, predictionRow);
            }
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
            for (int row = 0; row < SearchBlockDimension; row++)
            {
                Vector128<TSample> sourceRow = LoadSearchRow(source[(row * sourceStride)..]);
                Vector128<TSample> predictionRow = LoadSearchRow(prediction[(row * predictionStride)..]);
                sumOfSquares += TOperator.SumSquaredDifferences(sourceRow, predictionRow, out int rowSum);
                sum += rowSum;
            }
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
    /// Sums the squares of a contiguous signed residual block.
    /// </summary>
    /// <param name="residual">The residual samples.</param>
    /// <returns>The exact sum of squared sample differences.</returns>
    public static long SumSquares(ReadOnlySpan<short> residual)
    {
        ref short residualBase = ref MemoryMarshal.GetReference(residual);
        long sum = 0;
        int offset = 0;

        // Each short lane widens before multiplication, preserving the full 12-bit residual square.
        // The accumulated scalar is 64-bit because a complete encoder block can exceed 32-bit range.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint vectorCount = residual.Vector512Count<short>();

            for (; vectorCount > 0; vectorCount--, offset += Vector512<short>.Count)
            {
                Vector512<short> values = Unsafe.As<short, Vector512<short>>(ref Unsafe.Add(ref residualBase, offset));
                sum += SumSquares(values);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint vectorCount = residual[offset..].Vector256Count<short>();

            for (; vectorCount > 0; vectorCount--, offset += Vector256<short>.Count)
            {
                Vector256<short> values = Unsafe.As<short, Vector256<short>>(ref Unsafe.Add(ref residualBase, offset));
                sum += SumSquares(values);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint vectorCount = residual[offset..].Vector128Count<short>();

            for (; vectorCount > 0; vectorCount--, offset += Vector128<short>.Count)
            {
                Vector128<short> values = Unsafe.As<short, Vector128<short>>(ref Unsafe.Add(ref residualBase, offset));
                sum += SumSquares(values);
            }
        }

        for (; offset < residual.Length; offset++)
        {
            int value = Unsafe.Add(ref residualBase, offset);
            sum += value * value;
        }

        return sum;
    }

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
    {
        ref short residualBase = ref MemoryMarshal.GetReference(residual);
        long sumOfSquares = 0;
        long total = 0;
        int offset = 0;

        // Each short lane widens before multiplication, preserving the full 12-bit residual square.
        // The accumulated scalars are 64-bit because a complete encoder block can exceed 32-bit range.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint vectorCount = residual.Vector512Count<short>();

            for (; vectorCount > 0; vectorCount--, offset += Vector512<short>.Count)
            {
                Vector512<short> values = Unsafe.As<short, Vector512<short>>(ref Unsafe.Add(ref residualBase, offset));
                total += Vector512.Sum(Vector512.WidenLower(values)) + Vector512.Sum(Vector512.WidenUpper(values));
                sumOfSquares += SumSquares(values);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint vectorCount = residual[offset..].Vector256Count<short>();

            for (; vectorCount > 0; vectorCount--, offset += Vector256<short>.Count)
            {
                Vector256<short> values = Unsafe.As<short, Vector256<short>>(ref Unsafe.Add(ref residualBase, offset));
                total += Vector256.Sum(Vector256.WidenLower(values)) + Vector256.Sum(Vector256.WidenUpper(values));
                sumOfSquares += SumSquares(values);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint vectorCount = residual[offset..].Vector128Count<short>();

            for (; vectorCount > 0; vectorCount--, offset += Vector128<short>.Count)
            {
                Vector128<short> values = Unsafe.As<short, Vector128<short>>(ref Unsafe.Add(ref residualBase, offset));
                total += Vector128.Sum(Vector128.WidenLower(values)) + Vector128.Sum(Vector128.WidenUpper(values));
                sumOfSquares += SumSquares(values);
            }
        }

        for (; offset < residual.Length; offset++)
        {
            int value = Unsafe.Add(ref residualBase, offset);
            total += value;
            sumOfSquares += value * value;
        }

        sum = total;
        return sumOfSquares;
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
                    (Vector512<int> lower, Vector512<int> upper) = Vector512.Widen(
                        TOperator.LoadDifference(ref sourceRow, ref predictionRow, x, Vector512<short>.Zero));

                    squares += (lower * lower) + (upper * upper);
                }

                total += Vector512.Sum(squares);
            }

            if (use256 && x <= end256)
            {
                Vector256<int> squares = Vector256<int>.Zero;
                for (; x <= end256; x += Vector256<short>.Count)
                {
                    (Vector256<int> lower, Vector256<int> upper) = Vector256.Widen(
                        TOperator.LoadDifference(ref sourceRow, ref predictionRow, x, Vector256<short>.Zero));

                    squares += (lower * lower) + (upper * upper);
                }

                total += Vector256.Sum(squares);
            }

            if (use128 && x <= end128)
            {
                Vector128<int> squares = Vector128<int>.Zero;
                for (; x <= end128; x += Vector128<short>.Count)
                {
                    (Vector128<int> lower, Vector128<int> upper) = Vector128.Widen(
                        TOperator.LoadDifference(ref sourceRow, ref predictionRow, x, Vector128<short>.Zero));

                    squares += (lower * lower) + (upper * upper);
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

        for (int y = 0; y < height; y++)
        {
            // The rows are addressed by offset for the same reason the squared error addresses
            // them that way: a transform row is short, so per-row span work dominates.
            ref TSample sourceRow = ref Unsafe.Add(ref sourceBase, y * sourceStride);
            ref TSample predictionRow = ref Unsafe.Add(ref predictionBase, y * predictionStride);
            ref short residualRow = ref Unsafe.Add(ref residualBase, y * residualStride);
            int x = 0;

            // One vector of sixteen-bit lanes is one vector of residuals whatever the sample depth,
            // so a row of eight samples fills a vector and no transform width falls to the scalar
            // loop on its own.
            if (Vector512.IsHardwareAccelerated && width >= Vector512<short>.Count)
            {
                for (; x <= width - Vector512<short>.Count; x += Vector512<short>.Count)
                {
                    TOperator.LoadDifference(ref sourceRow, ref predictionRow, x, Vector512<short>.Zero)
                        .StoreUnsafe(ref residualRow, (nuint)x);
                }
            }

            if (Vector256.IsHardwareAccelerated && width - x >= Vector256<short>.Count)
            {
                for (; x <= width - Vector256<short>.Count; x += Vector256<short>.Count)
                {
                    TOperator.LoadDifference(ref sourceRow, ref predictionRow, x, Vector256<short>.Zero)
                        .StoreUnsafe(ref residualRow, (nuint)x);
                }
            }

            if (Vector128.IsHardwareAccelerated && width - x >= Vector128<short>.Count)
            {
                for (; x <= width - Vector128<short>.Count; x += Vector128<short>.Count)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long SumSquares(Vector128<short> values)
    {
        Vector128<int> lower = Vector128.WidenLower(values);
        Vector128<int> upper = Vector128.WidenUpper(values);
        return (long)Vector128.Sum(lower * lower) + Vector128.Sum(upper * upper);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long SumSquares(Vector256<short> values)
    {
        Vector256<int> lower = Vector256.WidenLower(values);
        Vector256<int> upper = Vector256.WidenUpper(values);
        return (long)Vector256.Sum(lower * lower) + Vector256.Sum(upper * upper);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long SumSquares(Vector512<short> values)
    {
        Vector512<int> lower = Vector512.WidenLower(values);
        Vector512<int> upper = Vector512.WidenUpper(values);
        return (long)Vector512.Sum(lower * lower) + Vector512.Sum(upper * upper);
    }
}
