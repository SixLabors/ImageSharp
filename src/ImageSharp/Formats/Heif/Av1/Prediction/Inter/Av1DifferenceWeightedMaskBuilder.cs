// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1CompoundInterPredictor;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1TranslationalInterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Builds AV1 difference-weighted compound masks.
/// </content>
internal static partial class Av1DifferenceWeightedMaskBuilder
{
    /// <summary>
    /// Fills an 8-bit difference-weighted compound mask.
    /// </summary>
    /// <param name="mask">The mask destination.</param>
    /// <param name="maskStride">The distance between mask rows.</param>
    /// <param name="first">The first predictor samples.</param>
    /// <param name="firstStride">The distance between first predictor rows.</param>
    /// <param name="second">The second predictor samples.</param>
    /// <param name="secondStride">The distance between second predictor rows.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="maskType">The mask type that selects the direct or the inverse mask.</param>
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
    /// <param name="mask">The mask destination.</param>
    /// <param name="maskStride">The distance between mask rows.</param>
    /// <param name="first">The first predictor samples.</param>
    /// <param name="firstStride">The distance between first predictor rows.</param>
    /// <param name="second">The second predictor samples.</param>
    /// <param name="secondStride">The distance between second predictor rows.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="maskType">The mask type that selects the direct or the inverse mask.</param>
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
        // Each mask value is min(64, 38 + (|first - second| >> 4)). The 8-bit shift of 4 divides the difference by 16.
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
                nuint vectorCount = Numerics.Vector512Count<byte>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector512<byte>.Count)
                {
                    Vector512<byte> firstVector = Vector512.LoadUnsafe(ref firstReference, (nuint)column);
                    Vector512<byte> secondVector = Vector512.LoadUnsafe(ref secondReference, (nuint)column);
                    TOperator.Create(firstVector, secondVector, 4, invert).StoreUnsafe(ref maskReference, (nuint)column);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint vectorCount = Numerics.Vector256Count<byte>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector256<byte>.Count)
                {
                    Vector256<byte> firstVector = Vector256.LoadUnsafe(ref firstReference, (nuint)column);
                    Vector256<byte> secondVector = Vector256.LoadUnsafe(ref secondReference, (nuint)column);
                    TOperator.Create(firstVector, secondVector, 4, invert).StoreUnsafe(ref maskReference, (nuint)column);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint vectorCount = Numerics.Vector128Count<byte>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector128<byte>.Count)
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
    /// <param name="mask">The mask destination.</param>
    /// <param name="maskStride">The distance between mask rows.</param>
    /// <param name="first">The first predictor samples.</param>
    /// <param name="firstStride">The distance between first predictor rows.</param>
    /// <param name="second">The second predictor samples.</param>
    /// <param name="secondStride">The distance between second predictor rows.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="maskType">The mask type that selects the direct or the inverse mask.</param>
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
    /// <param name="mask">The mask destination.</param>
    /// <param name="maskStride">The distance between mask rows.</param>
    /// <param name="first">The first predictor samples.</param>
    /// <param name="firstStride">The distance between first predictor rows.</param>
    /// <param name="second">The second predictor samples.</param>
    /// <param name="secondStride">The distance between second predictor rows.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="maskType">The mask type that selects the direct or the inverse mask.</param>
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
        // The shift first scales the difference down to the 8-bit range, then divides it by 16 as in the 8-bit mask.
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

            // Each step loads two vectors of 16-bit samples from each predictor and narrows the results to one vector of mask bytes.
            // The mask goes straight to its destination row, without a temporary buffer.
            if (Vector512.IsHardwareAccelerated)
            {
                nuint vectorCount = Numerics.Vector512Count<byte>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector512<byte>.Count)
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
                nuint vectorCount = Numerics.Vector256Count<byte>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector256<byte>.Count)
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
                nuint vectorCount = Numerics.Vector128Count<byte>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector128<byte>.Count)
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
