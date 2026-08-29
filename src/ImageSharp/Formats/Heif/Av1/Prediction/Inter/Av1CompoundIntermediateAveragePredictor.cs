// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1CompoundInterPredictor;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1InterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Reconstructs final samples by equal-averaging compound intermediates.
/// </content>
internal static partial class Av1CompoundIntermediateAveragePredictor
{
    /// <summary>
    /// Combines two compound intermediates by equal averaging.
    /// </summary>
    public static void AverageIntermediate(
        Span<byte> destination,
        int destinationStride,
        ReadOnlySpan<ushort> first,
        int firstStride,
        ReadOnlySpan<ushort> second,
        int secondStride,
        int width,
        int height,
        int bitDepth)
        => AverageIntermediate<CompoundIntermediateAverageOperator>(
            destination,
            destinationStride,
            first,
            firstStride,
            second,
            secondStride,
            width,
            height,
            bitDepth);

    /// <summary>
    /// Executes one closed equal-average compound-intermediate operator.
    /// </summary>
    /// <typeparam name="TOperator">The compound-intermediate operator.</typeparam>
    private static void AverageIntermediate<TOperator>(
        Span<byte> destination,
        int destinationStride,
        ReadOnlySpan<ushort> first,
        int firstStride,
        ReadOnlySpan<ushort> second,
        int secondStride,
        int width,
        int height,
        int bitDepth)
        where TOperator : struct, IAv1CompoundIntermediateAverageOperator
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

            if (Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector512<byte>.Count;
                for (; column <= vectorEnd; column += Vector512<byte>.Count)
                {
                    Vector512<ushort> first0 = Vector512.LoadUnsafe(ref firstReference, (nuint)column);
                    Vector512<ushort> first1 = Vector512.LoadUnsafe(ref firstReference, (nuint)(column + Vector512<ushort>.Count));
                    Vector512<ushort> second0 = Vector512.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector512<ushort> second1 = Vector512.LoadUnsafe(ref secondReference, (nuint)(column + Vector512<ushort>.Count));
                    TOperator.Average(first0, first1, second0, second1, roundBits, roundOffset)
                        .StoreUnsafe(ref destinationReference, (nuint)column);
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
                    TOperator.Average(first0, first1, second0, second1, roundBits, roundOffset)
                        .StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
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

                    TOperator.Average(first0, first1, second0, second1, roundBits, roundOffset).StoreUnsafe(
                        ref destinationReference,
                        (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                destinationRow[column] = TOperator.Average(firstRow[column], secondRow[column], roundBits, roundOffset);
            }
        }
    }
}
