// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <summary>
/// Applies the normative separable Wiener filter used by AV1 loop restoration.
/// </summary>
internal static partial class Av1WienerFilter
{
    /// <summary>
    /// The number of coefficients in the padded Wiener convolution kernel.
    /// </summary>
    private const int FilterTapCount = 8;

    /// <summary>
    /// The number of independent symmetric coefficients transmitted for each filter direction.
    /// </summary>
    private const int TransmittedCoefficientCount = 3;

    /// <summary>
    /// The number of fractional bits in the Wiener filter coefficients.
    /// </summary>
    private const int FilterBits = 7;

    /// <summary>
    /// The default first-pass rounding shift.
    /// </summary>
    private const int InitialHorizontalRoundBits = 3;

    /// <summary>
    /// The number of intermediate rows required beyond the destination stripe height.
    /// </summary>
    private const int IntermediateRowExtension = FilterTapCount - 1;

    /// <summary>
    /// Gets the number of intermediate samples required to filter a stripe.
    /// </summary>
    /// <param name="width">The destination stripe width.</param>
    /// <param name="height">The destination stripe height.</param>
    /// <returns>The required scratch-span length.</returns>
    public static int GetScratchLength(int width, int height) => width * (height + IntermediateRowExtension);

    /// <summary>
    /// Filters one restoration stripe from a source rectangle containing the required three-sample borders.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
    /// <param name="source">The source rectangle beginning three samples above and left of the destination stripe.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="destination">The destination span beginning at the restored stripe origin.</param>
    /// <param name="destinationStride">The number of samples between destination rows.</param>
    /// <param name="width">The stripe width in plane samples.</param>
    /// <param name="height">The stripe height in plane samples.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    /// <param name="horizontalCoefficients">The three transmitted horizontal coefficients.</param>
    /// <param name="verticalCoefficients">The three transmitted vertical coefficients.</param>
    /// <param name="scratch">Intermediate sample storage sized according to <see cref="GetScratchLength"/>.</param>
    public static void FilterStripe<TSample>(
        ReadOnlySpan<TSample> source,
        int sourceStride,
        Span<TSample> destination,
        int destinationStride,
        int width,
        int height,
        int bitDepth,
        ReadOnlySpan<int> horizontalCoefficients,
        ReadOnlySpan<int> verticalCoefficients,
        Span<ushort> scratch)
        where TSample : unmanaged
    {
        Span<short> horizontalFilter = stackalloc short[FilterTapCount];
        Span<short> verticalFilter = stackalloc short[FilterTapCount];
        PopulateFilter(horizontalCoefficients, horizontalFilter);
        PopulateFilter(verticalCoefficients, verticalFilter);

        int horizontalRoundBits = InitialHorizontalRoundBits;
        int intermediateBitCount = bitDepth + FilterBits - horizontalRoundBits + 2;
        if (intermediateBitCount > 16)
        {
            // Twelve-bit input would otherwise exceed the unsigned 16-bit intermediate used by
            // the normative two-pass convolution, so AV1 transfers those excess bits to pass two.
            horizontalRoundBits += intermediateBitCount - 16;
        }

        int verticalRoundBits = (FilterBits * 2) - horizontalRoundBits;
        int intermediateMaximum = (1 << (bitDepth + 1 + FilterBits - horizontalRoundBits)) - 1;
        int intermediateHeight = height + IntermediateRowExtension;
        int horizontalBias = (1 << (bitDepth + FilterBits - 1)) + (1 << (horizontalRoundBits - 1));
        FilterRows<TSample, ushort, WienerOperator>(
            source,
            sourceStride,
            scratch,
            width,
            width,
            intermediateHeight,
            1,
            horizontalFilter,
            horizontalBias,
            horizontalRoundBits,
            intermediateMaximum);

        // The first pass adds a positive bias before clipping to its intermediate precision. Remove that
        // bias only after the vertical convolution; clipping or subtracting it earlier changes edge samples.
        int maximumSample = (1 << bitDepth) - 1;
        int verticalBias = (1 << (verticalRoundBits - 1)) - (1 << (bitDepth + verticalRoundBits - 1));
        FilterRows<ushort, TSample, WienerOperator>(
            scratch,
            width,
            destination,
            destinationStride,
            width,
            height,
            width,
            verticalFilter,
            verticalBias,
            verticalRoundBits,
            maximumSample);
    }

    /// <summary>
    /// Expands three transmitted symmetric coefficients into the padded eight-tap convolution kernel.
    /// </summary>
    /// <param name="coefficients">The transmitted outer-to-inner coefficients.</param>
    /// <param name="filter">The destination eight-tap kernel.</param>
    private static void PopulateFilter(ReadOnlySpan<int> coefficients, Span<short> filter)
    {
        int outer = coefficients[0];
        int middle = coefficients[1];
        int inner = coefficients[2];
        filter[0] = (short)outer;
        filter[1] = (short)middle;
        filter[2] = (short)inner;

        // Including the implicit center weight makes both passes the same seven-tap operation.
        // The full kernel sums to 128; the caller supplies each pass's distinct offset and rounding.
        filter[TransmittedCoefficientCount] = (short)((1 << FilterBits) - (2 * (outer + middle + inner)));
        filter[4] = (short)inner;
        filter[5] = (short)middle;
        filter[6] = (short)outer;

        // The eighth interpolation slot contributes no sample to this seven-tap kernel.
        filter[7] = 0;
    }
}
