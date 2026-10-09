// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1CompoundInterPredictor;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1TranslationalInterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Reconstructs final samples by alpha-blending compound intermediates.
/// </content>
internal static partial class Av1CompoundIntermediateMaskBlendPredictor
{
    /// <summary>
    /// Blends two compound intermediates through a luma-resolution mask.
    /// </summary>
    /// <param name="destination">The reconstructed 8-bit samples.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="first">The first compound intermediate.</param>
    /// <param name="firstStride">The distance between first-intermediate rows in samples.</param>
    /// <param name="second">The second compound intermediate.</param>
    /// <param name="secondStride">The distance between second-intermediate rows in samples.</param>
    /// <param name="mask">The luma-resolution weights of the first predictor, 0 through 64.</param>
    /// <param name="maskStride">The distance between mask rows.</param>
    /// <param name="width">The active block width in plane samples.</param>
    /// <param name="height">The active block height in plane samples.</param>
    /// <param name="subX">The horizontal subsampling shift of the plane: 0 or 1.</param>
    /// <param name="subY">The vertical subsampling shift of the plane: 0 or 1.</param>
    /// <param name="bitDepth">The decoded sample precision that produced the intermediates.</param>
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
    /// <param name="destination">The reconstructed 8-bit samples.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="first">The first compound intermediate.</param>
    /// <param name="firstStride">The distance between first-intermediate rows in samples.</param>
    /// <param name="second">The second compound intermediate.</param>
    /// <param name="secondStride">The distance between second-intermediate rows in samples.</param>
    /// <param name="mask">The luma-resolution weights of the first predictor, 0 through 64.</param>
    /// <param name="maskStride">The distance between mask rows.</param>
    /// <param name="width">The active block width in plane samples.</param>
    /// <param name="height">The active block height in plane samples.</param>
    /// <param name="subX">The horizontal subsampling shift of the plane: 0 or 1.</param>
    /// <param name="subY">The vertical subsampling shift of the plane: 0 or 1.</param>
    /// <param name="bitDepth">The decoded sample precision that produced the intermediates.</param>
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

            // The vector loops run only when the mask has the plane resolution. A subsampled plane uses the scalar loop, which averages the mask samples.
            // Each step covers one byte vector of columns: one mask vector and two 16-bit vectors from each intermediate.
            if (Vector512.IsHardwareAccelerated && subX == 0 && subY == 0)
            {
                ref byte maskReference = ref MemoryMarshal.GetReference(mask);
                int maskRowOffset = row * maskStride;
                nuint vectorCount = Numerics.Vector512Count<byte>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector512<byte>.Count)
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
                nuint vectorCount = Numerics.Vector256Count<byte>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector256<byte>.Count)
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
                nuint vectorCount = Numerics.Vector128Count<byte>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector128<byte>.Count)
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
    /// Blends two high-bit-depth compound intermediates through a luma-resolution mask.
    /// </summary>
    /// <param name="destination">The reconstructed high-bit-depth samples.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="first">The first compound intermediate.</param>
    /// <param name="firstStride">The distance between first-intermediate rows in samples.</param>
    /// <param name="second">The second compound intermediate.</param>
    /// <param name="secondStride">The distance between second-intermediate rows in samples.</param>
    /// <param name="mask">The luma-resolution weights of the first predictor, 0 through 64.</param>
    /// <param name="maskStride">The distance between mask rows.</param>
    /// <param name="width">The active block width in plane samples.</param>
    /// <param name="height">The active block height in plane samples.</param>
    /// <param name="subX">The horizontal subsampling shift of the plane: 0 or 1.</param>
    /// <param name="subY">The vertical subsampling shift of the plane: 0 or 1.</param>
    /// <param name="bitDepth">The decoded sample precision.</param>
    public static void BlendIntermediate(
        Span<ushort> destination,
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
    /// Executes one closed high-bit-depth alpha-blend compound-intermediate operator.
    /// </summary>
    /// <typeparam name="TOperator">The compound-intermediate operator.</typeparam>
    /// <param name="destination">The reconstructed high-bit-depth samples.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="first">The first compound intermediate.</param>
    /// <param name="firstStride">The distance between first-intermediate rows in samples.</param>
    /// <param name="second">The second compound intermediate.</param>
    /// <param name="secondStride">The distance between second-intermediate rows in samples.</param>
    /// <param name="mask">The luma-resolution weights of the first predictor, 0 through 64.</param>
    /// <param name="maskStride">The distance between mask rows.</param>
    /// <param name="width">The active block width in plane samples.</param>
    /// <param name="height">The active block height in plane samples.</param>
    /// <param name="subX">The horizontal subsampling shift of the plane: 0 or 1.</param>
    /// <param name="subY">The vertical subsampling shift of the plane: 0 or 1.</param>
    /// <param name="bitDepth">The decoded sample precision.</param>
    private static void BlendIntermediate<TOperator>(
        Span<ushort> destination,
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
        int maximum = (1 << bitDepth) - 1;

        for (int row = 0; row < height; row++)
        {
            Span<ushort> destinationRow = destination.Slice(row * destinationStride, width);
            ReadOnlySpan<ushort> firstRow = first.Slice(row * firstStride, width);
            ReadOnlySpan<ushort> secondRow = second.Slice(row * secondStride, width);
            ref ushort destinationReference = ref MemoryMarshal.GetReference(destinationRow);
            ref ushort firstReference = ref MemoryMarshal.GetReference(firstRow);
            ref ushort secondReference = ref MemoryMarshal.GetReference(secondRow);
            int column = 0;

            // The vector loops run only when the mask has the plane resolution. A subsampled plane uses the scalar loop, which averages the mask samples.
            // Each step covers one byte vector of columns: one mask vector and two 16-bit vectors from each intermediate.
            if (Vector512.IsHardwareAccelerated && subX == 0 && subY == 0)
            {
                ref byte maskReference = ref MemoryMarshal.GetReference(mask);
                int maskRowOffset = row * maskStride;
                nuint vectorCount = Numerics.Vector512Count<byte>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector512<byte>.Count)
                {
                    Vector512<ushort> first0 = Vector512.LoadUnsafe(ref firstReference, (nuint)column);
                    Vector512<ushort> first1 = Vector512.LoadUnsafe(
                        ref firstReference,
                        (nuint)(column + Vector512<ushort>.Count));

                    Vector512<ushort> second0 = Vector512.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector512<ushort> second1 = Vector512.LoadUnsafe(
                        ref secondReference,
                        (nuint)(column + Vector512<ushort>.Count));

                    Vector512<byte> alpha = Vector512.LoadUnsafe(
                        ref maskReference,
                        (nuint)(maskRowOffset + column));

                    TOperator.BlendHighBitDepth(
                        first0,
                        first1,
                        second0,
                        second1,
                        alpha,
                        roundBits,
                        roundOffset,
                        maximum,
                        out Vector512<ushort> result0,
                        out Vector512<ushort> result1);

                    result0.StoreUnsafe(ref destinationReference, (nuint)column);
                    result1.StoreUnsafe(
                        ref destinationReference,
                        (nuint)(column + Vector512<ushort>.Count));
                }
            }

            if (Vector256.IsHardwareAccelerated && subX == 0 && subY == 0)
            {
                ref byte maskReference = ref MemoryMarshal.GetReference(mask);
                int maskRowOffset = row * maskStride;
                nuint vectorCount = Numerics.Vector256Count<byte>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector256<byte>.Count)
                {
                    Vector256<ushort> first0 = Vector256.LoadUnsafe(ref firstReference, (nuint)column);
                    Vector256<ushort> first1 = Vector256.LoadUnsafe(
                        ref firstReference,
                        (nuint)(column + Vector256<ushort>.Count));

                    Vector256<ushort> second0 = Vector256.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector256<ushort> second1 = Vector256.LoadUnsafe(
                        ref secondReference,
                        (nuint)(column + Vector256<ushort>.Count));

                    Vector256<byte> alpha = Vector256.LoadUnsafe(
                        ref maskReference,
                        (nuint)(maskRowOffset + column));

                    TOperator.BlendHighBitDepth(
                        first0,
                        first1,
                        second0,
                        second1,
                        alpha,
                        roundBits,
                        roundOffset,
                        maximum,
                        out Vector256<ushort> result0,
                        out Vector256<ushort> result1);

                    result0.StoreUnsafe(ref destinationReference, (nuint)column);
                    result1.StoreUnsafe(
                        ref destinationReference,
                        (nuint)(column + Vector256<ushort>.Count));
                }
            }

            if (Vector128.IsHardwareAccelerated && subX == 0 && subY == 0)
            {
                ref byte maskReference = ref MemoryMarshal.GetReference(mask);
                int maskRowOffset = row * maskStride;
                nuint vectorCount = Numerics.Vector128Count<byte>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector128<byte>.Count)
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

                    TOperator.BlendHighBitDepth(
                        first0,
                        first1,
                        second0,
                        second1,
                        alpha,
                        roundBits,
                        roundOffset,
                        maximum,
                        out Vector128<ushort> result0,
                        out Vector128<ushort> result1);

                    result0.StoreUnsafe(ref destinationReference, (nuint)column);
                    result1.StoreUnsafe(
                        ref destinationReference,
                        (nuint)(column + Vector128<ushort>.Count));
                }
            }

            for (; column < width; column++)
            {
                byte alpha = (byte)GetSubsampledMaskAlpha(mask, maskStride, row, column, subX, subY);
                destinationRow[column] = TOperator.BlendHighBitDepth(
                    firstRow[column],
                    secondRow[column],
                    alpha,
                    roundBits,
                    roundOffset,
                    maximum);
            }
        }
    }

    /// <summary>
    /// Gets the mask alpha for one plane sample. For a subsampled plane, it is the rounded average of the two or four covered luma mask samples.
    /// </summary>
    /// <param name="mask">The luma-resolution mask.</param>
    /// <param name="maskStride">The distance between mask rows.</param>
    /// <param name="row">The plane row of the sample.</param>
    /// <param name="column">The plane column of the sample.</param>
    /// <param name="subX">The horizontal subsampling shift of the plane: 0 or 1.</param>
    /// <param name="subY">The vertical subsampling shift of the plane: 0 or 1.</param>
    /// <returns>The mask alpha, 0 through 64.</returns>
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
