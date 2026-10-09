// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1TranslationalInterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Reconstructs reference-scaled inter prediction through variable-phase separable convolution.
/// </content>
internal static partial class Av1ScaledInterPredictor
{
    /// <summary>
    /// Gets the scratch capacity required by one scaled prediction block.
    /// </summary>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="verticalPhase">The Q10 vertical position of the first output row, relative to the origin row.</param>
    /// <param name="verticalStep">The Q10 vertical distance between consecutive output rows.</param>
    /// <returns>The number of signed 16-bit elements.</returns>
    public static int GetScaledScratchLength(int width, int height, int verticalPhase, int verticalStep)
    {
        // The intermediate rows cover every source row from the first to the last output row, plus the eight-tap support.
        int intermediateHeight = ((((height - 1) * verticalStep) + verticalPhase) >> Av1ReferenceScale.SubpixelBits) + FilterCoefficientCount;
        return Math.Max(width, Vector128<short>.Count) * intermediateHeight;
    }

    /// <summary>
    /// Gets an upper bound of the scratch capacity of one scaled prediction block from its dimensions only.
    /// </summary>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <returns>The number of signed 16-bit elements.</returns>
    public static int GetMaximumScaledScratchLength(int width, int height)
        => Math.Max(width, Vector128<short>.Count) * ((height * 2) + FilterCoefficientCount);

    /// <summary>
    /// Reconstructs an 8-bit scaled prediction with variable source positions and phases.
    /// </summary>
    /// <param name="source">The complete padded reference plane.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position origin sample within <paramref name="source"/>.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
    /// <param name="verticalFilter">The vertical interpolation filter.</param>
    /// <param name="horizontalPhase">The Q10 horizontal position of the first output column, relative to the origin sample.</param>
    /// <param name="horizontalStep">The Q10 horizontal distance between consecutive output columns.</param>
    /// <param name="verticalPhase">The Q10 vertical position of the first output row, relative to the origin row.</param>
    /// <param name="verticalStep">The Q10 vertical distance between consecutive output rows.</param>
    /// <param name="scratch">Signed intermediate storage of at least <see cref="GetScaledScratchLength"/> elements.</param>
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
    /// Reconstructs an 8-, 10-, or 12-bit scaled prediction with variable source positions and phases.
    /// </summary>
    /// <param name="source">The complete padded reference plane.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position origin sample within <paramref name="source"/>.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
    /// <param name="verticalFilter">The vertical interpolation filter.</param>
    /// <param name="horizontalPhase">The Q10 horizontal position of the first output column, relative to the origin sample.</param>
    /// <param name="horizontalStep">The Q10 horizontal distance between consecutive output columns.</param>
    /// <param name="verticalPhase">The Q10 vertical position of the first output row, relative to the origin row.</param>
    /// <param name="verticalStep">The Q10 vertical distance between consecutive output rows.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="scratch">Signed intermediate storage of at least <see cref="GetScaledScratchLength"/> elements.</param>
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
    /// <param name="source">The complete padded reference plane.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position origin sample within <paramref name="source"/>.</param>
    /// <param name="destination">The compound intermediate destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
    /// <param name="verticalFilter">The vertical interpolation filter.</param>
    /// <param name="horizontalPhase">The Q10 horizontal position of the first output column, relative to the origin sample.</param>
    /// <param name="horizontalStep">The Q10 horizontal distance between consecutive output columns.</param>
    /// <param name="verticalPhase">The Q10 vertical position of the first output row, relative to the origin row.</param>
    /// <param name="verticalStep">The Q10 vertical distance between consecutive output rows.</param>
    /// <param name="scratch">Signed intermediate storage of at least <see cref="GetScaledScratchLength"/> elements.</param>
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
    /// <param name="source">The complete padded reference plane.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position origin sample within <paramref name="source"/>.</param>
    /// <param name="destination">The compound intermediate destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
    /// <param name="verticalFilter">The vertical interpolation filter.</param>
    /// <param name="horizontalPhase">The Q10 horizontal position of the first output column, relative to the origin sample.</param>
    /// <param name="horizontalStep">The Q10 horizontal distance between consecutive output columns.</param>
    /// <param name="verticalPhase">The Q10 vertical position of the first output row, relative to the origin row.</param>
    /// <param name="verticalStep">The Q10 vertical distance between consecutive output rows.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="scratch">Signed intermediate storage of at least <see cref="GetScaledScratchLength"/> elements.</param>
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
    /// <typeparam name="TSource">The source sample type.</typeparam>
    /// <typeparam name="TDestination">The destination sample type.</typeparam>
    /// <typeparam name="TOperator">The operator that selects the single-reference or the compound output domain.</typeparam>
    /// <param name="source">The complete padded reference plane.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position origin sample within <paramref name="source"/>.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
    /// <param name="verticalFilter">The vertical interpolation filter.</param>
    /// <param name="horizontalPhase">The Q10 horizontal position of the first output column, relative to the origin sample.</param>
    /// <param name="horizontalStep">The Q10 horizontal distance between consecutive output columns.</param>
    /// <param name="verticalPhase">The Q10 vertical position of the first output row, relative to the origin row.</param>
    /// <param name="verticalStep">The Q10 vertical distance between consecutive output rows.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="scratch">Signed intermediate storage of at least <see cref="GetScaledScratchLength"/> elements.</param>
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
    /// <typeparam name="TSource">The source sample type.</typeparam>
    /// <typeparam name="TDestination">The destination sample type.</typeparam>
    /// <typeparam name="TOperator">The operator that selects the single-reference or the compound output domain.</typeparam>
    /// <typeparam name="THorizontal">The horizontal filter family.</typeparam>
    /// <param name="source">The complete padded reference plane.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position origin sample within <paramref name="source"/>.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="verticalFilter">The vertical interpolation filter.</param>
    /// <param name="horizontalPhase">The Q10 horizontal position of the first output column, relative to the origin sample.</param>
    /// <param name="horizontalStep">The Q10 horizontal distance between consecutive output columns.</param>
    /// <param name="verticalPhase">The Q10 vertical position of the first output row, relative to the origin row.</param>
    /// <param name="verticalStep">The Q10 vertical distance between consecutive output rows.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="scratch">Signed intermediate storage of at least <see cref="GetScaledScratchLength"/> elements.</param>
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
    /// <typeparam name="TSource">The source sample type.</typeparam>
    /// <typeparam name="TDestination">The destination sample type.</typeparam>
    /// <typeparam name="TOperator">The operator that selects the single-reference or the compound output domain.</typeparam>
    /// <typeparam name="THorizontal">The horizontal filter family.</typeparam>
    /// <typeparam name="TVertical">The vertical filter family.</typeparam>
    /// <param name="source">The complete padded reference plane.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position origin sample within <paramref name="source"/>.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="horizontalPhase">The Q10 horizontal position of the first output column, relative to the origin sample.</param>
    /// <param name="horizontalStep">The Q10 horizontal distance between consecutive output columns.</param>
    /// <param name="verticalPhase">The Q10 vertical position of the first output row, relative to the origin row.</param>
    /// <param name="verticalStep">The Q10 vertical distance between consecutive output rows.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="scratch">Signed intermediate storage of at least <see cref="GetScaledScratchLength"/> elements.</param>
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

        // In scaled prediction, each output column has its own integer source sample and filter phase.
        // The vector kernels gather these positions four lanes at a time into one multiply-add chain, without an allocated index map.
        // Intermediate row 0 is three rows above the origin row, at the first tap of an eight-tap vertical filter.
        for (int row = 0; row < intermediateHeight; row++)
        {
            ref TSource sourceRow = ref Unsafe.Add(ref sourceBase, (row - 3) * sourceStride);
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            int column = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                nuint vectorCount = Numerics.Vector512Count<int>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector512<int>.Count)
                {
                    Vector512<int> result = FilterScaledHorizontalVector512<TSource, THorizontal>(
                        ref sourceRow,
                        horizontalPhase,
                        horizontalStep,
                        column,
                        useReducedHorizontalFilter,
                        horizontalBias,
                        round0);

                    // The narrow packs the 32-bit results into the lower half of a 16-bit vector. Only that lower half is stored.
                    Av1NonDirectionalIntraPredictorBase.Narrow(result, Vector512<int>.Zero)
                        .GetLower()
                        .StoreUnsafe(ref scratchRow, (nuint)column);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint vectorCount = Numerics.Vector256Count<int>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector256<int>.Count)
                {
                    Vector256<int> result = FilterScaledHorizontalVector256<TSource, THorizontal>(
                        ref sourceRow,
                        horizontalPhase,
                        horizontalStep,
                        column,
                        useReducedHorizontalFilter,
                        horizontalBias,
                        round0);

                    Av1NonDirectionalIntraPredictorBase.Narrow(result, Vector256<int>.Zero)
                        .GetLower()
                        .StoreUnsafe(ref scratchRow, (nuint)column);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint vectorCount = Numerics.Vector128Count<int>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector128<int>.Count)
                {
                    Vector128<int> result = FilterScaledHorizontalVector128<TSource, THorizontal>(
                        ref sourceRow,
                        horizontalPhase,
                        horizontalStep,
                        column,
                        useReducedHorizontalFilter,
                        horizontalBias,
                        round0);

                    Av1NonDirectionalIntraPredictorBase.Narrow(result, Vector128<int>.Zero)
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

        // The operator selects the vertical shift and the offset to remove: the full rounding to samples, or the compound intermediate precision.
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
                nuint vectorCount = Numerics.Vector512Count<int>(width - column) / 2;

                if (vectorCount > 0)
                {
                    // Each step of the vertical kernel writes two vectors of results.
                    // Thus the constants are created only if at least two complete vectors remain in the row.
                    Vector512<int> initial = Vector512.Create(verticalBias);
                    Vector512<int> offset = Vector512.Create(roundOffset);

                    for (; vectorCount > 0; vectorCount--, column += Vector512<int>.Count * 2)
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
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint vectorCount = Numerics.Vector256Count<int>(width - column) / 2;

                if (vectorCount > 0)
                {
                    // A remainder of one vector goes to the next narrower tier, so the 256-bit constants are not created for it.
                    Vector256<int> initial = Vector256.Create(verticalBias);
                    Vector256<int> offset = Vector256.Create(roundOffset);

                    for (; vectorCount > 0; vectorCount--, column += Vector256<int>.Count * 2)
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
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint vectorCount = Numerics.Vector128Count<int>(width - column) / 2;

                if (vectorCount > 0)
                {
                    // The 128-bit constants are also not created when fewer than eight output samples remain.
                    Vector128<int> initial = Vector128.Create(verticalBias);
                    Vector128<int> offset = Vector128.Create(roundOffset);

                    for (; vectorCount > 0; vectorCount--, column += Vector128<int>.Count * 2)
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
    /// <typeparam name="T">The source sample type.</typeparam>
    /// <typeparam name="TFilter">The horizontal filter family.</typeparam>
    /// <param name="source">The origin sample of the source row.</param>
    /// <param name="phase">The Q10 horizontal position of output column zero.</param>
    /// <param name="step">The Q10 horizontal distance between consecutive output columns.</param>
    /// <param name="column">The first output column of the lane group.</param>
    /// <param name="useReducedFilter">Whether the block uses the reduced filter of a small block dimension.</param>
    /// <param name="bias">The start value of each sum.</param>
    /// <param name="round">The rounding shift applied to each sum.</param>
    /// <returns>The rounded sums of the four output columns.</returns>
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
    /// <typeparam name="T">The source sample type.</typeparam>
    /// <typeparam name="TFilter">The horizontal filter family.</typeparam>
    /// <param name="source">The origin sample of the source row.</param>
    /// <param name="phase">The Q10 horizontal position of output column zero.</param>
    /// <param name="step">The Q10 horizontal distance between consecutive output columns.</param>
    /// <param name="column">The first output column of the lane group.</param>
    /// <param name="useReducedFilter">Whether the block uses the reduced filter of a small block dimension.</param>
    /// <param name="bias">The start value of each sum.</param>
    /// <param name="round">The rounding shift applied to each sum.</param>
    /// <returns>The rounded sums of the eight output columns.</returns>
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
    /// <typeparam name="T">The source sample type.</typeparam>
    /// <typeparam name="TFilter">The horizontal filter family.</typeparam>
    /// <param name="source">The origin sample of the source row.</param>
    /// <param name="phase">The Q10 horizontal position of output column zero.</param>
    /// <param name="step">The Q10 horizontal distance between consecutive output columns.</param>
    /// <param name="column">The first output column of the lane group.</param>
    /// <param name="useReducedFilter">Whether the block uses the reduced filter of a small block dimension.</param>
    /// <param name="bias">The start value of each sum.</param>
    /// <param name="round">The rounding shift applied to each sum.</param>
    /// <returns>The rounded sums of the sixteen output columns.</returns>
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
    /// <typeparam name="T">The source sample type.</typeparam>
    /// <param name="source">The origin sample of the source row.</param>
    /// <param name="phase">The Q10 horizontal position of output column zero.</param>
    /// <param name="step">The Q10 horizontal distance between consecutive output columns.</param>
    /// <param name="column">The first of the four output columns.</param>
    /// <param name="tap">The filter tap.</param>
    /// <returns>The widened source samples, one for each output column.</returns>
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
    /// <typeparam name="TFilter">The horizontal filter family.</typeparam>
    /// <param name="phase">The Q10 horizontal position of output column zero.</param>
    /// <param name="step">The Q10 horizontal distance between consecutive output columns.</param>
    /// <param name="column">The first of the four output columns.</param>
    /// <param name="tap">The filter tap.</param>
    /// <param name="useReducedFilter">Whether the block uses the reduced filter of a small block dimension.</param>
    /// <returns>The coefficients, one for each output column.</returns>
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
    /// <typeparam name="T">The source sample type.</typeparam>
    /// <param name="source">The origin sample of the source row.</param>
    /// <param name="phase">The Q10 horizontal position of output column zero.</param>
    /// <param name="step">The Q10 horizontal distance between consecutive output columns.</param>
    /// <param name="column">The output column.</param>
    /// <param name="tap">The filter tap.</param>
    /// <returns>The widened source sample.</returns>
    private static int LoadScaledSample<T>(ref T source, int phase, int step, int column, int tap)
        where T : unmanaged
    {
        // The integer part of the Q10 position is the sample under tap 3, so tap 0 reads three samples to the left.
        int position = phase + (column * step);
        int sourceColumn = (position >> Av1ReferenceScale.SubpixelBits) - 3;
        return NativeOperator.Load(ref source, sourceColumn + tap);
    }

    /// <summary>
    /// Loads one variable-phase horizontal filter coefficient.
    /// </summary>
    /// <typeparam name="TFilter">The horizontal filter family.</typeparam>
    /// <param name="phase">The Q10 horizontal position of output column zero.</param>
    /// <param name="step">The Q10 horizontal distance between consecutive output columns.</param>
    /// <param name="column">The output column.</param>
    /// <param name="tap">The filter tap.</param>
    /// <param name="useReducedFilter">Whether the block uses the reduced filter of a small block dimension.</param>
    /// <returns>The Q7 coefficient.</returns>
    private static int LoadScaledCoefficient<TFilter>(int phase, int step, int column, int tap, bool useReducedFilter)
        where TFilter : struct, IAv1InterPredictorOperator
    {
        // The shift by 6 keeps the top four of the ten fraction bits, which is the one-sixteenth-sample filter phase.
        int position = phase + (column * step);
        return TFilter.GetCoefficients(
            (position & Av1ReferenceScale.SubpixelMask) >> 6,
            useReducedFilter)[tap];
    }
}
