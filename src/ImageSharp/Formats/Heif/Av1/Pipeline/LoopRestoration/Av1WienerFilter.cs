// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <summary>
/// Applies the normative separable Wiener filter used by AV1 loop restoration.
/// </summary>
internal static class Av1WienerFilter
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
    public static void FilterStripe(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        int bitDepth,
        ReadOnlySpan<int> horizontalCoefficients,
        ReadOnlySpan<int> verticalCoefficients,
        Span<ushort> scratch)
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
        int horizontalBias = 1 << (bitDepth + FilterBits - 1);
        for (int row = 0; row < intermediateHeight; row++)
        {
            int sourceRowOffset = row * sourceStride;
            int intermediateRowOffset = row * width;
            for (int column = 0; column < width; column++)
            {
                int sourceOffset = sourceRowOffset + column;
                int sum = DotProduct(source, sourceOffset, horizontalFilter);

                // The transmitted center coefficient excludes its implicit 128 contribution.
                // Adding the unfiltered center sample here reconstructs the complete kernel.
                sum += (source[sourceOffset + TransmittedCoefficientCount] << FilterBits) + horizontalBias;
                int value = RoundPowerOfTwo(sum, horizontalRoundBits);
                scratch[intermediateRowOffset + column] = (ushort)Av1Math.Clip3(0, intermediateMaximum, value);
            }
        }

        int maximumSample = (1 << bitDepth) - 1;
        int verticalBias = 1 << (bitDepth + verticalRoundBits - 1);
        for (int row = 0; row < height; row++)
        {
            int destinationRowOffset = row * destinationStride;
            for (int column = 0; column < width; column++)
            {
                int sum = 0;
                for (int tap = 0; tap < FilterTapCount; tap++)
                {
                    sum += scratch[((row + tap) * width) + column] * verticalFilter[tap];
                }

                int center = scratch[((row + TransmittedCoefficientCount) * width) + column];
                sum += (center << FilterBits) - verticalBias;
                destination[destinationRowOffset + column] =
                    (ushort)Av1Math.Clip3(0, maximumSample, RoundPowerOfTwo(sum, verticalRoundBits));
            }
        }
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
        filter[3] = (short)(-2 * (outer + middle + inner));
        filter[4] = (short)inner;
        filter[5] = (short)middle;
        filter[6] = (short)outer;

        // libaom stores a seven-tap Wiener kernel in the shared eight-tap interpolation shape.
        filter[7] = 0;
    }

    /// <summary>
    /// Computes one signed eight-tap horizontal filter product.
    /// </summary>
    /// <param name="source">The source rectangle containing the requested samples.</param>
    /// <param name="sourceOffset">The first source sample consumed by the filter.</param>
    /// <param name="filter">The eight signed filter coefficients.</param>
    /// <returns>The unrounded signed filter sum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DotProduct(ReadOnlySpan<ushort> source, int sourceOffset, ReadOnlySpan<short> filter)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            ref ushort sourceReference = ref MemoryMarshal.GetReference(source);
            ref short filterReference = ref MemoryMarshal.GetReference(filter);
            Vector128<short> samples = Vector128.LoadUnsafe(ref sourceReference, (nuint)sourceOffset).AsInt16();
            Vector128<short> coefficients = Vector128.LoadUnsafe(ref filterReference);
            Vector128<int> pairSums = Vector128_.MultiplyAddAdjacent(samples, coefficients);

            // The shared helper provides the architecture-specific adjacent products; reducing its
            // four 32-bit lanes scalarly avoids an additional platform-specific shuffle sequence.
            return pairSums.GetElement(0) + pairSums.GetElement(1) + pairSums.GetElement(2) + pairSums.GetElement(3);
        }

        int sum = 0;
        for (int tap = 0; tap < FilterTapCount; tap++)
        {
            sum += source[sourceOffset + tap] * filter[tap];
        }

        return sum;
    }

    /// <summary>
    /// Rounds a signed fixed-point value to the requested lower precision.
    /// </summary>
    /// <param name="value">The signed fixed-point value.</param>
    /// <param name="bitCount">The number of low bits to discard.</param>
    /// <returns>The rounded signed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundPowerOfTwo(int value, int bitCount)
        => (value + (1 << (bitCount - 1))) >> bitCount;
}
