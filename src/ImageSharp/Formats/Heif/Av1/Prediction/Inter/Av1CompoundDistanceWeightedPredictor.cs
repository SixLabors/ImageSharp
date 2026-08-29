// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1CompoundInterPredictor;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1InterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Reconstructs display-distance-weighted compound prediction.
/// </content>
internal static partial class Av1CompoundDistanceWeightedPredictor
{
    /// <summary>
    /// Combines two 8-bit predictors with AV1 display-distance weights.
    /// </summary>
    public static void DistanceWeighted(
        Span<byte> destination,
        int destinationStride,
        ReadOnlySpan<byte> second,
        int secondStride,
        int width,
        int height,
        int firstWeight,
        int secondWeight)
        => DistanceWeighted<CompoundDistanceWeightedOperator>(
            destination,
            destinationStride,
            second,
            secondStride,
            width,
            height,
            firstWeight,
            secondWeight);

    /// <summary>
    /// Executes one closed 8-bit distance-weighted compound operator.
    /// </summary>
    /// <typeparam name="TOperator">The compound arithmetic operator.</typeparam>
    private static void DistanceWeighted<TOperator>(
        Span<byte> destination,
        int destinationStride,
        ReadOnlySpan<byte> second,
        int secondStride,
        int width,
        int height,
        int firstWeight,
        int secondWeight)
        where TOperator : struct, IAv1CompoundDistanceWeightedOperator
    {
        for (int row = 0; row < height; row++)
        {
            Span<byte> destinationRow = destination.Slice(row * destinationStride, width);
            ReadOnlySpan<byte> secondRow = second.Slice(row * secondStride, width);
            ref byte destinationReference = ref MemoryMarshal.GetReference(destinationRow);
            ref byte secondReference = ref MemoryMarshal.GetReference(secondRow);
            int column = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector512<byte>.Count;
                for (; column <= vectorEnd; column += Vector512<byte>.Count)
                {
                    Vector512<byte> firstVector = Vector512.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector512<byte> secondVector = Vector512.LoadUnsafe(ref secondReference, (nuint)column);
                    TOperator.Blend(firstVector, secondVector, firstWeight, secondWeight).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector256<byte>.Count;
                for (; column <= vectorEnd; column += Vector256<byte>.Count)
                {
                    Vector256<byte> firstVector = Vector256.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector256<byte> secondVector = Vector256.LoadUnsafe(ref secondReference, (nuint)column);
                    TOperator.Blend(firstVector, secondVector, firstWeight, secondWeight).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector128<byte>.Count;
                for (; column <= vectorEnd; column += Vector128<byte>.Count)
                {
                    Vector128<byte> firstVector = Vector128.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector128<byte> secondVector = Vector128.LoadUnsafe(ref secondReference, (nuint)column);
                    TOperator.Blend(firstVector, secondVector, firstWeight, secondWeight).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                destinationRow[column] = TOperator.Blend(destinationRow[column], secondRow[column], firstWeight, secondWeight);
            }
        }
    }

    /// <summary>
    /// Combines two high-bit-depth predictors with AV1 display-distance weights.
    /// </summary>
    public static void DistanceWeighted(
        Span<ushort> destination,
        int destinationStride,
        ReadOnlySpan<ushort> second,
        int secondStride,
        int width,
        int height,
        int firstWeight,
        int secondWeight)
        => DistanceWeighted<CompoundDistanceWeightedOperator>(
            destination,
            destinationStride,
            second,
            secondStride,
            width,
            height,
            firstWeight,
            secondWeight);

    /// <summary>
    /// Executes one closed high-bit-depth distance-weighted compound operator.
    /// </summary>
    /// <typeparam name="TOperator">The compound arithmetic operator.</typeparam>
    private static void DistanceWeighted<TOperator>(
        Span<ushort> destination,
        int destinationStride,
        ReadOnlySpan<ushort> second,
        int secondStride,
        int width,
        int height,
        int firstWeight,
        int secondWeight)
        where TOperator : struct, IAv1CompoundDistanceWeightedOperator
    {
        for (int row = 0; row < height; row++)
        {
            Span<ushort> destinationRow = destination.Slice(row * destinationStride, width);
            ReadOnlySpan<ushort> secondRow = second.Slice(row * secondStride, width);
            ref ushort destinationReference = ref MemoryMarshal.GetReference(destinationRow);
            ref ushort secondReference = ref MemoryMarshal.GetReference(secondRow);
            int column = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector512<ushort>.Count;
                for (; column <= vectorEnd; column += Vector512<ushort>.Count)
                {
                    Vector512<ushort> firstVector = Vector512.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector512<ushort> secondVector = Vector512.LoadUnsafe(ref secondReference, (nuint)column);
                    TOperator.Blend(firstVector, secondVector, firstWeight, secondWeight).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector256<ushort>.Count;
                for (; column <= vectorEnd; column += Vector256<ushort>.Count)
                {
                    Vector256<ushort> firstVector = Vector256.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector256<ushort> secondVector = Vector256.LoadUnsafe(ref secondReference, (nuint)column);
                    TOperator.Blend(firstVector, secondVector, firstWeight, secondWeight).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector128<ushort>.Count;
                for (; column <= vectorEnd; column += Vector128<ushort>.Count)
                {
                    Vector128<ushort> firstVector = Vector128.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector128<ushort> secondVector = Vector128.LoadUnsafe(ref secondReference, (nuint)column);
                    TOperator.Blend(firstVector, secondVector, firstWeight, secondWeight).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                destinationRow[column] = TOperator.Blend(destinationRow[column], secondRow[column], firstWeight, secondWeight);
            }
        }
    }

    /// <summary>
    /// Combines two 8-bit predictors with display-distance weights without explicit hardware intrinsics.
    /// </summary>
    public static void DistanceWeightedScalar(
        Span<byte> destination,
        int destinationStride,
        ReadOnlySpan<byte> second,
        int secondStride,
        int width,
        int height,
        int firstWeight,
        int secondWeight)
        => DistanceWeightedScalar<CompoundDistanceWeightedOperator>(
            destination,
            destinationStride,
            second,
            secondStride,
            width,
            height,
            firstWeight,
            secondWeight);

    /// <summary>
    /// Executes one closed 8-bit distance-weighted compound operator without explicit hardware intrinsics.
    /// </summary>
    /// <typeparam name="TOperator">The compound arithmetic operator.</typeparam>
    private static void DistanceWeightedScalar<TOperator>(
        Span<byte> destination,
        int destinationStride,
        ReadOnlySpan<byte> second,
        int secondStride,
        int width,
        int height,
        int firstWeight,
        int secondWeight)
        where TOperator : struct, IAv1CompoundDistanceWeightedOperator
    {
        for (int row = 0; row < height; row++)
        {
            Span<byte> destinationRow = destination.Slice(row * destinationStride, width);
            ReadOnlySpan<byte> secondRow = second.Slice(row * secondStride, width);
            for (int column = 0; column < width; column++)
            {
                destinationRow[column] = TOperator.Blend(destinationRow[column], secondRow[column], firstWeight, secondWeight);
            }
        }
    }
}
