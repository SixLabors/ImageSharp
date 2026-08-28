// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Produces the biased high-precision intermediates required by compound inter prediction.
/// </content>
internal static partial class Av1InterPredictor
{
    /// <summary>
    /// The second-round shift retained by every compound convolution path.
    /// </summary>
    internal const int CompoundRound1Bits = 7;

    /// <summary>
    /// Reconstructs one 8-bit translational reference into AV1's unsigned compound intermediate format.
    /// </summary>
    public static void PredictCompound(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        Av1InterpolationFilter horizontalFilter,
        Av1InterpolationFilter verticalFilter,
        int horizontalPhase,
        int verticalPhase,
        Span<short> scratch)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            PredictCompoundVector128(
                source,
                sourceStride,
                sourceOrigin,
                destination,
                destinationStride,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                verticalPhase,
                scratch);

            return;
        }

        PredictCompoundScalar(
            source,
            sourceStride,
            sourceOrigin,
            destination,
            destinationStride,
            width,
            height,
            horizontalFilter,
            verticalFilter,
            horizontalPhase,
            verticalPhase,
            scratch);
    }

    /// <summary>
    /// Reconstructs one compound intermediate without explicit hardware intrinsics.
    /// </summary>
    public static void PredictCompoundScalar(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        Av1InterpolationFilter horizontalFilter,
        Av1InterpolationFilter verticalFilter,
        int horizontalPhase,
        int verticalPhase,
        Span<short> scratch)
    {
        ReadOnlySpan<short> horizontalCoefficients = GetCompoundCoefficients(horizontalFilter, horizontalPhase, width <= 4);
        ReadOnlySpan<short> verticalCoefficients = GetCompoundCoefficients(verticalFilter, verticalPhase, height <= 4);
        int roundBits = (2 * FilterBits) - Round0Bits - CompoundRound1Bits;
        int offsetBits = 8 + (2 * FilterBits) - Round0Bits;
        int roundOffset = (1 << (offsetBits - CompoundRound1Bits)) +
            (1 << (offsetBits - CompoundRound1Bits - 1));

        ref byte sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);

        if (horizontalPhase == 0 && verticalPhase == 0)
        {
            for (int row = 0; row < height; row++)
            {
                ref byte sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
                for (int column = 0; column < width; column++)
                {
                    Unsafe.Add(ref destinationRow, column) =
                        (ushort)((Unsafe.Add(ref sourceRow, column) << roundBits) + roundOffset);
                }
            }

            return;
        }

        if (verticalPhase == 0)
        {
            GetEffectiveKernel(horizontalCoefficients, out int firstCoefficient, out int tapCount);
            ref short coefficientBase = ref Unsafe.Add(
                ref MemoryMarshal.GetReference(horizontalCoefficients),
                firstCoefficient);

            int sourceOffset = firstCoefficient - 3;
            for (int row = 0; row < height; row++)
            {
                ref byte sourceRow = ref Unsafe.Add(ref sourceBase, (row * sourceStride) + sourceOffset);
                ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
                for (int column = 0; column < width; column++)
                {
                    int sum = ConvolveScalar(ref Unsafe.Add(ref sourceRow, column), 1, ref coefficientBase, tapCount);
                    int result = (RoundPowerOfTwo(sum, Round0Bits) << (FilterBits - CompoundRound1Bits)) + roundOffset;
                    Unsafe.Add(ref destinationRow, column) = (ushort)result;
                }
            }

            return;
        }

        if (horizontalPhase == 0)
        {
            GetEffectiveKernel(verticalCoefficients, out int firstCoefficient, out int tapCount);
            ref short coefficientBase = ref Unsafe.Add(
                ref MemoryMarshal.GetReference(verticalCoefficients),
                firstCoefficient);

            int sourceOffset = firstCoefficient - 3;
            int firstPassBits = FilterBits - Round0Bits;
            for (int row = 0; row < height; row++)
            {
                ref byte sourceRow = ref Unsafe.Add(ref sourceBase, (row + sourceOffset) * sourceStride);
                ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
                for (int column = 0; column < width; column++)
                {
                    int sum = ConvolveScalar(
                        ref Unsafe.Add(ref sourceRow, column),
                        sourceStride,
                        ref coefficientBase,
                        tapCount);

                    int result = RoundPowerOfTwo(sum << firstPassBits, CompoundRound1Bits) + roundOffset;
                    Unsafe.Add(ref destinationRow, column) = (ushort)result;
                }
            }

            return;
        }

        GetEffectiveKernel(horizontalCoefficients, out int firstHorizontalCoefficient, out int horizontalTapCount);
        GetEffectiveKernel(verticalCoefficients, out int firstVerticalCoefficient, out int verticalTapCount);
        ref short horizontalCoefficientBase = ref Unsafe.Add(
            ref MemoryMarshal.GetReference(horizontalCoefficients),
            firstHorizontalCoefficient);

        ref short verticalCoefficientBase = ref Unsafe.Add(
            ref MemoryMarshal.GetReference(verticalCoefficients),
            firstVerticalCoefficient);

        int scratchStride = Math.Max(width, MinimumScratchStride);
        int intermediateHeight = height + verticalTapCount - 1;
        int horizontalSourceOffset = firstHorizontalCoefficient - 3;
        int verticalSourceOffset = firstVerticalCoefficient - 3;
        int horizontalBias = 1 << (8 + FilterBits - 1);
        ref short scratchBase = ref MemoryMarshal.GetReference(scratch);

        // The Q7 horizontal pass keeps enough precision for the vertical pass while the positive bias makes every
        // intermediate representable by signed 16-bit scratch. This is the same no-round compound shape as libaom.
        for (int row = 0; row < intermediateHeight; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(
                ref sourceBase,
                ((row + verticalSourceOffset) * sourceStride) + horizontalSourceOffset);

            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            for (int column = 0; column < width; column++)
            {
                int sum = horizontalBias + ConvolveScalar(
                    ref Unsafe.Add(ref sourceRow, column),
                    1,
                    ref horizontalCoefficientBase,
                    horizontalTapCount);

                Unsafe.Add(ref scratchRow, column) = (short)RoundPowerOfTwo(sum, Round0Bits);
            }
        }

        int verticalBias = 1 << offsetBits;
        for (int row = 0; row < height; row++)
        {
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            for (int column = 0; column < width; column++)
            {
                int sum = verticalBias + ConvolveScalar(
                    ref Unsafe.Add(ref scratchRow, column),
                    scratchStride,
                    ref verticalCoefficientBase,
                    verticalTapCount);

                Unsafe.Add(ref destinationRow, column) = (ushort)RoundPowerOfTwo(sum, CompoundRound1Bits);
            }
        }
    }

    /// <summary>
    /// Reconstructs one compound intermediate through the 128-bit convolution tier.
    /// </summary>
    private static void PredictCompoundVector128(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        Av1InterpolationFilter horizontalFilter,
        Av1InterpolationFilter verticalFilter,
        int horizontalPhase,
        int verticalPhase,
        Span<short> scratch)
    {
        ReadOnlySpan<short> horizontalCoefficients = GetCompoundCoefficients(horizontalFilter, horizontalPhase, width <= 4);
        ReadOnlySpan<short> verticalCoefficients = GetCompoundCoefficients(verticalFilter, verticalPhase, height <= 4);
        int roundBits = (2 * FilterBits) - Round0Bits - CompoundRound1Bits;
        int offsetBits = 8 + (2 * FilterBits) - Round0Bits;
        int roundOffset = (1 << (offsetBits - CompoundRound1Bits)) +
            (1 << (offsetBits - CompoundRound1Bits - 1));

        if (horizontalPhase == 0 && verticalPhase == 0)
        {
            CopyCompoundVector128(
                source,
                sourceStride,
                sourceOrigin,
                destination,
                destinationStride,
                width,
                height,
                roundBits,
                roundOffset);

            return;
        }

        if (verticalPhase == 0)
        {
            GetEffectiveKernel(horizontalCoefficients, out int firstCoefficient, out int tapCount);
            FilterCompoundDirectVector128(
                source,
                sourceStride,
                sourceOrigin,
                destination,
                destinationStride,
                width,
                height,
                horizontalCoefficients[firstCoefficient..],
                tapCount,
                firstCoefficient - 3,
                tapStride: 1,
                preShift: 0,
                round: Round0Bits,
                roundOffset);

            return;
        }

        if (horizontalPhase == 0)
        {
            GetEffectiveKernel(verticalCoefficients, out int firstCoefficient, out int tapCount);
            FilterCompoundDirectVector128(
                source,
                sourceStride,
                sourceOrigin,
                destination,
                destinationStride,
                width,
                height,
                verticalCoefficients[firstCoefficient..],
                tapCount,
                (firstCoefficient - 3) * sourceStride,
                sourceStride,
                FilterBits - Round0Bits,
                CompoundRound1Bits,
                roundOffset);

            return;
        }

        GetEffectiveKernel(horizontalCoefficients, out int firstHorizontalCoefficient, out int horizontalTapCount);
        GetEffectiveKernel(verticalCoefficients, out int firstVerticalCoefficient, out int verticalTapCount);
        FilterCompound2DVector128(
            source,
            sourceStride,
            sourceOrigin,
            destination,
            destinationStride,
            width,
            height,
            horizontalCoefficients[firstHorizontalCoefficient..],
            horizontalTapCount,
            firstHorizontalCoefficient - 3,
            verticalCoefficients[firstVerticalCoefficient..],
            verticalTapCount,
            firstVerticalCoefficient - 3,
            scratch);
    }

    /// <summary>
    /// Copies integer-position samples into biased compound intermediates in sixteen-sample groups.
    /// </summary>
    private static void CopyCompoundVector128(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        int roundBits,
        int roundOffset)
    {
        ref byte sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        Vector128<ushort> offset = Vector128.Create((ushort)roundOffset);

        for (int row = 0; row < height; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
            ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int column = 0;
            int vectorEnd = width - Vector128<byte>.Count;
            for (; column <= vectorEnd; column += Vector128<byte>.Count)
            {
                Vector128<byte> samples = Vector128.LoadUnsafe(ref sourceRow, (nuint)column);
                ((Vector128.WidenLower(samples) << roundBits) + offset).StoreUnsafe(
                    ref destinationRow,
                    (nuint)column);

                ((Vector128.WidenUpper(samples) << roundBits) + offset).StoreUnsafe(
                    ref destinationRow,
                    (nuint)(column + Vector128<ushort>.Count));
            }

            if (column == 0)
            {
                // Narrow AV1 blocks still use the SIMD load; the width-specific stores preserve the adjacent block.
                Vector128<byte> samples = Vector128.LoadUnsafe(ref sourceRow);
                StoreCompoundVectors(
                    (Vector128.WidenLower(samples) << roundBits) + offset,
                    (Vector128.WidenUpper(samples) << roundBits) + offset,
                    ref destinationRow,
                    width);

                continue;
            }

            for (; column < width; column++)
            {
                Unsafe.Add(ref destinationRow, column) =
                    (ushort)((Unsafe.Add(ref sourceRow, column) << roundBits) + roundOffset);
            }
        }
    }

    /// <summary>
    /// Applies one compound convolution direction in sixteen-sample groups.
    /// </summary>
    private static void FilterCompoundDirectVector128(
        ReadOnlySpan<byte> source,
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
        int preShift,
        int round,
        int roundOffset)
    {
        ref byte sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short coefficientBase = ref MemoryMarshal.GetReference(coefficients);
        Vector128<int> offset = Vector128.Create(roundOffset);

        for (int row = 0; row < height; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(ref sourceBase, (row * sourceStride) + sourceOffset);
            ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int column = 0;
            int vectorEnd = width - Vector128<byte>.Count;
            for (; column <= vectorEnd; column += Vector128<byte>.Count)
            {
                Convolve(
                    ref sourceRow,
                    tapStride,
                    (nuint)column,
                    ref coefficientBase,
                    tapCount,
                    Vector128<int>.Zero,
                    out Vector128<int> result0,
                    out Vector128<int> result1,
                    out Vector128<int> result2,
                    out Vector128<int> result3);

                PrepareCompoundResults(
                    ref result0,
                    ref result1,
                    ref result2,
                    ref result3,
                    preShift,
                    round,
                    offset);

                StoreCompoundVectors(result0, result1, result2, result3, ref destinationRow, column, Vector128<byte>.Count);
            }

            if (column == 0)
            {
                Convolve(
                    ref sourceRow,
                    tapStride,
                    0,
                    ref coefficientBase,
                    tapCount,
                    Vector128<int>.Zero,
                    out Vector128<int> result0,
                    out Vector128<int> result1,
                    out Vector128<int> result2,
                    out Vector128<int> result3);

                PrepareCompoundResults(
                    ref result0,
                    ref result1,
                    ref result2,
                    ref result3,
                    preShift,
                    round,
                    offset);

                StoreCompoundVectors(result0, result1, result2, result3, ref destinationRow, 0, width);
                continue;
            }

            for (; column < width; column++)
            {
                int sum = ConvolveScalar(
                    ref Unsafe.Add(ref sourceRow, column),
                    tapStride,
                    ref coefficientBase,
                    tapCount);

                sum = RoundPowerOfTwo(sum << preShift, round) + roundOffset;
                Unsafe.Add(ref destinationRow, column) = (ushort)sum;
            }
        }
    }

    /// <summary>
    /// Applies separable compound convolution through caller-owned signed scratch.
    /// </summary>
    private static void FilterCompound2DVector128(
        ReadOnlySpan<byte> source,
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
        Span<short> scratch)
    {
        ref byte sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short scratchBase = ref MemoryMarshal.GetReference(scratch);
        ref short horizontalCoefficientBase = ref MemoryMarshal.GetReference(horizontalCoefficients);
        ref short verticalCoefficientBase = ref MemoryMarshal.GetReference(verticalCoefficients);
        int scratchStride = Math.Max(width, MinimumScratchStride);
        int intermediateHeight = height + verticalTapCount - 1;
        Vector128<int> horizontalBias = Vector128.Create(1 << (8 + FilterBits - 1));

        // The complete narrow-block vector is retained in scratch because the vertical pass consumes the same lanes.
        // Wider blocks use one vector per sixteen output samples and finish any nonstandard tail scalarly.
        for (int row = 0; row < intermediateHeight; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(
                ref sourceBase,
                ((row + verticalSourceOffset) * sourceStride) + horizontalSourceOffset);

            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            int column = 0;
            int vectorEnd = width - Vector128<byte>.Count;
            for (; column <= vectorEnd; column += Vector128<byte>.Count)
            {
                Convolve(
                    ref sourceRow,
                    1,
                    (nuint)column,
                    ref horizontalCoefficientBase,
                    horizontalTapCount,
                    horizontalBias,
                    out Vector128<int> result0,
                    out Vector128<int> result1,
                    out Vector128<int> result2,
                    out Vector128<int> result3);

                Av1IntraPredictorBase.Narrow(
                    RoundPowerOfTwo(result0, Round0Bits),
                    RoundPowerOfTwo(result1, Round0Bits)).StoreUnsafe(ref scratchRow, (nuint)column);

                Av1IntraPredictorBase.Narrow(
                    RoundPowerOfTwo(result2, Round0Bits),
                    RoundPowerOfTwo(result3, Round0Bits)).StoreUnsafe(
                        ref scratchRow,
                        (nuint)(column + Vector128<short>.Count));
            }

            if (column == 0)
            {
                Convolve(
                    ref sourceRow,
                    1,
                    0,
                    ref horizontalCoefficientBase,
                    horizontalTapCount,
                    horizontalBias,
                    out Vector128<int> result0,
                    out Vector128<int> result1,
                    out Vector128<int> result2,
                    out Vector128<int> result3);

                Av1IntraPredictorBase.Narrow(
                    RoundPowerOfTwo(result0, Round0Bits),
                    RoundPowerOfTwo(result1, Round0Bits)).StoreUnsafe(ref scratchRow);

                Av1IntraPredictorBase.Narrow(
                    RoundPowerOfTwo(result2, Round0Bits),
                    RoundPowerOfTwo(result3, Round0Bits)).StoreUnsafe(
                        ref scratchRow,
                        (nuint)Vector128<short>.Count);

                continue;
            }

            for (; column < width; column++)
            {
                int sum = (1 << (8 + FilterBits - 1)) + ConvolveScalar(
                    ref Unsafe.Add(ref sourceRow, column),
                    1,
                    ref horizontalCoefficientBase,
                    horizontalTapCount);

                Unsafe.Add(ref scratchRow, column) = (short)RoundPowerOfTwo(sum, Round0Bits);
            }
        }

        Vector128<int> verticalBias = Vector128.Create(1 << (8 + (2 * FilterBits) - Round0Bits));
        for (int row = 0; row < height; row++)
        {
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int column = 0;
            int vectorEnd = width - Vector128<byte>.Count;
            for (; column <= vectorEnd; column += Vector128<byte>.Count)
            {
                Convolve(
                    ref scratchRow,
                    scratchStride,
                    (nuint)column,
                    ref verticalCoefficientBase,
                    verticalTapCount,
                    verticalBias,
                    out Vector128<int> result0,
                    out Vector128<int> result1);

                Convolve(
                    ref scratchRow,
                    scratchStride,
                    (nuint)(column + Vector128<short>.Count),
                    ref verticalCoefficientBase,
                    verticalTapCount,
                    verticalBias,
                    out Vector128<int> result2,
                    out Vector128<int> result3);

                result0 = RoundPowerOfTwo(result0, CompoundRound1Bits);
                result1 = RoundPowerOfTwo(result1, CompoundRound1Bits);
                result2 = RoundPowerOfTwo(result2, CompoundRound1Bits);
                result3 = RoundPowerOfTwo(result3, CompoundRound1Bits);
                StoreCompoundVectors(result0, result1, result2, result3, ref destinationRow, column, Vector128<byte>.Count);
            }

            if (column == 0)
            {
                Convolve(
                    ref scratchRow,
                    scratchStride,
                    0,
                    ref verticalCoefficientBase,
                    verticalTapCount,
                    verticalBias,
                    out Vector128<int> result0,
                    out Vector128<int> result1);

                Convolve(
                    ref Unsafe.Add(ref scratchRow, Vector128<short>.Count),
                    scratchStride,
                    0,
                    ref verticalCoefficientBase,
                    verticalTapCount,
                    verticalBias,
                    out Vector128<int> result2,
                    out Vector128<int> result3);

                result0 = RoundPowerOfTwo(result0, CompoundRound1Bits);
                result1 = RoundPowerOfTwo(result1, CompoundRound1Bits);
                result2 = RoundPowerOfTwo(result2, CompoundRound1Bits);
                result3 = RoundPowerOfTwo(result3, CompoundRound1Bits);
                StoreCompoundVectors(result0, result1, result2, result3, ref destinationRow, 0, width);
                continue;
            }

            for (; column < width; column++)
            {
                int sum = (1 << (8 + (2 * FilterBits) - Round0Bits)) + ConvolveScalar(
                    ref Unsafe.Add(ref scratchRow, column),
                    scratchStride,
                    ref verticalCoefficientBase,
                    verticalTapCount);

                Unsafe.Add(ref destinationRow, column) =
                    (ushort)RoundPowerOfTwo(sum, CompoundRound1Bits);
            }
        }
    }

    /// <summary>
    /// Applies the compound direct-filter shifts and bias to sixteen convolution results.
    /// </summary>
    private static void PrepareCompoundResults(
        ref Vector128<int> result0,
        ref Vector128<int> result1,
        ref Vector128<int> result2,
        ref Vector128<int> result3,
        int preShift,
        int round,
        Vector128<int> offset)
    {
        result0 = RoundPowerOfTwo(result0 << preShift, round) + offset;
        result1 = RoundPowerOfTwo(result1 << preShift, round) + offset;
        result2 = RoundPowerOfTwo(result2 << preShift, round) + offset;
        result3 = RoundPowerOfTwo(result3 << preShift, round) + offset;
    }

    /// <summary>
    /// Packs and stores up to sixteen unsigned compound results.
    /// </summary>
    private static void StoreCompoundVectors(
        Vector128<int> result0,
        Vector128<int> result1,
        Vector128<int> result2,
        Vector128<int> result3,
        ref ushort destination,
        int destinationOffset,
        int width)
    {
        Vector128<ushort> lower = Av1IntraPredictorBase.Narrow(result0, result1).AsUInt16();
        Vector128<ushort> upper = Av1IntraPredictorBase.Narrow(result2, result3).AsUInt16();
        ref ushort destinationStart = ref Unsafe.Add(ref destination, destinationOffset);
        StoreCompoundVectors(lower, upper, ref destinationStart, width);
    }

    /// <summary>
    /// Stores up to sixteen packed compound results without crossing the logical block edge.
    /// </summary>
    private static void StoreCompoundVectors(
        Vector128<ushort> lower,
        Vector128<ushort> upper,
        ref ushort destination,
        int width)
    {
        int lowerWidth = Math.Min(width, Vector128<ushort>.Count);
        StorePartial(lower, ref destination, lowerWidth);
        if (width > Vector128<ushort>.Count)
        {
            StorePartial(
                upper,
                ref Unsafe.Add(ref destination, Vector128<ushort>.Count),
                width - Vector128<ushort>.Count);
        }
    }

    /// <summary>
    /// Gets the selected interpolation kernel for compound traversal.
    /// </summary>
    private static ReadOnlySpan<short> GetCompoundCoefficients(
        Av1InterpolationFilter filter,
        int phase,
        bool useReducedFilter)
        => filter switch
        {
            Av1InterpolationFilter.Regular => RegularOperator.GetCoefficients(phase, useReducedFilter),
            Av1InterpolationFilter.Smooth => SmoothOperator.GetCoefficients(phase, useReducedFilter),
            Av1InterpolationFilter.Sharp => SharpOperator.GetCoefficients(phase, useReducedFilter),
            _ => BilinearOperator.GetCoefficients(phase, useReducedFilter),
        };
}
