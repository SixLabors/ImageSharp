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
    /// Measures absolute prediction error over a rectangular block. It can measure every second row only.
    /// </summary>
    /// <param name="source">Source samples beginning at the block origin.</param>
    /// <param name="sourceStride">The source row stride, in samples.</param>
    /// <param name="prediction">Prediction samples beginning at the block origin.</param>
    /// <param name="predictionStride">The prediction row stride, in samples.</param>
    /// <param name="width">The block width, in samples.</param>
    /// <param name="height">The block height, in samples.</param>
    /// <param name="rowStep">One for every row, or two for every second row with a doubled error.</param>
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
    /// Measures absolute prediction error over a rectangular high bit depth block. It can measure every second row only.
    /// </summary>
    /// <param name="source">Source samples beginning at the block origin.</param>
    /// <param name="sourceStride">The source row stride, in samples.</param>
    /// <param name="prediction">Prediction samples beginning at the block origin.</param>
    /// <param name="predictionStride">The prediction row stride, in samples.</param>
    /// <param name="width">The block width, in samples.</param>
    /// <param name="height">The block height, in samples.</param>
    /// <param name="rowStep">One for every row, or two for every second row with a doubled error.</param>
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
    /// Traverses rectangular SAD candidates with the selected sample operator and descending vector widths.
    /// </summary>
    /// <typeparam name="TSample">The source and prediction sample type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="source">Source samples beginning at the block origin.</param>
    /// <param name="sourceStride">The source row stride, in samples.</param>
    /// <param name="prediction">Prediction samples beginning at the block origin.</param>
    /// <param name="predictionStride">The prediction row stride, in samples.</param>
    /// <param name="width">The block width, in samples.</param>
    /// <param name="height">The block height, in samples.</param>
    /// <param name="rowStep">One for every row, or two for every second row with a doubled error.</param>
    /// <returns>The unnormalized absolute difference over the block.</returns>
    public static int SumAbsoluteDifferences<TSample, TOperator>(
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
        // The running total stays in lanes for the whole block, and one reduction at the end gives the sum. One 32-bit lane can take
        // a 12-bit difference more than a million times. The largest AV1 block holds 16384 samples, so the block needs no intermediate fold.
        Vector512<uint> total512 = Vector512<uint>.Zero;
        Vector256<uint> total256 = Vector256<uint>.Zero;
        Vector128<uint> total128 = Vector128<uint>.Zero;
        int sum = 0;
        int y = 0;
        if (width == 4 && Vector128.IsHardwareAccelerated)
        {
            // A row of four samples fills only half a vector, so this path takes two sampled rows at a time. The second row of a pair
            // is one row step below the first. The search can sample every second row, so a row step can be two rows.
            Vector128<short> ones = Vector128.Create((short)1);
            ref TSample sourceStart = ref MemoryMarshal.GetReference(source);
            ref TSample predictionStart = ref MemoryMarshal.GetReference(prediction);
            nuint sourceStep = (nuint)(rowStep * sourceStride);
            nuint predictionStep = (nuint)(rowStep * predictionStride);
            for (; y + rowStep < height; y += 2 * rowStep)
            {
                Vector128<short> difference = TOperator.LoadDifferenceRowPair(
                    ref Unsafe.Add(ref sourceStart, (nuint)y * (nuint)sourceStride),
                    sourceStep,
                    ref Unsafe.Add(ref predictionStart, (nuint)y * (nuint)predictionStride),
                    predictionStep);

                // The vector holds the four differences of the first row, then the four of the second row. Abs makes each difference
                // positive. A multiply-add with ones adds each pair of neighbors into one 32-bit lane.
                total128 += Vector128_.MultiplyAddAdjacent(Vector128.Abs(difference), ones).AsUInt32();
            }
        }

        // Wider blocks use full native loads. A tail of eight samples uses the compact load. This load reads eight bytes or eight words
        // and does not read past the end of a short row.
        for (; y < height; y += rowStep)
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

            // A row of eight bytes fills half of a byte vector, so the compact load pads it with zeros. The padding is the same on both
            // sides, so its difference is zero and it adds nothing to the total.
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

        // The fold of the wide totals costs four adds and no branch. A width that the hardware does not have holds a zero vector,
        // so the unused stages add nothing to the result.
        total256 += total512.GetLower() + total512.GetUpper();
        total128 += total256.GetLower() + total256.GetUpper();
        sum += (int)Vector128.Sum(total128);

        // When the search reads every second row, the doubled total estimates the full block of even height. A precision normalization
        // comes after this scaling, so the truncation of fractional error units happens only once.
        return sum * rowStep;
    }

    /// <summary>
    /// Accumulates rectangular residual moments without storing an intermediate residual plane.
    /// </summary>
    /// <typeparam name="TSample">The source and prediction sample type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="source">Source samples beginning at the block origin.</param>
    /// <param name="sourceStride">The source row stride, in samples.</param>
    /// <param name="prediction">Prediction samples beginning at the block origin.</param>
    /// <param name="predictionStride">The prediction row stride, in samples.</param>
    /// <param name="width">The block width, in samples.</param>
    /// <param name="height">The block height, in samples.</param>
    /// <param name="sum">The unnormalized signed residual sum.</param>
    /// <param name="sumOfSquares">The unnormalized squared residual sum.</param>
    public static void GetMoments<TSample, TOperator>(
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

        // The signed total stays in lanes for the whole block. One lane takes two differences for each vector. A lane therefore gains
        // at most 8190 for each vector, and it cannot overflow inside any AV1 block.
        Vector512<int> sum512 = Vector512<int>.Zero;
        Vector256<int> sum256 = Vector256<int>.Zero;
        Vector128<int> sum128 = Vector128<int>.Zero;
        int y = 0;
        if (width == 4 && height <= 64 && Vector128.IsHardwareAccelerated)
        {
            // A row of four samples fills only half a vector, so this path takes two rows at a time. Each pair gives one vector of eight
            // differences. The path collects the sum of the differences and the sum of their squares. The variance uses both totals.
            //
            // The largest difference at 12 bits is 4095, so one lane gains at most 2 * 4095 * 4095 = 33538050 for each row pair.
            // A block of 64 rows has 32 pairs, so a lane stays below 2^31. An odd last row goes to the row loop.
            Vector128<short> ones = Vector128.Create((short)1);
            Vector128<int> squares = Vector128<int>.Zero;
            ref TSample sourceStart = ref MemoryMarshal.GetReference(source);
            ref TSample predictionStart = ref MemoryMarshal.GetReference(prediction);
            nuint sourceStep = (nuint)sourceStride;
            nuint predictionStep = (nuint)predictionStride;
            for (; y + 1 < height; y += 2)
            {
                Vector128<short> difference = TOperator.LoadDifferenceRowPair(
                    ref Unsafe.Add(ref sourceStart, (nuint)y * sourceStep),
                    sourceStep,
                    ref Unsafe.Add(ref predictionStart, (nuint)y * predictionStep),
                    predictionStep);

                // A multiply-add with ones adds each pair of neighbor differences into one 32-bit lane. A multiply-add of the vector with
                // itself squares each difference and adds each pair of neighbor squares.
                sum128 += Vector128_.MultiplyAddAdjacent(difference, ones);
                squares += Vector128_.MultiplyAddAdjacent(difference, difference);
            }

            // One lane fits in 32 bits, but the total of four lanes can be more than 2^32. The lanes therefore widen to 64 bits before
            // the final sum.
            Vector128<uint> squareLanes = squares.AsUInt32();
            sumOfSquares = (long)Vector128.Sum(Vector128.WidenLower(squareLanes) + Vector128.WidenUpper(squareLanes));
        }

        // Wider blocks use full native loads. A tail of eight samples uses the compact load. This load reads eight bytes or eight words
        // and does not read past the end of a short row.
        for (; y < height; y++)
        {
            ReadOnlySpan<TSample> sourceRow = source.Slice(y * sourceStride, width);
            ReadOnlySpan<TSample> predictionRow = prediction.Slice(y * predictionStride, width);
            ref TSample sourceBase = ref MemoryMarshal.GetReference(sourceRow);
            ref TSample predictionBase = ref MemoryMarshal.GetReference(predictionRow);
            int x = 0;

            // The squared total folds at the end of each row. A lane takes two squares for each vector, so it gains at most 33538050 for
            // each vector and overflows after 64 vectors. A row of the widest AV1 block is 16 vectors at the narrowest width.
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

            // The padding of the compact load is the same on both sides, so it adds a zero difference and a zero square.
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
    /// Loads exactly eight native-order samples. Byte rows use the lower half of the returned vector, and the upper half is zero.
    /// </summary>
    /// <typeparam name="TSample">The sample type.</typeparam>
    /// <param name="source">The row, starting at the first sample to load.</param>
    /// <returns>The eight samples in increasing column order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<TSample> LoadSearchRow<TSample>(ReadOnlySpan<TSample> source)
        where TSample : unmanaged
    {
        // The JIT removes this branch for each closed sample type. The 8-byte load for bytes never reads padding or the next row.
        // The operator widens only the eight loaded lanes.
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
    /// Replaces the residual of a transform block outside the frame with values derived from its visible part. The transform then codes
    /// no edge that the frame does not show. A two-dimensional transform takes the mean of the visible residual. A one-dimensional
    /// transform takes the mean of each visible row or column along its identity direction. The plain identity transform takes zero.
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
                    sum += SumSamples(residual.Slice(row * residualStride, visibleColumns));
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
            // The identity codes each row, so each hidden row repeats the mean of each visible column. Hidden columns get zero.
            if (visibleRows < rows)
            {
                // A transform is at most 64 samples wide.
                Span<short> averages = stackalloc short[64];
                averages = averages[..visibleColumns];
                averages.Clear();
                if (!completeBlockOutside)
                {
                    Span<int> sums = stackalloc int[64];
                    sums = sums[..visibleColumns];
                    sums.Clear();
                    for (int row = 0; row < visibleRows; row++)
                    {
                        AddColumns(residual.Slice(row * residualStride, visibleColumns), sums);
                    }

                    for (int column = 0; column < visibleColumns; column++)
                    {
                        averages[column] = (short)DivideAndRoundSigned(sums[column], visibleRows);
                    }
                }

                for (int row = visibleRows; row < rows; row++)
                {
                    averages.CopyTo(residual.Slice(row * residualStride, visibleColumns));
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

        // The identity codes each column, so the hidden columns of each visible row repeat the mean of that row. Hidden rows get zero.
        if (rightPixels != 0)
        {
            for (int row = 0; row < visibleRows; row++)
            {
                short average = 0;
                if (!completeBlockOutside)
                {
                    average = (short)DivideAndRoundSigned(SumSamples(residual.Slice(row * residualStride, visibleColumns)), visibleColumns);
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
    /// Sums residual samples in 32-bit lanes, widest vectors first.
    /// </summary>
    /// <param name="samples">The samples.</param>
    /// <returns>Their sum.</returns>
    private static int SumSamples(ReadOnlySpan<short> samples)
    {
        ref short sampleBase = ref MemoryMarshal.GetReference(samples);
        int length = samples.Length;
        int index = 0;
        int sum = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<int> total = Vector512<int>.Zero;
            for (; index <= length - Vector512<short>.Count; index += Vector512<short>.Count)
            {
                total += Vector512_.MultiplyAddAdjacent(Vector512.LoadUnsafe(ref sampleBase, (nuint)index), Vector512<short>.One);
            }

            sum += Vector512.Sum(total);
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<int> total = Vector256<int>.Zero;
            for (; index <= length - Vector256<short>.Count; index += Vector256<short>.Count)
            {
                total += Vector256_.MultiplyAddAdjacent(Vector256.LoadUnsafe(ref sampleBase, (nuint)index), Vector256<short>.One);
            }

            sum += Vector256.Sum(total);
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<int> total = Vector128<int>.Zero;
            for (; index <= length - Vector128<short>.Count; index += Vector128<short>.Count)
            {
                total += Vector128_.MultiplyAddAdjacent(Vector128.LoadUnsafe(ref sampleBase, (nuint)index), Vector128<short>.One);
            }

            sum += Vector128.Sum(total);
        }

        for (; index < length; index++)
        {
            sum += Unsafe.Add(ref sampleBase, (nuint)index);
        }

        return sum;
    }

    /// <summary>
    /// Adds one row of residual samples to 32-bit column sums, widest vectors first.
    /// </summary>
    /// <param name="row">The row samples.</param>
    /// <param name="sums">The column sums to update.</param>
    private static void AddColumns(ReadOnlySpan<short> row, Span<int> sums)
    {
        ref short rowBase = ref MemoryMarshal.GetReference(row);
        ref int sumBase = ref MemoryMarshal.GetReference(sums);
        int length = row.Length;
        int index = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; index <= length - Vector512<short>.Count; index += Vector512<short>.Count)
            {
                (Vector512<int> lower, Vector512<int> upper) = Vector512.Widen(Vector512.LoadUnsafe(ref rowBase, (nuint)index));
                (Vector512.LoadUnsafe(ref sumBase, (nuint)index) + lower).StoreUnsafe(ref sumBase, (nuint)index);
                (Vector512.LoadUnsafe(ref sumBase, (nuint)(index + Vector512<int>.Count)) + upper).StoreUnsafe(ref sumBase, (nuint)(index + Vector512<int>.Count));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; index <= length - Vector256<short>.Count; index += Vector256<short>.Count)
            {
                (Vector256<int> lower, Vector256<int> upper) = Vector256.Widen(Vector256.LoadUnsafe(ref rowBase, (nuint)index));
                (Vector256.LoadUnsafe(ref sumBase, (nuint)index) + lower).StoreUnsafe(ref sumBase, (nuint)index);
                (Vector256.LoadUnsafe(ref sumBase, (nuint)(index + Vector256<int>.Count)) + upper).StoreUnsafe(ref sumBase, (nuint)(index + Vector256<int>.Count));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; index <= length - Vector128<short>.Count; index += Vector128<short>.Count)
            {
                (Vector128<int> lower, Vector128<int> upper) = Vector128.Widen(Vector128.LoadUnsafe(ref rowBase, (nuint)index));
                (Vector128.LoadUnsafe(ref sumBase, (nuint)index) + lower).StoreUnsafe(ref sumBase, (nuint)index);
                (Vector128.LoadUnsafe(ref sumBase, (nuint)(index + Vector128<int>.Count)) + upper).StoreUnsafe(ref sumBase, (nuint)(index + Vector128<int>.Count));
            }
        }

        for (; index < length; index++)
        {
            Unsafe.Add(ref sumBase, (nuint)index) += Unsafe.Add(ref rowBase, (nuint)index);
        }
    }

    /// <summary>
    /// Divides and rounds half away from zero.
    /// </summary>
    /// <param name="numerator">The signed numerator.</param>
    /// <param name="denominator">The positive denominator.</param>
    /// <returns>The rounded quotient.</returns>
    private static int DivideAndRoundSigned(int numerator, int denominator)
        => numerator < 0 ? (numerator - (denominator / 2)) / denominator : (numerator + (denominator / 2)) / denominator;

    /// <summary>
    /// Gets whether the horizontal one-dimensional transform of a type is the identity.
    /// </summary>
    /// <param name="transformType">The transform type.</param>
    /// <returns><see langword="true"/> when the type transforms only the columns.</returns>
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
    /// Sums the squares of a contiguous signed residual block.
    /// </summary>
    /// <param name="residual">The residual samples.</param>
    /// <returns>The exact sum of squared sample differences.</returns>
    public static long SumSquares(ReadOnlySpan<short> residual)
        => SumSquares<ResidualSquaresOperator>(residual, residual.Length, residual.Length, 1);

    /// <summary>
    /// Sums the squares of a rectangle of a signed residual plane.
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
    /// A caller derives the mean and the variance of the residual of one transform block from both sums, without a second pass.
    /// </remarks>
    /// <param name="residual">The residual samples.</param>
    /// <param name="sum">The exact sum of the samples.</param>
    /// <returns>The exact sum of squared samples.</returns>
    public static long SumAndSumSquares(ReadOnlySpan<short> residual, out long sum)
        => SumAndSumSquares<ResidualSquaresOperator>(residual, residual.Length, residual.Length, 1, out sum);

    /// <summary>
    /// Sums a rectangle of a signed residual plane and the squares of its samples.
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
    /// <typeparam name="TOperator">The lane arithmetic.</typeparam>
    /// <param name="residual">The residual samples at the rectangle origin.</param>
    /// <param name="stride">The residual row stride.</param>
    /// <param name="width">The rectangle width.</param>
    /// <param name="height">The rectangle height.</param>
    /// <returns>The exact sum of squared samples.</returns>
    private static long SumSquares<TOperator>(ReadOnlySpan<short> residual, int stride, int width, int height)
        where TOperator : struct, IResidualSquaresOperator
    {
        // The slice checks every bound once, so the rows below can load by reference.
        ref short residualBase = ref MemoryMarshal.GetReference(residual[..(height == 0 ? 0 : ((height - 1) * stride) + width)]);

        // The squares accumulate in 64-bit lanes and reduce once at the end. A horizontal sum inside the loop costs a chain of shuffles
        // and adds, which is more than the lane work that it reduces. A 32-bit lane overflows after 64 vectors of 12-bit residuals.
        Vector512<long> total512 = Vector512<long>.Zero;
        Vector256<long> total256 = Vector256<long>.Zero;
        Vector128<long> total128 = Vector128<long>.Zero;
        long sum = 0;
        for (int y = 0; y < height; y++)
        {
            ref short rowBase = ref Unsafe.Add(ref residualBase, (nuint)y * (nuint)stride);
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
                sum = TOperator.AccumulateSquares(Unsafe.Add(ref rowBase, (nuint)x), sum);
            }
        }

        total256 += total512.GetLower() + total512.GetUpper();
        total128 += total256.GetLower() + total256.GetUpper();
        return sum + Vector128.Sum(total128);
    }

    /// <summary>
    /// Traverses the sum and the square sum of a residual rectangle at descending register widths.
    /// </summary>
    /// <typeparam name="TOperator">The lane arithmetic.</typeparam>
    /// <param name="residual">The residual samples at the rectangle origin.</param>
    /// <param name="stride">The residual row stride.</param>
    /// <param name="width">The rectangle width.</param>
    /// <param name="height">The rectangle height.</param>
    /// <param name="sum">The exact sum of the samples.</param>
    /// <returns>The exact sum of squared samples.</returns>
    private static long SumAndSumSquares<TOperator>(ReadOnlySpan<short> residual, int stride, int width, int height, out long sum)
        where TOperator : struct, IResidualSquaresOperator
    {
        // The slice checks every bound once, so the rows below can load by reference.
        ref short residualBase = ref MemoryMarshal.GetReference(residual[..(height == 0 ? 0 : ((height - 1) * stride) + width)]);

        // Both totals accumulate in 64-bit lanes and reduce once at the end, as in the square sum. The reduction stays outside the loop,
        // and a 32-bit lane is too narrow for a full encoder block.
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
            ref short rowBase = ref Unsafe.Add(ref residualBase, (nuint)y * (nuint)stride);
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
                short value = Unsafe.Add(ref rowBase, (nuint)x);
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

    /// <summary>
    /// Traverses the squared error between two strided sample planes at descending register widths.
    /// </summary>
    /// <typeparam name="TSample">The source and prediction sample type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="prediction">The prediction or reconstruction samples.</param>
    /// <param name="predictionStride">The prediction row stride.</param>
    /// <param name="width">The number of samples per row.</param>
    /// <param name="height">The number of rows.</param>
    /// <returns>The sum of squared sample differences.</returns>
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
        if (width < Vector256<short>.Count && height <= 64 && Vector128.IsHardwareAccelerated)
        {
            return SumSquaredErrorNarrow<TSample, TOperator>(ref sourceBase, sourceStride, ref predictionBase, predictionStride, width, height);
        }

        // Every row of a block has the same width, so the code finds the stage boundaries once, not once for each row.
        int end512 = width - Vector512<short>.Count;
        int end256 = width - Vector256<short>.Count;
        int end128 = width - Vector128<short>.Count;
        bool use512 = Vector512.IsHardwareAccelerated && end512 >= 0;
        bool use256 = Vector256.IsHardwareAccelerated && end256 >= 0;
        bool use128 = Vector128.IsHardwareAccelerated && end128 >= 0;
        long total = 0;

        for (int y = 0; y < height; y++)
        {
            // The code addresses each row by offset and does not slice it. A transform block is small. Two new spans and their vector
            // counts for each row cost more than the arithmetic of the row.
            ref TSample sourceRow = ref Unsafe.Add(ref sourceBase, (nuint)y * (nuint)sourceStride);
            ref TSample predictionRow = ref Unsafe.Add(ref predictionBase, (nuint)y * (nuint)predictionStride);
            int x = 0;

            // The squares accumulate in lanes and fold once for each row. A row holds at most 64 samples, and a squared difference of
            // 12-bit samples reaches 16769025. A row total therefore stays inside a 32-bit lane at either sample depth.
            // A measurement showed no gain from lanes that carry across the whole block, so the code keeps the simpler bound.
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
                int difference = TOperator.Subtract(Unsafe.Add(ref sourceRow, (nuint)x), Unsafe.Add(ref predictionRow, (nuint)x));
                total += difference * difference;
            }
        }

        return total;
    }

    /// <summary>
    /// Sums the squared differences between a source block and a prediction block that is narrower than sixteen samples.
    /// A vector holds eight differences. A row of four samples fills only half a vector, so two rows load together.
    /// A row of eight or more samples loads its first eight samples as one vector. Each vector lane keeps a running total for the whole block.
    /// The lanes add together once at the end.
    /// </summary>
    /// <remarks>
    /// The largest difference at 12 bits is 4095. One lane gains at most two squares of 4095 for each row, so after 64 rows a lane is
    /// still less than 2^32.
    /// </remarks>
    /// <typeparam name="TSample">The sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample operations.</typeparam>
    /// <param name="sourceBase">The first source sample.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="predictionBase">The first prediction sample.</param>
    /// <param name="predictionStride">The number of samples between prediction rows.</param>
    /// <param name="width">The block width, below sixteen.</param>
    /// <param name="height">The block height, at most sixty-four.</param>
    /// <returns>The exact sum of squared differences.</returns>
    private static long SumSquaredErrorNarrow<TSample, TOperator>(
        ref TSample sourceBase,
        int sourceStride,
        ref TSample predictionBase,
        int predictionStride,
        int width,
        int height)
        where TSample : unmanaged
        where TOperator : struct, IResidualOperator<TSample>
    {
        // The strides become native-width integers once, so each row address below is one multiply and one add.
        nuint sourceStep = (nuint)sourceStride;
        nuint predictionStep = (nuint)predictionStride;
        nuint columns = (nuint)width;

        // `laneSquares` keeps the squares that the vectors calculate. `scalarSquares` keeps the squares of the columns after the first
        // eight, which the scalar loop calculates one at a time.
        Vector128<int> laneSquares = Vector128<int>.Zero;
        long scalarSquares = 0;
        int row = 0;

        // Step 1: if the block is four samples wide, the code loads two rows at a time. The vector holds the four differences of the
        // first row, then the four differences of the second row. If the height is odd, step 2 does the last row.
        if (width == 4)
        {
            for (; row + 1 < height; row += 2)
            {
                ref TSample sourcePair = ref Unsafe.Add(ref sourceBase, (nuint)row * sourceStep);
                ref TSample predictionPair = ref Unsafe.Add(ref predictionBase, (nuint)row * predictionStep);
                Vector128<short> difference = TOperator.LoadDifferenceRowPair(ref sourcePair, sourceStep, ref predictionPair, predictionStep);

                // A multiply-add of the vector with itself squares each difference, then adds each pair of neighbor squares into one
                // 32-bit lane.
                laneSquares += Vector128_.MultiplyAddAdjacent(difference, difference);
            }
        }

        // Step 2: the code does each remaining row. If the row has eight or more samples, its first eight differences load as one vector.
        // The squares of the remaining columns then add one at a time. For example, a row of twelve samples does eight columns in the
        // vector and four in the scalar loop.
        bool rowFillsVector = width >= Vector128<short>.Count;
        for (; row < height; row++)
        {
            ref TSample sourceRow = ref Unsafe.Add(ref sourceBase, (nuint)row * sourceStep);
            ref TSample predictionRow = ref Unsafe.Add(ref predictionBase, (nuint)row * predictionStep);
            nuint column = 0;
            if (rowFillsVector)
            {
                Vector128<short> difference = TOperator.LoadDifference(ref sourceRow, ref predictionRow, 0, Vector128<short>.Zero);
                laneSquares += Vector128_.MultiplyAddAdjacent(difference, difference);
                column = (nuint)Vector128<short>.Count;
            }

            for (; column < columns; column++)
            {
                int difference = TOperator.Subtract(Unsafe.Add(ref sourceRow, column), Unsafe.Add(ref predictionRow, column));
                scalarSquares += difference * difference;
            }
        }

        // Step 3: the code adds the four lanes together. One lane stays less than 2^32 for 64 rows, but the total of four lanes can be
        // more than 2^32. The lanes therefore widen to 64 bits before the sum.
        Vector128<uint> lanes = laneSquares.AsUInt32();
        return scalarSquares + (long)Vector128.Sum(Vector128.WidenLower(lanes) + Vector128.WidenUpper(lanes));
    }

    /// <summary>
    /// Subtracts a prediction plane from its source plane at descending register widths.
    /// </summary>
    /// <typeparam name="TSample">The source and prediction sample type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="prediction">The prediction samples.</param>
    /// <param name="predictionStride">The prediction row stride.</param>
    /// <param name="residual">The destination residual samples.</param>
    /// <param name="residualStride">The residual row stride.</param>
    /// <param name="width">The number of samples per row.</param>
    /// <param name="height">The number of rows.</param>
    public static void Subtract<TSample, TOperator>(
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

        // Every row of a block has the same width, so the code selects the stages and their end points once, not once for each row.
        // One vector of 16-bit lanes is one vector of residuals for both sample depths. A row of eight samples therefore fills a vector,
        // and no transform width goes to the scalar loop alone.
        int end512 = width - Vector512<short>.Count;
        int end256 = width - Vector256<short>.Count;
        int end128 = width - Vector128<short>.Count;
        bool use512 = Vector512.IsHardwareAccelerated && end512 >= 0;
        bool use256 = Vector256.IsHardwareAccelerated && end256 >= 0;
        bool use128 = Vector128.IsHardwareAccelerated && end128 >= 0;
        int y = 0;
        if (width == 4 && Vector128.IsHardwareAccelerated)
        {
            // A row of four samples fills only half a vector, so this path takes two rows at a time. The vector holds the four residuals
            // of the first row in its low 64 bits and the four residuals of the second row in its high 64 bits.
            // One 64-bit store writes each half to its own residual row.
            nuint sourceStep = (nuint)sourceStride;
            nuint predictionStep = (nuint)predictionStride;
            nuint residualStep = (nuint)residualStride;
            for (; y + 1 < height; y += 2)
            {
                Vector128<ulong> difference = TOperator.LoadDifferenceRowPair(
                    ref Unsafe.Add(ref sourceBase, (nuint)y * sourceStep),
                    sourceStep,
                    ref Unsafe.Add(ref predictionBase, (nuint)y * predictionStep),
                    predictionStep).AsUInt64();

                ref short residualRow = ref Unsafe.Add(ref residualBase, (nuint)y * residualStep);
                Unsafe.WriteUnaligned(ref Unsafe.As<short, byte>(ref residualRow), difference.ToScalar());
                Unsafe.WriteUnaligned(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref residualRow, residualStep)), difference.GetElement(1));
            }
        }

        for (; y < height; y++)
        {
            // The code addresses each row by offset, as the squared error does. A transform row is short, so the span work for each row
            // costs more than the row arithmetic.
            ref TSample sourceRow = ref Unsafe.Add(ref sourceBase, (nuint)y * (nuint)sourceStride);
            ref TSample predictionRow = ref Unsafe.Add(ref predictionBase, (nuint)y * (nuint)predictionStride);
            ref short residualRow = ref Unsafe.Add(ref residualBase, (nuint)y * (nuint)residualStride);
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
                Unsafe.Add(ref residualRow, (nuint)x) = TOperator.Subtract(Unsafe.Add(ref sourceRow, (nuint)x), Unsafe.Add(ref predictionRow, (nuint)x));
            }
        }
    }
}
