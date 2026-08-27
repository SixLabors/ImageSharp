// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Defines interpolation operators and the generic traversal used by translational inter prediction.
/// </content>
internal static partial class Av1InterPredictor
{
    /// <summary>
    /// Supplies the normative Q7 coefficient kernel for one AV1 interpolation-filter family.
    /// </summary>
    /// <remarks>
    /// The closed operator type lets the JIT inline table selection into each horizontal and vertical filter pair.
    /// Width reduction is selected once per block dimension rather than inside the sample loops.
    /// </remarks>
    internal interface IAv1InterPredictorOperator
    {
        /// <summary>
        /// Gets the eight coefficients for a one-sixteenth-sample phase.
        /// </summary>
        /// <param name="phase">The fractional phase in the inclusive range zero through fifteen.</param>
        /// <param name="useReducedFilter">Indicates whether the coded block dimension is at most four samples.</param>
        /// <returns>The Q7 coefficients in increasing source-sample order.</returns>
        public static abstract ReadOnlySpan<short> GetCoefficients(int phase, bool useReducedFilter);
    }

    /// <summary>
    /// Selects an 8-bit vertical interpolation operator for a closed horizontal operator.
    /// </summary>
    /// <typeparam name="THorizontal">The horizontal filter family.</typeparam>
    private static void DispatchVertical<THorizontal>(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        Av1InterpolationFilter verticalFilter,
        int horizontalPhase,
        int verticalPhase,
        Span<short> scratch,
        bool scalarOnly)
        where THorizontal : struct, IAv1InterPredictorOperator
    {
        switch (verticalFilter)
        {
            case Av1InterpolationFilter.Regular:
                Predict<THorizontal, RegularOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    horizontalPhase,
                    verticalPhase,
                    scratch,
                    scalarOnly);

                break;
            case Av1InterpolationFilter.Smooth:
                Predict<THorizontal, SmoothOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    horizontalPhase,
                    verticalPhase,
                    scratch,
                    scalarOnly);

                break;
            case Av1InterpolationFilter.Sharp:
                Predict<THorizontal, SharpOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    horizontalPhase,
                    verticalPhase,
                    scratch,
                    scalarOnly);

                break;
            default:
                Predict<THorizontal, BilinearOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    horizontalPhase,
                    verticalPhase,
                    scratch,
                    scalarOnly);

                break;
        }
    }

    /// <summary>
    /// Selects a high-bit-depth vertical interpolation operator for a closed horizontal operator.
    /// </summary>
    /// <typeparam name="THorizontal">The horizontal filter family.</typeparam>
    private static void DispatchVertical<THorizontal>(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        Av1InterpolationFilter verticalFilter,
        int horizontalPhase,
        int verticalPhase,
        int bitDepth,
        Span<short> scratch,
        bool scalarOnly)
        where THorizontal : struct, IAv1InterPredictorOperator
    {
        switch (verticalFilter)
        {
            case Av1InterpolationFilter.Regular:
                Predict<THorizontal, RegularOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    horizontalPhase,
                    verticalPhase,
                    bitDepth,
                    scratch,
                    scalarOnly);

                break;
            case Av1InterpolationFilter.Smooth:
                Predict<THorizontal, SmoothOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    horizontalPhase,
                    verticalPhase,
                    bitDepth,
                    scratch,
                    scalarOnly);

                break;
            case Av1InterpolationFilter.Sharp:
                Predict<THorizontal, SharpOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    horizontalPhase,
                    verticalPhase,
                    bitDepth,
                    scratch,
                    scalarOnly);

                break;
            default:
                Predict<THorizontal, BilinearOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    horizontalPhase,
                    verticalPhase,
                    bitDepth,
                    scratch,
                    scalarOnly);

                break;
        }
    }

    /// <summary>
    /// Executes one closed 8-bit interpolation-filter pair.
    /// </summary>
    /// <typeparam name="THorizontal">The horizontal filter family.</typeparam>
    /// <typeparam name="TVertical">The vertical filter family.</typeparam>
    private static void Predict<THorizontal, TVertical>(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        int horizontalPhase,
        int verticalPhase,
        Span<short> scratch,
        bool scalarOnly)
        where THorizontal : struct, IAv1InterPredictorOperator
        where TVertical : struct, IAv1InterPredictorOperator
    {
        if (horizontalPhase == 0 && verticalPhase == 0)
        {
            Copy(source, sourceStride, sourceOrigin, destination, destinationStride, width, height, scalarOnly);
            return;
        }

        if (verticalPhase == 0)
        {
            ReadOnlySpan<short> coefficients = THorizontal.GetCoefficients(horizontalPhase, width <= 4);
            GetEffectiveKernel(coefficients, out int firstCoefficient, out int tapCount);

            FilterDirect(
                source,
                sourceStride,
                sourceOrigin,
                destination,
                destinationStride,
                width,
                height,
                coefficients[firstCoefficient..],
                tapCount,
                firstCoefficient - 3,
                1,
                Round0Bits,
                FilterBits - Round0Bits,
                scalarOnly);

            return;
        }

        if (horizontalPhase == 0)
        {
            ReadOnlySpan<short> coefficients = TVertical.GetCoefficients(verticalPhase, height <= 4);
            GetEffectiveKernel(coefficients, out int firstCoefficient, out int tapCount);

            FilterDirect(
                source,
                sourceStride,
                sourceOrigin,
                destination,
                destinationStride,
                width,
                height,
                coefficients[firstCoefficient..],
                tapCount,
                (firstCoefficient - 3) * sourceStride,
                sourceStride,
                FilterBits,
                0,
                scalarOnly);

            return;
        }

        ReadOnlySpan<short> horizontalCoefficients = THorizontal.GetCoefficients(horizontalPhase, width <= 4);
        ReadOnlySpan<short> verticalCoefficients = TVertical.GetCoefficients(verticalPhase, height <= 4);
        GetEffectiveKernel(horizontalCoefficients, out int firstHorizontalCoefficient, out int horizontalTapCount);
        GetEffectiveKernel(verticalCoefficients, out int firstVerticalCoefficient, out int verticalTapCount);

        Filter2D(
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
            8,
            scratch,
            scalarOnly);
    }

    /// <summary>
    /// Executes one closed high-bit-depth interpolation-filter pair.
    /// </summary>
    /// <typeparam name="THorizontal">The horizontal filter family.</typeparam>
    /// <typeparam name="TVertical">The vertical filter family.</typeparam>
    private static void Predict<THorizontal, TVertical>(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        int horizontalPhase,
        int verticalPhase,
        int bitDepth,
        Span<short> scratch,
        bool scalarOnly)
        where THorizontal : struct, IAv1InterPredictorOperator
        where TVertical : struct, IAv1InterPredictorOperator
    {
        if (horizontalPhase == 0 && verticalPhase == 0)
        {
            Copy(source, sourceStride, sourceOrigin, destination, destinationStride, width, height, scalarOnly);
            return;
        }

        // Twelve-bit samples require two additional first-pass rounding bits to keep libaom's signed intermediate
        // within sixteen bits. The second pass gives those bits back, preserving a total Q14 shift.
        int intermediateRange = bitDepth + FilterBits - Round0Bits + 2;
        int round0 = Round0Bits + Math.Max(intermediateRange - 16, 0);

        if (verticalPhase == 0)
        {
            ReadOnlySpan<short> coefficients = THorizontal.GetCoefficients(horizontalPhase, width <= 4);
            GetEffectiveKernel(coefficients, out int firstCoefficient, out int tapCount);

            FilterDirect(
                source,
                sourceStride,
                sourceOrigin,
                destination,
                destinationStride,
                width,
                height,
                coefficients[firstCoefficient..],
                tapCount,
                firstCoefficient - 3,
                1,
                round0,
                FilterBits - round0,
                bitDepth,
                scalarOnly);

            return;
        }

        if (horizontalPhase == 0)
        {
            ReadOnlySpan<short> coefficients = TVertical.GetCoefficients(verticalPhase, height <= 4);
            GetEffectiveKernel(coefficients, out int firstCoefficient, out int tapCount);

            FilterDirect(
                source,
                sourceStride,
                sourceOrigin,
                destination,
                destinationStride,
                width,
                height,
                coefficients[firstCoefficient..],
                tapCount,
                (firstCoefficient - 3) * sourceStride,
                sourceStride,
                FilterBits,
                0,
                bitDepth,
                scalarOnly);

            return;
        }

        ReadOnlySpan<short> horizontalCoefficients = THorizontal.GetCoefficients(horizontalPhase, width <= 4);
        ReadOnlySpan<short> verticalCoefficients = TVertical.GetCoefficients(verticalPhase, height <= 4);
        GetEffectiveKernel(horizontalCoefficients, out int firstHorizontalCoefficient, out int horizontalTapCount);
        GetEffectiveKernel(verticalCoefficients, out int firstVerticalCoefficient, out int verticalTapCount);

        Filter2D(
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
            scalarOnly);
    }

    /// <summary>
    /// Finds the centered nonzero portion of an eight-position interpolation kernel.
    /// </summary>
    /// <param name="coefficients">The selected Q7 phase kernel.</param>
    /// <param name="firstCoefficient">Receives the first coefficient used by the effective kernel.</param>
    /// <param name="tapCount">Receives the effective two-, four-, six-, or eight-tap length.</param>
    private static void GetEffectiveKernel(ReadOnlySpan<short> coefficients, out int firstCoefficient, out int tapCount)
    {
        // This matches libaom's get_filter_tap decision. Reducing symmetric zero endpoints avoids source loads and
        // multiply-adds while retaining the original tap-to-source alignment through firstCoefficient.
        if (coefficients[0] != 0 || coefficients[7] != 0)
        {
            firstCoefficient = 0;
            tapCount = 8;
        }
        else if (coefficients[1] != 0 || coefficients[6] != 0)
        {
            firstCoefficient = 1;
            tapCount = 6;
        }
        else if (coefficients[2] != 0 || coefficients[5] != 0)
        {
            firstCoefficient = 2;
            tapCount = 4;
        }
        else
        {
            firstCoefficient = 3;
            tapCount = 2;
        }
    }

    /// <summary>
    /// Selects one eight-coefficient phase from a flattened interpolation table.
    /// </summary>
    /// <param name="table">The sixteen consecutive phase kernels.</param>
    /// <param name="phase">The selected one-sixteenth-sample phase.</param>
    /// <returns>The selected Q7 coefficient kernel.</returns>
    private static ReadOnlySpan<short> GetPhase(ReadOnlySpan<short> table, int phase) => table.Slice(phase * FilterCoefficientCount, FilterCoefficientCount);
}
