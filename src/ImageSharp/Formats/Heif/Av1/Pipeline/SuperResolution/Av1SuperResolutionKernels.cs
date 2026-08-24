// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.SuperResolution;

/// <summary>
/// Provides the normative horizontal filter operations used by AV1 super-resolution upscaling.
/// </summary>
internal static class Av1SuperResolutionKernels
{
    /// <summary>
    /// The number of source samples consumed for each output sample.
    /// </summary>
    public const int FilterTapCount = 8;

    /// <summary>
    /// The number of replicated source samples reserved at each row edge.
    /// </summary>
    public const int SourceBorder = (FilterTapCount / 2) + 1;

    /// <summary>
    /// The number of fractional bits represented by each filter coefficient.
    /// </summary>
    private const int FilterBits = 7;

    /// <summary>
    /// The number of filter phases in the normative table, expressed as a base-two exponent.
    /// </summary>
    private const int SubpixelBits = 6;

    /// <summary>
    /// The number of fractional bits used by the horizontal sample position.
    /// </summary>
    private const int ScaleSubpixelBits = 14;

    /// <summary>
    /// The mask selecting the fractional sample position.
    /// </summary>
    private const int ScaleSubpixelMask = (1 << ScaleSubpixelBits) - 1;

    /// <summary>
    /// The number of low position bits omitted when selecting a filter phase.
    /// </summary>
    private const int ScaleExtraBits = ScaleSubpixelBits - SubpixelBits;

    /// <summary>
    /// The half-step bias applied before filter-phase selection.
    /// </summary>
    private const int ScaleExtraOffset = 1 << (ScaleExtraBits - 1);

    /// <summary>
    /// The flattened 64-phase, 8-tap normative AV1 super-resolution filter table.
    /// </summary>
    private static readonly short[] Filters =
    [
        0, 0, 0, 128, 0, 0, 0, 0, 0, 0, -1, 128, 2, -1, 0, 0,
        0, 1, -3, 127, 4, -2, 1, 0, 0, 1, -4, 127, 6, -3, 1, 0,
        0, 2, -6, 126, 8, -3, 1, 0, 0, 2, -7, 125, 11, -4, 1, 0,
        -1, 2, -8, 125, 13, -5, 2, 0, -1, 3, -9, 124, 15, -6, 2, 0,
        -1, 3, -10, 123, 18, -6, 2, -1, -1, 3, -11, 122, 20, -7, 3, -1,
        -1, 4, -12, 121, 22, -8, 3, -1, -1, 4, -13, 120, 25, -9, 3, -1,
        -1, 4, -14, 118, 28, -9, 3, -1, -1, 4, -15, 117, 30, -10, 4, -1,
        -1, 5, -16, 116, 32, -11, 4, -1, -1, 5, -16, 114, 35, -12, 4, -1,
        -1, 5, -17, 112, 38, -12, 4, -1, -1, 5, -18, 111, 40, -13, 5, -1,
        -1, 5, -18, 109, 43, -14, 5, -1, -1, 6, -19, 107, 45, -14, 5, -1,
        -1, 6, -19, 105, 48, -15, 5, -1, -1, 6, -19, 103, 51, -16, 5, -1,
        -1, 6, -20, 101, 53, -16, 6, -1, -1, 6, -20, 99, 56, -17, 6, -1,
        -1, 6, -20, 97, 58, -17, 6, -1, -1, 6, -20, 95, 61, -18, 6, -1,
        -2, 7, -20, 93, 64, -18, 6, -2, -2, 7, -20, 91, 66, -19, 6, -1,
        -2, 7, -20, 88, 69, -19, 6, -1, -2, 7, -20, 86, 71, -19, 6, -1,
        -2, 7, -20, 84, 74, -20, 7, -2, -2, 7, -20, 81, 76, -20, 7, -1,
        -2, 7, -20, 79, 79, -20, 7, -2, -1, 7, -20, 76, 81, -20, 7, -2,
        -2, 7, -20, 74, 84, -20, 7, -2, -1, 6, -19, 71, 86, -20, 7, -2,
        -1, 6, -19, 69, 88, -20, 7, -2, -1, 6, -19, 66, 91, -20, 7, -2,
        -2, 6, -18, 64, 93, -20, 7, -2, -1, 6, -18, 61, 95, -20, 6, -1,
        -1, 6, -17, 58, 97, -20, 6, -1, -1, 6, -17, 56, 99, -20, 6, -1,
        -1, 6, -16, 53, 101, -20, 6, -1, -1, 5, -16, 51, 103, -19, 6, -1,
        -1, 5, -15, 48, 105, -19, 6, -1, -1, 5, -14, 45, 107, -19, 6, -1,
        -1, 5, -14, 43, 109, -18, 5, -1, -1, 5, -13, 40, 111, -18, 5, -1,
        -1, 4, -12, 38, 112, -17, 5, -1, -1, 4, -12, 35, 114, -16, 5, -1,
        -1, 4, -11, 32, 116, -16, 5, -1, -1, 4, -10, 30, 117, -15, 4, -1,
        -1, 3, -9, 28, 118, -14, 4, -1, -1, 3, -9, 25, 120, -13, 4, -1,
        -1, 3, -8, 22, 121, -12, 4, -1, -1, 3, -7, 20, 122, -11, 3, -1,
        -1, 2, -6, 18, 123, -10, 3, -1, 0, 2, -6, 15, 124, -9, 3, -1,
        0, 2, -5, 13, 125, -8, 2, -1, 0, 1, -4, 11, 125, -7, 2, 0,
        0, 1, -3, 8, 126, -6, 2, 0, 0, 1, -3, 6, 127, -4, 1, 0,
        0, 1, -2, 4, 127, -3, 1, 0, 0, 0, -1, 2, 128, -1, 0, 0
    ];

    /// <summary>
    /// Derives the fixed-point source-position increment for an output row.
    /// </summary>
    /// <param name="inputLength">The coded plane width.</param>
    /// <param name="outputLength">The upscaled plane width.</param>
    /// <returns>The source-position increment with 14 fractional bits.</returns>
    public static int GetConvolveStep(int inputLength, int outputLength)
        => ((inputLength << ScaleSubpixelBits) + (outputLength / 2)) / outputLength;

    /// <summary>
    /// Derives the initial fixed-point source position for an output row.
    /// </summary>
    /// <param name="inputLength">The coded plane width.</param>
    /// <param name="outputLength">The upscaled plane width.</param>
    /// <param name="step">The source-position increment returned by <see cref="GetConvolveStep"/>.</param>
    /// <returns>The initial source position with 14 fractional bits.</returns>
    public static int GetInitialSubpixel(int inputLength, int outputLength, int step)
    {
        int error = (outputLength * step) - (inputLength << ScaleSubpixelBits);
        int initial = (-((outputLength - inputLength) << (ScaleSubpixelBits - 1)) + (outputLength / 2)) /
            outputLength;

        initial += ScaleExtraOffset - (error / 2);
        return initial & ScaleSubpixelMask;
    }

    /// <summary>
    /// Upscales one replicated-edge source row with the normative AV1 filter.
    /// </summary>
    /// <param name="source">The coded row with <see cref="SourceBorder"/> replicated samples on each edge.</param>
    /// <param name="destination">The upscaled destination row.</param>
    /// <param name="step">The fixed-point source-position increment.</param>
    /// <param name="initialSubpixel">The initial fixed-point source position.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    public static void UpscaleRow(
        ReadOnlySpan<ushort> source,
        Span<ushort> destination,
        int step,
        int initialSubpixel,
        int bitDepth)
    {
        int maximum = (1 << bitDepth) - 1;
        int sourcePosition = initialSubpixel;
        for (int column = 0; column < destination.Length; column++)
        {
            int integerPosition = sourcePosition >> ScaleSubpixelBits;
            int filterPhase = (sourcePosition & ScaleSubpixelMask) >> ScaleExtraBits;
            int sourceOffset = SourceBorder + integerPosition - (FilterTapCount / 2);
            int filterOffset = filterPhase * FilterTapCount;
            int sum = DotProduct(source, sourceOffset, filterOffset);

            destination[column] = (ushort)Av1Math.Clip3(0, maximum, (sum + (1 << (FilterBits - 1))) >> FilterBits);
            sourcePosition += step;
        }
    }

    /// <summary>
    /// Computes the exact signed eight-tap filter product for one output sample.
    /// </summary>
    /// <param name="source">The replicated-edge source row.</param>
    /// <param name="sourceOffset">The first source sample consumed by the filter.</param>
    /// <param name="filterOffset">The first coefficient in the selected filter phase.</param>
    /// <returns>The unrounded filter sum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DotProduct(ReadOnlySpan<ushort> source, int sourceOffset, int filterOffset)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            ref ushort sourceReference = ref MemoryMarshal.GetReference(source);
            ref short filterReference = ref MemoryMarshal.GetArrayDataReference(Filters);
            Vector128<short> samples = Vector128.LoadUnsafe(ref sourceReference, (nuint)sourceOffset).AsInt16();
            Vector128<short> coefficients = Vector128.LoadUnsafe(ref filterReference, (nuint)filterOffset);
            Vector128<int> pairSums = Vector128_.MultiplyAddAdjacent(samples, coefficients);

            // Reuse ImageSharp's architecture-specific adjacent multiply/add and finish its four
            // 32-bit lanes scalarly; this remains cheaper than constructing a per-pixel shuffle tree.
            return pairSums.GetElement(0) + pairSums.GetElement(1) + pairSums.GetElement(2) + pairSums.GetElement(3);
        }

        int sum = 0;
        for (int tap = 0; tap < FilterTapCount; tap++)
        {
            sum += source[sourceOffset + tap] * Filters[filterOffset + tap];
        }

        return sum;
    }
}
