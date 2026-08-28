// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Combines high-precision compound convolution intermediates into reconstructed samples.
/// </content>
internal static partial class Av1CompoundInterPredictor
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

                    AverageIntermediate(first0, first1, second0, second1, roundBits, roundOffset).StoreUnsafe(
                        ref destinationReference,
                        (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                // The reference average deliberately truncates here. The sole rounding step follows bias removal,
                // preventing the double rounding that occurs when each reference is first converted to pixels.
                int result = ((firstRow[column] + secondRow[column]) >> 1) - roundOffset;
                destinationRow[column] = (byte)Math.Clamp(RoundPowerOfTwo(result, roundBits), 0, byte.MaxValue);
            }
        }
    }

    /// <summary>
    /// Combines two compound intermediates using the decoded temporal-distance weights.
    /// </summary>
    public static void DistanceWeightedIntermediate(
        Span<byte> destination,
        int destinationStride,
        ReadOnlySpan<ushort> first,
        int firstStride,
        ReadOnlySpan<ushort> second,
        int secondStride,
        int width,
        int height,
        int firstWeight,
        int secondWeight,
        int bitDepth)
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

                    DistanceWeightedIntermediate(
                        first0,
                        first1,
                        second0,
                        second1,
                        firstWeight,
                        secondWeight,
                        roundBits,
                        roundOffset).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                int result = ((firstRow[column] * firstWeight) + (secondRow[column] * secondWeight)) >> DistanceWeightBits;
                result -= roundOffset;
                destinationRow[column] = (byte)Math.Clamp(RoundPowerOfTwo(result, roundBits), 0, byte.MaxValue);
            }
        }
    }

    /// <summary>
    /// Fills a luma-resolution difference-weighted mask from compound intermediates.
    /// </summary>
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
    {
        bool invert = maskType == Av1DifferenceWeightedMaskType.Type38Inverse;
        GetIntermediateRounding(bitDepth, out int roundBits, out _);
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

                    DifferenceWeightedIntermediate(
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
                int difference = Math.Abs(firstRow[column] - secondRow[column]);
                difference = RoundPowerOfTwo(difference, differenceRound);
                int alpha = Math.Min(MaximumMaskAlpha, 38 + (difference >> 4));
                maskRow[column] = (byte)(invert ? MaximumMaskAlpha - alpha : alpha);
            }
        }
    }

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

                    BlendIntermediate(
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
                int alpha = GetSubsampledMaskAlpha(mask, maskStride, row, column, subX, subY);

                // Mask blending also truncates its Q6 result because final pixel rounding is still pending. Adding
                // a half-unit here would produce a second rounding step and diverge from pinned libaom.
                int result = ((alpha * firstRow[column]) + ((MaximumMaskAlpha - alpha) * secondRow[column])) >> MaskWeightBits;
                result -= roundOffset;
                destinationRow[column] = (byte)Math.Clamp(RoundPowerOfTwo(result, roundBits), 0, byte.MaxValue);
            }
        }
    }

    /// <summary>
    /// Equal-averages sixteen compound lanes and converts them to final 8-bit samples.
    /// </summary>
    private static Vector128<byte> AverageIntermediate(
        Vector128<ushort> first0,
        Vector128<ushort> first1,
        Vector128<ushort> second0,
        Vector128<ushort> second1,
        int roundBits,
        int roundOffset)
        => Vector128.Narrow(
            FinalizeIntermediate(
                (first0 & second0) + ((first0 ^ second0) >> 1),
                roundBits,
                roundOffset),
            FinalizeIntermediate(
                (first1 & second1) + ((first1 ^ second1) >> 1),
                roundBits,
                roundOffset));

    /// <summary>
    /// Distance-weights sixteen compound lanes and converts them to final 8-bit samples.
    /// </summary>
    private static Vector128<byte> DistanceWeightedIntermediate(
        Vector128<ushort> first0,
        Vector128<ushort> first1,
        Vector128<ushort> second0,
        Vector128<ushort> second1,
        int firstWeight,
        int secondWeight,
        int roundBits,
        int roundOffset)
        => Vector128.Narrow(
            DistanceWeightedIntermediate(first0, second0, firstWeight, secondWeight, roundBits, roundOffset),
            DistanceWeightedIntermediate(first1, second1, firstWeight, secondWeight, roundBits, roundOffset));

    /// <summary>
    /// Distance-weights eight compound lanes without overflowing the unsigned intermediate range.
    /// </summary>
    private static Vector128<ushort> DistanceWeightedIntermediate(
        Vector128<ushort> first,
        Vector128<ushort> second,
        int firstWeight,
        int secondWeight,
        int roundBits,
        int roundOffset)
    {
        Vector128<int> firstLower = Vector128.WidenLower(first).AsInt32();
        Vector128<int> firstUpper = Vector128.WidenUpper(first).AsInt32();
        Vector128<int> secondLower = Vector128.WidenLower(second).AsInt32();
        Vector128<int> secondUpper = Vector128.WidenUpper(second).AsInt32();
        Vector128<int> lower =
            ((firstLower * firstWeight) + (secondLower * secondWeight)) >> DistanceWeightBits;

        Vector128<int> upper =
            ((firstUpper * firstWeight) + (secondUpper * secondWeight)) >> DistanceWeightBits;

        return Vector128.Narrow(
            FinalizeIntermediate(lower, roundBits, roundOffset),
            FinalizeIntermediate(upper, roundBits, roundOffset)).AsUInt16();
    }

    /// <summary>
    /// Creates sixteen difference-weighted mask values from compound intermediates.
    /// </summary>
    private static Vector128<byte> DifferenceWeightedIntermediate(
        Vector128<ushort> first0,
        Vector128<ushort> first1,
        Vector128<ushort> second0,
        Vector128<ushort> second1,
        int differenceRound,
        bool invert)
        => Vector128.Narrow(
            DifferenceWeightedIntermediate(first0, second0, differenceRound, invert),
            DifferenceWeightedIntermediate(first1, second1, differenceRound, invert));

    /// <summary>
    /// Creates eight difference-weighted mask values without losing the required pre-alpha rounding.
    /// </summary>
    private static Vector128<ushort> DifferenceWeightedIntermediate(
        Vector128<ushort> first,
        Vector128<ushort> second,
        int differenceRound,
        bool invert)
    {
        Vector128<ushort> difference = Vector128.Max(first, second) - Vector128.Min(first, second);
        Vector128<int> lower = DifferenceWeightedIntermediate(
            Vector128.WidenLower(difference).AsInt32(),
            differenceRound,
            invert);

        Vector128<int> upper = DifferenceWeightedIntermediate(
            Vector128.WidenUpper(difference).AsInt32(),
            differenceRound,
            invert);

        return Vector128.Narrow(lower, upper).AsUInt16();
    }

    /// <summary>
    /// Converts four intermediate differences to the decoded type-38 mask range.
    /// </summary>
    private static Vector128<int> DifferenceWeightedIntermediate(
        Vector128<int> difference,
        int differenceRound,
        bool invert)
    {
        if (differenceRound != 0)
        {
            difference = (difference + Vector128.Create(1 << (differenceRound - 1))) >> differenceRound;
        }

        Vector128<int> maximum = Vector128.Create(MaximumMaskAlpha);
        Vector128<int> alpha = Vector128.Min(maximum, (difference >> 4) + Vector128.Create(38));
        return invert ? maximum - alpha : alpha;
    }

    /// <summary>
    /// Mask-blends sixteen compound lanes and converts them to final 8-bit samples.
    /// </summary>
    private static Vector128<byte> BlendIntermediate(
        Vector128<ushort> first0,
        Vector128<ushort> first1,
        Vector128<ushort> second0,
        Vector128<ushort> second1,
        Vector128<byte> alpha,
        int roundBits,
        int roundOffset)
        => Vector128.Narrow(
            BlendIntermediate(
                first0,
                second0,
                Vector128.WidenLower(alpha),
                roundBits,
                roundOffset),
            BlendIntermediate(
                first1,
                second1,
                Vector128.WidenUpper(alpha),
                roundBits,
                roundOffset));

    /// <summary>
    /// Mask-blends eight compound lanes after widening every product to signed 32-bit precision.
    /// </summary>
    private static Vector128<ushort> BlendIntermediate(
        Vector128<ushort> first,
        Vector128<ushort> second,
        Vector128<ushort> alpha,
        int roundBits,
        int roundOffset)
    {
        Vector128<int> firstLower = Vector128.WidenLower(first).AsInt32();
        Vector128<int> firstUpper = Vector128.WidenUpper(first).AsInt32();
        Vector128<int> secondLower = Vector128.WidenLower(second).AsInt32();
        Vector128<int> secondUpper = Vector128.WidenUpper(second).AsInt32();
        Vector128<int> alphaLower = Vector128.WidenLower(alpha).AsInt32();
        Vector128<int> alphaUpper = Vector128.WidenUpper(alpha).AsInt32();
        Vector128<int> maximum = Vector128.Create(MaximumMaskAlpha);
        Vector128<int> lower =
            ((alphaLower * firstLower) + ((maximum - alphaLower) * secondLower)) >> MaskWeightBits;

        Vector128<int> upper =
            ((alphaUpper * firstUpper) + ((maximum - alphaUpper) * secondUpper)) >> MaskWeightBits;

        return Vector128.Narrow(
            FinalizeIntermediate(lower, roundBits, roundOffset),
            FinalizeIntermediate(upper, roundBits, roundOffset)).AsUInt16();
    }

    /// <summary>
    /// Removes the compound bias and final fractional precision from eight unsigned lanes.
    /// </summary>
    private static Vector128<ushort> FinalizeIntermediate(
        Vector128<ushort> value,
        int roundBits,
        int roundOffset)
    {
        Vector128<short> result = (value - Vector128.Create((ushort)roundOffset)).AsInt16();
        if (roundBits != 0)
        {
            result = (result + Vector128.Create((short)(1 << (roundBits - 1)))) >> roundBits;
        }

        result = Vector128.Max(Vector128<short>.Zero, Vector128.Min(Vector128.Create((short)byte.MaxValue), result));
        return result.AsUInt16();
    }

    /// <summary>
    /// Removes the compound bias and final fractional precision from four widened lanes.
    /// </summary>
    private static Vector128<int> FinalizeIntermediate(
        Vector128<int> value,
        int roundBits,
        int roundOffset)
    {
        Vector128<int> result = value - Vector128.Create(roundOffset);
        if (roundBits != 0)
        {
            result = (result + Vector128.Create(1 << (roundBits - 1))) >> roundBits;
        }

        return Vector128.Max(Vector128<int>.Zero, Vector128.Min(Vector128.Create((int)byte.MaxValue), result));
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

    /// <summary>
    /// Derives the bias and remaining fractional precision of a compound intermediate.
    /// </summary>
    private static void GetIntermediateRounding(int bitDepth, out int roundBits, out int roundOffset)
    {
        int intermediateRange = bitDepth + 7 - 3 + 2;
        int round0 = 3 + Math.Max(intermediateRange - 16, 0);
        int offsetBits = bitDepth + 14 - round0;
        roundBits = 14 - round0 - Av1InterPredictor.CompoundRound1Bits;
        roundOffset = (1 << (offsetBits - Av1InterPredictor.CompoundRound1Bits)) +
            (1 << (offsetBits - Av1InterPredictor.CompoundRound1Bits - 1));
    }

    /// <summary>
    /// Applies AV1's positive power-of-two rounding rule.
    /// </summary>
    private static int RoundPowerOfTwo(int value, int bits)
        => bits == 0 ? value : (value + (1 << (bits - 1))) >> bits;
}
