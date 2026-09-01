// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Provides separable SIMD convolution for 8-bit single-reference prediction.
/// </content>
internal static partial class Av1TranslationalInterPredictor
{
    /// <summary>
    /// Filters an 8-bit block in sixteen-sample vectors through caller-owned signed scratch.
    /// </summary>
    private static void Filter2D(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        ReadOnlySpan<short> horizontalCoefficients,
        int horizontalTapCount,
        int horizontalSourceOffset,
        ReadOnlySpan<short> verticalCoefficients,
        int verticalTapCount,
        int verticalSourceOffset,
        int bitDepth,
        int round0,
        Span<short> scratch,
        Vector128<int> initial)
    {
        ref byte sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short scratchBase = ref MemoryMarshal.GetReference(scratch);
        ref short horizontalCoefficientBase = ref MemoryMarshal.GetReference(horizontalCoefficients);
        ref short verticalCoefficientBase = ref MemoryMarshal.GetReference(verticalCoefficients);
        int scratchStride = Math.Max(width, MinimumScratchStride);
        int intermediateHeight = height + verticalTapCount - 1;
        Vector128<int> horizontalInitial = initial + Vector128.Create(1 << (bitDepth + FilterBits - 1));

        for (int row = 0; row < intermediateHeight; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(ref sourceBase, ((row + verticalSourceOffset) * sourceStride) + horizontalSourceOffset);
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            int processedColumns = 0;

            if (width < Vector128<byte>.Count)
            {
                Convolve(
                    ref sourceRow,
                    1,
                    0,
                    ref horizontalCoefficientBase,
                    horizontalTapCount,
                    horizontalInitial,
                    out Vector128<int> result0,
                    out Vector128<int> result1,
                    out Vector128<int> result2,
                    out Vector128<int> result3);

                Av1NonDirectionalIntraPredictorBase.Narrow(RoundPowerOfTwo(result0, round0), RoundPowerOfTwo(result1, round0)).StoreUnsafe(ref scratchRow);
                Av1NonDirectionalIntraPredictorBase.Narrow(RoundPowerOfTwo(result2, round0), RoundPowerOfTwo(result3, round0)).StoreUnsafe(ref scratchRow, (nuint)Vector128<short>.Count);
                continue;
            }

            int vectorEnd = width - Vector128<byte>.Count;
            for (; processedColumns <= vectorEnd; processedColumns += Vector128<byte>.Count)
            {
                Convolve(
                    ref sourceRow,
                    1,
                    (nuint)processedColumns,
                    ref horizontalCoefficientBase,
                    horizontalTapCount,
                    horizontalInitial,
                    out Vector128<int> result0,
                    out Vector128<int> result1,
                    out Vector128<int> result2,
                    out Vector128<int> result3);

                Av1NonDirectionalIntraPredictorBase.Narrow(RoundPowerOfTwo(result0, round0), RoundPowerOfTwo(result1, round0)).StoreUnsafe(ref scratchRow, (nuint)processedColumns);
                Av1NonDirectionalIntraPredictorBase.Narrow(
                    RoundPowerOfTwo(result2, round0),
                    RoundPowerOfTwo(result3, round0)).StoreUnsafe(
                        ref scratchRow,
                        (nuint)(processedColumns + Vector128<short>.Count));
            }

            FilterHorizontalTail(ref sourceRow, ref scratchRow, processedColumns, width, ref horizontalCoefficientBase, horizontalTapCount, bitDepth, round0);
        }

        int round1 = (2 * FilterBits) - round0;
        int offsetBits = bitDepth + (2 * FilterBits) - round0;
        Vector128<int> verticalInitial = initial + Vector128.Create(1 << offsetBits);
        Vector128<int> roundOffset = Vector128.Create((1 << (offsetBits - round1)) + (1 << (offsetBits - round1 - 1)));

        for (int row = 0; row < height; row++)
        {
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int processedColumns = 0;

            if (width < Vector128<byte>.Count)
            {
                Convolve(
                    ref scratchRow,
                    scratchStride,
                    0,
                    ref verticalCoefficientBase,
                    verticalTapCount,
                    verticalInitial,
                    out Vector128<int> result0,
                    out Vector128<int> result1);

                Convolve(
                    ref Unsafe.Add(ref scratchRow, Vector128<short>.Count),
                    scratchStride,
                    0,
                    ref verticalCoefficientBase,
                    verticalTapCount,
                    verticalInitial,
                    out Vector128<int> result2,
                    out Vector128<int> result3);

                result0 = RoundPowerOfTwo(result0, round1) - roundOffset;
                result1 = RoundPowerOfTwo(result1, round1) - roundOffset;
                result2 = RoundPowerOfTwo(result2, round1) - roundOffset;
                result3 = RoundPowerOfTwo(result3, round1) - roundOffset;
                StorePartial(PackBytes(result0, result1, result2, result3), ref destinationRow, width);
                continue;
            }

            int vectorEnd = width - Vector128<byte>.Count;
            for (; processedColumns <= vectorEnd; processedColumns += Vector128<byte>.Count)
            {
                Convolve(
                    ref scratchRow,
                    scratchStride,
                    (nuint)processedColumns,
                    ref verticalCoefficientBase,
                    verticalTapCount,
                    verticalInitial,
                    out Vector128<int> result0,
                    out Vector128<int> result1);

                Convolve(
                    ref scratchRow,
                    scratchStride,
                    (nuint)(processedColumns + Vector128<short>.Count),
                    ref verticalCoefficientBase,
                    verticalTapCount,
                    verticalInitial,
                    out Vector128<int> result2,
                    out Vector128<int> result3);

                result0 = RoundPowerOfTwo(result0, round1) - roundOffset;
                result1 = RoundPowerOfTwo(result1, round1) - roundOffset;
                result2 = RoundPowerOfTwo(result2, round1) - roundOffset;
                result3 = RoundPowerOfTwo(result3, round1) - roundOffset;
                PackBytes(result0, result1, result2, result3).StoreUnsafe(ref destinationRow, (nuint)processedColumns);
            }

            FilterVerticalTail(ref scratchRow, ref destinationRow, processedColumns, width, scratchStride, ref verticalCoefficientBase, verticalTapCount, bitDepth, round0);
        }
    }

    /// <summary>
    /// Filters an 8-bit block in thirty-two-sample vectors through caller-owned signed scratch.
    /// </summary>
    private static void Filter2D(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        ReadOnlySpan<short> horizontalCoefficients,
        int horizontalTapCount,
        int horizontalSourceOffset,
        ReadOnlySpan<short> verticalCoefficients,
        int verticalTapCount,
        int verticalSourceOffset,
        int bitDepth,
        int round0,
        Span<short> scratch,
        Vector256<int> initial)
    {
        ref byte sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short scratchBase = ref MemoryMarshal.GetReference(scratch);
        ref short horizontalCoefficientBase = ref MemoryMarshal.GetReference(horizontalCoefficients);
        ref short verticalCoefficientBase = ref MemoryMarshal.GetReference(verticalCoefficients);
        int scratchStride = Math.Max(width, MinimumScratchStride);
        int intermediateHeight = height + verticalTapCount - 1;
        Vector256<int> horizontalInitial = initial + Vector256.Create(1 << (bitDepth + FilterBits - 1));
        int vectorEnd = width - Vector256<byte>.Count;

        for (int row = 0; row < intermediateHeight; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(ref sourceBase, ((row + verticalSourceOffset) * sourceStride) + horizontalSourceOffset);
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            int processedColumns = 0;

            for (; processedColumns <= vectorEnd; processedColumns += Vector256<byte>.Count)
            {
                Convolve(
                    ref sourceRow,
                    1,
                    (nuint)processedColumns,
                    ref horizontalCoefficientBase,
                    horizontalTapCount,
                    horizontalInitial,
                    out Vector256<int> result0,
                    out Vector256<int> result1,
                    out Vector256<int> result2,
                    out Vector256<int> result3);

                Av1NonDirectionalIntraPredictorBase.Narrow(RoundPowerOfTwo(result0, round0), RoundPowerOfTwo(result1, round0)).StoreUnsafe(ref scratchRow, (nuint)processedColumns);
                Av1NonDirectionalIntraPredictorBase.Narrow(
                    RoundPowerOfTwo(result2, round0),
                    RoundPowerOfTwo(result3, round0)).StoreUnsafe(
                        ref scratchRow,
                        (nuint)(processedColumns + Vector256<short>.Count));
            }

            FilterHorizontalTail(ref sourceRow, ref scratchRow, processedColumns, width, ref horizontalCoefficientBase, horizontalTapCount, bitDepth, round0);
        }

        int round1 = (2 * FilterBits) - round0;
        int offsetBits = bitDepth + (2 * FilterBits) - round0;
        Vector256<int> verticalInitial = initial + Vector256.Create(1 << offsetBits);
        Vector256<int> roundOffset = Vector256.Create((1 << (offsetBits - round1)) + (1 << (offsetBits - round1 - 1)));

        for (int row = 0; row < height; row++)
        {
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int processedColumns = 0;

            for (; processedColumns <= vectorEnd; processedColumns += Vector256<byte>.Count)
            {
                Convolve(
                    ref scratchRow,
                    scratchStride,
                    (nuint)processedColumns,
                    ref verticalCoefficientBase,
                    verticalTapCount,
                    verticalInitial,
                    out Vector256<int> result0,
                    out Vector256<int> result1);

                Convolve(
                    ref scratchRow,
                    scratchStride,
                    (nuint)(processedColumns + Vector256<short>.Count),
                    ref verticalCoefficientBase,
                    verticalTapCount,
                    verticalInitial,
                    out Vector256<int> result2,
                    out Vector256<int> result3);

                result0 = RoundPowerOfTwo(result0, round1) - roundOffset;
                result1 = RoundPowerOfTwo(result1, round1) - roundOffset;
                result2 = RoundPowerOfTwo(result2, round1) - roundOffset;
                result3 = RoundPowerOfTwo(result3, round1) - roundOffset;
                PackBytes(result0, result1, result2, result3).StoreUnsafe(ref destinationRow, (nuint)processedColumns);
            }

            FilterVerticalTail(ref scratchRow, ref destinationRow, processedColumns, width, scratchStride, ref verticalCoefficientBase, verticalTapCount, bitDepth, round0);
        }
    }

    /// <summary>
    /// Filters an 8-bit block in sixty-four-sample vectors through caller-owned signed scratch.
    /// </summary>
    private static void Filter2D(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        ReadOnlySpan<short> horizontalCoefficients,
        int horizontalTapCount,
        int horizontalSourceOffset,
        ReadOnlySpan<short> verticalCoefficients,
        int verticalTapCount,
        int verticalSourceOffset,
        int bitDepth,
        int round0,
        Span<short> scratch,
        Vector512<int> initial)
    {
        ref byte sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short scratchBase = ref MemoryMarshal.GetReference(scratch);
        ref short horizontalCoefficientBase = ref MemoryMarshal.GetReference(horizontalCoefficients);
        ref short verticalCoefficientBase = ref MemoryMarshal.GetReference(verticalCoefficients);
        int scratchStride = Math.Max(width, MinimumScratchStride);
        int intermediateHeight = height + verticalTapCount - 1;
        Vector512<int> horizontalInitial = initial + Vector512.Create(1 << (bitDepth + FilterBits - 1));
        int vectorEnd = width - Vector512<byte>.Count;

        for (int row = 0; row < intermediateHeight; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(ref sourceBase, ((row + verticalSourceOffset) * sourceStride) + horizontalSourceOffset);
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            int processedColumns = 0;

            for (; processedColumns <= vectorEnd; processedColumns += Vector512<byte>.Count)
            {
                Convolve(
                    ref sourceRow,
                    1,
                    (nuint)processedColumns,
                    ref horizontalCoefficientBase,
                    horizontalTapCount,
                    horizontalInitial,
                    out Vector512<int> result0,
                    out Vector512<int> result1,
                    out Vector512<int> result2,
                    out Vector512<int> result3);

                Av1NonDirectionalIntraPredictorBase.Narrow(RoundPowerOfTwo(result0, round0), RoundPowerOfTwo(result1, round0)).StoreUnsafe(ref scratchRow, (nuint)processedColumns);
                Av1NonDirectionalIntraPredictorBase.Narrow(
                    RoundPowerOfTwo(result2, round0),
                    RoundPowerOfTwo(result3, round0)).StoreUnsafe(
                        ref scratchRow,
                        (nuint)(processedColumns + Vector512<short>.Count));
            }

            FilterHorizontalTail(ref sourceRow, ref scratchRow, processedColumns, width, ref horizontalCoefficientBase, horizontalTapCount, bitDepth, round0);
        }

        int round1 = (2 * FilterBits) - round0;
        int offsetBits = bitDepth + (2 * FilterBits) - round0;
        Vector512<int> verticalInitial = initial + Vector512.Create(1 << offsetBits);
        Vector512<int> roundOffset = Vector512.Create((1 << (offsetBits - round1)) + (1 << (offsetBits - round1 - 1)));

        for (int row = 0; row < height; row++)
        {
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int processedColumns = 0;

            for (; processedColumns <= vectorEnd; processedColumns += Vector512<byte>.Count)
            {
                Convolve(
                    ref scratchRow,
                    scratchStride,
                    (nuint)processedColumns,
                    ref verticalCoefficientBase,
                    verticalTapCount,
                    verticalInitial,
                    out Vector512<int> result0,
                    out Vector512<int> result1);

                Convolve(
                    ref scratchRow,
                    scratchStride,
                    (nuint)(processedColumns + Vector512<short>.Count),
                    ref verticalCoefficientBase,
                    verticalTapCount,
                    verticalInitial,
                    out Vector512<int> result2,
                    out Vector512<int> result3);

                result0 = RoundPowerOfTwo(result0, round1) - roundOffset;
                result1 = RoundPowerOfTwo(result1, round1) - roundOffset;
                result2 = RoundPowerOfTwo(result2, round1) - roundOffset;
                result3 = RoundPowerOfTwo(result3, round1) - roundOffset;
                PackBytes(result0, result1, result2, result3).StoreUnsafe(ref destinationRow, (nuint)processedColumns);
            }

            FilterVerticalTail(ref scratchRow, ref destinationRow, processedColumns, width, scratchStride, ref verticalCoefficientBase, verticalTapCount, bitDepth, round0);
        }
    }

    /// <summary>
    /// Finishes an 8-bit horizontal intermediate row after its selected vector width.
    /// </summary>
    private static void FilterHorizontalTail(ref byte source, ref short scratch, int firstColumn, int width, ref short coefficients, int tapCount, int bitDepth, int round0)
    {
        int horizontalBias = 1 << (bitDepth + FilterBits - 1);
        for (int column = firstColumn; column < width; column++)
        {
            int sum = horizontalBias + ConvolveScalar(ref Unsafe.Add(ref source, column), 1, ref coefficients, tapCount);
            Unsafe.Add(ref scratch, column) = (short)RoundPowerOfTwo(sum, round0);
        }
    }

    /// <summary>
    /// Finishes an 8-bit vertical output row after its selected vector width.
    /// </summary>
    private static void FilterVerticalTail(
        ref short scratch,
        ref byte destination,
        int firstColumn,
        int width,
        int scratchStride,
        ref short coefficients,
        int tapCount,
        int bitDepth,
        int round0)
    {
        int round1 = (2 * FilterBits) - round0;
        int offsetBits = bitDepth + (2 * FilterBits) - round0;
        int verticalBias = 1 << offsetBits;
        int roundOffset = (1 << (offsetBits - round1)) + (1 << (offsetBits - round1 - 1));

        for (int column = firstColumn; column < width; column++)
        {
            int sum = verticalBias + ConvolveScalar(ref Unsafe.Add(ref scratch, column), scratchStride, ref coefficients, tapCount);
            int result = RoundPowerOfTwo(sum, round1) - roundOffset;
            Unsafe.Add(ref destination, column) = (byte)Math.Clamp(result, byte.MinValue, byte.MaxValue);
        }
    }

    /// <summary>
    /// Applies separable two-dimensional filtering to an 8-bit block without explicit hardware intrinsics.
    /// </summary>
    private static void Filter2DScalar(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        ReadOnlySpan<short> horizontalCoefficients,
        int horizontalTapCount,
        int horizontalSourceOffset,
        ReadOnlySpan<short> verticalCoefficients,
        int verticalTapCount,
        int verticalSourceOffset,
        int bitDepth,
        int round0,
        Span<short> scratch)
    {
        ref byte sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short scratchBase = ref MemoryMarshal.GetReference(scratch);
        ref short horizontalCoefficientBase = ref MemoryMarshal.GetReference(horizontalCoefficients);
        ref short verticalCoefficientBase = ref MemoryMarshal.GetReference(verticalCoefficients);
        int scratchStride = Math.Max(width, MinimumScratchStride);
        int intermediateHeight = height + verticalTapCount - 1;
        int horizontalBias = 1 << (bitDepth + FilterBits - 1);

        // The first intermediate row corresponds to the uppermost vertical tap. Horizontal filtering therefore starts
        // above the nominal source origin and writes one row for every vertical-tap position needed by the final pass.
        for (int row = 0; row < intermediateHeight; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(ref sourceBase, ((row + verticalSourceOffset) * sourceStride) + horizontalSourceOffset);
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);

            for (int column = 0; column < width; column++)
            {
                int sum = horizontalBias + ConvolveScalar(ref Unsafe.Add(ref sourceRow, column), 1, ref horizontalCoefficientBase, horizontalTapCount);
                Unsafe.Add(ref scratchRow, column) = (short)RoundPowerOfTwo(sum, round0);
            }
        }

        int round1 = (2 * FilterBits) - round0;
        int offsetBits = bitDepth + (2 * FilterBits) - round0;
        int verticalBias = 1 << offsetBits;
        int roundOffset = (1 << (offsetBits - round1)) + (1 << (offsetBits - round1 - 1));

        // The biased first pass keeps every intermediate nonnegative and representable by a signed 16-bit lane.
        // Removing both bias terms after the vertical Q7 filter reproduces the reference decoder's single-reference rounding exactly.
        for (int row = 0; row < height; row++)
        {
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

            for (int column = 0; column < width; column++)
            {
                int sum = verticalBias + ConvolveScalar(ref Unsafe.Add(ref scratchRow, column), scratchStride, ref verticalCoefficientBase, verticalTapCount);
                int result = RoundPowerOfTwo(sum, round1) - roundOffset;
                Unsafe.Add(ref destinationRow, column) = (byte)Math.Clamp(result, byte.MinValue, byte.MaxValue);
            }
        }
    }
}
