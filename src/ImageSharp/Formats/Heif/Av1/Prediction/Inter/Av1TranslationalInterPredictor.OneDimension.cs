// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Provides SIMD kernels for horizontal-only and vertical-only single-reference filtering.
/// </content>
internal static partial class Av1TranslationalInterPredictor
{
    /// <summary>
    /// Filters an 8-bit block in sixteen-sample vectors.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The distance between source rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    /// <param name="coefficients">The filter coefficients, starting at the first applied tap.</param>
    /// <param name="tapCount">The number of applied filter taps.</param>
    /// <param name="sourceOffset">The offset from the integer-position sample to the sample of the first applied tap.</param>
    /// <param name="tapStride">The distance between consecutive tap samples: one for a horizontal filter, or the row stride for a vertical filter.</param>
    /// <param name="firstRound">The first rounding shift.</param>
    /// <param name="secondRound">The second rounding shift. Zero skips this rounding.</param>
    /// <param name="initial">The start value of each accumulator lane. The vector type selects this overload.</param>
    private static void FilterDirect(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        ReadOnlySpan<short> coefficients,
        int tapCount,
        int sourceOffset,
        int tapStride,
        int firstRound,
        int secondRound,
        Vector128<int> initial)
    {
        ref byte sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short coefficientBase = ref MemoryMarshal.GetReference(coefficients);

        for (int row = 0; row < height; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(ref sourceBase, (row * sourceStride) + sourceOffset);
            ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int processedColumns = 0;

            if (width < Vector128<byte>.Count)
            {
                // Blocks of two, four, and eight samples are narrower than one byte vector. The padding of the reference plane makes the full load safe.
                // The store writes only the row width, so the adjacent destination samples do not change.
                Convolve(
                    ref sourceRow,
                    tapStride,
                    0,
                    ref coefficientBase,
                    tapCount,
                    initial,
                    out Vector128<int> result0,
                    out Vector128<int> result1,
                    out Vector128<int> result2,
                    out Vector128<int> result3);

                Round(ref result0, ref result1, ref result2, ref result3, firstRound, secondRound);
                StorePartial(PackBytes(result0, result1, result2, result3), ref destinationRow, width);
                continue;
            }

            nuint vectorCount = Numerics.Vector128Count<byte>(width - processedColumns);
            for (; vectorCount > 0; vectorCount--, processedColumns += Vector128<byte>.Count)
            {
                Convolve(
                    ref sourceRow,
                    tapStride,
                    (nuint)processedColumns,
                    ref coefficientBase,
                    tapCount,
                    initial,
                    out Vector128<int> result0,
                    out Vector128<int> result1,
                    out Vector128<int> result2,
                    out Vector128<int> result3);

                Round(ref result0, ref result1, ref result2, ref result3, firstRound, secondRound);
                PackBytes(result0, result1, result2, result3).StoreUnsafe(ref destinationRow, (nuint)processedColumns);
            }

            FilterDirectTail(ref sourceRow, ref destinationRow, processedColumns, width, tapStride, ref coefficientBase, tapCount, firstRound, secondRound);
        }
    }

    /// <summary>
    /// Filters an 8-bit block in thirty-two-sample vectors.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The distance between source rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    /// <param name="coefficients">The filter coefficients, starting at the first applied tap.</param>
    /// <param name="tapCount">The number of applied filter taps.</param>
    /// <param name="sourceOffset">The offset from the integer-position sample to the sample of the first applied tap.</param>
    /// <param name="tapStride">The distance between consecutive tap samples: one for a horizontal filter, or the row stride for a vertical filter.</param>
    /// <param name="firstRound">The first rounding shift.</param>
    /// <param name="secondRound">The second rounding shift. Zero skips this rounding.</param>
    /// <param name="initial">The start value of each accumulator lane. The vector type selects this overload.</param>
    private static void FilterDirect(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        ReadOnlySpan<short> coefficients,
        int tapCount,
        int sourceOffset,
        int tapStride,
        int firstRound,
        int secondRound,
        Vector256<int> initial)
    {
        ref byte sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short coefficientBase = ref MemoryMarshal.GetReference(coefficients);
        int vectorEnd = (int)(Numerics.Vector256Count<byte>(width) * (nuint)Vector256<byte>.Count);

        for (int row = 0; row < height; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(ref sourceBase, (row * sourceStride) + sourceOffset);
            ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int processedColumns = 0;

            for (; processedColumns < vectorEnd; processedColumns += Vector256<byte>.Count)
            {
                Convolve(
                    ref sourceRow,
                    tapStride,
                    (nuint)processedColumns,
                    ref coefficientBase,
                    tapCount,
                    initial,
                    out Vector256<int> result0,
                    out Vector256<int> result1,
                    out Vector256<int> result2,
                    out Vector256<int> result3);

                Round(ref result0, ref result1, ref result2, ref result3, firstRound, secondRound);
                PackBytes(result0, result1, result2, result3).StoreUnsafe(ref destinationRow, (nuint)processedColumns);
            }

            FilterDirectTail(ref sourceRow, ref destinationRow, processedColumns, width, tapStride, ref coefficientBase, tapCount, firstRound, secondRound);
        }
    }

    /// <summary>
    /// Filters an 8-bit block in sixty-four-sample vectors.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The distance between source rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    /// <param name="coefficients">The filter coefficients, starting at the first applied tap.</param>
    /// <param name="tapCount">The number of applied filter taps.</param>
    /// <param name="sourceOffset">The offset from the integer-position sample to the sample of the first applied tap.</param>
    /// <param name="tapStride">The distance between consecutive tap samples: one for a horizontal filter, or the row stride for a vertical filter.</param>
    /// <param name="firstRound">The first rounding shift.</param>
    /// <param name="secondRound">The second rounding shift. Zero skips this rounding.</param>
    /// <param name="initial">The start value of each accumulator lane. The vector type selects this overload.</param>
    private static void FilterDirect(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        ReadOnlySpan<short> coefficients,
        int tapCount,
        int sourceOffset,
        int tapStride,
        int firstRound,
        int secondRound,
        Vector512<int> initial)
    {
        ref byte sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short coefficientBase = ref MemoryMarshal.GetReference(coefficients);
        int vectorEnd = (int)(Numerics.Vector512Count<byte>(width) * (nuint)Vector512<byte>.Count);

        for (int row = 0; row < height; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(ref sourceBase, (row * sourceStride) + sourceOffset);
            ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int processedColumns = 0;

            for (; processedColumns < vectorEnd; processedColumns += Vector512<byte>.Count)
            {
                Convolve(
                    ref sourceRow,
                    tapStride,
                    (nuint)processedColumns,
                    ref coefficientBase,
                    tapCount,
                    initial,
                    out Vector512<int> result0,
                    out Vector512<int> result1,
                    out Vector512<int> result2,
                    out Vector512<int> result3);

                Round(ref result0, ref result1, ref result2, ref result3, firstRound, secondRound);
                PackBytes(result0, result1, result2, result3).StoreUnsafe(ref destinationRow, (nuint)processedColumns);
            }

            FilterDirectTail(ref sourceRow, ref destinationRow, processedColumns, width, tapStride, ref coefficientBase, tapCount, firstRound, secondRound);
        }
    }

    /// <summary>
    /// Filters a high-bit-depth block in eight-sample vectors.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The distance between source rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    /// <param name="coefficients">The filter coefficients, starting at the first applied tap.</param>
    /// <param name="tapCount">The number of applied filter taps.</param>
    /// <param name="sourceOffset">The offset from the integer-position sample to the sample of the first applied tap.</param>
    /// <param name="tapStride">The distance between consecutive tap samples: one for a horizontal filter, or the row stride for a vertical filter.</param>
    /// <param name="firstRound">The first rounding shift.</param>
    /// <param name="secondRound">The second rounding shift. Zero skips this rounding.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="initial">The start value of each accumulator lane. The vector type selects this overload.</param>
    private static void FilterDirect(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        ReadOnlySpan<short> coefficients,
        int tapCount,
        int sourceOffset,
        int tapStride,
        int firstRound,
        int secondRound,
        int bitDepth,
        Vector128<int> initial)
    {
        ref ushort sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short coefficientBase = ref MemoryMarshal.GetReference(coefficients);
        int maximum = (1 << bitDepth) - 1;

        for (int row = 0; row < height; row++)
        {
            ref ushort sourceRowUnsigned = ref Unsafe.Add(ref sourceBase, (row * sourceStride) + sourceOffset);
            ref short sourceRow = ref Unsafe.As<ushort, short>(ref sourceRowUnsigned);
            ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int processedColumns = 0;

            if (width < Vector128<ushort>.Count)
            {
                Convolve(ref sourceRow, tapStride, 0, ref coefficientBase, tapCount, initial, out Vector128<int> result0, out Vector128<int> result1);
                Round(ref result0, ref result1, firstRound, secondRound);

                // Subsampled chroma of a block smaller than 8x8 can be two samples wide. The full source load stays for throughput.
                // The store writes only the row width, so the chroma prediction of the adjacent block does not change.
                StorePartial(PackHighBitDepth(result0, result1, maximum), ref destinationRow, width);
                continue;
            }

            nuint vectorCount = Numerics.Vector128Count<ushort>(width - processedColumns);
            for (; vectorCount > 0; vectorCount--, processedColumns += Vector128<ushort>.Count)
            {
                Convolve(ref sourceRow, tapStride, (nuint)processedColumns, ref coefficientBase, tapCount, initial, out Vector128<int> result0, out Vector128<int> result1);
                Round(ref result0, ref result1, firstRound, secondRound);
                PackHighBitDepth(result0, result1, maximum).StoreUnsafe(ref destinationRow, (nuint)processedColumns);
            }

            FilterDirectTail(ref sourceRowUnsigned, ref destinationRow, processedColumns, width, tapStride, ref coefficientBase, tapCount, firstRound, secondRound, maximum);
        }
    }

    /// <summary>
    /// Filters a high-bit-depth block in sixteen-sample vectors.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The distance between source rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    /// <param name="coefficients">The filter coefficients, starting at the first applied tap.</param>
    /// <param name="tapCount">The number of applied filter taps.</param>
    /// <param name="sourceOffset">The offset from the integer-position sample to the sample of the first applied tap.</param>
    /// <param name="tapStride">The distance between consecutive tap samples: one for a horizontal filter, or the row stride for a vertical filter.</param>
    /// <param name="firstRound">The first rounding shift.</param>
    /// <param name="secondRound">The second rounding shift. Zero skips this rounding.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="initial">The start value of each accumulator lane. The vector type selects this overload.</param>
    private static void FilterDirect(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        ReadOnlySpan<short> coefficients,
        int tapCount,
        int sourceOffset,
        int tapStride,
        int firstRound,
        int secondRound,
        int bitDepth,
        Vector256<int> initial)
    {
        ref ushort sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short coefficientBase = ref MemoryMarshal.GetReference(coefficients);
        int maximum = (1 << bitDepth) - 1;
        int vectorEnd = (int)(Numerics.Vector256Count<ushort>(width) * (nuint)Vector256<ushort>.Count);

        for (int row = 0; row < height; row++)
        {
            ref ushort sourceRowUnsigned = ref Unsafe.Add(ref sourceBase, (row * sourceStride) + sourceOffset);
            ref short sourceRow = ref Unsafe.As<ushort, short>(ref sourceRowUnsigned);
            ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int processedColumns = 0;

            for (; processedColumns < vectorEnd; processedColumns += Vector256<ushort>.Count)
            {
                Convolve(ref sourceRow, tapStride, (nuint)processedColumns, ref coefficientBase, tapCount, initial, out Vector256<int> result0, out Vector256<int> result1);
                Round(ref result0, ref result1, firstRound, secondRound);
                PackHighBitDepth(result0, result1, maximum).StoreUnsafe(ref destinationRow, (nuint)processedColumns);
            }

            FilterDirectTail(ref sourceRowUnsigned, ref destinationRow, processedColumns, width, tapStride, ref coefficientBase, tapCount, firstRound, secondRound, maximum);
        }
    }

    /// <summary>
    /// Filters a high-bit-depth block in thirty-two-sample vectors.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The distance between source rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    /// <param name="coefficients">The filter coefficients, starting at the first applied tap.</param>
    /// <param name="tapCount">The number of applied filter taps.</param>
    /// <param name="sourceOffset">The offset from the integer-position sample to the sample of the first applied tap.</param>
    /// <param name="tapStride">The distance between consecutive tap samples: one for a horizontal filter, or the row stride for a vertical filter.</param>
    /// <param name="firstRound">The first rounding shift.</param>
    /// <param name="secondRound">The second rounding shift. Zero skips this rounding.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="initial">The start value of each accumulator lane. The vector type selects this overload.</param>
    private static void FilterDirect(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        ReadOnlySpan<short> coefficients,
        int tapCount,
        int sourceOffset,
        int tapStride,
        int firstRound,
        int secondRound,
        int bitDepth,
        Vector512<int> initial)
    {
        ref ushort sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short coefficientBase = ref MemoryMarshal.GetReference(coefficients);
        int maximum = (1 << bitDepth) - 1;
        int vectorEnd = (int)(Numerics.Vector512Count<ushort>(width) * (nuint)Vector512<ushort>.Count);

        for (int row = 0; row < height; row++)
        {
            ref ushort sourceRowUnsigned = ref Unsafe.Add(ref sourceBase, (row * sourceStride) + sourceOffset);
            ref short sourceRow = ref Unsafe.As<ushort, short>(ref sourceRowUnsigned);
            ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int processedColumns = 0;

            for (; processedColumns < vectorEnd; processedColumns += Vector512<ushort>.Count)
            {
                Convolve(ref sourceRow, tapStride, (nuint)processedColumns, ref coefficientBase, tapCount, initial, out Vector512<int> result0, out Vector512<int> result1);
                Round(ref result0, ref result1, firstRound, secondRound);
                PackHighBitDepth(result0, result1, maximum).StoreUnsafe(ref destinationRow, (nuint)processedColumns);
            }

            FilterDirectTail(ref sourceRowUnsigned, ref destinationRow, processedColumns, width, tapStride, ref coefficientBase, tapCount, firstRound, secondRound, maximum);
        }
    }

    /// <summary>
    /// Applies the two direct-filter rounding stages to sixteen 8-bit results.
    /// </summary>
    /// <param name="result0">The first four sums, rounded in place.</param>
    /// <param name="result1">The second four sums, rounded in place.</param>
    /// <param name="result2">The third four sums, rounded in place.</param>
    /// <param name="result3">The fourth four sums, rounded in place.</param>
    /// <param name="firstRound">The first rounding shift.</param>
    /// <param name="secondRound">The second rounding shift. Zero skips this rounding.</param>
    private static void Round(ref Vector128<int> result0, ref Vector128<int> result1, ref Vector128<int> result2, ref Vector128<int> result3, int firstRound, int secondRound)
    {
        result0 = RoundPowerOfTwo(RoundPowerOfTwo(result0, firstRound), secondRound);
        result1 = RoundPowerOfTwo(RoundPowerOfTwo(result1, firstRound), secondRound);
        result2 = RoundPowerOfTwo(RoundPowerOfTwo(result2, firstRound), secondRound);
        result3 = RoundPowerOfTwo(RoundPowerOfTwo(result3, firstRound), secondRound);
    }

    /// <summary>
    /// Applies the two direct-filter rounding stages to thirty-two 8-bit results.
    /// </summary>
    /// <param name="result0">The first eight sums, rounded in place.</param>
    /// <param name="result1">The second eight sums, rounded in place.</param>
    /// <param name="result2">The third eight sums, rounded in place.</param>
    /// <param name="result3">The fourth eight sums, rounded in place.</param>
    /// <param name="firstRound">The first rounding shift.</param>
    /// <param name="secondRound">The second rounding shift. Zero skips this rounding.</param>
    private static void Round(ref Vector256<int> result0, ref Vector256<int> result1, ref Vector256<int> result2, ref Vector256<int> result3, int firstRound, int secondRound)
    {
        result0 = RoundPowerOfTwo(RoundPowerOfTwo(result0, firstRound), secondRound);
        result1 = RoundPowerOfTwo(RoundPowerOfTwo(result1, firstRound), secondRound);
        result2 = RoundPowerOfTwo(RoundPowerOfTwo(result2, firstRound), secondRound);
        result3 = RoundPowerOfTwo(RoundPowerOfTwo(result3, firstRound), secondRound);
    }

    /// <summary>
    /// Applies the two direct-filter rounding stages to sixty-four 8-bit results.
    /// </summary>
    /// <param name="result0">The first sixteen sums, rounded in place.</param>
    /// <param name="result1">The second sixteen sums, rounded in place.</param>
    /// <param name="result2">The third sixteen sums, rounded in place.</param>
    /// <param name="result3">The fourth sixteen sums, rounded in place.</param>
    /// <param name="firstRound">The first rounding shift.</param>
    /// <param name="secondRound">The second rounding shift. Zero skips this rounding.</param>
    private static void Round(ref Vector512<int> result0, ref Vector512<int> result1, ref Vector512<int> result2, ref Vector512<int> result3, int firstRound, int secondRound)
    {
        result0 = RoundPowerOfTwo(RoundPowerOfTwo(result0, firstRound), secondRound);
        result1 = RoundPowerOfTwo(RoundPowerOfTwo(result1, firstRound), secondRound);
        result2 = RoundPowerOfTwo(RoundPowerOfTwo(result2, firstRound), secondRound);
        result3 = RoundPowerOfTwo(RoundPowerOfTwo(result3, firstRound), secondRound);
    }

    /// <summary>
    /// Applies the two direct-filter rounding stages to eight high-bit-depth results.
    /// </summary>
    /// <param name="result0">The first four sums, rounded in place.</param>
    /// <param name="result1">The second four sums, rounded in place.</param>
    /// <param name="firstRound">The first rounding shift.</param>
    /// <param name="secondRound">The second rounding shift. Zero skips this rounding.</param>
    private static void Round(ref Vector128<int> result0, ref Vector128<int> result1, int firstRound, int secondRound)
    {
        result0 = RoundPowerOfTwo(RoundPowerOfTwo(result0, firstRound), secondRound);
        result1 = RoundPowerOfTwo(RoundPowerOfTwo(result1, firstRound), secondRound);
    }

    /// <summary>
    /// Applies the two direct-filter rounding stages to sixteen high-bit-depth results.
    /// </summary>
    /// <param name="result0">The first eight sums, rounded in place.</param>
    /// <param name="result1">The second eight sums, rounded in place.</param>
    /// <param name="firstRound">The first rounding shift.</param>
    /// <param name="secondRound">The second rounding shift. Zero skips this rounding.</param>
    private static void Round(ref Vector256<int> result0, ref Vector256<int> result1, int firstRound, int secondRound)
    {
        result0 = RoundPowerOfTwo(RoundPowerOfTwo(result0, firstRound), secondRound);
        result1 = RoundPowerOfTwo(RoundPowerOfTwo(result1, firstRound), secondRound);
    }

    /// <summary>
    /// Applies the two direct-filter rounding stages to thirty-two high-bit-depth results.
    /// </summary>
    /// <param name="result0">The first sixteen sums, rounded in place.</param>
    /// <param name="result1">The second sixteen sums, rounded in place.</param>
    /// <param name="firstRound">The first rounding shift.</param>
    /// <param name="secondRound">The second rounding shift. Zero skips this rounding.</param>
    private static void Round(ref Vector512<int> result0, ref Vector512<int> result1, int firstRound, int secondRound)
    {
        result0 = RoundPowerOfTwo(RoundPowerOfTwo(result0, firstRound), secondRound);
        result1 = RoundPowerOfTwo(RoundPowerOfTwo(result1, firstRound), secondRound);
    }

    /// <summary>
    /// Finishes an 8-bit row after its selected vector width.
    /// </summary>
    /// <param name="source">The sample that the first tap reads for column zero.</param>
    /// <param name="destination">The first sample of the destination row.</param>
    /// <param name="firstColumn">The first column that the vector loop did not write.</param>
    /// <param name="width">The row width in samples.</param>
    /// <param name="tapStride">The distance between the samples of consecutive taps.</param>
    /// <param name="coefficients">The first applied filter coefficient.</param>
    /// <param name="tapCount">The number of applied filter taps.</param>
    /// <param name="firstRound">The first rounding shift.</param>
    /// <param name="secondRound">The second rounding shift. Zero skips this rounding.</param>
    private static void FilterDirectTail(
        ref byte source,
        ref byte destination,
        int firstColumn,
        int width,
        int tapStride,
        ref short coefficients,
        int tapCount,
        int firstRound,
        int secondRound)
    {
        for (int column = firstColumn; column < width; column++)
        {
            int sum = ConvolveScalar(ref Unsafe.Add(ref source, column), tapStride, ref coefficients, tapCount);
            sum = RoundPowerOfTwo(sum, firstRound);
            sum = RoundPowerOfTwo(sum, secondRound);
            Unsafe.Add(ref destination, column) = (byte)Math.Clamp(sum, byte.MinValue, byte.MaxValue);
        }
    }

    /// <summary>
    /// Finishes a high-bit-depth row after its selected vector width.
    /// </summary>
    /// <param name="source">The sample that the first tap reads for column zero.</param>
    /// <param name="destination">The first sample of the destination row.</param>
    /// <param name="firstColumn">The first column that the vector loop did not write.</param>
    /// <param name="width">The row width in samples.</param>
    /// <param name="tapStride">The distance between the samples of consecutive taps.</param>
    /// <param name="coefficients">The first applied filter coefficient.</param>
    /// <param name="tapCount">The number of applied filter taps.</param>
    /// <param name="firstRound">The first rounding shift.</param>
    /// <param name="secondRound">The second rounding shift. Zero skips this rounding.</param>
    /// <param name="maximum">The largest sample value of the bit depth.</param>
    private static void FilterDirectTail(
        ref ushort source,
        ref ushort destination,
        int firstColumn,
        int width,
        int tapStride,
        ref short coefficients,
        int tapCount,
        int firstRound,
        int secondRound,
        int maximum)
    {
        for (int column = firstColumn; column < width; column++)
        {
            int sum = ConvolveScalar(ref Unsafe.Add(ref source, column), tapStride, ref coefficients, tapCount);
            sum = RoundPowerOfTwo(sum, firstRound);
            sum = RoundPowerOfTwo(sum, secondRound);
            Unsafe.Add(ref destination, column) = (ushort)Math.Clamp(sum, 0, maximum);
        }
    }

    /// <summary>
    /// Stores the two-, four-, or eight-sample prefix of a sixteen-byte prediction vector.
    /// </summary>
    /// <param name="value">The prediction vector.</param>
    /// <param name="destination">The first destination sample.</param>
    /// <param name="width">The prefix width: 2, 4, or 8. Any width other than 4 or 8 stores two samples.</param>
    private static void StorePartial(Vector128<byte> value, ref byte destination, int width)
    {
        if (width == 8)
        {
            value.GetLower().StoreUnsafe(ref destination);
        }
        else if (width == 4)
        {
            Unsafe.As<byte, uint>(ref destination) = value.AsUInt32().GetElement(0);
        }
        else
        {
            Unsafe.As<byte, ushort>(ref destination) = value.AsUInt16().GetElement(0);
        }
    }

    /// <summary>
    /// Stores the two-, four-, or eight-sample prefix of an eight-ushort prediction vector.
    /// </summary>
    /// <param name="value">The prediction vector.</param>
    /// <param name="destination">The first destination sample.</param>
    /// <param name="width">The prefix width: 2, 4, or 8. Any width other than 4 or 8 stores two samples.</param>
    private static void StorePartial(Vector128<ushort> value, ref ushort destination, int width)
    {
        if (width == 8)
        {
            value.StoreUnsafe(ref destination);
        }
        else if (width == 4)
        {
            value.GetLower().StoreUnsafe(ref destination);
        }
        else
        {
            Unsafe.As<ushort, uint>(ref destination) = value.AsUInt32().GetElement(0);
        }
    }

    /// <summary>
    /// Applies a one-dimensional 8-bit filter without explicit hardware intrinsics.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The distance between source rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    /// <param name="coefficients">The filter coefficients, starting at the first applied tap.</param>
    /// <param name="tapCount">The number of applied filter taps.</param>
    /// <param name="sourceOffset">The offset from the integer-position sample to the sample of the first applied tap.</param>
    /// <param name="tapStride">The distance between consecutive tap samples: one for a horizontal filter, or the row stride for a vertical filter.</param>
    /// <param name="firstRound">The first rounding shift.</param>
    /// <param name="secondRound">The second rounding shift. Zero skips this rounding.</param>
    private static void FilterDirectScalar(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        ReadOnlySpan<short> coefficients,
        int tapCount,
        int sourceOffset,
        int tapStride,
        int firstRound,
        int secondRound)
    {
        ref byte sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short coefficientBase = ref MemoryMarshal.GetReference(coefficients);

        for (int row = 0; row < height; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(ref sourceBase, (row * sourceStride) + sourceOffset);
            ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

            for (int column = 0; column < width; column++)
            {
                int sum = ConvolveScalar(ref Unsafe.Add(ref sourceRow, column), tapStride, ref coefficientBase, tapCount);
                sum = RoundPowerOfTwo(sum, firstRound);

                if (secondRound != 0)
                {
                    sum = RoundPowerOfTwo(sum, secondRound);
                }

                Unsafe.Add(ref destinationRow, column) = (byte)Math.Clamp(sum, byte.MinValue, byte.MaxValue);
            }
        }
    }

    /// <summary>
    /// Applies a one-dimensional high-bit-depth filter without explicit hardware intrinsics.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The distance between source rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    /// <param name="coefficients">The filter coefficients, starting at the first applied tap.</param>
    /// <param name="tapCount">The number of applied filter taps.</param>
    /// <param name="sourceOffset">The offset from the integer-position sample to the sample of the first applied tap.</param>
    /// <param name="tapStride">The distance between consecutive tap samples: one for a horizontal filter, or the row stride for a vertical filter.</param>
    /// <param name="firstRound">The first rounding shift.</param>
    /// <param name="secondRound">The second rounding shift. Zero skips this rounding.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    private static void FilterDirectScalar(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        ReadOnlySpan<short> coefficients,
        int tapCount,
        int sourceOffset,
        int tapStride,
        int firstRound,
        int secondRound,
        int bitDepth)
    {
        ref ushort sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short coefficientBase = ref MemoryMarshal.GetReference(coefficients);
        int maximum = (1 << bitDepth) - 1;

        for (int row = 0; row < height; row++)
        {
            ref ushort sourceRow = ref Unsafe.Add(ref sourceBase, (row * sourceStride) + sourceOffset);
            ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

            for (int column = 0; column < width; column++)
            {
                int sum = ConvolveScalar(ref Unsafe.Add(ref sourceRow, column), tapStride, ref coefficientBase, tapCount);
                sum = RoundPowerOfTwo(sum, firstRound);

                if (secondRound != 0)
                {
                    sum = RoundPowerOfTwo(sum, secondRound);
                }

                Unsafe.Add(ref destinationRow, column) = (ushort)Math.Clamp(sum, 0, maximum);
            }
        }
    }
}
