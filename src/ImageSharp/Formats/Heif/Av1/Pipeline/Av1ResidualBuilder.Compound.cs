// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Measures compound predictions: a searched prediction blended with a fixed second prediction.
/// </content>
internal static partial class Av1ResidualBuilder
{
    /// <summary>
    /// Measures the absolute error of a compound prediction, optionally sampling alternate rows. Reference:
    /// aom_sad{W}x{H}_avg (comp_avg_pred) for equal weights, and masked_sad() of aom_masked_sad{W}x{H} for
    /// mask weights.
    /// </summary>
    /// <param name="source">Source samples beginning at the block origin.</param>
    /// <param name="sourceStride">The source row stride, in samples.</param>
    /// <param name="prediction">The searched prediction samples beginning at the block origin.</param>
    /// <param name="predictionStride">The searched prediction row stride, in samples.</param>
    /// <param name="secondPrediction">The fixed second prediction, packed at the block width.</param>
    /// <param name="mask">The six-bit weights of the searched prediction, packed at the block width; empty selects equal weights.</param>
    /// <param name="width">The block width, in samples.</param>
    /// <param name="height">The block height, in samples.</param>
    /// <param name="rowStep">One for every row, or two for alternating rows with doubled error.</param>
    /// <returns>The unnormalized absolute difference over the block.</returns>
    public static int SumCompoundAbsoluteDifferences(
        ReadOnlySpan<byte> source,
        int sourceStride,
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        ReadOnlySpan<byte> secondPrediction,
        ReadOnlySpan<byte> mask,
        int width,
        int height,
        int rowStep)
        => SumCompoundAbsoluteDifferences<byte, ByteOperator>(
            source, sourceStride, prediction, predictionStride, secondPrediction, mask, width, height, rowStep);

    /// <summary>
    /// Measures the absolute error of a compound prediction, optionally sampling alternate rows. Reference:
    /// aom_highbd_sad{W}x{H}_avg for equal weights, and highbd_masked_sad() for mask weights.
    /// </summary>
    /// <param name="source">Source samples beginning at the block origin.</param>
    /// <param name="sourceStride">The source row stride, in samples.</param>
    /// <param name="prediction">The searched prediction samples beginning at the block origin.</param>
    /// <param name="predictionStride">The searched prediction row stride, in samples.</param>
    /// <param name="secondPrediction">The fixed second prediction, packed at the block width.</param>
    /// <param name="mask">The six-bit weights of the searched prediction, packed at the block width; empty selects equal weights.</param>
    /// <param name="width">The block width, in samples.</param>
    /// <param name="height">The block height, in samples.</param>
    /// <param name="rowStep">One for every row, or two for alternating rows with doubled error.</param>
    /// <returns>The unnormalized absolute difference over the block.</returns>
    public static int SumCompoundAbsoluteDifferences(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        ReadOnlySpan<ushort> prediction,
        int predictionStride,
        ReadOnlySpan<ushort> secondPrediction,
        ReadOnlySpan<byte> mask,
        int width,
        int height,
        int rowStep)
        => SumCompoundAbsoluteDifferences<ushort, UInt16Operator>(
            source, sourceStride, prediction, predictionStride, secondPrediction, mask, width, height, rowStep);

    /// <summary>
    /// Measures the signed and squared error of a compound prediction. Reference: the accumulation of
    /// aom_variance after comp_avg_pred for equal weights, and of masked_variance() for mask weights.
    /// </summary>
    /// <param name="source">Source samples beginning at the block origin.</param>
    /// <param name="sourceStride">The source row stride, in samples.</param>
    /// <param name="prediction">The searched prediction samples beginning at the block origin.</param>
    /// <param name="predictionStride">The searched prediction row stride, in samples.</param>
    /// <param name="secondPrediction">The fixed second prediction, packed at the block width.</param>
    /// <param name="mask">The six-bit weights of the searched prediction, packed at the block width; empty selects equal weights.</param>
    /// <param name="width">The block width, in samples.</param>
    /// <param name="height">The block height, in samples.</param>
    /// <param name="sum">The unnormalized signed residual sum.</param>
    /// <param name="sumOfSquares">The unnormalized squared residual sum.</param>
    public static void GetCompoundMoments(
        ReadOnlySpan<byte> source,
        int sourceStride,
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        ReadOnlySpan<byte> secondPrediction,
        ReadOnlySpan<byte> mask,
        int width,
        int height,
        out int sum,
        out long sumOfSquares)
        => GetCompoundMoments<byte, ByteOperator>(
            source, sourceStride, prediction, predictionStride, secondPrediction, mask, width, height, out sum, out sumOfSquares);

    /// <summary>
    /// Measures the signed and squared error of a compound prediction. Reference: the accumulation of
    /// aom_highbd_variance after the high-bit-depth comp_avg_pred for equal weights, and of
    /// highbd_masked_variance() for mask weights.
    /// </summary>
    /// <param name="source">Source samples beginning at the block origin.</param>
    /// <param name="sourceStride">The source row stride, in samples.</param>
    /// <param name="prediction">The searched prediction samples beginning at the block origin.</param>
    /// <param name="predictionStride">The searched prediction row stride, in samples.</param>
    /// <param name="secondPrediction">The fixed second prediction, packed at the block width.</param>
    /// <param name="mask">The six-bit weights of the searched prediction, packed at the block width; empty selects equal weights.</param>
    /// <param name="width">The block width, in samples.</param>
    /// <param name="height">The block height, in samples.</param>
    /// <param name="sum">The unnormalized signed residual sum.</param>
    /// <param name="sumOfSquares">The unnormalized squared residual sum.</param>
    public static void GetCompoundMoments(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        ReadOnlySpan<ushort> prediction,
        int predictionStride,
        ReadOnlySpan<ushort> secondPrediction,
        ReadOnlySpan<byte> mask,
        int width,
        int height,
        out int sum,
        out long sumOfSquares)
        => GetCompoundMoments<ushort, UInt16Operator>(
            source, sourceStride, prediction, predictionStride, secondPrediction, mask, width, height, out sum, out sumOfSquares);

    /// <summary>
    /// Traverses a compound SAD with the selected sample operator and descending vector widths.
    /// </summary>
    private static int SumCompoundAbsoluteDifferences<TSample, TOperator>(
        ReadOnlySpan<TSample> source,
        int sourceStride,
        ReadOnlySpan<TSample> prediction,
        int predictionStride,
        ReadOnlySpan<TSample> secondPrediction,
        ReadOnlySpan<byte> mask,
        int width,
        int height,
        int rowStep)
        where TSample : unmanaged
        where TOperator : struct, IResidualOperator<TSample>
    {
        // The blend choice is the same for every vector of the block, so its branch always predicts.
        bool equalWeights = mask.IsEmpty;
        if (!equalWeights)
        {
            _ = mask[((height - 1) * width) + width - 1];
        }

        // The totals follow the bounds of the plain SAD traversal: the blend is a sample again.
        Vector512<uint> total512 = Vector512<uint>.Zero;
        Vector256<uint> total256 = Vector256<uint>.Zero;
        Vector128<uint> total128 = Vector128<uint>.Zero;
        int sum = 0;
        for (int y = 0; y < height; y += rowStep)
        {
            int packedOffset = y * width;
            ReadOnlySpan<TSample> sourceRow = source.Slice(y * sourceStride, width);
            ReadOnlySpan<TSample> predictionRow = prediction.Slice(y * predictionStride, width);
            ReadOnlySpan<TSample> secondRow = secondPrediction.Slice(packedOffset, width);
            ref TSample sourceBase = ref MemoryMarshal.GetReference(sourceRow);
            ref TSample predictionBase = ref MemoryMarshal.GetReference(predictionRow);
            ref TSample secondBase = ref MemoryMarshal.GetReference(secondRow);
            ref byte maskBase = ref equalWeights ? ref Unsafe.NullRef<byte>() : ref Unsafe.Add(ref MemoryMarshal.GetReference(mask), packedOffset);
            int x = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                for (; x <= width - Vector512<TSample>.Count; x += Vector512<TSample>.Count)
                {
                    Vector512<TSample> searched = Vector512.LoadUnsafe(ref predictionBase, (nuint)x);
                    Vector512<TSample> second = Vector512.LoadUnsafe(ref secondBase, (nuint)x);
                    Vector512<TSample> blended = equalWeights
                        ? TOperator.Average(searched, second)
                        : TOperator.Blend(searched, second, TOperator.LoadMask(ref maskBase, (nuint)x, default(Vector512<TSample>)));
                    total512 = TOperator.AccumulateAbsoluteDifferences(Vector512.LoadUnsafe(ref sourceBase, (nuint)x), blended, total512);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x <= width - Vector256<TSample>.Count; x += Vector256<TSample>.Count)
                {
                    Vector256<TSample> searched = Vector256.LoadUnsafe(ref predictionBase, (nuint)x);
                    Vector256<TSample> second = Vector256.LoadUnsafe(ref secondBase, (nuint)x);
                    Vector256<TSample> blended = equalWeights
                        ? TOperator.Average(searched, second)
                        : TOperator.Blend(searched, second, TOperator.LoadMask(ref maskBase, (nuint)x, default(Vector256<TSample>)));
                    total256 = TOperator.AccumulateAbsoluteDifferences(Vector256.LoadUnsafe(ref sourceBase, (nuint)x), blended, total256);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x <= width - Vector128<TSample>.Count; x += Vector128<TSample>.Count)
                {
                    Vector128<TSample> searched = Vector128.LoadUnsafe(ref predictionBase, (nuint)x);
                    Vector128<TSample> second = Vector128.LoadUnsafe(ref secondBase, (nuint)x);
                    Vector128<TSample> blended = equalWeights
                        ? TOperator.Average(searched, second)
                        : TOperator.Blend(searched, second, TOperator.LoadMask(ref maskBase, (nuint)x, default(Vector128<TSample>)));
                    total128 = TOperator.AccumulateAbsoluteDifferences(Vector128.LoadUnsafe(ref sourceBase, (nuint)x), blended, total128);
                }
            }

            // An eight-byte row fills half of a byte vector. The compact loads pad the source, both
            // predictions and the weights with zeros, and a blend of zeros is zero, so the padding adds
            // nothing to the total.
            if (Vector128.IsHardwareAccelerated && x <= width - SearchBlockDimension)
            {
                Vector128<TSample> searched = LoadSearchRow(predictionRow[x..]);
                Vector128<TSample> second = LoadSearchRow(secondRow[x..]);
                Vector128<TSample> blended = equalWeights
                    ? TOperator.Average(searched, second)
                    : TOperator.Blend(searched, second, TOperator.LoadSearchMask(mask[(packedOffset + x)..]));
                total128 = TOperator.AccumulateAbsoluteDifferences(LoadSearchRow(sourceRow[x..]), blended, total128);
                x += SearchBlockDimension;
            }

            for (; x < width; x++)
            {
                TSample blended = equalWeights
                    ? TOperator.Average(predictionRow[x], secondRow[x])
                    : TOperator.Blend(predictionRow[x], secondRow[x], mask[packedOffset + x]);
                sum += TOperator.SumAbsoluteDifferences(sourceRow[x], blended);
            }
        }

        total256 += total512.GetLower() + total512.GetUpper();
        total128 += total256.GetLower() + total256.GetUpper();
        sum += (int)Vector128.Sum(total128);

        // Alternate-row sampling represents the full block. Normalize bit depth only after this scaling.
        return sum * rowStep;
    }

    /// <summary>
    /// Accumulates compound residual moments with the selected sample operator and descending vector widths.
    /// </summary>
    private static void GetCompoundMoments<TSample, TOperator>(
        ReadOnlySpan<TSample> source,
        int sourceStride,
        ReadOnlySpan<TSample> prediction,
        int predictionStride,
        ReadOnlySpan<TSample> secondPrediction,
        ReadOnlySpan<byte> mask,
        int width,
        int height,
        out int sum,
        out long sumOfSquares)
        where TSample : unmanaged
        where TOperator : struct, IResidualOperator<TSample>
    {
        bool equalWeights = mask.IsEmpty;
        if (!equalWeights)
        {
            _ = mask[((height - 1) * width) + width - 1];
        }

        sum = 0;
        sumOfSquares = 0;

        // The totals follow the bounds of the plain moment traversal: the blend is a sample again.
        Vector512<int> sum512 = Vector512<int>.Zero;
        Vector256<int> sum256 = Vector256<int>.Zero;
        Vector128<int> sum128 = Vector128<int>.Zero;
        for (int y = 0; y < height; y++)
        {
            int packedOffset = y * width;
            ReadOnlySpan<TSample> sourceRow = source.Slice(y * sourceStride, width);
            ReadOnlySpan<TSample> predictionRow = prediction.Slice(y * predictionStride, width);
            ReadOnlySpan<TSample> secondRow = secondPrediction.Slice(packedOffset, width);
            ref TSample sourceBase = ref MemoryMarshal.GetReference(sourceRow);
            ref TSample predictionBase = ref MemoryMarshal.GetReference(predictionRow);
            ref TSample secondBase = ref MemoryMarshal.GetReference(secondRow);
            ref byte maskBase = ref equalWeights ? ref Unsafe.NullRef<byte>() : ref Unsafe.Add(ref MemoryMarshal.GetReference(mask), packedOffset);
            int x = 0;
            Vector512<int> squares512 = Vector512<int>.Zero;
            Vector256<int> squares256 = Vector256<int>.Zero;
            Vector128<int> squares128 = Vector128<int>.Zero;

            if (Vector512.IsHardwareAccelerated)
            {
                for (; x <= width - Vector512<TSample>.Count; x += Vector512<TSample>.Count)
                {
                    Vector512<TSample> searched = Vector512.LoadUnsafe(ref predictionBase, (nuint)x);
                    Vector512<TSample> second = Vector512.LoadUnsafe(ref secondBase, (nuint)x);
                    Vector512<TSample> blended = equalWeights
                        ? TOperator.Average(searched, second)
                        : TOperator.Blend(searched, second, TOperator.LoadMask(ref maskBase, (nuint)x, default(Vector512<TSample>)));
                    TOperator.AccumulateMoments(Vector512.LoadUnsafe(ref sourceBase, (nuint)x), blended, ref sum512, ref squares512);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x <= width - Vector256<TSample>.Count; x += Vector256<TSample>.Count)
                {
                    Vector256<TSample> searched = Vector256.LoadUnsafe(ref predictionBase, (nuint)x);
                    Vector256<TSample> second = Vector256.LoadUnsafe(ref secondBase, (nuint)x);
                    Vector256<TSample> blended = equalWeights
                        ? TOperator.Average(searched, second)
                        : TOperator.Blend(searched, second, TOperator.LoadMask(ref maskBase, (nuint)x, default(Vector256<TSample>)));
                    TOperator.AccumulateMoments(Vector256.LoadUnsafe(ref sourceBase, (nuint)x), blended, ref sum256, ref squares256);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x <= width - Vector128<TSample>.Count; x += Vector128<TSample>.Count)
                {
                    Vector128<TSample> searched = Vector128.LoadUnsafe(ref predictionBase, (nuint)x);
                    Vector128<TSample> second = Vector128.LoadUnsafe(ref secondBase, (nuint)x);
                    Vector128<TSample> blended = equalWeights
                        ? TOperator.Average(searched, second)
                        : TOperator.Blend(searched, second, TOperator.LoadMask(ref maskBase, (nuint)x, default(Vector128<TSample>)));
                    TOperator.AccumulateMoments(Vector128.LoadUnsafe(ref sourceBase, (nuint)x), blended, ref sum128, ref squares128);
                }
            }

            // The zero padding of the compact loads blends to zero, so it adds a zero difference and a
            // zero square.
            if (Vector128.IsHardwareAccelerated && x <= width - SearchBlockDimension)
            {
                Vector128<TSample> searched = LoadSearchRow(predictionRow[x..]);
                Vector128<TSample> second = LoadSearchRow(secondRow[x..]);
                Vector128<TSample> blended = equalWeights
                    ? TOperator.Average(searched, second)
                    : TOperator.Blend(searched, second, TOperator.LoadSearchMask(mask[(packedOffset + x)..]));
                TOperator.AccumulateMoments(LoadSearchRow(sourceRow[x..]), blended, ref sum128, ref squares128);
                x += SearchBlockDimension;
            }

            squares256 += squares512.GetLower() + squares512.GetUpper();
            squares128 += squares256.GetLower() + squares256.GetUpper();
            sumOfSquares += Vector128.Sum(squares128);

            for (; x < width; x++)
            {
                TSample blended = equalWeights
                    ? TOperator.Average(predictionRow[x], secondRow[x])
                    : TOperator.Blend(predictionRow[x], secondRow[x], mask[packedOffset + x]);
                int difference = TOperator.Subtract(sourceRow[x], blended);
                sum += difference;
                sumOfSquares += difference * difference;
            }
        }

        sum256 += sum512.GetLower() + sum512.GetUpper();
        sum128 += sum256.GetLower() + sum256.GetUpper();
        sum += Vector128.Sum(sum128);
    }
}
