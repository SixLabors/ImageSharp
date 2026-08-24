// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;

/// <summary>
/// Applies the scalar AV1 deblocking kernels to low- and high-bit-depth sample edges.
/// </summary>
internal static class Av1LoopFilterKernels
{
    /// <summary>
    /// The number of samples processed along one transform edge at a time.
    /// </summary>
    private const int EdgeLaneCount = 4;

    /// <summary>
    /// The number of samples read by the widest AV1 deblocking kernel.
    /// </summary>
    private const int MaximumKernelSampleCount = 14;

    /// <summary>
    /// Applies one low-bit-depth deblocking kernel to four adjacent samples along an edge.
    /// </summary>
    /// <param name="samples">The plane storage containing the edge and its neighboring samples.</param>
    /// <param name="q0Offset">The offset of the first sample on the Q side of the edge.</param>
    /// <param name="pixelStep">The storage distance between adjacent samples across the edge.</param>
    /// <param name="lineStep">The storage distance between adjacent samples along the edge.</param>
    /// <param name="filterLength">The signaled 4-, 6-, 8-, or 14-tap filter length.</param>
    /// <param name="limit">The threshold for adjacent samples on either side of the edge.</param>
    /// <param name="boundaryLimit">The threshold for the discontinuity across the edge.</param>
    /// <param name="highEdgeVarianceThreshold">The threshold that selects the narrow high-variance adjustment.</param>
    public static void FilterLowBitDepthEdge(
        Span<byte> samples,
        int q0Offset,
        int pixelStep,
        int lineStep,
        int filterLength,
        int limit,
        int boundaryLimit,
        int highEdgeVarianceThreshold)
    {
        int radius = GetFilterRadius(filterLength);
        Span<int> kernelSamples = stackalloc int[MaximumKernelSampleCount];

        for (int lane = 0; lane < EdgeLaneCount; lane++)
        {
            int laneOffset = q0Offset + (lane * lineStep);

            // Samples are centered in the scratch span as p6..p0,q0..q6. Loading only the selected radius
            // avoids touching pixels that a narrow filter is not permitted to address near a frame boundary.
            for (int distance = 1; distance <= radius; distance++)
            {
                kernelSamples[7 - distance] = samples[laneOffset - (distance * pixelStep)];
                kernelSamples[6 + distance] = samples[laneOffset + ((distance - 1) * pixelStep)];
            }

            FilterSamples(
                kernelSamples,
                filterLength,
                limit,
                boundaryLimit,
                highEdgeVarianceThreshold,
                8);

            for (int distance = 1; distance <= radius; distance++)
            {
                samples[laneOffset - (distance * pixelStep)] = (byte)kernelSamples[7 - distance];
                samples[laneOffset + ((distance - 1) * pixelStep)] = (byte)kernelSamples[6 + distance];
            }
        }
    }

    /// <summary>
    /// Applies one high-bit-depth deblocking kernel to four adjacent samples along an edge.
    /// </summary>
    /// <param name="samples">The plane storage containing the edge and its neighboring samples.</param>
    /// <param name="q0Offset">The offset of the first sample on the Q side of the edge.</param>
    /// <param name="pixelStep">The storage distance between adjacent samples across the edge.</param>
    /// <param name="lineStep">The storage distance between adjacent samples along the edge.</param>
    /// <param name="filterLength">The signaled 4-, 6-, 8-, or 14-tap filter length.</param>
    /// <param name="limit">The eight-bit-domain threshold for adjacent samples on either side of the edge.</param>
    /// <param name="boundaryLimit">The eight-bit-domain threshold for the discontinuity across the edge.</param>
    /// <param name="highEdgeVarianceThreshold">The eight-bit-domain threshold that selects the narrow adjustment.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    public static void FilterHighBitDepthEdge(
        Span<ushort> samples,
        int q0Offset,
        int pixelStep,
        int lineStep,
        int filterLength,
        int limit,
        int boundaryLimit,
        int highEdgeVarianceThreshold,
        int bitDepth)
    {
        int radius = GetFilterRadius(filterLength);
        Span<int> kernelSamples = stackalloc int[MaximumKernelSampleCount];

        for (int lane = 0; lane < EdgeLaneCount; lane++)
        {
            int laneOffset = q0Offset + (lane * lineStep);

            // AV1 scales thresholds rather than samples for high-bit-depth filtering, so the exact reconstructed
            // values are retained in the scratch span and shared with the low-bit-depth arithmetic below.
            for (int distance = 1; distance <= radius; distance++)
            {
                kernelSamples[7 - distance] = samples[laneOffset - (distance * pixelStep)];
                kernelSamples[6 + distance] = samples[laneOffset + ((distance - 1) * pixelStep)];
            }

            FilterSamples(
                kernelSamples,
                filterLength,
                limit,
                boundaryLimit,
                highEdgeVarianceThreshold,
                bitDepth);

            for (int distance = 1; distance <= radius; distance++)
            {
                samples[laneOffset - (distance * pixelStep)] = (ushort)kernelSamples[7 - distance];
                samples[laneOffset + ((distance - 1) * pixelStep)] = (ushort)kernelSamples[6 + distance];
            }
        }
    }

    /// <summary>
    /// Gets the number of samples read from each side of an edge for a filter length.
    /// </summary>
    /// <param name="filterLength">The AV1 filter length.</param>
    /// <returns>The sample radius on either side of the edge.</returns>
    private static int GetFilterRadius(int filterLength) => filterLength switch
    {
        4 => 2,
        6 => 3,
        8 => 4,
        14 => 7,
        _ => 0
    };

    /// <summary>
    /// Selects and applies the AV1 filter arithmetic for one edge lane.
    /// </summary>
    /// <param name="samples">The p6..p0,q0..q6 scratch samples.</param>
    /// <param name="filterLength">The AV1 filter length.</param>
    /// <param name="limit">The eight-bit-domain adjacent-sample threshold.</param>
    /// <param name="boundaryLimit">The eight-bit-domain edge-discontinuity threshold.</param>
    /// <param name="highEdgeVarianceThreshold">The eight-bit-domain high-edge-variance threshold.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    private static void FilterSamples(
        Span<int> samples,
        int filterLength,
        int limit,
        int boundaryLimit,
        int highEdgeVarianceThreshold,
        int bitDepth)
    {
        int thresholdScale = 1 << (bitDepth - 8);
        int scaledLimit = limit * thresholdScale;
        int scaledBoundaryLimit = boundaryLimit * thresholdScale;
        bool filterEnabled;
        bool flat;

        switch (filterLength)
        {
            case 4:
                filterEnabled = IsFilter2Enabled(samples, scaledLimit, scaledBoundaryLimit);
                Filter4(samples, filterEnabled, highEdgeVarianceThreshold * thresholdScale, bitDepth);
                break;
            case 6:
                filterEnabled = IsChromaFilterEnabled(samples, scaledLimit, scaledBoundaryLimit);
                flat = IsChromaFlat(samples, thresholdScale);
                Filter6(samples, filterEnabled, flat, highEdgeVarianceThreshold * thresholdScale, bitDepth);
                break;
            case 8:
                filterEnabled = IsFilterEnabled(samples, scaledLimit, scaledBoundaryLimit);
                flat = IsFlat(samples, thresholdScale);
                Filter8(samples, filterEnabled, flat, highEdgeVarianceThreshold * thresholdScale, bitDepth);
                break;
            case 14:
                filterEnabled = IsFilterEnabled(samples, scaledLimit, scaledBoundaryLimit);
                flat = IsFlat(samples, thresholdScale);
                bool outerFlat = IsOuterFlat(samples, thresholdScale);
                Filter14(samples, filterEnabled, flat, outerFlat, highEdgeVarianceThreshold * thresholdScale, bitDepth);
                break;
        }
    }

    /// <summary>
    /// Determines whether a 4-tap filter may modify an edge.
    /// </summary>
    /// <param name="samples">The p6..p0,q0..q6 scratch samples.</param>
    /// <param name="limit">The scaled adjacent-sample threshold.</param>
    /// <param name="boundaryLimit">The scaled edge-discontinuity threshold.</param>
    /// <returns><see langword="true"/> when the edge satisfies the AV1 filter mask.</returns>
    private static bool IsFilter2Enabled(ReadOnlySpan<int> samples, int limit, int boundaryLimit)
        => Math.Abs(samples[5] - samples[6]) <= limit &&
            Math.Abs(samples[8] - samples[7]) <= limit &&
            ((2 * Math.Abs(samples[6] - samples[7])) + (Math.Abs(samples[5] - samples[8]) / 2)) <= boundaryLimit;

    /// <summary>
    /// Determines whether an 8- or 14-tap luma filter may modify an edge.
    /// </summary>
    /// <param name="samples">The p6..p0,q0..q6 scratch samples.</param>
    /// <param name="limit">The scaled adjacent-sample threshold.</param>
    /// <param name="boundaryLimit">The scaled edge-discontinuity threshold.</param>
    /// <returns><see langword="true"/> when the edge satisfies the AV1 filter mask.</returns>
    private static bool IsFilterEnabled(ReadOnlySpan<int> samples, int limit, int boundaryLimit)
        => Math.Abs(samples[3] - samples[4]) <= limit &&
            Math.Abs(samples[4] - samples[5]) <= limit &&
            Math.Abs(samples[5] - samples[6]) <= limit &&
            Math.Abs(samples[8] - samples[7]) <= limit &&
            Math.Abs(samples[9] - samples[8]) <= limit &&
            Math.Abs(samples[10] - samples[9]) <= limit &&
            ((2 * Math.Abs(samples[6] - samples[7])) + (Math.Abs(samples[5] - samples[8]) / 2)) <= boundaryLimit;

    /// <summary>
    /// Determines whether a 6-tap chroma filter may modify an edge.
    /// </summary>
    /// <param name="samples">The p6..p0,q0..q6 scratch samples.</param>
    /// <param name="limit">The scaled adjacent-sample threshold.</param>
    /// <param name="boundaryLimit">The scaled edge-discontinuity threshold.</param>
    /// <returns><see langword="true"/> when the edge satisfies the AV1 chroma filter mask.</returns>
    private static bool IsChromaFilterEnabled(ReadOnlySpan<int> samples, int limit, int boundaryLimit)
        => Math.Abs(samples[4] - samples[5]) <= limit &&
            Math.Abs(samples[5] - samples[6]) <= limit &&
            Math.Abs(samples[8] - samples[7]) <= limit &&
            Math.Abs(samples[9] - samples[8]) <= limit &&
            ((2 * Math.Abs(samples[6] - samples[7])) + (Math.Abs(samples[5] - samples[8]) / 2)) <= boundaryLimit;

    /// <summary>
    /// Determines whether the inner four samples on each side form a flat edge.
    /// </summary>
    /// <param name="samples">The p6..p0,q0..q6 scratch samples.</param>
    /// <param name="threshold">The scaled flatness threshold.</param>
    /// <returns><see langword="true"/> when the samples satisfy the AV1 flatness mask.</returns>
    private static bool IsFlat(ReadOnlySpan<int> samples, int threshold)
        => Math.Abs(samples[5] - samples[6]) <= threshold &&
            Math.Abs(samples[8] - samples[7]) <= threshold &&
            Math.Abs(samples[4] - samples[6]) <= threshold &&
            Math.Abs(samples[9] - samples[7]) <= threshold &&
            Math.Abs(samples[3] - samples[6]) <= threshold &&
            Math.Abs(samples[10] - samples[7]) <= threshold;

    /// <summary>
    /// Determines whether the three chroma samples on each side form a flat edge.
    /// </summary>
    /// <param name="samples">The p6..p0,q0..q6 scratch samples.</param>
    /// <param name="threshold">The scaled flatness threshold.</param>
    /// <returns><see langword="true"/> when the samples satisfy the AV1 chroma flatness mask.</returns>
    private static bool IsChromaFlat(ReadOnlySpan<int> samples, int threshold)
        => Math.Abs(samples[5] - samples[6]) <= threshold &&
            Math.Abs(samples[8] - samples[7]) <= threshold &&
            Math.Abs(samples[4] - samples[6]) <= threshold &&
            Math.Abs(samples[9] - samples[7]) <= threshold;

    /// <summary>
    /// Determines whether the outer samples permit the 14-tap flat filter.
    /// </summary>
    /// <param name="samples">The p6..p0,q0..q6 scratch samples.</param>
    /// <param name="threshold">The scaled flatness threshold.</param>
    /// <returns><see langword="true"/> when the outer samples satisfy the AV1 flatness mask.</returns>
    private static bool IsOuterFlat(ReadOnlySpan<int> samples, int threshold)
        => Math.Abs(samples[1] - samples[6]) <= threshold &&
            Math.Abs(samples[11] - samples[7]) <= threshold &&
            Math.Abs(samples[2] - samples[6]) <= threshold &&
            Math.Abs(samples[12] - samples[7]) <= threshold &&
            Math.Abs(samples[0] - samples[6]) <= threshold &&
            Math.Abs(samples[13] - samples[7]) <= threshold;

    /// <summary>
    /// Applies the narrow signed-saturating AV1 edge adjustment.
    /// </summary>
    /// <param name="samples">The p6..p0,q0..q6 scratch samples.</param>
    /// <param name="filterEnabled">Whether the filter mask permits modification.</param>
    /// <param name="highEdgeVarianceThreshold">The scaled high-edge-variance threshold.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    private static void Filter4(Span<int> samples, bool filterEnabled, int highEdgeVarianceThreshold, int bitDepth)
    {
        if (!filterEnabled)
        {
            return;
        }

        int signedOffset = 128 << (bitDepth - 8);
        int signedMinimum = -signedOffset;
        int signedMaximum = signedOffset - 1;
        int p1 = samples[5] - signedOffset;
        int p0 = samples[6] - signedOffset;
        int q0 = samples[7] - signedOffset;
        int q1 = samples[8] - signedOffset;
        bool highEdgeVariance = Math.Abs(samples[5] - samples[6]) > highEdgeVarianceThreshold ||
            Math.Abs(samples[8] - samples[7]) > highEdgeVarianceThreshold;

        // The saturating signed-domain arithmetic is normative. Clipping only the final samples is not equivalent
        // because the intermediate delta can saturate before the asymmetric +4/+3 rounding is applied.
        int filter = highEdgeVariance ? Math.Clamp(p1 - q1, signedMinimum, signedMaximum) : 0;
        filter = Math.Clamp(filter + (3 * (q0 - p0)), signedMinimum, signedMaximum);

        int filter1 = Math.Clamp(filter + 4, signedMinimum, signedMaximum) >> 3;
        int filter2 = Math.Clamp(filter + 3, signedMinimum, signedMaximum) >> 3;
        samples[7] = Math.Clamp(q0 - filter1, signedMinimum, signedMaximum) + signedOffset;
        samples[6] = Math.Clamp(p0 + filter2, signedMinimum, signedMaximum) + signedOffset;

        int outerFilter = highEdgeVariance ? 0 : (filter1 + 1) >> 1;
        samples[8] = Math.Clamp(q1 - outerFilter, signedMinimum, signedMaximum) + signedOffset;
        samples[5] = Math.Clamp(p1 + outerFilter, signedMinimum, signedMaximum) + signedOffset;
    }

    /// <summary>
    /// Applies the AV1 6-tap chroma filter or its 4-tap fallback.
    /// </summary>
    /// <param name="samples">The p6..p0,q0..q6 scratch samples.</param>
    /// <param name="filterEnabled">Whether the filter mask permits modification.</param>
    /// <param name="flat">Whether the samples permit the wider flat filter.</param>
    /// <param name="highEdgeVarianceThreshold">The scaled high-edge-variance threshold.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    private static void Filter6(Span<int> samples, bool filterEnabled, bool flat, int highEdgeVarianceThreshold, int bitDepth)
    {
        if (filterEnabled && flat)
        {
            int p2 = samples[4];
            int p1 = samples[5];
            int p0 = samples[6];
            int q0 = samples[7];
            int q1 = samples[8];
            int q2 = samples[9];

            samples[5] = RoundPowerOfTwo((3 * p2) + (2 * p1) + (2 * p0) + q0, 3);
            samples[6] = RoundPowerOfTwo(p2 + (2 * p1) + (2 * p0) + (2 * q0) + q1, 3);
            samples[7] = RoundPowerOfTwo(p1 + (2 * p0) + (2 * q0) + (2 * q1) + q2, 3);
            samples[8] = RoundPowerOfTwo(p0 + (2 * q0) + (2 * q1) + (3 * q2), 3);
            return;
        }

        Filter4(samples, filterEnabled, highEdgeVarianceThreshold, bitDepth);
    }

    /// <summary>
    /// Applies the AV1 8-tap luma filter or its 4-tap fallback.
    /// </summary>
    /// <param name="samples">The p6..p0,q0..q6 scratch samples.</param>
    /// <param name="filterEnabled">Whether the filter mask permits modification.</param>
    /// <param name="flat">Whether the samples permit the wider flat filter.</param>
    /// <param name="highEdgeVarianceThreshold">The scaled high-edge-variance threshold.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    private static void Filter8(Span<int> samples, bool filterEnabled, bool flat, int highEdgeVarianceThreshold, int bitDepth)
    {
        if (filterEnabled && flat)
        {
            int p3 = samples[3];
            int p2 = samples[4];
            int p1 = samples[5];
            int p0 = samples[6];
            int q0 = samples[7];
            int q1 = samples[8];
            int q2 = samples[9];
            int q3 = samples[10];

            samples[4] = RoundPowerOfTwo((3 * p3) + (2 * p2) + p1 + p0 + q0, 3);
            samples[5] = RoundPowerOfTwo((2 * p3) + p2 + (2 * p1) + p0 + q0 + q1, 3);
            samples[6] = RoundPowerOfTwo(p3 + p2 + p1 + (2 * p0) + q0 + q1 + q2, 3);
            samples[7] = RoundPowerOfTwo(p2 + p1 + p0 + (2 * q0) + q1 + q2 + q3, 3);
            samples[8] = RoundPowerOfTwo(p1 + p0 + q0 + (2 * q1) + q2 + (2 * q3), 3);
            samples[9] = RoundPowerOfTwo(p0 + q0 + q1 + (2 * q2) + (3 * q3), 3);
            return;
        }

        Filter4(samples, filterEnabled, highEdgeVarianceThreshold, bitDepth);
    }

    /// <summary>
    /// Applies the AV1 14-tap luma filter or its 8-/4-tap fallback.
    /// </summary>
    /// <param name="samples">The p6..p0,q0..q6 scratch samples.</param>
    /// <param name="filterEnabled">Whether the filter mask permits modification.</param>
    /// <param name="flat">Whether the inner samples permit a wider flat filter.</param>
    /// <param name="outerFlat">Whether the outer samples permit the 14-tap flat filter.</param>
    /// <param name="highEdgeVarianceThreshold">The scaled high-edge-variance threshold.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    private static void Filter14(Span<int> samples, bool filterEnabled, bool flat, bool outerFlat, int highEdgeVarianceThreshold, int bitDepth)
    {
        if (filterEnabled && flat && outerFlat)
        {
            int p6 = samples[0];
            int p5 = samples[1];
            int p4 = samples[2];
            int p3 = samples[3];
            int p2 = samples[4];
            int p1 = samples[5];
            int p0 = samples[6];
            int q0 = samples[7];
            int q1 = samples[8];
            int q2 = samples[9];
            int q3 = samples[10];
            int q4 = samples[11];
            int q5 = samples[12];
            int q6 = samples[13];

            // The repeated endpoint weights extend the normative 13-tap window without reading beyond p6/q6.
            samples[1] = RoundPowerOfTwo((7 * p6) + (2 * p5) + (2 * p4) + p3 + p2 + p1 + p0 + q0, 4);
            samples[2] = RoundPowerOfTwo((5 * p6) + (2 * p5) + (2 * p4) + (2 * p3) + p2 + p1 + p0 + q0 + q1, 4);
            samples[3] = RoundPowerOfTwo((4 * p6) + p5 + (2 * p4) + (2 * p3) + (2 * p2) + p1 + p0 + q0 + q1 + q2, 4);
            samples[4] = RoundPowerOfTwo((3 * p6) + p5 + p4 + (2 * p3) + (2 * p2) + (2 * p1) + p0 + q0 + q1 + q2 + q3, 4);
            samples[5] = RoundPowerOfTwo((2 * p6) + p5 + p4 + p3 + (2 * p2) + (2 * p1) + (2 * p0) + q0 + q1 + q2 + q3 + q4, 4);
            samples[6] = RoundPowerOfTwo(p6 + p5 + p4 + p3 + p2 + (2 * p1) + (2 * p0) + (2 * q0) + q1 + q2 + q3 + q4 + q5, 4);
            samples[7] = RoundPowerOfTwo(p5 + p4 + p3 + p2 + p1 + (2 * p0) + (2 * q0) + (2 * q1) + q2 + q3 + q4 + q5 + q6, 4);
            samples[8] = RoundPowerOfTwo(p4 + p3 + p2 + p1 + p0 + (2 * q0) + (2 * q1) + (2 * q2) + q3 + q4 + q5 + (2 * q6), 4);
            samples[9] = RoundPowerOfTwo(p3 + p2 + p1 + p0 + q0 + (2 * q1) + (2 * q2) + (2 * q3) + q4 + q5 + (3 * q6), 4);
            samples[10] = RoundPowerOfTwo(p2 + p1 + p0 + q0 + q1 + (2 * q2) + (2 * q3) + (2 * q4) + q5 + (4 * q6), 4);
            samples[11] = RoundPowerOfTwo(p1 + p0 + q0 + q1 + q2 + (2 * q3) + (2 * q4) + (2 * q5) + (5 * q6), 4);
            samples[12] = RoundPowerOfTwo(p0 + q0 + q1 + q2 + q3 + (2 * q4) + (2 * q5) + (7 * q6), 4);
            return;
        }

        Filter8(samples, filterEnabled, flat, highEdgeVarianceThreshold, bitDepth);
    }

    /// <summary>
    /// Rounds an integer while dividing by a power of two.
    /// </summary>
    /// <param name="value">The integer to round.</param>
    /// <param name="bitCount">The base-two divisor exponent.</param>
    /// <returns>The rounded quotient.</returns>
    private static int RoundPowerOfTwo(int value, int bitCount)
        => (value + (1 << (bitCount - 1))) >> bitCount;
}
