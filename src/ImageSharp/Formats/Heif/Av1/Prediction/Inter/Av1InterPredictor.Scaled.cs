// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Reconstructs reference-scaled inter prediction through variable-phase separable convolution.
/// </content>
internal static partial class Av1InterPredictor
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
        => DispatchScaled<byte, ScaledByteOperator>(
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
        => DispatchScaled<ushort, ScaledUInt16Operator>(
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
    private static void DispatchScaled<T, TSample>(
        ReadOnlySpan<T> source,
        int sourceStride,
        int sourceOrigin,
        Span<T> destination,
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
        where T : unmanaged
        where TSample : struct, IScaledSampleOperator<T>
    {
        switch (horizontalFilter)
        {
            case Av1InterpolationFilter.Regular:
                DispatchScaledVertical<T, TSample, RegularOperator>(
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
                DispatchScaledVertical<T, TSample, SmoothOperator>(
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
                DispatchScaledVertical<T, TSample, SharpOperator>(
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
                DispatchScaledVertical<T, TSample, BilinearOperator>(
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
    private static void DispatchScaledVertical<T, TSample, THorizontal>(
        ReadOnlySpan<T> source,
        int sourceStride,
        int sourceOrigin,
        Span<T> destination,
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
        where T : unmanaged
        where TSample : struct, IScaledSampleOperator<T>
        where THorizontal : struct, IAv1InterPredictorOperator
    {
        switch (verticalFilter)
        {
            case Av1InterpolationFilter.Regular:
                PredictScaled<T, TSample, THorizontal, RegularOperator>(
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
                PredictScaled<T, TSample, THorizontal, SmoothOperator>(
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
                PredictScaled<T, TSample, THorizontal, SharpOperator>(
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
                PredictScaled<T, TSample, THorizontal, BilinearOperator>(
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
    private static void PredictScaled<T, TSample, THorizontal, TVertical>(
        ReadOnlySpan<T> source,
        int sourceStride,
        int sourceOrigin,
        Span<T> destination,
        int destinationStride,
        int width,
        int height,
        int horizontalPhase,
        int horizontalStep,
        int verticalPhase,
        int verticalStep,
        int bitDepth,
        Span<short> scratch)
        where T : unmanaged
        where TSample : struct, IScaledSampleOperator<T>
        where THorizontal : struct, IAv1InterPredictorOperator
        where TVertical : struct, IAv1InterPredictorOperator
    {
        ref T sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref T destinationBase = ref MemoryMarshal.GetReference(destination);
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
            ref T sourceRow = ref Unsafe.Add(ref sourceBase, (row - 3) * sourceStride);
            ref short scratchRow = ref Unsafe.Add(ref scratchBase, row * scratchStride);
            int column = 0;
            if (Vector128.IsHardwareAccelerated)
            {
                for (; column <= width - Vector128<int>.Count; column += Vector128<int>.Count)
                {
                    int position0 = horizontalPhase + (column * horizontalStep);
                    int position1 = position0 + horizontalStep;
                    int position2 = position1 + horizontalStep;
                    int position3 = position2 + horizontalStep;
                    int source0 = (position0 >> Av1ReferenceScale.SubpixelBits) - 3;
                    int source1 = (position1 >> Av1ReferenceScale.SubpixelBits) - 3;
                    int source2 = (position2 >> Av1ReferenceScale.SubpixelBits) - 3;
                    int source3 = (position3 >> Av1ReferenceScale.SubpixelBits) - 3;
                    ReadOnlySpan<short> coefficients0 = THorizontal.GetCoefficients((position0 & Av1ReferenceScale.SubpixelMask) >> 6, useReducedHorizontalFilter);
                    ReadOnlySpan<short> coefficients1 = THorizontal.GetCoefficients((position1 & Av1ReferenceScale.SubpixelMask) >> 6, useReducedHorizontalFilter);
                    ReadOnlySpan<short> coefficients2 = THorizontal.GetCoefficients((position2 & Av1ReferenceScale.SubpixelMask) >> 6, useReducedHorizontalFilter);
                    ReadOnlySpan<short> coefficients3 = THorizontal.GetCoefficients((position3 & Av1ReferenceScale.SubpixelMask) >> 6, useReducedHorizontalFilter);
                    Vector128<int> result = Vector128.Create(horizontalBias);
                    for (int tap = 0; tap < FilterCoefficientCount; tap++)
                    {
                        Vector128<int> samples = Vector128.Create(
                            TSample.Load(ref sourceRow, source0 + tap),
                            TSample.Load(ref sourceRow, source1 + tap),
                            TSample.Load(ref sourceRow, source2 + tap),
                            TSample.Load(ref sourceRow, source3 + tap));

                        Vector128<int> coefficients = Vector128.Create(
                            (int)coefficients0[tap],
                            coefficients1[tap],
                            coefficients2[tap],
                            coefficients3[tap]);

                        result += samples * coefficients;
                    }

                    Vector64<short> intermediate = Av1IntraPredictorBase.Narrow(
                        RoundPowerOfTwo(result, round0),
                        Vector128<int>.Zero).GetLower();

                    intermediate.StoreUnsafe(ref scratchRow, (nuint)column);
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
                    sum += coefficients[tap] * TSample.Load(ref sourceRow, sourceColumn + tap);
                }

                Unsafe.Add(ref scratchRow, column) = (short)RoundPowerOfTwo(sum, round0);
            }
        }

        int round1 = (2 * FilterBits) - round0;
        int offsetBits = bitDepth + (2 * FilterBits) - round0;
        int verticalBias = 1 << offsetBits;
        int roundOffset = (1 << (offsetBits - round1)) + (1 << (offsetBits - round1 - 1));
        for (int row = 0; row < height; row++)
        {
            int position = verticalPhase + (row * verticalStep);
            int sourceRowIndex = position >> Av1ReferenceScale.SubpixelBits;
            ReadOnlySpan<short> coefficients = TVertical.GetCoefficients(
                (position & Av1ReferenceScale.SubpixelMask) >> 6,
                useReducedVerticalFilter);

            ref short scratchRow = ref Unsafe.Add(ref scratchBase, sourceRowIndex * scratchStride);
            ref short coefficientBase = ref MemoryMarshal.GetReference(coefficients);
            ref T destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int column = 0;
            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<int> initial = Vector128.Create(verticalBias);
                Vector128<int> offset = Vector128.Create(roundOffset);
                for (; column <= width - Vector128<short>.Count; column += Vector128<short>.Count)
                {
                    Convolve(
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
                    TSample.StoreVector(ref destinationRow, column, result0, result1, bitDepth);
                }
            }

            for (; column < width; column++)
            {
                int sum = verticalBias + ConvolveScalar(
                    ref Unsafe.Add(ref scratchRow, column),
                    scratchStride,
                    ref coefficientBase,
                    FilterCoefficientCount);

                TSample.StoreScalar(
                    ref destinationRow,
                    column,
                    RoundPowerOfTwo(sum, round1) - roundOffset,
                    bitDepth);
            }
        }
    }
}
