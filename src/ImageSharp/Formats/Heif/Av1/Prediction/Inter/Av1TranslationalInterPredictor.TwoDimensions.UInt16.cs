// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Provides separable SIMD convolution for unsigned 16-bit single-reference prediction.
/// </content>
internal static partial class Av1TranslationalInterPredictor
{
    /// <summary>
    /// Filters a high-bit-depth block in eight-sample vectors through caller-owned signed scratch.
    /// </summary>
    private static void Filter2D(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
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
        ref ushort sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short scratchBase = ref MemoryMarshal.GetReference(scratch);
        ref short horizontalCoefficientBase = ref MemoryMarshal.GetReference(horizontalCoefficients);
        ref short verticalCoefficientBase = ref MemoryMarshal.GetReference(verticalCoefficients);
        int scratchStride = Math.Max(width, MinimumScratchStride);
        int intermediateHeight = height + verticalTapCount - 1;
        Vector128<int> horizontalInitial = initial + Vector128.Create(1 << (bitDepth + FilterBits - 1));

        for (int row = 0; row < intermediateHeight; row++)
        {
            ref ushort sourceRowUnsigned = ref Unsafe.Add(
                ref sourceBase,
                ((row + verticalSourceOffset) * sourceStride) + horizontalSourceOffset);

            ref short sourceRow = ref Unsafe.As<ushort, short>(ref sourceRowUnsigned);
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            int processedColumns = 0;

            if (width < Vector128<ushort>.Count)
            {
                // Subsampled sub-8x8 chroma can be two samples wide. The reference plane is padded for the full source
                // load, and the minimum scratch stride preserves all eight intermediate lanes for the vertical pass.
                Convolve(
                    ref sourceRow,
                    1,
                    0,
                    ref horizontalCoefficientBase,
                    horizontalTapCount,
                    horizontalInitial,
                    out Vector128<int> result0,
                    out Vector128<int> result1);

                Av1NonDirectionalIntraPredictorBase.Narrow(
                    RoundPowerOfTwo(result0, round0),
                    RoundPowerOfTwo(result1, round0)).StoreUnsafe(ref scratchRow);

                continue;
            }

            nuint vectorCount = Numerics.Vector128Count<ushort>(width - processedColumns);
            for (; vectorCount > 0; vectorCount--, processedColumns += Vector128<ushort>.Count)
            {
                Convolve(
                    ref sourceRow,
                    1,
                    (nuint)processedColumns,
                    ref horizontalCoefficientBase,
                    horizontalTapCount,
                    horizontalInitial,
                    out Vector128<int> result0,
                    out Vector128<int> result1);

                Av1NonDirectionalIntraPredictorBase.Narrow(
                    RoundPowerOfTwo(result0, round0),
                    RoundPowerOfTwo(result1, round0)).StoreUnsafe(ref scratchRow, (nuint)processedColumns);
            }

            FilterHorizontalTail(
                ref sourceRowUnsigned,
                ref scratchRow,
                processedColumns,
                width,
                ref horizontalCoefficientBase,
                horizontalTapCount,
                bitDepth,
                round0);
        }

        int round1 = (2 * FilterBits) - round0;
        int offsetBits = bitDepth + (2 * FilterBits) - round0;
        Vector128<int> verticalInitial = initial + Vector128.Create(1 << offsetBits);
        Vector128<int> roundOffset = Vector128.Create(
            (1 << (offsetBits - round1)) + (1 << (offsetBits - round1 - 1)));

        int maximum = (1 << bitDepth) - 1;

        for (int row = 0; row < height; row++)
        {
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int processedColumns = 0;

            if (width < Vector128<ushort>.Count)
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

                result0 = RoundPowerOfTwo(result0, round1) - roundOffset;
                result1 = RoundPowerOfTwo(result1, round1) - roundOffset;

                // Retain the vector convolution for two- and four-sample rows while leaving adjacent destination
                // samples owned by the neighboring luma block untouched.
                StorePartial(PackHighBitDepth(result0, result1, maximum), ref destinationRow, width);
                continue;
            }

            nuint vectorCount = Numerics.Vector128Count<ushort>(width - processedColumns);
            for (; vectorCount > 0; vectorCount--, processedColumns += Vector128<ushort>.Count)
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

                result0 = RoundPowerOfTwo(result0, round1) - roundOffset;
                result1 = RoundPowerOfTwo(result1, round1) - roundOffset;
                PackHighBitDepth(result0, result1, maximum).StoreUnsafe(ref destinationRow, (nuint)processedColumns);
            }

            FilterVerticalTail(
                ref scratchRow,
                ref destinationRow,
                processedColumns,
                width,
                scratchStride,
                ref verticalCoefficientBase,
                verticalTapCount,
                bitDepth,
                round0);
        }
    }

    /// <summary>
    /// Filters a high-bit-depth block in sixteen-sample vectors through caller-owned signed scratch.
    /// </summary>
    private static void Filter2D(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
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
        ref ushort sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short scratchBase = ref MemoryMarshal.GetReference(scratch);
        ref short horizontalCoefficientBase = ref MemoryMarshal.GetReference(horizontalCoefficients);
        ref short verticalCoefficientBase = ref MemoryMarshal.GetReference(verticalCoefficients);
        int scratchStride = Math.Max(width, MinimumScratchStride);
        int intermediateHeight = height + verticalTapCount - 1;
        Vector256<int> horizontalInitial = initial + Vector256.Create(1 << (bitDepth + FilterBits - 1));
        int vectorEnd = (int)(Numerics.Vector256Count<ushort>(width) * (nuint)Vector256<ushort>.Count);

        for (int row = 0; row < intermediateHeight; row++)
        {
            ref ushort sourceRowUnsigned = ref Unsafe.Add(
                ref sourceBase,
                ((row + verticalSourceOffset) * sourceStride) + horizontalSourceOffset);

            ref short sourceRow = ref Unsafe.As<ushort, short>(ref sourceRowUnsigned);
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            int processedColumns = 0;

            for (; processedColumns < vectorEnd; processedColumns += Vector256<ushort>.Count)
            {
                Convolve(
                    ref sourceRow,
                    1,
                    (nuint)processedColumns,
                    ref horizontalCoefficientBase,
                    horizontalTapCount,
                    horizontalInitial,
                    out Vector256<int> result0,
                    out Vector256<int> result1);

                Av1NonDirectionalIntraPredictorBase.Narrow(
                    RoundPowerOfTwo(result0, round0),
                    RoundPowerOfTwo(result1, round0)).StoreUnsafe(ref scratchRow, (nuint)processedColumns);
            }

            FilterHorizontalTail(
                ref sourceRowUnsigned,
                ref scratchRow,
                processedColumns,
                width,
                ref horizontalCoefficientBase,
                horizontalTapCount,
                bitDepth,
                round0);
        }

        int round1 = (2 * FilterBits) - round0;
        int offsetBits = bitDepth + (2 * FilterBits) - round0;
        Vector256<int> verticalInitial = initial + Vector256.Create(1 << offsetBits);
        Vector256<int> roundOffset = Vector256.Create(
            (1 << (offsetBits - round1)) + (1 << (offsetBits - round1 - 1)));

        int maximum = (1 << bitDepth) - 1;

        for (int row = 0; row < height; row++)
        {
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int processedColumns = 0;

            for (; processedColumns < vectorEnd; processedColumns += Vector256<ushort>.Count)
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

                result0 = RoundPowerOfTwo(result0, round1) - roundOffset;
                result1 = RoundPowerOfTwo(result1, round1) - roundOffset;
                PackHighBitDepth(result0, result1, maximum).StoreUnsafe(ref destinationRow, (nuint)processedColumns);
            }

            FilterVerticalTail(
                ref scratchRow,
                ref destinationRow,
                processedColumns,
                width,
                scratchStride,
                ref verticalCoefficientBase,
                verticalTapCount,
                bitDepth,
                round0);
        }
    }

    /// <summary>
    /// Filters a high-bit-depth block in thirty-two-sample vectors through caller-owned signed scratch.
    /// </summary>
    private static void Filter2D(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
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
        ref ushort sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short scratchBase = ref MemoryMarshal.GetReference(scratch);
        ref short horizontalCoefficientBase = ref MemoryMarshal.GetReference(horizontalCoefficients);
        ref short verticalCoefficientBase = ref MemoryMarshal.GetReference(verticalCoefficients);
        int scratchStride = Math.Max(width, MinimumScratchStride);
        int intermediateHeight = height + verticalTapCount - 1;
        Vector512<int> horizontalInitial = initial + Vector512.Create(1 << (bitDepth + FilterBits - 1));
        int vectorEnd = (int)(Numerics.Vector512Count<ushort>(width) * (nuint)Vector512<ushort>.Count);

        for (int row = 0; row < intermediateHeight; row++)
        {
            ref ushort sourceRowUnsigned = ref Unsafe.Add(
                ref sourceBase,
                ((row + verticalSourceOffset) * sourceStride) + horizontalSourceOffset);

            ref short sourceRow = ref Unsafe.As<ushort, short>(ref sourceRowUnsigned);
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            int processedColumns = 0;

            for (; processedColumns < vectorEnd; processedColumns += Vector512<ushort>.Count)
            {
                Convolve(
                    ref sourceRow,
                    1,
                    (nuint)processedColumns,
                    ref horizontalCoefficientBase,
                    horizontalTapCount,
                    horizontalInitial,
                    out Vector512<int> result0,
                    out Vector512<int> result1);

                Av1NonDirectionalIntraPredictorBase.Narrow(
                    RoundPowerOfTwo(result0, round0),
                    RoundPowerOfTwo(result1, round0)).StoreUnsafe(ref scratchRow, (nuint)processedColumns);
            }

            FilterHorizontalTail(
                ref sourceRowUnsigned,
                ref scratchRow,
                processedColumns,
                width,
                ref horizontalCoefficientBase,
                horizontalTapCount,
                bitDepth,
                round0);
        }

        int round1 = (2 * FilterBits) - round0;
        int offsetBits = bitDepth + (2 * FilterBits) - round0;
        Vector512<int> verticalInitial = initial + Vector512.Create(1 << offsetBits);
        Vector512<int> roundOffset = Vector512.Create(
            (1 << (offsetBits - round1)) + (1 << (offsetBits - round1 - 1)));

        int maximum = (1 << bitDepth) - 1;

        for (int row = 0; row < height; row++)
        {
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int processedColumns = 0;

            for (; processedColumns < vectorEnd; processedColumns += Vector512<ushort>.Count)
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

                result0 = RoundPowerOfTwo(result0, round1) - roundOffset;
                result1 = RoundPowerOfTwo(result1, round1) - roundOffset;
                PackHighBitDepth(result0, result1, maximum).StoreUnsafe(ref destinationRow, (nuint)processedColumns);
            }

            FilterVerticalTail(
                ref scratchRow,
                ref destinationRow,
                processedColumns,
                width,
                scratchStride,
                ref verticalCoefficientBase,
                verticalTapCount,
                bitDepth,
                round0);
        }
    }

    /// <summary>
    /// Finishes a high-bit-depth horizontal intermediate row after its selected vector width.
    /// </summary>
    private static void FilterHorizontalTail(
        ref ushort source,
        ref short scratch,
        int firstColumn,
        int width,
        ref short coefficients,
        int tapCount,
        int bitDepth,
        int round0)
    {
        int horizontalBias = 1 << (bitDepth + FilterBits - 1);
        for (int column = firstColumn; column < width; column++)
        {
            int sum = horizontalBias + ConvolveScalar(
                ref Unsafe.Add(ref source, column),
                1,
                ref coefficients,
                tapCount);

            Unsafe.Add(ref scratch, column) = (short)RoundPowerOfTwo(sum, round0);
        }
    }

    /// <summary>
    /// Finishes a high-bit-depth vertical output row after its selected vector width.
    /// </summary>
    private static void FilterVerticalTail(
        ref short scratch,
        ref ushort destination,
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
        int maximum = (1 << bitDepth) - 1;

        for (int column = firstColumn; column < width; column++)
        {
            int sum = verticalBias + ConvolveScalar(
                ref Unsafe.Add(ref scratch, column),
                scratchStride,
                ref coefficients,
                tapCount);

            int result = RoundPowerOfTwo(sum, round1) - roundOffset;
            Unsafe.Add(ref destination, column) = (ushort)Math.Clamp(result, 0, maximum);
        }
    }

    /// <summary>
    /// Applies separable two-dimensional filtering to a high-bit-depth block without explicit hardware intrinsics.
    /// </summary>
    private static void Filter2DScalar(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
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
        ref ushort sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short scratchBase = ref MemoryMarshal.GetReference(scratch);
        ref short horizontalCoefficientBase = ref MemoryMarshal.GetReference(horizontalCoefficients);
        ref short verticalCoefficientBase = ref MemoryMarshal.GetReference(verticalCoefficients);
        int scratchStride = Math.Max(width, MinimumScratchStride);
        int intermediateHeight = height + verticalTapCount - 1;
        int horizontalBias = 1 << (bitDepth + FilterBits - 1);

        for (int row = 0; row < intermediateHeight; row++)
        {
            ref ushort sourceRow = ref Unsafe.Add(ref sourceBase, ((row + verticalSourceOffset) * sourceStride) + horizontalSourceOffset);
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
        int maximum = (1 << bitDepth) - 1;

        for (int row = 0; row < height; row++)
        {
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

            for (int column = 0; column < width; column++)
            {
                int sum = verticalBias + ConvolveScalar(ref Unsafe.Add(ref scratchRow, column), scratchStride, ref verticalCoefficientBase, verticalTapCount);
                int result = RoundPowerOfTwo(sum, round1) - roundOffset;
                Unsafe.Add(ref destinationRow, column) = (ushort)Math.Clamp(result, 0, maximum);
            }
        }
    }
}
