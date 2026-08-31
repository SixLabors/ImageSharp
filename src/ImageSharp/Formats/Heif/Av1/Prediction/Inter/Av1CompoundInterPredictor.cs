// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1InterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Produces the biased high-precision intermediates required by compound inter prediction.
/// </content>
internal static partial class Av1CompoundInterPredictor
{
    /// <summary>
    /// The second-round shift retained by every compound convolution path.
    /// </summary>
    internal const int CompoundRound1Bits = 7;

    /// <summary>
    /// The fixed-point precision used by AV1 distance weights.
    /// </summary>
    internal const int DistanceWeightBits = 4;

    /// <summary>
    /// The fixed-point precision used by AV1 compound masks.
    /// </summary>
    internal const int MaskWeightBits = 6;

    /// <summary>
    /// The inclusive upper bound for an AV1 compound-mask alpha value.
    /// </summary>
    internal const int MaximumMaskAlpha = 1 << MaskWeightBits;

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
        => PredictCompound<CompoundPredictionOperator>(
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
            scratch,
            useSimd: true);

    /// <summary>
    /// Reconstructs one high-bit-depth translational reference into AV1's unsigned compound intermediate format.
    /// </summary>
    public static void PredictCompound(
        ReadOnlySpan<ushort> source,
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
        int bitDepth,
        Span<short> scratch)
        => PredictCompound<CompoundPredictionOperator>(
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
            bitDepth,
            scratch,
            useSimd: true);

    /// <summary>
    /// Executes one closed compound-prediction conversion operator.
    /// </summary>
    /// <typeparam name="TOperator">The compound-prediction conversion operator.</typeparam>
    private static void PredictCompound<TOperator>(
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
        Span<short> scratch,
        bool useSimd)
        where TOperator : struct, IAv1CompoundPredictionOperator
    {
        ReadOnlySpan<short> horizontalCoefficients = GetCompoundCoefficients(horizontalFilter, horizontalPhase, width <= 4);
        ReadOnlySpan<short> verticalCoefficients = GetCompoundCoefficients(verticalFilter, verticalPhase, height <= 4);
        int roundBits = (2 * FilterBits) - Round0Bits - CompoundRound1Bits;
        int offsetBits = 8 + (2 * FilterBits) - Round0Bits;
        int roundOffset = (1 << (offsetBits - CompoundRound1Bits)) +
            (1 << (offsetBits - CompoundRound1Bits - 1));

        if (horizontalPhase == 0 && verticalPhase == 0)
        {
            CopyCompound<TOperator>(
                source,
                sourceStride,
                sourceOrigin,
                destination,
                destinationStride,
                width,
                height,
                roundBits,
                roundOffset,
                useSimd);

            return;
        }

        if (verticalPhase == 0)
        {
            GetEffectiveKernel(horizontalCoefficients, out int firstCoefficient, out int tapCount);
            FilterCompoundDirect<TOperator>(
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
                roundOffset,
                useSimd);

            return;
        }

        if (horizontalPhase == 0)
        {
            GetEffectiveKernel(verticalCoefficients, out int firstCoefficient, out int tapCount);
            FilterCompoundDirect<TOperator>(
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
                roundOffset,
                useSimd);

            return;
        }

        GetEffectiveKernel(horizontalCoefficients, out int firstHorizontalCoefficient, out int horizontalTapCount);
        GetEffectiveKernel(verticalCoefficients, out int firstVerticalCoefficient, out int verticalTapCount);
        FilterCompound2D<TOperator>(
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
            scratch,
            useSimd);
    }

    /// <summary>
    /// Executes one closed high-bit-depth compound-prediction conversion operator.
    /// </summary>
    /// <typeparam name="TOperator">The compound-prediction conversion operator.</typeparam>
    private static void PredictCompound<TOperator>(
        ReadOnlySpan<ushort> source,
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
        int bitDepth,
        Span<short> scratch,
        bool useSimd)
        where TOperator : struct, IAv1CompoundPredictionOperator
    {
        ReadOnlySpan<short> horizontalCoefficients = GetCompoundCoefficients(horizontalFilter, horizontalPhase, width <= 4);
        ReadOnlySpan<short> verticalCoefficients = GetCompoundCoefficients(verticalFilter, verticalPhase, height <= 4);

        // The reference decoder increases the first-round shift only for 12-bit input. This keeps the signed horizontal
        // intermediate within sixteen bits while preserving the same total Q14 convolution precision.
        int intermediateRange = bitDepth + FilterBits - Round0Bits + 2;
        int round0 = Round0Bits + Math.Max(intermediateRange - 16, 0);
        int roundBits = (2 * FilterBits) - round0 - CompoundRound1Bits;
        int offsetBits = bitDepth + (2 * FilterBits) - round0;
        int roundOffset = (1 << (offsetBits - CompoundRound1Bits)) +
            (1 << (offsetBits - CompoundRound1Bits - 1));

        if (horizontalPhase == 0 && verticalPhase == 0)
        {
            CopyCompound<TOperator>(
                source,
                sourceStride,
                sourceOrigin,
                destination,
                destinationStride,
                width,
                height,
                roundBits,
                roundOffset,
                useSimd);

            return;
        }

        if (verticalPhase == 0)
        {
            GetEffectiveKernel(horizontalCoefficients, out int firstCoefficient, out int tapCount);
            FilterCompoundDirect<TOperator>(
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
                round: round0,
                roundOffset,
                useSimd);

            return;
        }

        if (horizontalPhase == 0)
        {
            GetEffectiveKernel(verticalCoefficients, out int firstCoefficient, out int tapCount);
            FilterCompoundDirect<TOperator>(
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
                FilterBits - round0,
                CompoundRound1Bits,
                roundOffset,
                useSimd);

            return;
        }

        GetEffectiveKernel(horizontalCoefficients, out int firstHorizontalCoefficient, out int horizontalTapCount);
        GetEffectiveKernel(verticalCoefficients, out int firstVerticalCoefficient, out int verticalTapCount);
        FilterCompound2D<TOperator>(
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
            bitDepth,
            round0,
            scratch,
            useSimd);
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
        => PredictCompound<CompoundPredictionOperator>(
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
            scratch,
            useSimd: false);

    /// <summary>
    /// Reconstructs one high-bit-depth compound intermediate without explicit hardware intrinsics.
    /// </summary>
    public static void PredictCompoundScalar(
        ReadOnlySpan<ushort> source,
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
        int bitDepth,
        Span<short> scratch)
        => PredictCompound<CompoundPredictionOperator>(
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
            bitDepth,
            scratch,
            useSimd: false);

    /// <summary>
    /// Copies integer-position samples through one closed compound-prediction operator.
    /// </summary>
    /// <typeparam name="TOperator">The compound-prediction conversion operator.</typeparam>
    private static void CopyCompound<TOperator>(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        int roundBits,
        int roundOffset,
        bool useSimd)
        where TOperator : struct, IAv1CompoundPredictionOperator
    {
        ref byte sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);

        for (int row = 0; row < height; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
            ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int column = 0;

            if (useSimd && Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector512<byte>.Count;
                for (; column <= vectorEnd; column += Vector512<byte>.Count)
                {
                    Vector512<byte> samples = Vector512.LoadUnsafe(ref sourceRow, (nuint)column);
                    TOperator.Copy(samples, roundBits, roundOffset, out Vector512<ushort> lower, out Vector512<ushort> upper);
                    lower.StoreUnsafe(ref destinationRow, (nuint)column);
                    upper.StoreUnsafe(ref destinationRow, (nuint)(column + Vector512<ushort>.Count));
                }
            }

            if (useSimd && Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector256<byte>.Count;
                for (; column <= vectorEnd; column += Vector256<byte>.Count)
                {
                    Vector256<byte> samples = Vector256.LoadUnsafe(ref sourceRow, (nuint)column);
                    TOperator.Copy(samples, roundBits, roundOffset, out Vector256<ushort> lower, out Vector256<ushort> upper);
                    lower.StoreUnsafe(ref destinationRow, (nuint)column);
                    upper.StoreUnsafe(ref destinationRow, (nuint)(column + Vector256<ushort>.Count));
                }
            }

            if (useSimd && Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector128<byte>.Count;
                for (; column <= vectorEnd; column += Vector128<byte>.Count)
                {
                    Vector128<byte> samples = Vector128.LoadUnsafe(ref sourceRow, (nuint)column);
                    TOperator.Copy(samples, roundBits, roundOffset, out Vector128<ushort> lower, out Vector128<ushort> upper);
                    lower.StoreUnsafe(ref destinationRow, (nuint)column);
                    upper.StoreUnsafe(ref destinationRow, (nuint)(column + Vector128<ushort>.Count));
                }
            }

            for (; column < width; column++)
            {
                Unsafe.Add(ref destinationRow, column) = TOperator.Copy(Unsafe.Add(ref sourceRow, column), roundBits, roundOffset);
            }
        }
    }

    /// <summary>
    /// Copies high-bit-depth integer-position samples through one closed compound-prediction operator.
    /// </summary>
    /// <typeparam name="TOperator">The compound-prediction conversion operator.</typeparam>
    private static void CopyCompound<TOperator>(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        int roundBits,
        int roundOffset,
        bool useSimd)
        where TOperator : struct, IAv1CompoundPredictionOperator
    {
        ref ushort sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);

        for (int row = 0; row < height; row++)
        {
            ref ushort sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
            ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int column = 0;

            if (useSimd && Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector512<ushort>.Count;
                for (; column <= vectorEnd; column += Vector512<ushort>.Count)
                {
                    Vector512<ushort> samples = Vector512.LoadUnsafe(ref sourceRow, (nuint)column);
                    TOperator.CopyHighBitDepth(samples, roundBits, roundOffset)
                        .StoreUnsafe(ref destinationRow, (nuint)column);
                }
            }

            if (useSimd && Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector256<ushort>.Count;
                for (; column <= vectorEnd; column += Vector256<ushort>.Count)
                {
                    Vector256<ushort> samples = Vector256.LoadUnsafe(ref sourceRow, (nuint)column);
                    TOperator.CopyHighBitDepth(samples, roundBits, roundOffset)
                        .StoreUnsafe(ref destinationRow, (nuint)column);
                }
            }

            if (useSimd && Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector128<ushort>.Count;
                for (; column <= vectorEnd; column += Vector128<ushort>.Count)
                {
                    Vector128<ushort> samples = Vector128.LoadUnsafe(ref sourceRow, (nuint)column);
                    TOperator.CopyHighBitDepth(samples, roundBits, roundOffset)
                        .StoreUnsafe(ref destinationRow, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                Unsafe.Add(ref destinationRow, column) = TOperator.CopyHighBitDepth(
                    Unsafe.Add(ref sourceRow, column),
                    roundBits,
                    roundOffset);
            }
        }
    }

    /// <summary>
    /// Applies one compound convolution direction through one closed conversion operator.
    /// </summary>
    /// <typeparam name="TOperator">The compound-prediction conversion operator.</typeparam>
    private static void FilterCompoundDirect<TOperator>(
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
        int roundOffset,
        bool useSimd)
        where TOperator : struct, IAv1CompoundPredictionOperator
    {
        ref byte sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short coefficientBase = ref MemoryMarshal.GetReference(coefficients);

        for (int row = 0; row < height; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(ref sourceBase, (row * sourceStride) + sourceOffset);
            ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int column = 0;

            if (useSimd && Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector512<byte>.Count;
                for (; column <= vectorEnd; column += Vector512<byte>.Count)
                {
                    Convolve(
                        ref sourceRow,
                        tapStride,
                        (nuint)column,
                        ref coefficientBase,
                        tapCount,
                        Vector512<int>.Zero,
                        out Vector512<int> result0,
                        out Vector512<int> result1,
                        out Vector512<int> result2,
                        out Vector512<int> result3);

                    TOperator.PrepareDirect(result0, result1, preShift, round, roundOffset)
                        .StoreUnsafe(ref destinationRow, (nuint)column);

                    TOperator.PrepareDirect(result2, result3, preShift, round, roundOffset)
                        .StoreUnsafe(ref destinationRow, (nuint)(column + Vector512<ushort>.Count));
                }
            }

            if (useSimd && Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector256<byte>.Count;
                for (; column <= vectorEnd; column += Vector256<byte>.Count)
                {
                    Convolve(
                        ref sourceRow,
                        tapStride,
                        (nuint)column,
                        ref coefficientBase,
                        tapCount,
                        Vector256<int>.Zero,
                        out Vector256<int> result0,
                        out Vector256<int> result1,
                        out Vector256<int> result2,
                        out Vector256<int> result3);

                    TOperator.PrepareDirect(result0, result1, preShift, round, roundOffset)
                        .StoreUnsafe(ref destinationRow, (nuint)column);

                    TOperator.PrepareDirect(result2, result3, preShift, round, roundOffset)
                        .StoreUnsafe(ref destinationRow, (nuint)(column + Vector256<ushort>.Count));
                }
            }

            if (useSimd && Vector128.IsHardwareAccelerated)
            {
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

                    TOperator.PrepareDirect(result0, result1, preShift, round, roundOffset)
                        .StoreUnsafe(ref destinationRow, (nuint)column);

                    TOperator.PrepareDirect(result2, result3, preShift, round, roundOffset)
                        .StoreUnsafe(ref destinationRow, (nuint)(column + Vector128<ushort>.Count));
                }
            }

            for (; column < width; column++)
            {
                int result = ConvolveScalar(
                    ref Unsafe.Add(ref sourceRow, column),
                    tapStride,
                    ref coefficientBase,
                    tapCount);

                Unsafe.Add(ref destinationRow, column) = TOperator.PrepareDirect(result, preShift, round, roundOffset);
            }
        }
    }

    /// <summary>
    /// Applies one high-bit-depth compound convolution direction through one closed conversion operator.
    /// </summary>
    /// <typeparam name="TOperator">The compound-prediction conversion operator.</typeparam>
    private static void FilterCompoundDirect<TOperator>(
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
        int preShift,
        int round,
        int roundOffset,
        bool useSimd)
        where TOperator : struct, IAv1CompoundPredictionOperator
    {
        ref ushort sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short coefficientBase = ref MemoryMarshal.GetReference(coefficients);

        for (int row = 0; row < height; row++)
        {
            ref ushort sourceRowUnsigned = ref Unsafe.Add(ref sourceBase, (row * sourceStride) + sourceOffset);
            ref short sourceRow = ref Unsafe.As<ushort, short>(ref sourceRowUnsigned);
            ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int column = 0;

            if (useSimd && Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector512<ushort>.Count;
                for (; column <= vectorEnd; column += Vector512<ushort>.Count)
                {
                    Convolve(
                        ref sourceRow,
                        tapStride,
                        (nuint)column,
                        ref coefficientBase,
                        tapCount,
                        Vector512<int>.Zero,
                        out Vector512<int> lower,
                        out Vector512<int> upper);

                    TOperator.PrepareDirect(lower, upper, preShift, round, roundOffset)
                        .StoreUnsafe(ref destinationRow, (nuint)column);
                }
            }

            if (useSimd && Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector256<ushort>.Count;
                for (; column <= vectorEnd; column += Vector256<ushort>.Count)
                {
                    Convolve(
                        ref sourceRow,
                        tapStride,
                        (nuint)column,
                        ref coefficientBase,
                        tapCount,
                        Vector256<int>.Zero,
                        out Vector256<int> lower,
                        out Vector256<int> upper);

                    TOperator.PrepareDirect(lower, upper, preShift, round, roundOffset)
                        .StoreUnsafe(ref destinationRow, (nuint)column);
                }
            }

            if (useSimd && Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector128<ushort>.Count;
                for (; column <= vectorEnd; column += Vector128<ushort>.Count)
                {
                    Convolve(
                        ref sourceRow,
                        tapStride,
                        (nuint)column,
                        ref coefficientBase,
                        tapCount,
                        Vector128<int>.Zero,
                        out Vector128<int> lower,
                        out Vector128<int> upper);

                    TOperator.PrepareDirect(lower, upper, preShift, round, roundOffset)
                        .StoreUnsafe(ref destinationRow, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                int result = ConvolveScalar(
                    ref Unsafe.Add(ref sourceRowUnsigned, column),
                    tapStride,
                    ref coefficientBase,
                    tapCount);

                Unsafe.Add(ref destinationRow, column) = TOperator.PrepareDirect(result, preShift, round, roundOffset);
            }
        }
    }

    /// <summary>
    /// Applies separable compound convolution through caller-owned signed scratch.
    /// </summary>
    /// <typeparam name="TOperator">The compound-prediction conversion operator.</typeparam>
    private static void FilterCompound2D<TOperator>(
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
        Span<short> scratch,
        bool useSimd)
        where TOperator : struct, IAv1CompoundPredictionOperator
    {
        ref byte sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short scratchBase = ref MemoryMarshal.GetReference(scratch);
        ref short horizontalCoefficientBase = ref MemoryMarshal.GetReference(horizontalCoefficients);
        ref short verticalCoefficientBase = ref MemoryMarshal.GetReference(verticalCoefficients);
        int scratchStride = Math.Max(width, MinimumScratchStride);
        int intermediateHeight = height + verticalTapCount - 1;

        // The horizontal pass retains Q7 precision in signed scratch. Each SIMD stage continues at the shared
        // column offset so mixed-width rows need no padding stores and never cross the logical block edge.
        for (int row = 0; row < intermediateHeight; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(
                ref sourceBase,
                ((row + verticalSourceOffset) * sourceStride) + horizontalSourceOffset);

            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            int column = 0;

            if (useSimd && Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector512<byte>.Count;
                for (; column <= vectorEnd; column += Vector512<byte>.Count)
                {
                    Convolve(
                        ref sourceRow,
                        1,
                        (nuint)column,
                        ref horizontalCoefficientBase,
                        horizontalTapCount,
                        Vector512<int>.Zero,
                        out Vector512<int> result0,
                        out Vector512<int> result1,
                        out Vector512<int> result2,
                        out Vector512<int> result3);

                    TOperator.PrepareHorizontal(result0, result1).StoreUnsafe(ref scratchRow, (nuint)column);
                    TOperator.PrepareHorizontal(result2, result3)
                        .StoreUnsafe(ref scratchRow, (nuint)(column + Vector512<short>.Count));
                }
            }

            if (useSimd && Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector256<byte>.Count;
                for (; column <= vectorEnd; column += Vector256<byte>.Count)
                {
                    Convolve(
                        ref sourceRow,
                        1,
                        (nuint)column,
                        ref horizontalCoefficientBase,
                        horizontalTapCount,
                        Vector256<int>.Zero,
                        out Vector256<int> result0,
                        out Vector256<int> result1,
                        out Vector256<int> result2,
                        out Vector256<int> result3);

                    TOperator.PrepareHorizontal(result0, result1).StoreUnsafe(ref scratchRow, (nuint)column);
                    TOperator.PrepareHorizontal(result2, result3)
                        .StoreUnsafe(ref scratchRow, (nuint)(column + Vector256<short>.Count));
                }
            }

            if (useSimd && Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector128<byte>.Count;
                for (; column <= vectorEnd; column += Vector128<byte>.Count)
                {
                    Convolve(
                        ref sourceRow,
                        1,
                        (nuint)column,
                        ref horizontalCoefficientBase,
                        horizontalTapCount,
                        Vector128<int>.Zero,
                        out Vector128<int> result0,
                        out Vector128<int> result1,
                        out Vector128<int> result2,
                        out Vector128<int> result3);

                    TOperator.PrepareHorizontal(result0, result1).StoreUnsafe(ref scratchRow, (nuint)column);
                    TOperator.PrepareHorizontal(result2, result3)
                        .StoreUnsafe(ref scratchRow, (nuint)(column + Vector128<short>.Count));
                }
            }

            for (; column < width; column++)
            {
                int result = ConvolveScalar(
                    ref Unsafe.Add(ref sourceRow, column),
                    1,
                    ref horizontalCoefficientBase,
                    horizontalTapCount);

                Unsafe.Add(ref scratchRow, column) = TOperator.PrepareHorizontal(result);
            }
        }

        for (int row = 0; row < height; row++)
        {
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int column = 0;

            if (useSimd && Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector512<short>.Count;
                for (; column <= vectorEnd; column += Vector512<short>.Count)
                {
                    Convolve(
                        ref scratchRow,
                        scratchStride,
                        (nuint)column,
                        ref verticalCoefficientBase,
                        verticalTapCount,
                        Vector512<int>.Zero,
                        out Vector512<int> lower,
                        out Vector512<int> upper);

                    TOperator.PrepareVertical(lower, upper).StoreUnsafe(ref destinationRow, (nuint)column);
                }
            }

            if (useSimd && Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector256<short>.Count;
                for (; column <= vectorEnd; column += Vector256<short>.Count)
                {
                    Convolve(
                        ref scratchRow,
                        scratchStride,
                        (nuint)column,
                        ref verticalCoefficientBase,
                        verticalTapCount,
                        Vector256<int>.Zero,
                        out Vector256<int> lower,
                        out Vector256<int> upper);

                    TOperator.PrepareVertical(lower, upper).StoreUnsafe(ref destinationRow, (nuint)column);
                }
            }

            if (useSimd && Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector128<short>.Count;
                for (; column <= vectorEnd; column += Vector128<short>.Count)
                {
                    Convolve(
                        ref scratchRow,
                        scratchStride,
                        (nuint)column,
                        ref verticalCoefficientBase,
                        verticalTapCount,
                        Vector128<int>.Zero,
                        out Vector128<int> lower,
                        out Vector128<int> upper);

                    TOperator.PrepareVertical(lower, upper).StoreUnsafe(ref destinationRow, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                int result = ConvolveScalar(
                    ref Unsafe.Add(ref scratchRow, column),
                    scratchStride,
                    ref verticalCoefficientBase,
                    verticalTapCount);

                Unsafe.Add(ref destinationRow, column) = TOperator.PrepareVertical(result);
            }
        }
    }

    /// <summary>
    /// Applies separable high-bit-depth compound convolution through caller-owned signed scratch.
    /// </summary>
    /// <typeparam name="TOperator">The compound-prediction conversion operator.</typeparam>
    private static void FilterCompound2D<TOperator>(
        ReadOnlySpan<ushort> source,
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
        int bitDepth,
        int round0,
        Span<short> scratch,
        bool useSimd)
        where TOperator : struct, IAv1CompoundPredictionOperator
    {
        ref ushort sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short scratchBase = ref MemoryMarshal.GetReference(scratch);
        ref short horizontalCoefficientBase = ref MemoryMarshal.GetReference(horizontalCoefficients);
        ref short verticalCoefficientBase = ref MemoryMarshal.GetReference(verticalCoefficients);
        int scratchStride = Math.Max(width, MinimumScratchStride);
        int intermediateHeight = height + verticalTapCount - 1;
        int horizontalBias = 1 << (bitDepth + FilterBits - 1);
        int verticalBias = 1 << (bitDepth + (2 * FilterBits) - round0);

        // High-bit-depth input is still below short.MaxValue. Reinterpreting the source lets the shared signed
        // widening kernels apply negative filter coefficients without copying or allocating a conversion buffer.
        for (int row = 0; row < intermediateHeight; row++)
        {
            ref ushort sourceRowUnsigned = ref Unsafe.Add(
                ref sourceBase,
                ((row + verticalSourceOffset) * sourceStride) + horizontalSourceOffset);

            ref short sourceRow = ref Unsafe.As<ushort, short>(ref sourceRowUnsigned);
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            int column = 0;

            if (useSimd && Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector512<ushort>.Count;
                for (; column <= vectorEnd; column += Vector512<ushort>.Count)
                {
                    Convolve(
                        ref sourceRow,
                        1,
                        (nuint)column,
                        ref horizontalCoefficientBase,
                        horizontalTapCount,
                        Vector512<int>.Zero,
                        out Vector512<int> lower,
                        out Vector512<int> upper);

                    TOperator.PrepareHighBitDepthHorizontal(lower, upper, horizontalBias, round0)
                        .StoreUnsafe(ref scratchRow, (nuint)column);
                }
            }

            if (useSimd && Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector256<ushort>.Count;
                for (; column <= vectorEnd; column += Vector256<ushort>.Count)
                {
                    Convolve(
                        ref sourceRow,
                        1,
                        (nuint)column,
                        ref horizontalCoefficientBase,
                        horizontalTapCount,
                        Vector256<int>.Zero,
                        out Vector256<int> lower,
                        out Vector256<int> upper);

                    TOperator.PrepareHighBitDepthHorizontal(lower, upper, horizontalBias, round0)
                        .StoreUnsafe(ref scratchRow, (nuint)column);
                }
            }

            if (useSimd && Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector128<ushort>.Count;
                for (; column <= vectorEnd; column += Vector128<ushort>.Count)
                {
                    Convolve(
                        ref sourceRow,
                        1,
                        (nuint)column,
                        ref horizontalCoefficientBase,
                        horizontalTapCount,
                        Vector128<int>.Zero,
                        out Vector128<int> lower,
                        out Vector128<int> upper);

                    TOperator.PrepareHighBitDepthHorizontal(lower, upper, horizontalBias, round0)
                        .StoreUnsafe(ref scratchRow, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                int result = ConvolveScalar(
                    ref Unsafe.Add(ref sourceRowUnsigned, column),
                    1,
                    ref horizontalCoefficientBase,
                    horizontalTapCount);

                Unsafe.Add(ref scratchRow, column) =
                    TOperator.PrepareHighBitDepthHorizontal(result, horizontalBias, round0);
            }
        }

        for (int row = 0; row < height; row++)
        {
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int column = 0;

            if (useSimd && Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector512<short>.Count;
                for (; column <= vectorEnd; column += Vector512<short>.Count)
                {
                    Convolve(
                        ref scratchRow,
                        scratchStride,
                        (nuint)column,
                        ref verticalCoefficientBase,
                        verticalTapCount,
                        Vector512<int>.Zero,
                        out Vector512<int> lower,
                        out Vector512<int> upper);

                    TOperator.PrepareHighBitDepthVertical(lower, upper, verticalBias)
                        .StoreUnsafe(ref destinationRow, (nuint)column);
                }
            }

            if (useSimd && Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector256<short>.Count;
                for (; column <= vectorEnd; column += Vector256<short>.Count)
                {
                    Convolve(
                        ref scratchRow,
                        scratchStride,
                        (nuint)column,
                        ref verticalCoefficientBase,
                        verticalTapCount,
                        Vector256<int>.Zero,
                        out Vector256<int> lower,
                        out Vector256<int> upper);

                    TOperator.PrepareHighBitDepthVertical(lower, upper, verticalBias)
                        .StoreUnsafe(ref destinationRow, (nuint)column);
                }
            }

            if (useSimd && Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector128<short>.Count;
                for (; column <= vectorEnd; column += Vector128<short>.Count)
                {
                    Convolve(
                        ref scratchRow,
                        scratchStride,
                        (nuint)column,
                        ref verticalCoefficientBase,
                        verticalTapCount,
                        Vector128<int>.Zero,
                        out Vector128<int> lower,
                        out Vector128<int> upper);

                    TOperator.PrepareHighBitDepthVertical(lower, upper, verticalBias)
                        .StoreUnsafe(ref destinationRow, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                int result = ConvolveScalar(
                    ref Unsafe.Add(ref scratchRow, column),
                    scratchStride,
                    ref verticalCoefficientBase,
                    verticalTapCount);

                Unsafe.Add(ref destinationRow, column) =
                    TOperator.PrepareHighBitDepthVertical(result, verticalBias);
            }
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
