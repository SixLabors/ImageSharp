// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1CompoundInterPredictor;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1InterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Builds AV1 difference-weighted compound masks.
/// </content>
internal static partial class Av1DifferenceWeightedMaskBuilder
{
    /// <summary>
    /// Fills an 8-bit difference-weighted compound mask.
    /// </summary>
    public static void FillDifferenceWeightedMask(
        Span<byte> mask,
        int maskStride,
        ReadOnlySpan<byte> first,
        int firstStride,
        ReadOnlySpan<byte> second,
        int secondStride,
        int width,
        int height,
        Av1DifferenceWeightedMaskType maskType)
        => FillDifferenceWeightedMask<DifferenceWeightedMaskOperator>(
            mask,
            maskStride,
            first,
            firstStride,
            second,
            secondStride,
            width,
            height,
            maskType);

    /// <summary>
    /// Executes one closed 8-bit difference-weighted mask operator.
    /// </summary>
    /// <typeparam name="TOperator">The difference-weighted mask operator.</typeparam>
    private static void FillDifferenceWeightedMask<TOperator>(
        Span<byte> mask,
        int maskStride,
        ReadOnlySpan<byte> first,
        int firstStride,
        ReadOnlySpan<byte> second,
        int secondStride,
        int width,
        int height,
        Av1DifferenceWeightedMaskType maskType)
        where TOperator : struct, IAv1DifferenceWeightedMaskOperator
    {
        bool invert = maskType == Av1DifferenceWeightedMaskType.Type38Inverse;
        for (int row = 0; row < height; row++)
        {
            Span<byte> maskRow = mask.Slice(row * maskStride, width);
            ReadOnlySpan<byte> firstRow = first.Slice(row * firstStride, width);
            ReadOnlySpan<byte> secondRow = second.Slice(row * secondStride, width);
            ref byte maskReference = ref MemoryMarshal.GetReference(maskRow);
            ref byte firstReference = ref MemoryMarshal.GetReference(firstRow);
            ref byte secondReference = ref MemoryMarshal.GetReference(secondRow);
            int column = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector512<byte>.Count;
                for (; column <= vectorEnd; column += Vector512<byte>.Count)
                {
                    Vector512<byte> firstVector = Vector512.LoadUnsafe(ref firstReference, (nuint)column);
                    Vector512<byte> secondVector = Vector512.LoadUnsafe(ref secondReference, (nuint)column);
                    TOperator.Create(firstVector, secondVector, 4, invert).StoreUnsafe(ref maskReference, (nuint)column);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector256<byte>.Count;
                for (; column <= vectorEnd; column += Vector256<byte>.Count)
                {
                    Vector256<byte> firstVector = Vector256.LoadUnsafe(ref firstReference, (nuint)column);
                    Vector256<byte> secondVector = Vector256.LoadUnsafe(ref secondReference, (nuint)column);
                    TOperator.Create(firstVector, secondVector, 4, invert).StoreUnsafe(ref maskReference, (nuint)column);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector128<byte>.Count;
                for (; column <= vectorEnd; column += Vector128<byte>.Count)
                {
                    Vector128<byte> firstVector = Vector128.LoadUnsafe(ref firstReference, (nuint)column);
                    Vector128<byte> secondVector = Vector128.LoadUnsafe(ref secondReference, (nuint)column);
                    TOperator.Create(firstVector, secondVector, 4, invert).StoreUnsafe(ref maskReference, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                maskRow[column] = TOperator.Create(firstRow[column], secondRow[column], 4, invert);
            }
        }
    }

    /// <summary>
    /// Fills a high-bit-depth difference-weighted compound mask.
    /// </summary>
    public static void FillDifferenceWeightedMask(
        Span<byte> mask,
        int maskStride,
        ReadOnlySpan<ushort> first,
        int firstStride,
        ReadOnlySpan<ushort> second,
        int secondStride,
        int width,
        int height,
        int bitDepth,
        Av1DifferenceWeightedMaskType maskType)
        => FillDifferenceWeightedMask<DifferenceWeightedMaskOperator>(
            mask,
            maskStride,
            first,
            firstStride,
            second,
            secondStride,
            width,
            height,
            bitDepth,
            maskType);

    /// <summary>
    /// Executes one closed high-bit-depth difference-weighted mask operator.
    /// </summary>
    /// <typeparam name="TOperator">The difference-weighted mask operator.</typeparam>
    private static void FillDifferenceWeightedMask<TOperator>(
        Span<byte> mask,
        int maskStride,
        ReadOnlySpan<ushort> first,
        int firstStride,
        ReadOnlySpan<ushort> second,
        int secondStride,
        int width,
        int height,
        int bitDepth,
        Av1DifferenceWeightedMaskType maskType)
        where TOperator : struct, IAv1DifferenceWeightedMaskOperator
    {
        bool invert = maskType == Av1DifferenceWeightedMaskType.Type38Inverse;
        int differenceShift = bitDepth - 8 + 4;
        for (int row = 0; row < height; row++)
        {
            Span<byte> maskRow = mask.Slice(row * maskStride, width);
            ReadOnlySpan<ushort> firstRow = first.Slice(row * firstStride, width);
            ReadOnlySpan<ushort> secondRow = second.Slice(row * secondStride, width);
            ref byte maskReference = ref MemoryMarshal.GetReference(maskRow);
            ref ushort firstReference = ref MemoryMarshal.GetReference(firstRow);
            ref ushort secondReference = ref MemoryMarshal.GetReference(secondRow);
            int column = 0;

            // Two input vectors narrow to one packed byte mask. This keeps mask construction contiguous and avoids
            // temporary buffers before the following vector blend consumes the complete plane block.
            if (Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector512<byte>.Count;
                for (; column <= vectorEnd; column += Vector512<byte>.Count)
                {
                    Vector512<ushort> first0 = Vector512.LoadUnsafe(ref firstReference, (nuint)column);
                    Vector512<ushort> first1 = Vector512.LoadUnsafe(ref firstReference, (nuint)(column + Vector512<ushort>.Count));
                    Vector512<ushort> second0 = Vector512.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector512<ushort> second1 = Vector512.LoadUnsafe(ref secondReference, (nuint)(column + Vector512<ushort>.Count));
                    TOperator.Create(first0, first1, second0, second1, differenceShift, invert)
                        .StoreUnsafe(ref maskReference, (nuint)column);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector256<byte>.Count;
                for (; column <= vectorEnd; column += Vector256<byte>.Count)
                {
                    Vector256<ushort> first0 = Vector256.LoadUnsafe(ref firstReference, (nuint)column);
                    Vector256<ushort> first1 = Vector256.LoadUnsafe(ref firstReference, (nuint)(column + Vector256<ushort>.Count));
                    Vector256<ushort> second0 = Vector256.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector256<ushort> second1 = Vector256.LoadUnsafe(ref secondReference, (nuint)(column + Vector256<ushort>.Count));
                    TOperator.Create(first0, first1, second0, second1, differenceShift, invert)
                        .StoreUnsafe(ref maskReference, (nuint)column);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector128<byte>.Count;
                for (; column <= vectorEnd; column += Vector128<byte>.Count)
                {
                    Vector128<ushort> first0 = Vector128.LoadUnsafe(ref firstReference, (nuint)column);
                    Vector128<ushort> first1 = Vector128.LoadUnsafe(ref firstReference, (nuint)(column + Vector128<ushort>.Count));
                    Vector128<ushort> second0 = Vector128.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector128<ushort> second1 = Vector128.LoadUnsafe(ref secondReference, (nuint)(column + Vector128<ushort>.Count));
                    TOperator.Create(first0, first1, second0, second1, differenceShift, invert)
                        .StoreUnsafe(ref maskReference, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                maskRow[column] = TOperator.Create(firstRow[column], secondRow[column], differenceShift, invert);
            }
        }
    }
}
