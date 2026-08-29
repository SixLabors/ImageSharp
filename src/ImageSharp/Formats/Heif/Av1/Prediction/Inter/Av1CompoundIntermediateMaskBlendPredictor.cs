// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1CompoundInterPredictor;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1InterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Reconstructs final samples by alpha-blending compound intermediates.
/// </content>
internal static partial class Av1CompoundIntermediateMaskBlendPredictor
{
    /// <summary>
    /// Blends two compound intermediates through a luma-resolution mask.
    /// </summary>
    public static void BlendIntermediate(
        Span<byte> destination,
        int destinationStride,
        ReadOnlySpan<ushort> first,
        int firstStride,
        ReadOnlySpan<ushort> second,
        int secondStride,
        ReadOnlySpan<byte> mask,
        int maskStride,
        int width,
        int height,
        int subX,
        int subY,
        int bitDepth)
        => BlendIntermediate<CompoundIntermediateMaskBlendOperator>(
            destination,
            destinationStride,
            first,
            firstStride,
            second,
            secondStride,
            mask,
            maskStride,
            width,
            height,
            subX,
            subY,
            bitDepth);

    /// <summary>
    /// Executes one closed alpha-blend compound-intermediate operator.
    /// </summary>
    /// <typeparam name="TOperator">The compound-intermediate operator.</typeparam>
    private static void BlendIntermediate<TOperator>(
        Span<byte> destination,
        int destinationStride,
        ReadOnlySpan<ushort> first,
        int firstStride,
        ReadOnlySpan<ushort> second,
        int secondStride,
        ReadOnlySpan<byte> mask,
        int maskStride,
        int width,
        int height,
        int subX,
        int subY,
        int bitDepth)
        where TOperator : struct, IAv1CompoundIntermediateMaskBlendOperator
    {
        GetIntermediateRounding(bitDepth, out int roundBits, out int roundOffset);
        for (int row = 0; row < height; row++)
        {
            Span<byte> destinationRow = destination.Slice(row * destinationStride, width);
            ReadOnlySpan<ushort> firstRow = first.Slice(row * firstStride, width);
            ReadOnlySpan<ushort> secondRow = second.Slice(row * secondStride, width);
            ref byte destinationReference = ref MemoryMarshal.GetReference(destinationRow);
            ref ushort firstReference = ref MemoryMarshal.GetReference(firstRow);
            ref ushort secondReference = ref MemoryMarshal.GetReference(secondRow);
            int column = 0;

            if (Vector512.IsHardwareAccelerated && subX == 0 && subY == 0)
            {
                ref byte maskReference = ref MemoryMarshal.GetReference(mask);
                int maskRowOffset = row * maskStride;
                int vectorEnd = width - Vector512<byte>.Count;
                for (; column <= vectorEnd; column += Vector512<byte>.Count)
                {
                    Vector512<ushort> first0 = Vector512.LoadUnsafe(ref firstReference, (nuint)column);
                    Vector512<ushort> first1 = Vector512.LoadUnsafe(ref firstReference, (nuint)(column + Vector512<ushort>.Count));
                    Vector512<ushort> second0 = Vector512.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector512<ushort> second1 = Vector512.LoadUnsafe(ref secondReference, (nuint)(column + Vector512<ushort>.Count));
                    Vector512<byte> alpha = Vector512.LoadUnsafe(ref maskReference, (nuint)(maskRowOffset + column));
                    TOperator.Blend(first0, first1, second0, second1, alpha, roundBits, roundOffset)
                        .StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector256.IsHardwareAccelerated && subX == 0 && subY == 0)
            {
                ref byte maskReference = ref MemoryMarshal.GetReference(mask);
                int maskRowOffset = row * maskStride;
                int vectorEnd = width - Vector256<byte>.Count;
                for (; column <= vectorEnd; column += Vector256<byte>.Count)
                {
                    Vector256<ushort> first0 = Vector256.LoadUnsafe(ref firstReference, (nuint)column);
                    Vector256<ushort> first1 = Vector256.LoadUnsafe(ref firstReference, (nuint)(column + Vector256<ushort>.Count));
                    Vector256<ushort> second0 = Vector256.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector256<ushort> second1 = Vector256.LoadUnsafe(ref secondReference, (nuint)(column + Vector256<ushort>.Count));
                    Vector256<byte> alpha = Vector256.LoadUnsafe(ref maskReference, (nuint)(maskRowOffset + column));
                    TOperator.Blend(first0, first1, second0, second1, alpha, roundBits, roundOffset)
                        .StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector128.IsHardwareAccelerated && subX == 0 && subY == 0)
            {
                ref byte maskReference = ref MemoryMarshal.GetReference(mask);
                int maskRowOffset = row * maskStride;
                int vectorEnd = width - Vector128<byte>.Count;
                for (; column <= vectorEnd; column += Vector128<byte>.Count)
                {
                    Vector128<ushort> first0 = Vector128.LoadUnsafe(ref firstReference, (nuint)column);
                    Vector128<ushort> first1 = Vector128.LoadUnsafe(
                        ref firstReference,
                        (nuint)(column + Vector128<ushort>.Count));

                    Vector128<ushort> second0 = Vector128.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector128<ushort> second1 = Vector128.LoadUnsafe(
                        ref secondReference,
                        (nuint)(column + Vector128<ushort>.Count));

                    Vector128<byte> alpha = Vector128.LoadUnsafe(
                        ref maskReference,
                        (nuint)(maskRowOffset + column));

                    TOperator.Blend(
                        first0,
                        first1,
                        second0,
                        second1,
                        alpha,
                        roundBits,
                        roundOffset).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                byte alpha = (byte)GetSubsampledMaskAlpha(mask, maskStride, row, column, subX, subY);
                destinationRow[column] = TOperator.Blend(firstRow[column], secondRow[column], alpha, roundBits, roundOffset);
            }
        }
    }

    /// <summary>
    /// Gets the mask alpha for one plane sample, averaging its two or four luma samples when required.
    /// </summary>
    private static int GetSubsampledMaskAlpha(
        ReadOnlySpan<byte> mask,
        int maskStride,
        int row,
        int column,
        int subX,
        int subY)
    {
        int maskRow = row << subY;
        int maskColumn = column << subX;
        int alpha = mask[(maskRow * maskStride) + maskColumn];
        if (subX != 0)
        {
            alpha += mask[(maskRow * maskStride) + maskColumn + 1];
        }

        if (subY != 0)
        {
            int lowerOffset = ((maskRow + 1) * maskStride) + maskColumn;
            alpha += mask[lowerOffset];
            if (subX != 0)
            {
                alpha += mask[lowerOffset + 1];
            }
        }

        int sampleCountShift = subX + subY;
        return sampleCountShift == 0
            ? alpha
            : RoundPowerOfTwo(alpha, sampleCountShift);
    }
}
