// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;

/// <content>
/// Builds the zero-mean Q3 predictor surface that chroma-from-luma prediction scales.
/// </content>
/// <remarks>
/// <para>
/// One lane is one chroma column throughout. A luma sample becomes a Q3 value by a shift of three. A subsampled chroma column is the sum of the luma samples
/// that map to it. A shift then gives the result the same three fractional bits. Thus a horizontal pair uses a shift of two, and a horizontal and vertical quad
/// uses a shift of one.
/// </para>
/// <para>
/// The traversals below are generic over the sample type, so the code is not written twice for the two sample depths. The depth-specific loads are beside them.
/// The loop restoration filter and the film grain synthesis use the same pattern.
/// </para>
/// </remarks>
internal partial class Av1ChromaFromLumaContext
{
    /// <summary>
    /// Subsamples one reconstructed eight-bit luma block and removes its rounded Q3 mean.
    /// </summary>
    /// <param name="input">The reconstructed luma samples.</param>
    /// <param name="inputStride">The distance, in samples, between input rows.</param>
    /// <param name="output">The fixed-stride Q3 predictor workspace.</param>
    /// <param name="transformSize">The chroma transform dimensions.</param>
    /// <param name="lumaExtent">The luma samples the encoder coded for this block.</param>
    /// <param name="subsamplingX">Whether two horizontal luma samples map to each chroma sample.</param>
    /// <param name="subsamplingY">Whether two vertical luma samples map to each chroma sample.</param>
    public static void PrepareBlock(
        ReadOnlySpan<byte> input,
        int inputStride,
        Span<short> output,
        Av1TransformSize transformSize,
        Size lumaExtent,
        bool subsamplingX,
        bool subsamplingY)
        => PrepareBlockCore(input, inputStride, output, transformSize, lumaExtent, subsamplingX, subsamplingY);

    /// <summary>
    /// Subsamples one reconstructed high-bit-depth luma block and removes its rounded Q3 mean.
    /// </summary>
    /// <param name="input">The reconstructed luma samples.</param>
    /// <param name="inputStride">The distance, in samples, between input rows.</param>
    /// <param name="output">The fixed-stride Q3 predictor workspace.</param>
    /// <param name="transformSize">The chroma transform dimensions.</param>
    /// <param name="lumaExtent">The luma samples the encoder coded for this block.</param>
    /// <param name="subsamplingX">Whether two horizontal luma samples map to each chroma sample.</param>
    /// <param name="subsamplingY">Whether two vertical luma samples map to each chroma sample.</param>
    public static void PrepareBlock(
        ReadOnlySpan<short> input,
        int inputStride,
        Span<short> output,
        Av1TransformSize transformSize,
        Size lumaExtent,
        bool subsamplingX,
        bool subsamplingY)
        => PrepareBlockCore(input, inputStride, output, transformSize, lumaExtent, subsamplingX, subsamplingY);

    /// <summary>
    /// Subsamples one reconstructed luma block of either depth and removes its rounded Q3 mean.
    /// </summary>
    /// <typeparam name="TSample">The sample type, byte or short, that the frame bit depth selects.</typeparam>
    /// <param name="input">The reconstructed luma samples.</param>
    /// <param name="inputStride">The distance, in samples, between input rows.</param>
    /// <param name="output">The fixed-stride Q3 predictor workspace.</param>
    /// <param name="transformSize">The chroma transform dimensions.</param>
    /// <param name="lumaExtent">The luma samples the encoder coded for this block.</param>
    /// <param name="subsamplingX">Whether two horizontal luma samples map to each chroma sample.</param>
    /// <param name="subsamplingY">Whether two vertical luma samples map to each chroma sample.</param>
    private static void PrepareBlockCore<TSample>(
        ReadOnlySpan<TSample> input,
        int inputStride,
        Span<short> output,
        Av1TransformSize transformSize,
        Size lumaExtent,
        bool subsamplingX,
        bool subsamplingY)
        where TSample : unmanaged
    {
        int subX = subsamplingX ? 1 : 0;
        int subY = subsamplingY ? 1 : 0;

        // The store covers each coded luma transform. Beyond the coded extent, nothing is stored. Thus the pad step repeats the last stored column and row
        // there.
        int lumaWidth = Math.Min(lumaExtent.Width, transformSize.GetWidth() << subX);
        int lumaHeight = Math.Min(lumaExtent.Height, transformSize.GetHeight() << subY);
        StoreSamples(input, inputStride, 0, lumaWidth, lumaHeight, output, subsamplingX, subsamplingY);
        Pad(output, lumaWidth >> subX, lumaHeight >> subY, transformSize.GetWidth(), transformSize.GetHeight());
        SubtractAverage(output, transformSize);
    }

    /// <summary>
    /// Stores reconstructed luma samples in the Q3 predictor surface.
    /// </summary>
    /// <typeparam name="TSample">The sample type, byte or short, that the frame bit depth selects.</typeparam>
    /// <param name="input">The reconstructed luma samples.</param>
    /// <param name="inputStride">The distance, in samples, between input rows.</param>
    /// <param name="outputOffset">The first destination sample in the fixed-stride predictor buffer.</param>
    /// <param name="width">The luma width in samples.</param>
    /// <param name="height">The luma height in samples.</param>
    /// <param name="output">The fixed-stride Q3 predictor workspace.</param>
    /// <param name="subsamplingX">Whether horizontal luma pairs are subsampled.</param>
    /// <param name="subsamplingY">Whether vertical luma pairs are subsampled.</param>
    private static void StoreSamples<TSample>(
        ReadOnlySpan<TSample> input,
        int inputStride,
        int outputOffset,
        int width,
        int height,
        Span<short> output,
        bool subsamplingX,
        bool subsamplingY)
        where TSample : unmanaged
    {
        if (subsamplingX)
        {
            StorePairedSamples(input, inputStride, outputOffset, width, height, output, subsamplingY);
            return;
        }

        StoreDirectSamples(input, inputStride, outputOffset, width, height, output);
    }

    /// <summary>
    /// Stores luma samples that map one to one onto chroma columns.
    /// </summary>
    /// <typeparam name="TSample">The sample type, byte or short, that the frame bit depth selects.</typeparam>
    /// <param name="input">The reconstructed luma samples.</param>
    /// <param name="inputStride">The distance, in samples, between input rows.</param>
    /// <param name="outputOffset">The first destination sample in the fixed-stride predictor buffer.</param>
    /// <param name="width">The luma width in samples.</param>
    /// <param name="height">The luma height in samples.</param>
    /// <param name="output">The fixed-stride Q3 predictor workspace.</param>
    private static void StoreDirectSamples<TSample>(
        ReadOnlySpan<TSample> input,
        int inputStride,
        int outputOffset,
        int width,
        int height,
        Span<short> output)
        where TSample : unmanaged
    {
        ref TSample inputBase = ref MemoryMarshal.GetReference(input);
        ref short outputBase = ref MemoryMarshal.GetReference(output);

        for (int row = 0; row < height; row++)
        {
            ref TSample inputRow = ref Unsafe.Add(ref inputBase, row * inputStride);
            ref short outputRow = ref Unsafe.Add(ref outputBase, outputOffset + (row * BufferLine));
            int column = 0;

            // The vector widths share one column offset and run from wide to narrow. A narrower vector takes the remainder that a wider vector cannot fill. The
            // scalar loop takes the rest.
            if (Vector512.IsHardwareAccelerated)
            {
                for (; column <= width - Vector512<short>.Count; column += Vector512<short>.Count)
                {
                    (LoadSamples(ref Unsafe.Add(ref inputRow, column), Vector512<short>.Zero) << Q3Shift)
                        .StoreUnsafe(ref outputRow, (nuint)column);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; column <= width - Vector256<short>.Count; column += Vector256<short>.Count)
                {
                    (LoadSamples(ref Unsafe.Add(ref inputRow, column), Vector256<short>.Zero) << Q3Shift)
                        .StoreUnsafe(ref outputRow, (nuint)column);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; column <= width - Vector128<short>.Count; column += Vector128<short>.Count)
                {
                    (LoadSamples(ref Unsafe.Add(ref inputRow, column), Vector128<short>.Zero) << Q3Shift)
                        .StoreUnsafe(ref outputRow, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                Unsafe.Add(ref outputRow, column) = (short)(LoadSample(ref Unsafe.Add(ref inputRow, column)) << Q3Shift);
            }
        }
    }

    /// <summary>
    /// Stores luma samples that map in horizontal pairs onto chroma columns.
    /// </summary>
    /// <typeparam name="TSample">The sample type, byte or short, that the frame bit depth selects.</typeparam>
    /// <param name="input">The reconstructed luma samples.</param>
    /// <param name="inputStride">The distance, in samples, between input rows.</param>
    /// <param name="outputOffset">The first destination sample in the fixed-stride predictor buffer.</param>
    /// <param name="width">The luma width in samples.</param>
    /// <param name="height">The luma height in samples.</param>
    /// <param name="output">The fixed-stride Q3 predictor workspace.</param>
    /// <param name="subsamplingY">Whether vertical luma pairs are subsampled as well.</param>
    /// <remarks>
    /// A chroma column takes two luma samples, or four when the vertical axis is also subsampled. The remaining shift keeps every case at three fractional
    /// bits. A sum of two needs a shift of two, and a sum of four needs a shift of one.
    /// </remarks>
    private static void StorePairedSamples<TSample>(
        ReadOnlySpan<TSample> input,
        int inputStride,
        int outputOffset,
        int width,
        int height,
        Span<short> output,
        bool subsamplingY)
        where TSample : unmanaged
    {
        ref TSample inputBase = ref MemoryMarshal.GetReference(input);
        ref short outputBase = ref MemoryMarshal.GetReference(output);
        int rowStep = subsamplingY ? 2 : 1;
        int shift = subsamplingY ? Q3Shift - 2 : Q3Shift - 1;

        for (int row = 0; row < height; row += rowStep)
        {
            ref TSample inputRow = ref Unsafe.Add(ref inputBase, row * inputStride);
            ref TSample nextInputRow = ref Unsafe.Add(ref inputRow, subsamplingY ? inputStride : 0);
            ref short outputRow = ref Unsafe.Add(ref outputBase, outputOffset + ((row / rowStep) * BufferLine));
            int column = 0;

            // Each stage reads twice as many luma samples as the chroma columns that it writes. Thus the destination offset is always half the source offset.
            if (Vector512.IsHardwareAccelerated)
            {
                for (; column <= width - (2 * Vector512<short>.Count); column += 2 * Vector512<short>.Count)
                {
                    Vector512<short> sum = LoadPairSums(ref Unsafe.Add(ref inputRow, column), Vector512<short>.Zero);
                    if (subsamplingY)
                    {
                        sum += LoadPairSums(ref Unsafe.Add(ref nextInputRow, column), Vector512<short>.Zero);
                    }

                    (sum << shift).StoreUnsafe(ref outputRow, (nuint)(column >> 1));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; column <= width - (2 * Vector256<short>.Count); column += 2 * Vector256<short>.Count)
                {
                    Vector256<short> sum = LoadPairSums(ref Unsafe.Add(ref inputRow, column), Vector256<short>.Zero);
                    if (subsamplingY)
                    {
                        sum += LoadPairSums(ref Unsafe.Add(ref nextInputRow, column), Vector256<short>.Zero);
                    }

                    (sum << shift).StoreUnsafe(ref outputRow, (nuint)(column >> 1));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; column <= width - (2 * Vector128<short>.Count); column += 2 * Vector128<short>.Count)
                {
                    Vector128<short> sum = LoadPairSums(ref Unsafe.Add(ref inputRow, column), Vector128<short>.Zero);
                    if (subsamplingY)
                    {
                        sum += LoadPairSums(ref Unsafe.Add(ref nextInputRow, column), Vector128<short>.Zero);
                    }

                    (sum << shift).StoreUnsafe(ref outputRow, (nuint)(column >> 1));
                }
            }

            for (; column < width; column += 2)
            {
                int sum = LoadSample(ref Unsafe.Add(ref inputRow, column)) +
                    LoadSample(ref Unsafe.Add(ref inputRow, column + 1));

                if (subsamplingY)
                {
                    sum += LoadSample(ref Unsafe.Add(ref nextInputRow, column)) +
                        LoadSample(ref Unsafe.Add(ref nextInputRow, column + 1));
                }

                Unsafe.Add(ref outputRow, column >> 1) = (short)(sum << shift);
            }
        }
    }

    /// <summary>
    /// Subtracts the rounded Q3 average from each predictor sample. The result is the alternating-current part that chroma-from-luma prediction scales.
    /// </summary>
    /// <param name="buffer">The fixed-stride Q3 predictor workspace.</param>
    /// <param name="transformSize">The populated predictor dimensions.</param>
    private static void SubtractAverage(Span<short> buffer, Av1TransformSize transformSize)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        ref short bufferBase = ref MemoryMarshal.GetReference(buffer);

        // Transform dimensions are powers of two, so the division by the sample count is an exact right shift. The sum starts at half the sample count. This
        // gives the rounding to the nearest whole Q3 value that AV1 defines.
        int sumQ3 = (width * height) >> 1;
        for (int row = 0; row < height; row++)
        {
            sumQ3 += SumRow(ref Unsafe.Add(ref bufferBase, row * BufferLine), width);
        }

        int sampleCountLog2 = transformSize.GetBlockWidthLog2() + transformSize.GetBlockHeightLog2();
        short averageQ3 = (short)(sumQ3 >> sampleCountLog2);
        for (int row = 0; row < height; row++)
        {
            SubtractRow(ref Unsafe.Add(ref bufferBase, row * BufferLine), width, averageQ3);
        }
    }

    /// <summary>
    /// Sums one row of the predictor surface.
    /// </summary>
    /// <param name="row">The first sample of the row.</param>
    /// <param name="width">The number of samples in the row.</param>
    /// <returns>The total of the row.</returns>
    /// <remarks>
    /// A Q3 sample of a twelve-bit frame reaches 32760. A 32 by 32 transform holds 1024 of them. Thus the total needs more than sixteen bits, and the lanes
    /// widen before they accumulate.
    /// </remarks>
    private static int SumRow(ref short row, int width)
    {
        int total = 0;
        int column = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            for (; column <= width - Vector512<short>.Count; column += Vector512<short>.Count)
            {
                (Vector512<int> lower, Vector512<int> upper) = Vector512.Widen(Vector512.LoadUnsafe(ref row, (nuint)column));
                total += Vector512.Sum(lower) + Vector512.Sum(upper);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; column <= width - Vector256<short>.Count; column += Vector256<short>.Count)
            {
                (Vector256<int> lower, Vector256<int> upper) = Vector256.Widen(Vector256.LoadUnsafe(ref row, (nuint)column));
                total += Vector256.Sum(lower) + Vector256.Sum(upper);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; column <= width - Vector128<short>.Count; column += Vector128<short>.Count)
            {
                (Vector128<int> lower, Vector128<int> upper) = Vector128.Widen(Vector128.LoadUnsafe(ref row, (nuint)column));
                total += Vector128.Sum(lower) + Vector128.Sum(upper);
            }
        }

        for (; column < width; column++)
        {
            total += Unsafe.Add(ref row, column);
        }

        return total;
    }

    /// <summary>
    /// Subtracts one value from every sample of one row of the predictor surface.
    /// </summary>
    /// <param name="row">The first sample of the row.</param>
    /// <param name="width">The number of samples in the row.</param>
    /// <param name="averageQ3">The value to subtract.</param>
    private static void SubtractRow(ref short row, int width, short averageQ3)
    {
        int column = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<short> average = Vector512.Create(averageQ3);
            for (; column <= width - Vector512<short>.Count; column += Vector512<short>.Count)
            {
                (Vector512.LoadUnsafe(ref row, (nuint)column) - average).StoreUnsafe(ref row, (nuint)column);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<short> average = Vector256.Create(averageQ3);
            for (; column <= width - Vector256<short>.Count; column += Vector256<short>.Count)
            {
                (Vector256.LoadUnsafe(ref row, (nuint)column) - average).StoreUnsafe(ref row, (nuint)column);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<short> average = Vector128.Create(averageQ3);
            for (; column <= width - Vector128<short>.Count; column += Vector128<short>.Count)
            {
                (Vector128.LoadUnsafe(ref row, (nuint)column) - average).StoreUnsafe(ref row, (nuint)column);
            }
        }

        for (; column < width; column++)
        {
            Unsafe.Add(ref row, column) -= averageQ3;
        }
    }
}
