// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Provides SIMD kernels for horizontal-only and vertical-only single-reference filtering.
/// </content>
internal static partial class Av1InterPredictor
{
    /// <summary>
    /// Filters an 8-bit block in sixteen-sample vectors.
    /// </summary>
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
                // Four- and eight-sample AV1 blocks are smaller than one byte vector. Reference-plane padding makes
                // the complete load readable, while the width-specific store leaves adjacent destination samples intact.
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

            int vectorEnd = width - Vector128<byte>.Count;
            for (; processedColumns <= vectorEnd; processedColumns += Vector128<byte>.Count)
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
        int vectorEnd = width - Vector256<byte>.Count;

        for (int row = 0; row < height; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(ref sourceBase, (row * sourceStride) + sourceOffset);
            ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int processedColumns = 0;

            for (; processedColumns <= vectorEnd; processedColumns += Vector256<byte>.Count)
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
        int vectorEnd = width - Vector512<byte>.Count;

        for (int row = 0; row < height; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(ref sourceBase, (row * sourceStride) + sourceOffset);
            ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int processedColumns = 0;

            for (; processedColumns <= vectorEnd; processedColumns += Vector512<byte>.Count)
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

                // The only legal AV1 width below eight is four samples, exactly the lower Vector64 half.
                PackHighBitDepth(result0, result1, maximum).GetLower().StoreUnsafe(ref destinationRow);
                continue;
            }

            int vectorEnd = width - Vector128<ushort>.Count;
            for (; processedColumns <= vectorEnd; processedColumns += Vector128<ushort>.Count)
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
        int vectorEnd = width - Vector256<ushort>.Count;

        for (int row = 0; row < height; row++)
        {
            ref ushort sourceRowUnsigned = ref Unsafe.Add(ref sourceBase, (row * sourceStride) + sourceOffset);
            ref short sourceRow = ref Unsafe.As<ushort, short>(ref sourceRowUnsigned);
            ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int processedColumns = 0;

            for (; processedColumns <= vectorEnd; processedColumns += Vector256<ushort>.Count)
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
        int vectorEnd = width - Vector512<ushort>.Count;

        for (int row = 0; row < height; row++)
        {
            ref ushort sourceRowUnsigned = ref Unsafe.Add(ref sourceBase, (row * sourceStride) + sourceOffset);
            ref short sourceRow = ref Unsafe.As<ushort, short>(ref sourceRowUnsigned);
            ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int processedColumns = 0;

            for (; processedColumns <= vectorEnd; processedColumns += Vector512<ushort>.Count)
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
    private static void Round(ref Vector128<int> result0, ref Vector128<int> result1, int firstRound, int secondRound)
    {
        result0 = RoundPowerOfTwo(RoundPowerOfTwo(result0, firstRound), secondRound);
        result1 = RoundPowerOfTwo(RoundPowerOfTwo(result1, firstRound), secondRound);
    }

    /// <summary>
    /// Applies the two direct-filter rounding stages to sixteen high-bit-depth results.
    /// </summary>
    private static void Round(ref Vector256<int> result0, ref Vector256<int> result1, int firstRound, int secondRound)
    {
        result0 = RoundPowerOfTwo(RoundPowerOfTwo(result0, firstRound), secondRound);
        result1 = RoundPowerOfTwo(RoundPowerOfTwo(result1, firstRound), secondRound);
    }

    /// <summary>
    /// Applies the two direct-filter rounding stages to thirty-two high-bit-depth results.
    /// </summary>
    private static void Round(ref Vector512<int> result0, ref Vector512<int> result1, int firstRound, int secondRound)
    {
        result0 = RoundPowerOfTwo(RoundPowerOfTwo(result0, firstRound), secondRound);
        result1 = RoundPowerOfTwo(RoundPowerOfTwo(result1, firstRound), secondRound);
    }

    /// <summary>
    /// Finishes an 8-bit row after its selected vector width.
    /// </summary>
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
    /// Stores the four- or eight-sample prefix of a sixteen-byte prediction vector.
    /// </summary>
    private static void StorePartial(Vector128<byte> value, ref byte destination, int width)
    {
        if (width == 8)
        {
            value.GetLower().StoreUnsafe(ref destination);
        }
        else
        {
            Unsafe.As<byte, uint>(ref destination) = value.AsUInt32().GetElement(0);
        }
    }

    /// <summary>
    /// Applies a one-dimensional 8-bit filter without explicit hardware intrinsics.
    /// </summary>
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
