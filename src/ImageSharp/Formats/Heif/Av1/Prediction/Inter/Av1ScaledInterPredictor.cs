// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1InterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Reconstructs reference-scaled inter prediction through variable-phase separable convolution.
/// </content>
internal static partial class Av1ScaledInterPredictor
{
    /// <summary>
    /// Gets the scratch capacity required by one scaled prediction block.
    /// </summary>
    public static int GetScaledScratchLength(int width, int height, int verticalPhase, int verticalStep)
    {
        int intermediateHeight = ((((height - 1) * verticalStep) + verticalPhase) >> Av1ReferenceScale.SubpixelBits) + FilterCoefficientCount;
        return Math.Max(width, Vector128<short>.Count) * intermediateHeight;
    }

    /// <summary>
    /// Gets a dimension-only upper bound for one scaled prediction block's scratch capacity.
    /// </summary>
    public static int GetMaximumScaledScratchLength(int width, int height)
        => Math.Max(width, Vector128<short>.Count) * ((height * 2) + FilterCoefficientCount);

    /// <summary>
    /// Reconstructs an 8-bit scaled prediction using variable source positions and phases.
    /// </summary>
    public static void PredictScaled(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        Av1InterpolationFilter horizontalFilter,
        Av1InterpolationFilter verticalFilter,
        int horizontalPhase,
        int horizontalStep,
        int verticalPhase,
        int verticalStep,
        Span<short> scratch)
        => DispatchScaled<byte, byte, NativeOperator>(
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
            horizontalStep,
            verticalPhase,
            verticalStep,
            8,
            scratch);

    /// <summary>
    /// Reconstructs an 8-, 10-, or 12-bit scaled prediction using variable source positions and phases.
    /// </summary>
    public static void PredictScaled(
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
        int horizontalStep,
        int verticalPhase,
        int verticalStep,
        int bitDepth,
        Span<short> scratch)
        => DispatchScaled<ushort, ushort, NativeOperator>(
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
            horizontalStep,
            verticalPhase,
            verticalStep,
            bitDepth,
            scratch);

    /// <summary>
    /// Reconstructs an 8-bit scaled predictor into the no-round compound intermediate domain.
    /// </summary>
    public static void PredictScaledCompound(
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
        int horizontalStep,
        int verticalPhase,
        int verticalStep,
        Span<short> scratch)
        => DispatchScaled<byte, ushort, CompoundOperator>(
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
            horizontalStep,
            verticalPhase,
            verticalStep,
            8,
            scratch);

    /// <summary>
    /// Reconstructs an 8-, 10-, or 12-bit scaled predictor into the no-round compound intermediate domain.
    /// </summary>
    public static void PredictScaledCompound(
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
        int horizontalStep,
        int verticalPhase,
        int verticalStep,
        int bitDepth,
        Span<short> scratch)
        => DispatchScaled<ushort, ushort, CompoundOperator>(
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
            horizontalStep,
            verticalPhase,
            verticalStep,
            bitDepth,
            scratch);

    /// <summary>
    /// Selects the horizontal filter family for scaled prediction.
    /// </summary>
    private static void DispatchScaled<TSource, TDestination, TOperator>(
        ReadOnlySpan<TSource> source,
        int sourceStride,
        int sourceOrigin,
        Span<TDestination> destination,
        int destinationStride,
        int width,
        int height,
        Av1InterpolationFilter horizontalFilter,
        Av1InterpolationFilter verticalFilter,
        int horizontalPhase,
        int horizontalStep,
        int verticalPhase,
        int verticalStep,
        int bitDepth,
        Span<short> scratch)
        where TSource : unmanaged
        where TDestination : unmanaged
        where TOperator : struct, IAv1ScaledPredictionOperator
    {
        switch (horizontalFilter)
        {
            case Av1InterpolationFilter.Regular:
                DispatchScaledVertical<TSource, TDestination, TOperator, RegularOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    verticalFilter,
                    horizontalPhase,
                    horizontalStep,
                    verticalPhase,
                    verticalStep,
                    bitDepth,
                    scratch);

                break;
            case Av1InterpolationFilter.Smooth:
                DispatchScaledVertical<TSource, TDestination, TOperator, SmoothOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    verticalFilter,
                    horizontalPhase,
                    horizontalStep,
                    verticalPhase,
                    verticalStep,
                    bitDepth,
                    scratch);

                break;
            case Av1InterpolationFilter.Sharp:
                DispatchScaledVertical<TSource, TDestination, TOperator, SharpOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    verticalFilter,
                    horizontalPhase,
                    horizontalStep,
                    verticalPhase,
                    verticalStep,
                    bitDepth,
                    scratch);

                break;
            default:
                DispatchScaledVertical<TSource, TDestination, TOperator, BilinearOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    verticalFilter,
                    horizontalPhase,
                    horizontalStep,
                    verticalPhase,
                    verticalStep,
                    bitDepth,
                    scratch);

                break;
        }
    }

    /// <summary>
    /// Selects the vertical filter family for a closed horizontal scaled-prediction operator.
    /// </summary>
    private static void DispatchScaledVertical<TSource, TDestination, TOperator, THorizontal>(
        ReadOnlySpan<TSource> source,
        int sourceStride,
        int sourceOrigin,
        Span<TDestination> destination,
        int destinationStride,
        int width,
        int height,
        Av1InterpolationFilter verticalFilter,
        int horizontalPhase,
        int horizontalStep,
        int verticalPhase,
        int verticalStep,
        int bitDepth,
        Span<short> scratch)
        where TSource : unmanaged
        where TDestination : unmanaged
        where TOperator : struct, IAv1ScaledPredictionOperator
        where THorizontal : struct, IAv1InterPredictorOperator
    {
        switch (verticalFilter)
        {
            case Av1InterpolationFilter.Regular:
                PredictScaled<TSource, TDestination, TOperator, THorizontal, RegularOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    horizontalPhase,
                    horizontalStep,
                    verticalPhase,
                    verticalStep,
                    bitDepth,
                    scratch);

                break;
            case Av1InterpolationFilter.Smooth:
                PredictScaled<TSource, TDestination, TOperator, THorizontal, SmoothOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    horizontalPhase,
                    horizontalStep,
                    verticalPhase,
                    verticalStep,
                    bitDepth,
                    scratch);

                break;
            case Av1InterpolationFilter.Sharp:
                PredictScaled<TSource, TDestination, TOperator, THorizontal, SharpOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    horizontalPhase,
                    horizontalStep,
                    verticalPhase,
                    verticalStep,
                    bitDepth,
                    scratch);

                break;
            default:
                PredictScaled<TSource, TDestination, TOperator, THorizontal, BilinearOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    horizontalPhase,
                    horizontalStep,
                    verticalPhase,
                    verticalStep,
                    bitDepth,
                    scratch);

                break;
        }
    }

    /// <summary>
    /// Applies variable-phase horizontal filtering followed by variable-phase vertical filtering.
    /// </summary>
    private static void PredictScaled<TSource, TDestination, TOperator, THorizontal, TVertical>(
        ReadOnlySpan<TSource> source,
        int sourceStride,
        int sourceOrigin,
        Span<TDestination> destination,
        int destinationStride,
        int width,
        int height,
        int horizontalPhase,
        int horizontalStep,
        int verticalPhase,
        int verticalStep,
        int bitDepth,
        Span<short> scratch)
        where TSource : unmanaged
        where TDestination : unmanaged
        where TOperator : struct, IAv1ScaledPredictionOperator
        where THorizontal : struct, IAv1InterPredictorOperator
        where TVertical : struct, IAv1InterPredictorOperator
    {
        ref TSource sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref TDestination destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short scratchBase = ref MemoryMarshal.GetReference(scratch);
        int scratchStride = Math.Max(width, Vector128<short>.Count);
        int intermediateHeight = ((((height - 1) * verticalStep) + verticalPhase) >> Av1ReferenceScale.SubpixelBits) + FilterCoefficientCount;
        int horizontalBias = 1 << (bitDepth + FilterBits - 1);
        int intermediateRange = bitDepth + FilterBits - Round0Bits + 2;
        int round0 = Round0Bits + Math.Max(intermediateRange - 16, 0);
        bool useReducedHorizontalFilter = width <= 4;
        bool useReducedVerticalFilter = height <= 4;

        // Scaled positions change both the integer source sample and filter phase at each output column. Four-lane
        // vectors gather those independent positions into one multiply-accumulate chain without allocating an index map.
        for (int row = 0; row < intermediateHeight; row++)
        {
            ref TSource sourceRow = ref Unsafe.Add(ref sourceBase, (row - 3) * sourceStride);
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            int column = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = width - Vector512<int>.Count;
                for (; column <= oneVectorFromEnd; column += Vector512<int>.Count)
                {
                    Vector512<int> result = FilterScaledHorizontalVector512<TSource, THorizontal>(
                        ref sourceRow,
                        horizontalPhase,
                        horizontalStep,
                        column,
                        useReducedHorizontalFilter,
                        horizontalBias,
                        round0);

                    Av1IntraPredictorBase.Narrow(result, Vector512<int>.Zero)
                        .GetLower()
                        .StoreUnsafe(ref scratchRow, (nuint)column);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = width - Vector256<int>.Count;
                for (; column <= oneVectorFromEnd; column += Vector256<int>.Count)
                {
                    Vector256<int> result = FilterScaledHorizontalVector256<TSource, THorizontal>(
                        ref sourceRow,
                        horizontalPhase,
                        horizontalStep,
                        column,
                        useReducedHorizontalFilter,
                        horizontalBias,
                        round0);

                    Av1IntraPredictorBase.Narrow(result, Vector256<int>.Zero)
                        .GetLower()
                        .StoreUnsafe(ref scratchRow, (nuint)column);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; column <= width - Vector128<int>.Count; column += Vector128<int>.Count)
                {
                    Vector128<int> result = FilterScaledHorizontalVector128<TSource, THorizontal>(
                        ref sourceRow,
                        horizontalPhase,
                        horizontalStep,
                        column,
                        useReducedHorizontalFilter,
                        horizontalBias,
                        round0);

                    Av1IntraPredictorBase.Narrow(result, Vector128<int>.Zero)
                        .GetLower()
                        .StoreUnsafe(ref scratchRow, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                int position = horizontalPhase + (column * horizontalStep);
                int sourceColumn = (position >> Av1ReferenceScale.SubpixelBits) - 3;
                ReadOnlySpan<short> coefficients = THorizontal.GetCoefficients(
                    (position & Av1ReferenceScale.SubpixelMask) >> 6,
                    useReducedHorizontalFilter);

                int sum = horizontalBias;
                for (int tap = 0; tap < FilterCoefficientCount; tap++)
                {
                    sum = NativeOperator.MultiplyAdd(sum, NativeOperator.Load(ref sourceRow, sourceColumn + tap), coefficients[tap]);
                }

                Unsafe.Add(ref scratchRow, column) = (short)RoundPowerOfTwo(sum, round0);
            }
        }

        int round1 = TOperator.GetVerticalRound(round0);
        int offsetBits = bitDepth + (2 * FilterBits) - round0;
        int verticalBias = 1 << offsetBits;
        int roundOffset = TOperator.GetRoundOffset(offsetBits, round1);
        for (int row = 0; row < height; row++)
        {
            int position = verticalPhase + (row * verticalStep);
            int sourceRowIndex = position >> Av1ReferenceScale.SubpixelBits;
            ReadOnlySpan<short> coefficients = TVertical.GetCoefficients(
                (position & Av1ReferenceScale.SubpixelMask) >> 6,
                useReducedVerticalFilter);

            ref short scratchRow = ref Unsafe.Add(ref scratchBase, sourceRowIndex * scratchStride);
            ref short coefficientBase = ref MemoryMarshal.GetReference(coefficients);
            ref TDestination destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int column = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                Vector512<int> initial = Vector512.Create(verticalBias);
                Vector512<int> offset = Vector512.Create(roundOffset);
                int oneVectorFromEnd = width - (Vector512<int>.Count * 2);
                for (; column <= oneVectorFromEnd; column += Vector512<int>.Count * 2)
                {
                    NativeOperator.Convolve(
                        ref scratchRow,
                        scratchStride,
                        (nuint)column,
                        ref coefficientBase,
                        FilterCoefficientCount,
                        initial,
                        out Vector512<int> result0,
                        out Vector512<int> result1);

                    result0 = RoundPowerOfTwo(result0, round1) - offset;
                    result1 = RoundPowerOfTwo(result1, round1) - offset;
                    TOperator.Store(ref destinationRow, column, result0, result1, bitDepth);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<int> initial = Vector256.Create(verticalBias);
                Vector256<int> offset = Vector256.Create(roundOffset);
                int oneVectorFromEnd = width - (Vector256<int>.Count * 2);
                for (; column <= oneVectorFromEnd; column += Vector256<int>.Count * 2)
                {
                    NativeOperator.Convolve(
                        ref scratchRow,
                        scratchStride,
                        (nuint)column,
                        ref coefficientBase,
                        FilterCoefficientCount,
                        initial,
                        out Vector256<int> result0,
                        out Vector256<int> result1);

                    result0 = RoundPowerOfTwo(result0, round1) - offset;
                    result1 = RoundPowerOfTwo(result1, round1) - offset;
                    TOperator.Store(ref destinationRow, column, result0, result1, bitDepth);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<int> initial = Vector128.Create(verticalBias);
                Vector128<int> offset = Vector128.Create(roundOffset);
                int oneVectorFromEnd = width - (Vector128<int>.Count * 2);
                for (; column <= oneVectorFromEnd; column += Vector128<int>.Count * 2)
                {
                    NativeOperator.Convolve(
                        ref scratchRow,
                        scratchStride,
                        (nuint)column,
                        ref coefficientBase,
                        FilterCoefficientCount,
                        initial,
                        out Vector128<int> result0,
                        out Vector128<int> result1);

                    result0 = RoundPowerOfTwo(result0, round1) - offset;
                    result1 = RoundPowerOfTwo(result1, round1) - offset;
                    TOperator.Store(ref destinationRow, column, result0, result1, bitDepth);
                }
            }

            for (; column < width; column++)
            {
                int sum = verticalBias + NativeOperator.Convolve(
                    ref Unsafe.Add(ref scratchRow, column),
                    scratchStride,
                    ref coefficientBase,
                    FilterCoefficientCount);

                TOperator.Store(
                    ref destinationRow,
                    column,
                    RoundPowerOfTwo(sum, round1) - roundOffset,
                    bitDepth);
            }
        }
    }

    /// <summary>
    /// Filters four independently positioned horizontal samples through a closed scaled-prediction operator.
    /// </summary>
    private static Vector128<int> FilterScaledHorizontalVector128<T, TFilter>(
        ref T source,
        int phase,
        int step,
        int column,
        bool useReducedFilter,
        int bias,
        int round)
        where T : unmanaged
        where TFilter : struct, IAv1InterPredictorOperator
    {
        Vector128<int> result = Vector128.Create(bias);
        for (int tap = 0; tap < FilterCoefficientCount; tap++)
        {
            result = NativeOperator.MultiplyAdd(
                result,
                LoadScaledSamplesVector128<T>(ref source, phase, step, column, tap),
                LoadScaledCoefficientsVector128<TFilter>(phase, step, column, tap, useReducedFilter));
        }

        return RoundPowerOfTwo(result, round);
    }

    /// <summary>
    /// Filters eight independently positioned horizontal samples through a closed scaled-prediction operator.
    /// </summary>
    private static Vector256<int> FilterScaledHorizontalVector256<T, TFilter>(
        ref T source,
        int phase,
        int step,
        int column,
        bool useReducedFilter,
        int bias,
        int round)
        where T : unmanaged
        where TFilter : struct, IAv1InterPredictorOperator
    {
        Vector256<int> result = Vector256.Create(bias);
        for (int tap = 0; tap < FilterCoefficientCount; tap++)
        {
            Vector256<int> samples = Vector256.Create(
                LoadScaledSamplesVector128<T>(ref source, phase, step, column, tap),
                LoadScaledSamplesVector128<T>(ref source, phase, step, column + Vector128<int>.Count, tap));

            Vector256<int> coefficients = Vector256.Create(
                LoadScaledCoefficientsVector128<TFilter>(phase, step, column, tap, useReducedFilter),
                LoadScaledCoefficientsVector128<TFilter>(phase, step, column + Vector128<int>.Count, tap, useReducedFilter));

            result = NativeOperator.MultiplyAdd(result, samples, coefficients);
        }

        return RoundPowerOfTwo(result, round);
    }

    /// <summary>
    /// Filters sixteen independently positioned horizontal samples through a closed scaled-prediction operator.
    /// </summary>
    private static Vector512<int> FilterScaledHorizontalVector512<T, TFilter>(
        ref T source,
        int phase,
        int step,
        int column,
        bool useReducedFilter,
        int bias,
        int round)
        where T : unmanaged
        where TFilter : struct, IAv1InterPredictorOperator
    {
        Vector512<int> result = Vector512.Create(bias);
        for (int tap = 0; tap < FilterCoefficientCount; tap++)
        {
            Vector256<int> sampleLower = Vector256.Create(
                LoadScaledSamplesVector128<T>(ref source, phase, step, column, tap),
                LoadScaledSamplesVector128<T>(ref source, phase, step, column + Vector128<int>.Count, tap));

            Vector256<int> sampleUpper = Vector256.Create(
                LoadScaledSamplesVector128<T>(ref source, phase, step, column + Vector256<int>.Count, tap),
                LoadScaledSamplesVector128<T>(ref source, phase, step, column + Vector256<int>.Count + Vector128<int>.Count, tap));

            Vector256<int> coefficientLower = Vector256.Create(
                LoadScaledCoefficientsVector128<TFilter>(phase, step, column, tap, useReducedFilter),
                LoadScaledCoefficientsVector128<TFilter>(phase, step, column + Vector128<int>.Count, tap, useReducedFilter));

            Vector256<int> coefficientUpper = Vector256.Create(
                LoadScaledCoefficientsVector128<TFilter>(phase, step, column + Vector256<int>.Count, tap, useReducedFilter),
                LoadScaledCoefficientsVector128<TFilter>(phase, step, column + Vector256<int>.Count + Vector128<int>.Count, tap, useReducedFilter));

            result = NativeOperator.MultiplyAdd(
                result,
                Vector512.Create(sampleLower, sampleUpper),
                Vector512.Create(coefficientLower, coefficientUpper));
        }

        return RoundPowerOfTwo(result, round);
    }

    /// <summary>
    /// Gathers four variable-position source samples for one horizontal filter tap.
    /// </summary>
    private static Vector128<int> LoadScaledSamplesVector128<T>(
        ref T source,
        int phase,
        int step,
        int column,
        int tap)
        where T : unmanaged
        => Vector128.Create(
            LoadScaledSample(ref source, phase, step, column, tap),
            LoadScaledSample(ref source, phase, step, column + 1, tap),
            LoadScaledSample(ref source, phase, step, column + 2, tap),
            LoadScaledSample(ref source, phase, step, column + 3, tap));

    /// <summary>
    /// Gathers four variable-phase coefficients for one horizontal filter tap.
    /// </summary>
    private static Vector128<int> LoadScaledCoefficientsVector128<TFilter>(
        int phase,
        int step,
        int column,
        int tap,
        bool useReducedFilter)
        where TFilter : struct, IAv1InterPredictorOperator
        => Vector128.Create(
            LoadScaledCoefficient<TFilter>(phase, step, column, tap, useReducedFilter),
            LoadScaledCoefficient<TFilter>(phase, step, column + 1, tap, useReducedFilter),
            LoadScaledCoefficient<TFilter>(phase, step, column + 2, tap, useReducedFilter),
            LoadScaledCoefficient<TFilter>(phase, step, column + 3, tap, useReducedFilter));

    /// <summary>
    /// Loads one variable-position source sample for a horizontal filter tap.
    /// </summary>
    private static int LoadScaledSample<T>(ref T source, int phase, int step, int column, int tap)
        where T : unmanaged
    {
        int position = phase + (column * step);
        int sourceColumn = (position >> Av1ReferenceScale.SubpixelBits) - 3;
        return NativeOperator.Load(ref source, sourceColumn + tap);
    }

    /// <summary>
    /// Loads one variable-phase horizontal filter coefficient.
    /// </summary>
    private static int LoadScaledCoefficient<TFilter>(int phase, int step, int column, int tap, bool useReducedFilter)
        where TFilter : struct, IAv1InterPredictorOperator
    {
        int position = phase + (column * step);
        return TFilter.GetCoefficients(
            (position & Av1ReferenceScale.SubpixelMask) >> 6,
            useReducedFilter)[tap];
    }
}
