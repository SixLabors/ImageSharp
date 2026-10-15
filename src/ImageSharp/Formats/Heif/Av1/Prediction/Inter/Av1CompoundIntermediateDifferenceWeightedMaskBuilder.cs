// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1CompoundInterPredictor;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1TranslationalInterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Builds difference-weighted masks from compound intermediates.
/// </content>
internal static partial class Av1CompoundIntermediateDifferenceWeightedMaskBuilder
{
    /// <summary>
    /// Fills a luma-resolution difference-weighted mask from compound intermediates.
    /// </summary>
    /// <param name="mask">The mask output. Each value is the weight of the first predictor, 0 through 64.</param>
    /// <param name="maskStride">The distance between mask rows.</param>
    /// <param name="first">The first compound intermediate.</param>
    /// <param name="firstStride">The distance between first-intermediate rows in samples.</param>
    /// <param name="second">The second compound intermediate.</param>
    /// <param name="secondStride">The distance between second-intermediate rows in samples.</param>
    /// <param name="width">The active block width.</param>
    /// <param name="height">The active block height.</param>
    /// <param name="bitDepth">The decoded sample precision that produced the intermediates.</param>
    /// <param name="maskType">The difference-weighted mask type. The inverse type swaps the weights of the two predictors.</param>
    public static void FillDifferenceWeightedIntermediateMask(
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
        => FillDifferenceWeightedIntermediateMask<CompoundIntermediateDifferenceWeightedMaskOperator>(
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
    /// Executes one closed difference-mask compound-intermediate operator.
    /// </summary>
    /// <typeparam name="TOperator">The compound-intermediate operator.</typeparam>
    /// <param name="mask">The mask output.</param>
    /// <param name="maskStride">The distance between mask rows.</param>
    /// <param name="first">The first compound intermediate.</param>
    /// <param name="firstStride">The distance between first-intermediate rows in samples.</param>
    /// <param name="second">The second compound intermediate.</param>
    /// <param name="secondStride">The distance between second-intermediate rows in samples.</param>
    /// <param name="width">The active block width.</param>
    /// <param name="height">The active block height.</param>
    /// <param name="bitDepth">The decoded sample precision that produced the intermediates.</param>
    /// <param name="maskType">The difference-weighted mask type.</param>
    private static void FillDifferenceWeightedIntermediateMask<TOperator>(
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
        where TOperator : struct, IAv1CompoundIntermediateDifferenceWeightedMaskOperator
    {
        bool invert = maskType == Av1DifferenceWeightedMaskType.Type38Inverse;
        GetIntermediateRounding(bitDepth, out int roundBits, out _);

        // The shift removes the fractional bits of the intermediates and scales the difference from bitDepth to 8-bit sample units.
        int differenceRound = roundBits + bitDepth - 8;
        for (int row = 0; row < height; row++)
        {
            Span<byte> maskRow = mask.Slice(row * maskStride, width);
            ReadOnlySpan<ushort> firstRow = first.Slice(row * firstStride, width);
            ReadOnlySpan<ushort> secondRow = second.Slice(row * secondStride, width);
            ref byte maskReference = ref MemoryMarshal.GetReference(maskRow);
            ref ushort firstReference = ref MemoryMarshal.GetReference(firstRow);
            ref ushort secondReference = ref MemoryMarshal.GetReference(secondRow);
            int column = 0;

            // Each step loads two 16-bit vectors from each intermediate and stores one byte vector that covers the same columns.
            // The widest supported vectors take the row first. Each narrower width takes the columns that remain, and the scalar loop finishes the tail.
            if (Vector512.IsHardwareAccelerated)
            {
                nuint vectorCount = Numerics.Vector512Count<byte>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector512<byte>.Count)
                {
                    Vector512<ushort> first0 = Vector512.LoadUnsafe(ref firstReference, (nuint)column);
                    Vector512<ushort> first1 = Vector512.LoadUnsafe(ref firstReference, (nuint)(column + Vector512<ushort>.Count));
                    Vector512<ushort> second0 = Vector512.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector512<ushort> second1 = Vector512.LoadUnsafe(ref secondReference, (nuint)(column + Vector512<ushort>.Count));
                    TOperator.CreateMask(first0, first1, second0, second1, differenceRound, invert)
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
                    TOperator.CreateMask(first0, first1, second0, second1, differenceRound, invert)
                        .StoreUnsafe(ref maskReference, (nuint)column);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
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

                    TOperator.CreateMask(
                        first0,
                        first1,
                        second0,
                        second1,
                        differenceRound,
                        invert).StoreUnsafe(ref maskReference, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                maskRow[column] = TOperator.CreateMask(firstRow[column], secondRow[column], differenceRound, invert);
            }
        }
    }
}
