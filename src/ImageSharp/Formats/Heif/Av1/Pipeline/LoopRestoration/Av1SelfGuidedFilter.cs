// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <summary>
/// Applies the normative AV1 self-guided restoration filter and projection.
/// </summary>
internal static class Av1SelfGuidedFilter
{
    /// <summary>
    /// The number of source samples required on every side of a filtered processing unit.
    /// </summary>
    private const int Border = 3;

    /// <summary>
    /// The number of fractional bits retained by each self-guided filter result.
    /// </summary>
    private const int RestorationBits = 4;

    /// <summary>
    /// The number of fractional bits used by the decoded projection coefficients.
    /// </summary>
    private const int ProjectionBits = 7;

    /// <summary>
    /// The number of fractional bits used by the local self-guided blend factors.
    /// </summary>
    private const int SelfGuidedBits = 8;

    /// <summary>
    /// The number of fractional bits used by the self-guided scale table.
    /// </summary>
    private const int ScaleBits = 20;

    /// <summary>
    /// The number of fractional bits used by reciprocal window-area values.
    /// </summary>
    private const int ReciprocalBits = 12;

    /// <summary>
    /// The complete fixed-point self-guided blend range.
    /// </summary>
    private const int SelfGuidedScale = 1 << SelfGuidedBits;

    /// <summary>
    /// Gets the radii selected by each of the sixteen self-guided parameter sets.
    /// </summary>
    private static ReadOnlySpan<int> ParameterRadii =>
    [
        2, 1, 2, 1, 2, 1, 2, 1,
        2, 1, 2, 1, 2, 1, 2, 1,
        2, 1, 2, 1, 0, 1, 0, 1,
        0, 1, 0, 1, 2, 0, 2, 0,
    ];

    /// <summary>
    /// Gets the variance scales selected by each of the sixteen self-guided parameter sets.
    /// </summary>
    private static ReadOnlySpan<int> ParameterScales =>
    [
        140, 3236, 112, 2158, 93, 1618, 80, 1438,
        70, 1295, 58, 1177, 47, 1079, 37, 996,
        30, 925, 25, 863, -1, 2589, -1, 1618,
        -1, 1177, -1, 925, 56, -1, 22, -1,
    ];

    /// <summary>
    /// Gets the table mapping a bounded variance measure to its fixed-point local sample blend factor.
    /// </summary>
    private static ReadOnlySpan<ushort> XByXPlusOne =>
    [
        1, 128, 171, 192, 205, 213, 219, 224, 228, 230, 233, 235, 236, 238, 239,
        240, 241, 242, 243, 243, 244, 244, 245, 245, 246, 246, 247, 247, 247, 247,
        248, 248, 248, 248, 249, 249, 249, 249, 249, 250, 250, 250, 250, 250, 250,
        250, 251, 251, 251, 251, 251, 251, 251, 251, 251, 251, 252, 252, 252, 252,
        252, 252, 252, 252, 252, 252, 252, 252, 252, 252, 252, 252, 252, 253, 253,
        253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 253,
        253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 254, 254, 254,
        254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254,
        254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254,
        254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254,
        254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254,
        254, 254, 254, 254, 254, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
        255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
        255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
        255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
        255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
        255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
        256,
    ];

    /// <summary>
    /// Gets fixed-point reciprocals for every supported square-window area.
    /// </summary>
    private static ReadOnlySpan<ushort> OneByX =>
    [
        4096, 2048, 1365, 1024, 819, 683, 585, 512, 455, 410, 372, 341, 315,
        293, 273, 256, 241, 228, 216, 205, 195, 186, 178, 171, 164,
    ];

    /// <summary>
    /// Gets the number of integer samples required to filter one processing unit.
    /// </summary>
    /// <param name="width">The destination processing-unit width.</param>
    /// <param name="height">The destination processing-unit height.</param>
    /// <returns>The required scratch-span length.</returns>
    public static int GetScratchLength(int width, int height)
    {
        int filteredLength = width * height;
        int coefficientLength = GetCoefficientBufferLength(width, height);
        return (filteredLength * 2) + (coefficientLength * 2);
    }

    /// <summary>
    /// Filters one processing unit from a source rectangle containing the required three-sample borders.
    /// </summary>
    /// <param name="source">The source rectangle beginning three samples above and left of the processing unit.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="destination">The destination span beginning at the restored processing-unit origin.</param>
    /// <param name="destinationStride">The number of samples between destination rows.</param>
    /// <param name="width">The processing-unit width in plane samples.</param>
    /// <param name="height">The processing-unit height in plane samples.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    /// <param name="parameterSetIndex">The decoded self-guided parameter-set index.</param>
    /// <param name="projectionCoefficients">The two transmitted projection coefficients.</param>
    /// <param name="scratch">Integer storage sized according to <see cref="GetScratchLength"/>.</param>
    public static void FilterBlock(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        int bitDepth,
        int parameterSetIndex,
        ReadOnlySpan<int> projectionCoefficients,
        Span<int> scratch)
    {
        int filteredLength = width * height;
        Span<int> filtered0 = scratch[..filteredLength];
        Span<int> filtered1 = scratch.Slice(filteredLength, filteredLength);
        int coefficientLength = GetCoefficientBufferLength(width, height);
        Span<int> blendFactors = scratch.Slice(filteredLength * 2, coefficientLength);
        Span<int> localMeans = scratch.Slice((filteredLength * 2) + coefficientLength, coefficientLength);

        int parameterOffset = parameterSetIndex * 2;
        ReadOnlySpan<int> radii = ParameterRadii.Slice(parameterOffset, 2);
        ReadOnlySpan<int> scales = ParameterScales.Slice(parameterOffset, 2);
        if (radii[0] > 0)
        {
            CalculateIntermediateCoefficients(
                source,
                sourceStride,
                width,
                height,
                bitDepth,
                radii[0],
                scales[0],
                skipAlternateRows: true,
                blendFactors,
                localMeans);

            CalculateRadiusTwoFilter(source, sourceStride, width, height, blendFactors, localMeans, filtered0);
        }

        if (radii[1] > 0)
        {
            CalculateIntermediateCoefficients(
                source,
                sourceStride,
                width,
                height,
                bitDepth,
                radii[1],
                scales[1],
                skipAlternateRows: false,
                blendFactors,
                localMeans);

            CalculateRadiusOneFilter(source, sourceStride, width, height, blendFactors, localMeans, filtered1);
        }

        int projection0;
        int projection1;
        if (radii[0] == 0)
        {
            projection0 = 0;
            projection1 = (1 << ProjectionBits) - projectionCoefficients[1];
        }
        else if (radii[1] == 0)
        {
            projection0 = projectionCoefficients[0];
            projection1 = 0;
        }
        else
        {
            projection0 = projectionCoefficients[0];
            projection1 = (1 << ProjectionBits) - projection0 - projectionCoefficients[1];
        }

        int maximumSample = (1 << bitDepth) - 1;
        for (int row = 0; row < height; row++)
        {
            int sourceRowOffset = (row + Border) * sourceStride;
            int destinationRowOffset = row * destinationStride;
            int filteredRowOffset = row * width;
            for (int column = 0; column < width; column++)
            {
                int filteredOffset = filteredRowOffset + column;
                int unfiltered = source[sourceRowOffset + column + Border] << RestorationBits;
                int projected = unfiltered << ProjectionBits;
                if (radii[0] > 0)
                {
                    projected += projection0 * (filtered0[filteredOffset] - unfiltered);
                }

                if (radii[1] > 0)
                {
                    projected += projection1 * (filtered1[filteredOffset] - unfiltered);
                }

                destination[destinationRowOffset + column] =
                    (ushort)Av1Math.Clip3(
                        0,
                        maximumSample,
                        RoundPowerOfTwo(projected, ProjectionBits + RestorationBits));
            }
        }
    }

    /// <summary>
    /// Calculates the local blend factor and mean for the requested filter radius.
    /// </summary>
    /// <param name="source">The bordered processing-unit source rectangle.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="width">The processing-unit width in samples.</param>
    /// <param name="height">The processing-unit height in samples.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    /// <param name="radius">The square-window radius.</param>
    /// <param name="scale">The variance scale for the selected parameter set and radius.</param>
    /// <param name="skipAlternateRows">Whether only the rows consumed by the radius-two filter are calculated.</param>
    /// <param name="blendFactors">The destination buffer for local sample blend factors.</param>
    /// <param name="localMeans">The destination buffer for scaled local means.</param>
    private static void CalculateIntermediateCoefficients(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int width,
        int height,
        int bitDepth,
        int radius,
        int scale,
        bool skipAlternateRows,
        Span<int> blendFactors,
        Span<int> localMeans)
    {
        int bufferStride = width + 2;
        int bufferOrigin = bufferStride + 1;
        int windowDiameter = (radius * 2) + 1;
        int windowArea = windowDiameter * windowDiameter;
        int rowStep = skipAlternateRows ? 2 : 1;
        ReadOnlySpan<ushort> xByXPlusOne = XByXPlusOne;
        ReadOnlySpan<ushort> oneByX = OneByX;

        for (int row = -1; row < height + 1; row += rowStep)
        {
            int centerY = row + Border;
            int centerX = Border - 1;
            int sum = 0;
            int squareSum = 0;
            for (int windowY = centerY - radius; windowY <= centerY + radius; windowY++)
            {
                int sourceRowOffset = windowY * sourceStride;
                for (int windowX = centerX - radius; windowX <= centerX + radius; windowX++)
                {
                    int sample = source[sourceRowOffset + windowX];
                    sum += sample;
                    squareSum += sample * sample;
                }
            }

            for (int column = -1; column < width + 1; column++)
            {
                if (column > -1)
                {
                    int departingX = centerX - radius;
                    int arrivingX = centerX + radius + 1;
                    for (int windowY = centerY - radius; windowY <= centerY + radius; windowY++)
                    {
                        int sourceRowOffset = windowY * sourceStride;
                        int departingSample = source[sourceRowOffset + departingX];
                        int arrivingSample = source[sourceRowOffset + arrivingX];
                        sum += arrivingSample - departingSample;
                        squareSum += (arrivingSample * arrivingSample) - (departingSample * departingSample);
                    }

                    centerX++;
                }

                int normalizedSquareSum = RoundPowerOfTwo(squareSum, 2 * (bitDepth - 8));
                int normalizedSum = RoundPowerOfTwo(sum, bitDepth - 8);
                uint squareOfSum = (uint)normalizedSum * (uint)normalizedSum;
                uint scaledSquareSum = (uint)normalizedSquareSum * (uint)windowArea;

                // High-bit-depth normalization can round a nearly flat window's squared mean
                // above its mean square. AV1 saturates that rounding artefact to zero variance.
                uint variance = scaledSquareSum < squareOfSum ? 0 : scaledSquareSum - squareOfSum;
                uint varianceIndex = RoundPowerOfTwo(variance * (uint)scale, ScaleBits);
                int coefficientOffset = bufferOrigin + (row * bufferStride) + column;
                int blendFactor = xByXPlusOne[(int)Math.Min(varianceIndex, 255U)];
                blendFactors[coefficientOffset] = blendFactor;

                // The zero-variance table entry is deliberately one rather than zero. This keeps
                // the complementary factor below 256 and the scaled mean inside its proven range.
                uint meanProduct =
                    (uint)(SelfGuidedScale - blendFactor) * (uint)sum * oneByX[windowArea - 1];

                localMeans[coefficientOffset] = (int)RoundPowerOfTwo(meanProduct, ReciprocalBits);
            }
        }
    }

    /// <summary>
    /// Produces the radius-two filtered values from alternate coefficient rows.
    /// </summary>
    /// <param name="source">The bordered processing-unit source rectangle.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="width">The processing-unit width in samples.</param>
    /// <param name="height">The processing-unit height in samples.</param>
    /// <param name="blendFactors">The local sample blend factors.</param>
    /// <param name="localMeans">The scaled local means.</param>
    /// <param name="filtered">The destination fixed-point filtered values.</param>
    private static void CalculateRadiusTwoFilter(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int width,
        int height,
        ReadOnlySpan<int> blendFactors,
        ReadOnlySpan<int> localMeans,
        Span<int> filtered)
    {
        int bufferStride = width + 2;
        int bufferOrigin = bufferStride + 1;
        for (int row = 0; row < height; row++)
        {
            int sourceRowOffset = (row + Border) * sourceStride;
            int filteredRowOffset = row * width;
            for (int column = 0; column < width; column++)
            {
                int coefficientOffset = bufferOrigin + (row * bufferStride) + column;
                int blendFactor;
                int localMean;
                int roundingBits;
                if ((row & 1) == 0)
                {
                    blendFactor =
                        (6 * (blendFactors[coefficientOffset - bufferStride] + blendFactors[coefficientOffset + bufferStride]))
                        + (5 * (
                            blendFactors[coefficientOffset - bufferStride - 1]
                            + blendFactors[coefficientOffset - bufferStride + 1]
                            + blendFactors[coefficientOffset + bufferStride - 1]
                            + blendFactors[coefficientOffset + bufferStride + 1]));

                    localMean =
                        (6 * (localMeans[coefficientOffset - bufferStride] + localMeans[coefficientOffset + bufferStride]))
                        + (5 * (
                            localMeans[coefficientOffset - bufferStride - 1]
                            + localMeans[coefficientOffset - bufferStride + 1]
                            + localMeans[coefficientOffset + bufferStride - 1]
                            + localMeans[coefficientOffset + bufferStride + 1]));

                    roundingBits = SelfGuidedBits + 5 - RestorationBits;
                }
                else
                {
                    blendFactor =
                        (6 * blendFactors[coefficientOffset])
                        + (5 * (blendFactors[coefficientOffset - 1] + blendFactors[coefficientOffset + 1]));

                    localMean =
                        (6 * localMeans[coefficientOffset])
                        + (5 * (localMeans[coefficientOffset - 1] + localMeans[coefficientOffset + 1]));

                    roundingBits = SelfGuidedBits + 4 - RestorationBits;
                }

                int value =
                    (blendFactor * source[sourceRowOffset + column + Border]) + localMean;

                filtered[filteredRowOffset + column] = RoundPowerOfTwo(value, roundingBits);
            }
        }
    }

    /// <summary>
    /// Produces the radius-one filtered values from the complete coefficient grid.
    /// </summary>
    /// <param name="source">The bordered processing-unit source rectangle.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="width">The processing-unit width in samples.</param>
    /// <param name="height">The processing-unit height in samples.</param>
    /// <param name="blendFactors">The local sample blend factors.</param>
    /// <param name="localMeans">The scaled local means.</param>
    /// <param name="filtered">The destination fixed-point filtered values.</param>
    private static void CalculateRadiusOneFilter(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int width,
        int height,
        ReadOnlySpan<int> blendFactors,
        ReadOnlySpan<int> localMeans,
        Span<int> filtered)
    {
        int bufferStride = width + 2;
        int bufferOrigin = bufferStride + 1;
        int roundingBits = SelfGuidedBits + 5 - RestorationBits;
        for (int row = 0; row < height; row++)
        {
            int sourceRowOffset = (row + Border) * sourceStride;
            int filteredRowOffset = row * width;
            for (int column = 0; column < width; column++)
            {
                int coefficientOffset = bufferOrigin + (row * bufferStride) + column;
                int blendFactor =
                    (4 * (
                        blendFactors[coefficientOffset]
                        + blendFactors[coefficientOffset - 1]
                        + blendFactors[coefficientOffset + 1]
                        + blendFactors[coefficientOffset - bufferStride]
                        + blendFactors[coefficientOffset + bufferStride]))
                    + (3 * (
                        blendFactors[coefficientOffset - bufferStride - 1]
                        + blendFactors[coefficientOffset - bufferStride + 1]
                        + blendFactors[coefficientOffset + bufferStride - 1]
                        + blendFactors[coefficientOffset + bufferStride + 1]));

                int localMean =
                    (4 * (
                        localMeans[coefficientOffset]
                        + localMeans[coefficientOffset - 1]
                        + localMeans[coefficientOffset + 1]
                        + localMeans[coefficientOffset - bufferStride]
                        + localMeans[coefficientOffset + bufferStride]))
                    + (3 * (
                        localMeans[coefficientOffset - bufferStride - 1]
                        + localMeans[coefficientOffset - bufferStride + 1]
                        + localMeans[coefficientOffset + bufferStride - 1]
                        + localMeans[coefficientOffset + bufferStride + 1]));

                int value =
                    (blendFactor * source[sourceRowOffset + column + Border]) + localMean;

                filtered[filteredRowOffset + column] = RoundPowerOfTwo(value, roundingBits);
            }
        }
    }

    /// <summary>
    /// Gets the number of entries in one bordered intermediate-coefficient buffer.
    /// </summary>
    /// <param name="width">The processing-unit width.</param>
    /// <param name="height">The processing-unit height.</param>
    /// <returns>The number of required integer entries.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetCoefficientBufferLength(int width, int height) => (width + 2) * (height + 2);

    /// <summary>
    /// Rounds a signed fixed-point value to the requested lower precision.
    /// </summary>
    /// <param name="value">The signed fixed-point value.</param>
    /// <param name="bitCount">The number of low bits to discard.</param>
    /// <returns>The rounded signed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundPowerOfTwo(int value, int bitCount)
        => bitCount == 0 ? value : (value + (1 << (bitCount - 1))) >> bitCount;

    /// <summary>
    /// Rounds an unsigned fixed-point value to the requested lower precision.
    /// </summary>
    /// <param name="value">The unsigned fixed-point value.</param>
    /// <param name="bitCount">The number of low bits to discard.</param>
    /// <returns>The rounded unsigned value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RoundPowerOfTwo(uint value, int bitCount)
        => bitCount == 0 ? value : (value + (1U << (bitCount - 1))) >> bitCount;
}
