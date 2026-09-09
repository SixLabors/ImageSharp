// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;

/// <summary>
/// Applies the AV1 deblocking kernels to four-sample edge segments.
/// </summary>
/// <remarks>
/// Each 32-bit vector lane represents one sample position along the edge. The orientation-specific operators gather or
/// load the corresponding p6..p0,q0..q6 neighborhood, after which masks, flatness tests, and filter equations remain
/// lane-wise. Conditional selection preserves unfiltered lanes while allowing four adjacent edge positions to share
/// one kernel invocation.
/// </remarks>
internal static partial class Av1DeblockingFilter
{
    /// <summary>
    /// Filters four rows crossing one vertical boundary in eight-bit storage.
    /// </summary>
    /// <param name="samples">The plane storage containing the edge and its neighboring samples.</param>
    /// <param name="q0Offset">The offset of the first Q-side sample.</param>
    /// <param name="stride">The number of samples between adjacent rows.</param>
    /// <param name="filterLength">The signaled 4-, 6-, 8-, or 14-tap filter length.</param>
    /// <param name="limit">The threshold for adjacent samples on either side of the edge.</param>
    /// <param name="boundaryLimit">The threshold for the discontinuity across the edge.</param>
    /// <param name="highEdgeVarianceThreshold">The threshold that selects the narrow high-variance adjustment.</param>
    public static void FilterVertical(
        Span<byte> samples,
        int q0Offset,
        int stride,
        int filterLength,
        int limit,
        int boundaryLimit,
        int highEdgeVarianceThreshold)
        => Filter<byte, VerticalByteEdgeOperator>(samples, q0Offset, stride, filterLength, limit, boundaryLimit, highEdgeVarianceThreshold, 8);

    /// <summary>
    /// Filters four columns crossing one horizontal boundary in eight-bit storage.
    /// </summary>
    /// <param name="samples">The plane storage containing the edge and its neighboring samples.</param>
    /// <param name="q0Offset">The offset of the first Q-side sample.</param>
    /// <param name="stride">The number of samples between adjacent rows.</param>
    /// <param name="filterLength">The signaled 4-, 6-, 8-, or 14-tap filter length.</param>
    /// <param name="limit">The threshold for adjacent samples on either side of the edge.</param>
    /// <param name="boundaryLimit">The threshold for the discontinuity across the edge.</param>
    /// <param name="highEdgeVarianceThreshold">The threshold that selects the narrow high-variance adjustment.</param>
    public static void FilterHorizontal(
        Span<byte> samples,
        int q0Offset,
        int stride,
        int filterLength,
        int limit,
        int boundaryLimit,
        int highEdgeVarianceThreshold)
        => Filter<byte, HorizontalByteEdgeOperator>(samples, q0Offset, stride, filterLength, limit, boundaryLimit, highEdgeVarianceThreshold, 8);

    /// <summary>
    /// Filters four rows crossing one vertical boundary in 16-bit storage.
    /// </summary>
    /// <param name="samples">The plane storage containing the edge and its neighboring samples.</param>
    /// <param name="q0Offset">The offset of the first Q-side sample.</param>
    /// <param name="stride">The number of samples between adjacent rows.</param>
    /// <param name="filterLength">The signaled 4-, 6-, 8-, or 14-tap filter length.</param>
    /// <param name="limit">The eight-bit-domain threshold for adjacent samples on either side of the edge.</param>
    /// <param name="boundaryLimit">The eight-bit-domain threshold for the discontinuity across the edge.</param>
    /// <param name="highEdgeVarianceThreshold">The eight-bit-domain threshold that selects the narrow high-variance adjustment.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    public static void FilterVertical(
        Span<ushort> samples,
        int q0Offset,
        int stride,
        int filterLength,
        int limit,
        int boundaryLimit,
        int highEdgeVarianceThreshold,
        int bitDepth)
        => Filter<ushort, VerticalUInt16EdgeOperator>(samples, q0Offset, stride, filterLength, limit, boundaryLimit, highEdgeVarianceThreshold, bitDepth);

    /// <summary>
    /// Filters four columns crossing one horizontal boundary in 16-bit storage.
    /// </summary>
    /// <param name="samples">The plane storage containing the edge and its neighboring samples.</param>
    /// <param name="q0Offset">The offset of the first Q-side sample.</param>
    /// <param name="stride">The number of samples between adjacent rows.</param>
    /// <param name="filterLength">The signaled 4-, 6-, 8-, or 14-tap filter length.</param>
    /// <param name="limit">The eight-bit-domain threshold for adjacent samples on either side of the edge.</param>
    /// <param name="boundaryLimit">The eight-bit-domain threshold for the discontinuity across the edge.</param>
    /// <param name="highEdgeVarianceThreshold">The eight-bit-domain threshold that selects the narrow high-variance adjustment.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    public static void FilterHorizontal(
        Span<ushort> samples,
        int q0Offset,
        int stride,
        int filterLength,
        int limit,
        int boundaryLimit,
        int highEdgeVarianceThreshold,
        int bitDepth)
        => Filter<ushort, HorizontalUInt16EdgeOperator>(samples, q0Offset, stride, filterLength, limit, boundaryLimit, highEdgeVarianceThreshold, bitDepth);

    /// <summary>
    /// Selects the packed or scalar kernel through one closed edge-access operator.
    /// </summary>
    /// <typeparam name="TSample">The reconstructed sample storage type.</typeparam>
    /// <typeparam name="TEdgeOperator">The orientation- and storage-specific edge operator.</typeparam>
    /// <param name="samples">The plane storage containing the edge and its neighboring samples.</param>
    /// <param name="q0Offset">The offset of the first Q-side sample.</param>
    /// <param name="stride">The number of samples between adjacent rows.</param>
    /// <param name="filterLength">The signaled filter length.</param>
    /// <param name="limit">The eight-bit-domain adjacent-sample threshold.</param>
    /// <param name="boundaryLimit">The eight-bit-domain edge-discontinuity threshold.</param>
    /// <param name="highEdgeVarianceThreshold">The eight-bit-domain high-edge-variance threshold.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    public static void Filter<TSample, TEdgeOperator>(
        Span<TSample> samples,
        int q0Offset,
        int stride,
        int filterLength,
        int limit,
        int boundaryLimit,
        int highEdgeVarianceThreshold,
        int bitDepth)
        where TSample : unmanaged
        where TEdgeOperator : struct, IEdgeOperator<TSample>
    {
        ref TSample sampleBase = ref MemoryMarshal.GetReference(samples);
        if (Vector128.IsHardwareAccelerated)
        {
            FilterVector<TSample, TEdgeOperator>(ref sampleBase, q0Offset, stride, filterLength, limit, boundaryLimit, highEdgeVarianceThreshold, bitDepth);
            return;
        }

        FilterScalar<TSample, TEdgeOperator>(ref sampleBase, q0Offset, stride, filterLength, limit, boundaryLimit, highEdgeVarianceThreshold, bitDepth);
    }

    /// <summary>
    /// Applies one packed AV1 kernel with each 32-bit lane representing one row or column along the edge.
    /// </summary>
    /// <typeparam name="TSample">The reconstructed sample storage type.</typeparam>
    /// <typeparam name="TEdgeOperator">The orientation- and storage-specific edge operator.</typeparam>
    /// <param name="samples">The first element in the plane storage.</param>
    /// <param name="q0Offset">The offset of the first Q-side sample.</param>
    /// <param name="stride">The number of samples between adjacent rows.</param>
    /// <param name="filterLength">The signaled filter length.</param>
    /// <param name="limit">The eight-bit-domain adjacent-sample threshold.</param>
    /// <param name="boundaryLimit">The eight-bit-domain edge-discontinuity threshold.</param>
    /// <param name="highEdgeVarianceThreshold">The eight-bit-domain high-edge-variance threshold.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    private static void FilterVector<TSample, TEdgeOperator>(
        ref TSample samples,
        int q0Offset,
        int stride,
        int filterLength,
        int limit,
        int boundaryLimit,
        int highEdgeVarianceThreshold,
        int bitDepth)
        where TSample : unmanaged
        where TEdgeOperator : struct, IEdgeOperator<TSample>
    {
        int radius = GetFilterRadius(filterLength);
        InlineArray14<Vector128<int>> window = default;

        // AV1 names the samples p6..p0,q0..q6. Loading them into that exact order lets the packed equations below
        // follow the normative scalar formulas without lane shuffles or an intermediate per-edge sample buffer.
        for (int distance = 1; distance <= radius; distance++)
        {
            window[7 - distance] = TEdgeOperator.LoadVector(ref samples, q0Offset, stride, -distance);
            window[6 + distance] = TEdgeOperator.LoadVector(ref samples, q0Offset, stride, distance - 1);
        }

        FilterSamples(ref window, filterLength, limit, boundaryLimit, highEdgeVarianceThreshold, bitDepth);

        int modifiedRadius = GetModifiedRadius(filterLength);
        for (int distance = 1; distance <= modifiedRadius; distance++)
        {
            TEdgeOperator.StoreVector(ref samples, q0Offset, stride, -distance, window[7 - distance]);
            TEdgeOperator.StoreVector(ref samples, q0Offset, stride, distance - 1, window[6 + distance]);
        }
    }

    /// <summary>
    /// Applies the scalar fallback to each of the four samples along an edge.
    /// </summary>
    /// <typeparam name="TSample">The reconstructed sample storage type.</typeparam>
    /// <typeparam name="TEdgeOperator">The orientation- and storage-specific edge operator.</typeparam>
    /// <param name="samples">The first element in the plane storage.</param>
    /// <param name="q0Offset">The offset of the first Q-side sample.</param>
    /// <param name="stride">The number of samples between adjacent rows.</param>
    /// <param name="filterLength">The signaled filter length.</param>
    /// <param name="limit">The eight-bit-domain adjacent-sample threshold.</param>
    /// <param name="boundaryLimit">The eight-bit-domain edge-discontinuity threshold.</param>
    /// <param name="highEdgeVarianceThreshold">The eight-bit-domain high-edge-variance threshold.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    private static void FilterScalar<TSample, TEdgeOperator>(
        ref TSample samples,
        int q0Offset,
        int stride,
        int filterLength,
        int limit,
        int boundaryLimit,
        int highEdgeVarianceThreshold,
        int bitDepth)
        where TSample : unmanaged
        where TEdgeOperator : struct, IEdgeOperator<TSample>
    {
        int radius = GetFilterRadius(filterLength);
        int modifiedRadius = GetModifiedRadius(filterLength);
        InlineArray14<int> window = default;
        Span<int> sampleWindow = window;

        for (int index = 0; index < 4; index++)
        {
            // The fixed p6..q6 window is reused for all four lanes. Every value consumed by the selected kernel is
            // overwritten before filtering, so the fallback requires neither per-lane clearing nor stack allocation.
            for (int distance = 1; distance <= radius; distance++)
            {
                sampleWindow[7 - distance] = TEdgeOperator.LoadScalar(ref samples, q0Offset, stride, -distance, index);
                sampleWindow[6 + distance] = TEdgeOperator.LoadScalar(ref samples, q0Offset, stride, distance - 1, index);
            }

            FilterSamples(sampleWindow, filterLength, limit, boundaryLimit, highEdgeVarianceThreshold, bitDepth);

            for (int distance = 1; distance <= modifiedRadius; distance++)
            {
                TEdgeOperator.StoreScalar(ref samples, q0Offset, stride, -distance, index, sampleWindow[7 - distance]);
                TEdgeOperator.StoreScalar(ref samples, q0Offset, stride, distance - 1, index, sampleWindow[6 + distance]);
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
    /// Gets the number of samples that a filter can modify on each side of an edge.
    /// </summary>
    /// <param name="filterLength">The AV1 filter length.</param>
    /// <returns>The modified sample radius on either side of the edge.</returns>
    private static int GetModifiedRadius(int filterLength) => filterLength switch
    {
        4 or 6 => 2,
        8 => 3,
        14 => 6,
        _ => 0
    };

    /// <summary>
    /// Selects and applies the packed filter arithmetic for four edge lanes.
    /// </summary>
    /// <param name="samples">The packed p6..p0,q0..q6 sample window.</param>
    /// <param name="filterLength">The signaled filter length.</param>
    /// <param name="limit">The eight-bit-domain adjacent-sample threshold.</param>
    /// <param name="boundaryLimit">The eight-bit-domain edge-discontinuity threshold.</param>
    /// <param name="highEdgeVarianceThreshold">The eight-bit-domain high-edge-variance threshold.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    private static void FilterSamples(
        ref InlineArray14<Vector128<int>> samples,
        int filterLength,
        int limit,
        int boundaryLimit,
        int highEdgeVarianceThreshold,
        int bitDepth)
    {
        int thresholdScale = 1 << (bitDepth - 8);
        Vector128<int> limitVector = Vector128.Create(limit * thresholdScale);
        Vector128<int> boundaryLimitVector = Vector128.Create(boundaryLimit * thresholdScale);
        Vector128<int> varianceThresholdVector = Vector128.Create(highEdgeVarianceThreshold * thresholdScale);
        Vector128<int> flatnessThresholdVector = Vector128.Create(thresholdScale);

        switch (filterLength)
        {
            case 4:
                Filter4(ref samples, IsFilter2Enabled(ref samples, limitVector, boundaryLimitVector), varianceThresholdVector, bitDepth);
                break;
            case 6:
                Filter6(
                    ref samples,
                    IsChromaFilterEnabled(ref samples, limitVector, boundaryLimitVector),
                    IsChromaFlat(ref samples, flatnessThresholdVector),
                    varianceThresholdVector,
                    bitDepth);
                break;
            case 8:
                Filter8(
                    ref samples,
                    IsFilterEnabled(ref samples, limitVector, boundaryLimitVector),
                    IsFlat(ref samples, flatnessThresholdVector),
                    varianceThresholdVector,
                    bitDepth);
                break;
            case 14:
                Filter14(
                    ref samples,
                    IsFilterEnabled(ref samples, limitVector, boundaryLimitVector),
                    IsFlat(ref samples, flatnessThresholdVector),
                    IsOuterFlat(ref samples, flatnessThresholdVector),
                    varianceThresholdVector,
                    bitDepth);
                break;
        }
    }

    /// <summary>
    /// Selects and applies the scalar filter arithmetic for one edge lane.
    /// </summary>
    /// <param name="samples">The scalar p6..p0,q0..q6 sample window.</param>
    /// <param name="filterLength">The signaled filter length.</param>
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

        switch (filterLength)
        {
            case 4:
                Filter4(samples, IsFilter2Enabled(samples, scaledLimit, scaledBoundaryLimit), highEdgeVarianceThreshold * thresholdScale, bitDepth);
                break;
            case 6:
                Filter6(
                    samples,
                    IsChromaFilterEnabled(samples, scaledLimit, scaledBoundaryLimit),
                    IsChromaFlat(samples, thresholdScale),
                    highEdgeVarianceThreshold * thresholdScale,
                    bitDepth);
                break;
            case 8:
                Filter8(
                    samples,
                    IsFilterEnabled(samples, scaledLimit, scaledBoundaryLimit),
                    IsFlat(samples, thresholdScale),
                    highEdgeVarianceThreshold * thresholdScale,
                    bitDepth);
                break;
            case 14:
                Filter14(
                    samples,
                    IsFilterEnabled(samples, scaledLimit, scaledBoundaryLimit),
                    IsFlat(samples, thresholdScale),
                    IsOuterFlat(samples, thresholdScale),
                    highEdgeVarianceThreshold * thresholdScale,
                    bitDepth);
                break;
        }
    }

    /// <summary>
    /// Determines which packed lanes satisfy the four-tap AV1 filter mask.
    /// </summary>
    /// <param name="samples">The packed p6..p0,q0..q6 sample window.</param>
    /// <param name="limit">The scaled adjacent-sample threshold.</param>
    /// <param name="boundaryLimit">The scaled edge-discontinuity threshold.</param>
    /// <returns>A mask containing all bits set in each enabled lane.</returns>
    private static Vector128<int> IsFilter2Enabled(ref InlineArray14<Vector128<int>> samples, Vector128<int> limit, Vector128<int> boundaryLimit)
        => Vector128.LessThanOrEqual(Vector128.Abs(samples[5] - samples[6]), limit)
            & Vector128.LessThanOrEqual(Vector128.Abs(samples[8] - samples[7]), limit)
            & Vector128.LessThanOrEqual((Vector128.Abs(samples[6] - samples[7]) * 2) + (Vector128.Abs(samples[5] - samples[8]) >> 1), boundaryLimit);

    /// <summary>
    /// Determines which packed lanes satisfy the eight- and fourteen-tap AV1 filter mask.
    /// </summary>
    /// <param name="samples">The packed p6..p0,q0..q6 sample window.</param>
    /// <param name="limit">The scaled adjacent-sample threshold.</param>
    /// <param name="boundaryLimit">The scaled edge-discontinuity threshold.</param>
    /// <returns>A mask containing all bits set in each enabled lane.</returns>
    private static Vector128<int> IsFilterEnabled(ref InlineArray14<Vector128<int>> samples, Vector128<int> limit, Vector128<int> boundaryLimit)
        => Vector128.LessThanOrEqual(Vector128.Abs(samples[3] - samples[4]), limit)
            & Vector128.LessThanOrEqual(Vector128.Abs(samples[4] - samples[5]), limit)
            & Vector128.LessThanOrEqual(Vector128.Abs(samples[5] - samples[6]), limit)
            & Vector128.LessThanOrEqual(Vector128.Abs(samples[8] - samples[7]), limit)
            & Vector128.LessThanOrEqual(Vector128.Abs(samples[9] - samples[8]), limit)
            & Vector128.LessThanOrEqual(Vector128.Abs(samples[10] - samples[9]), limit)
            & Vector128.LessThanOrEqual((Vector128.Abs(samples[6] - samples[7]) * 2) + (Vector128.Abs(samples[5] - samples[8]) >> 1), boundaryLimit);

    /// <summary>
    /// Determines which packed lanes satisfy the six-tap chroma filter mask.
    /// </summary>
    /// <param name="samples">The packed p6..p0,q0..q6 sample window.</param>
    /// <param name="limit">The scaled adjacent-sample threshold.</param>
    /// <param name="boundaryLimit">The scaled edge-discontinuity threshold.</param>
    /// <returns>A mask containing all bits set in each enabled lane.</returns>
    private static Vector128<int> IsChromaFilterEnabled(ref InlineArray14<Vector128<int>> samples, Vector128<int> limit, Vector128<int> boundaryLimit)
        => Vector128.LessThanOrEqual(Vector128.Abs(samples[4] - samples[5]), limit)
            & Vector128.LessThanOrEqual(Vector128.Abs(samples[5] - samples[6]), limit)
            & Vector128.LessThanOrEqual(Vector128.Abs(samples[8] - samples[7]), limit)
            & Vector128.LessThanOrEqual(Vector128.Abs(samples[9] - samples[8]), limit)
            & Vector128.LessThanOrEqual((Vector128.Abs(samples[6] - samples[7]) * 2) + (Vector128.Abs(samples[5] - samples[8]) >> 1), boundaryLimit);

    /// <summary>
    /// Determines which packed lanes satisfy the inner flatness mask.
    /// </summary>
    /// <param name="samples">The packed p6..p0,q0..q6 sample window.</param>
    /// <param name="threshold">The scaled flatness threshold.</param>
    /// <returns>A mask containing all bits set in each flat lane.</returns>
    private static Vector128<int> IsFlat(ref InlineArray14<Vector128<int>> samples, Vector128<int> threshold)
        => Vector128.LessThanOrEqual(Vector128.Abs(samples[5] - samples[6]), threshold)
            & Vector128.LessThanOrEqual(Vector128.Abs(samples[8] - samples[7]), threshold)
            & Vector128.LessThanOrEqual(Vector128.Abs(samples[4] - samples[6]), threshold)
            & Vector128.LessThanOrEqual(Vector128.Abs(samples[9] - samples[7]), threshold)
            & Vector128.LessThanOrEqual(Vector128.Abs(samples[3] - samples[6]), threshold)
            & Vector128.LessThanOrEqual(Vector128.Abs(samples[10] - samples[7]), threshold);

    /// <summary>
    /// Determines which packed lanes satisfy the chroma flatness mask.
    /// </summary>
    /// <param name="samples">The packed p6..p0,q0..q6 sample window.</param>
    /// <param name="threshold">The scaled flatness threshold.</param>
    /// <returns>A mask containing all bits set in each flat lane.</returns>
    private static Vector128<int> IsChromaFlat(ref InlineArray14<Vector128<int>> samples, Vector128<int> threshold)
        => Vector128.LessThanOrEqual(Vector128.Abs(samples[5] - samples[6]), threshold)
            & Vector128.LessThanOrEqual(Vector128.Abs(samples[8] - samples[7]), threshold)
            & Vector128.LessThanOrEqual(Vector128.Abs(samples[4] - samples[6]), threshold)
            & Vector128.LessThanOrEqual(Vector128.Abs(samples[9] - samples[7]), threshold);

    /// <summary>
    /// Determines which packed lanes satisfy the outer fourteen-tap flatness mask.
    /// </summary>
    /// <param name="samples">The packed p6..p0,q0..q6 sample window.</param>
    /// <param name="threshold">The scaled flatness threshold.</param>
    /// <returns>A mask containing all bits set in each flat lane.</returns>
    private static Vector128<int> IsOuterFlat(ref InlineArray14<Vector128<int>> samples, Vector128<int> threshold)
        => Vector128.LessThanOrEqual(Vector128.Abs(samples[1] - samples[6]), threshold)
            & Vector128.LessThanOrEqual(Vector128.Abs(samples[11] - samples[7]), threshold)
            & Vector128.LessThanOrEqual(Vector128.Abs(samples[2] - samples[6]), threshold)
            & Vector128.LessThanOrEqual(Vector128.Abs(samples[12] - samples[7]), threshold)
            & Vector128.LessThanOrEqual(Vector128.Abs(samples[0] - samples[6]), threshold)
            & Vector128.LessThanOrEqual(Vector128.Abs(samples[13] - samples[7]), threshold);

    /// <summary>
    /// Applies the packed narrow signed-saturating AV1 edge adjustment.
    /// </summary>
    /// <param name="samples">The packed p6..p0,q0..q6 sample window.</param>
    /// <param name="filterEnabled">The per-lane filter-enable mask.</param>
    /// <param name="highEdgeVarianceThreshold">The scaled high-edge-variance threshold.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    private static void Filter4(
        ref InlineArray14<Vector128<int>> samples,
        Vector128<int> filterEnabled,
        Vector128<int> highEdgeVarianceThreshold,
        int bitDepth)
    {
        Vector128<int> p1 = samples[5];
        Vector128<int> p0 = samples[6];
        Vector128<int> q0 = samples[7];
        Vector128<int> q1 = samples[8];
        Vector128<int> highEdgeVariance = Vector128.GreaterThan(Vector128.Abs(p1 - p0), highEdgeVarianceThreshold)
            | Vector128.GreaterThan(Vector128.Abs(q1 - q0), highEdgeVarianceThreshold);

        int signedOffset = 128 << (bitDepth - 8);
        Vector128<int> minimum = Vector128.Create(-signedOffset);
        Vector128<int> maximum = Vector128.Create(signedOffset - 1);
        Vector128<int> offset = Vector128.Create(signedOffset);
        Vector128<int> signedP1 = p1 - offset;
        Vector128<int> signedP0 = p0 - offset;
        Vector128<int> signedQ0 = q0 - offset;
        Vector128<int> signedQ1 = q1 - offset;

        // the reference decoder performs every delta operation in the signed sample domain. Saturating only the final samples is
        // not equivalent because the intermediate delta can clip before the asymmetric +4/+3 rounding is applied.
        Vector128<int> filter = Vector128.ConditionalSelect(highEdgeVariance, Vector128.Clamp(signedP1 - signedQ1, minimum, maximum), Vector128<int>.Zero);
        filter = Vector128.Clamp(filter + (3 * (signedQ0 - signedP0)), minimum, maximum) & filterEnabled;

        Vector128<int> filter1 = Vector128.Clamp(filter + Vector128.Create(4), minimum, maximum) >> 3;
        Vector128<int> filter2 = Vector128.Clamp(filter + Vector128.Create(3), minimum, maximum) >> 3;
        samples[7] = Vector128.Clamp(signedQ0 - filter1, minimum, maximum) + offset;
        samples[6] = Vector128.Clamp(signedP0 + filter2, minimum, maximum) + offset;

        Vector128<int> outerFilter = ((filter1 + Vector128<int>.One) >> 1) & ~highEdgeVariance;
        samples[8] = Vector128.Clamp(signedQ1 - outerFilter, minimum, maximum) + offset;
        samples[5] = Vector128.Clamp(signedP1 + outerFilter, minimum, maximum) + offset;
    }

    /// <summary>
    /// Applies the packed six-tap chroma filter or its four-tap fallback.
    /// </summary>
    /// <param name="samples">The packed p6..p0,q0..q6 sample window.</param>
    /// <param name="filterEnabled">The per-lane filter-enable mask.</param>
    /// <param name="flat">The per-lane inner-flatness mask.</param>
    /// <param name="highEdgeVarianceThreshold">The scaled high-edge-variance threshold.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    private static void Filter6(
        ref InlineArray14<Vector128<int>> samples,
        Vector128<int> filterEnabled,
        Vector128<int> flat,
        Vector128<int> highEdgeVarianceThreshold,
        int bitDepth)
    {
        Vector128<int> p2 = samples[4];
        Vector128<int> p1 = samples[5];
        Vector128<int> p0 = samples[6];
        Vector128<int> q0 = samples[7];
        Vector128<int> q1 = samples[8];
        Vector128<int> q2 = samples[9];
        Vector128<int> wideFilter = filterEnabled & flat;

        Filter4(ref samples, filterEnabled, highEdgeVarianceThreshold, bitDepth);
        if (Vector128.EqualsAll(wideFilter, Vector128<int>.Zero))
        {
            return;
        }

        Vector128<int> filteredP1 = RoundPowerOfTwo((3 * p2) + (2 * p1) + (2 * p0) + q0, 3);
        Vector128<int> filteredP0 = RoundPowerOfTwo(p2 + (2 * p1) + (2 * p0) + (2 * q0) + q1, 3);
        Vector128<int> filteredQ0 = RoundPowerOfTwo(p1 + (2 * p0) + (2 * q0) + (2 * q1) + q2, 3);
        Vector128<int> filteredQ1 = RoundPowerOfTwo(p0 + (2 * q0) + (2 * q1) + (3 * q2), 3);
        samples[5] = Vector128.ConditionalSelect(wideFilter, filteredP1, samples[5]);
        samples[6] = Vector128.ConditionalSelect(wideFilter, filteredP0, samples[6]);
        samples[7] = Vector128.ConditionalSelect(wideFilter, filteredQ0, samples[7]);
        samples[8] = Vector128.ConditionalSelect(wideFilter, filteredQ1, samples[8]);
    }

    /// <summary>
    /// Applies the packed eight-tap luma filter or its four-tap fallback.
    /// </summary>
    /// <param name="samples">The packed p6..p0,q0..q6 sample window.</param>
    /// <param name="filterEnabled">The per-lane filter-enable mask.</param>
    /// <param name="flat">The per-lane inner-flatness mask.</param>
    /// <param name="highEdgeVarianceThreshold">The scaled high-edge-variance threshold.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    private static void Filter8(
        ref InlineArray14<Vector128<int>> samples,
        Vector128<int> filterEnabled,
        Vector128<int> flat,
        Vector128<int> highEdgeVarianceThreshold,
        int bitDepth)
    {
        Vector128<int> p3 = samples[3];
        Vector128<int> p2 = samples[4];
        Vector128<int> p1 = samples[5];
        Vector128<int> p0 = samples[6];
        Vector128<int> q0 = samples[7];
        Vector128<int> q1 = samples[8];
        Vector128<int> q2 = samples[9];
        Vector128<int> q3 = samples[10];
        Vector128<int> wideFilter = filterEnabled & flat;

        Filter4(ref samples, filterEnabled, highEdgeVarianceThreshold, bitDepth);
        if (Vector128.EqualsAll(wideFilter, Vector128<int>.Zero))
        {
            return;
        }

        Vector128<int> filteredP2 = RoundPowerOfTwo((3 * p3) + (2 * p2) + p1 + p0 + q0, 3);
        Vector128<int> filteredP1 = RoundPowerOfTwo((2 * p3) + p2 + (2 * p1) + p0 + q0 + q1, 3);
        Vector128<int> filteredP0 = RoundPowerOfTwo(p3 + p2 + p1 + (2 * p0) + q0 + q1 + q2, 3);
        Vector128<int> filteredQ0 = RoundPowerOfTwo(p2 + p1 + p0 + (2 * q0) + q1 + q2 + q3, 3);
        Vector128<int> filteredQ1 = RoundPowerOfTwo(p1 + p0 + q0 + (2 * q1) + q2 + (2 * q3), 3);
        Vector128<int> filteredQ2 = RoundPowerOfTwo(p0 + q0 + q1 + (2 * q2) + (3 * q3), 3);
        samples[4] = Vector128.ConditionalSelect(wideFilter, filteredP2, samples[4]);
        samples[5] = Vector128.ConditionalSelect(wideFilter, filteredP1, samples[5]);
        samples[6] = Vector128.ConditionalSelect(wideFilter, filteredP0, samples[6]);
        samples[7] = Vector128.ConditionalSelect(wideFilter, filteredQ0, samples[7]);
        samples[8] = Vector128.ConditionalSelect(wideFilter, filteredQ1, samples[8]);
        samples[9] = Vector128.ConditionalSelect(wideFilter, filteredQ2, samples[9]);
    }

    /// <summary>
    /// Applies the packed fourteen-tap luma filter or its eight- and four-tap fallbacks.
    /// </summary>
    /// <param name="samples">The packed p6..p0,q0..q6 sample window.</param>
    /// <param name="filterEnabled">The per-lane filter-enable mask.</param>
    /// <param name="flat">The per-lane inner-flatness mask.</param>
    /// <param name="outerFlat">The per-lane outer-flatness mask.</param>
    /// <param name="highEdgeVarianceThreshold">The scaled high-edge-variance threshold.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    private static void Filter14(
        ref InlineArray14<Vector128<int>> samples,
        Vector128<int> filterEnabled,
        Vector128<int> flat,
        Vector128<int> outerFlat,
        Vector128<int> highEdgeVarianceThreshold,
        int bitDepth)
    {
        Vector128<int> p6 = samples[0];
        Vector128<int> p5 = samples[1];
        Vector128<int> p4 = samples[2];
        Vector128<int> p3 = samples[3];
        Vector128<int> p2 = samples[4];
        Vector128<int> p1 = samples[5];
        Vector128<int> p0 = samples[6];
        Vector128<int> q0 = samples[7];
        Vector128<int> q1 = samples[8];
        Vector128<int> q2 = samples[9];
        Vector128<int> q3 = samples[10];
        Vector128<int> q4 = samples[11];
        Vector128<int> q5 = samples[12];
        Vector128<int> q6 = samples[13];
        Vector128<int> wideFilter = filterEnabled & flat & outerFlat;

        // The eight-tap routine first produces the normative fallback. Lanes satisfying the outer flatness mask are
        // then replaced with the wider results, matching the reference decoder's mask blend without evaluating lanes independently.
        Filter8(ref samples, filterEnabled, flat, highEdgeVarianceThreshold, bitDepth);
        if (Vector128.EqualsAll(wideFilter, Vector128<int>.Zero))
        {
            return;
        }

        Vector128<int> filteredP5 = RoundPowerOfTwo((7 * p6) + (2 * p5) + (2 * p4) + p3 + p2 + p1 + p0 + q0, 4);
        Vector128<int> filteredP4 = RoundPowerOfTwo((5 * p6) + (2 * p5) + (2 * p4) + (2 * p3) + p2 + p1 + p0 + q0 + q1, 4);
        Vector128<int> filteredP3 = RoundPowerOfTwo((4 * p6) + p5 + (2 * p4) + (2 * p3) + (2 * p2) + p1 + p0 + q0 + q1 + q2, 4);
        Vector128<int> filteredP2 = RoundPowerOfTwo((3 * p6) + p5 + p4 + (2 * p3) + (2 * p2) + (2 * p1) + p0 + q0 + q1 + q2 + q3, 4);
        Vector128<int> filteredP1 = RoundPowerOfTwo((2 * p6) + p5 + p4 + p3 + (2 * p2) + (2 * p1) + (2 * p0) + q0 + q1 + q2 + q3 + q4, 4);
        Vector128<int> filteredP0 = RoundPowerOfTwo(p6 + p5 + p4 + p3 + p2 + (2 * p1) + (2 * p0) + (2 * q0) + q1 + q2 + q3 + q4 + q5, 4);
        Vector128<int> filteredQ0 = RoundPowerOfTwo(p5 + p4 + p3 + p2 + p1 + (2 * p0) + (2 * q0) + (2 * q1) + q2 + q3 + q4 + q5 + q6, 4);
        Vector128<int> filteredQ1 = RoundPowerOfTwo(p4 + p3 + p2 + p1 + p0 + (2 * q0) + (2 * q1) + (2 * q2) + q3 + q4 + q5 + (2 * q6), 4);
        Vector128<int> filteredQ2 = RoundPowerOfTwo(p3 + p2 + p1 + p0 + q0 + (2 * q1) + (2 * q2) + (2 * q3) + q4 + q5 + (3 * q6), 4);
        Vector128<int> filteredQ3 = RoundPowerOfTwo(p2 + p1 + p0 + q0 + q1 + (2 * q2) + (2 * q3) + (2 * q4) + q5 + (4 * q6), 4);
        Vector128<int> filteredQ4 = RoundPowerOfTwo(p1 + p0 + q0 + q1 + q2 + (2 * q3) + (2 * q4) + (2 * q5) + (5 * q6), 4);
        Vector128<int> filteredQ5 = RoundPowerOfTwo(p0 + q0 + q1 + q2 + q3 + (2 * q4) + (2 * q5) + (7 * q6), 4);

        samples[1] = Vector128.ConditionalSelect(wideFilter, filteredP5, samples[1]);
        samples[2] = Vector128.ConditionalSelect(wideFilter, filteredP4, samples[2]);
        samples[3] = Vector128.ConditionalSelect(wideFilter, filteredP3, samples[3]);
        samples[4] = Vector128.ConditionalSelect(wideFilter, filteredP2, samples[4]);
        samples[5] = Vector128.ConditionalSelect(wideFilter, filteredP1, samples[5]);
        samples[6] = Vector128.ConditionalSelect(wideFilter, filteredP0, samples[6]);
        samples[7] = Vector128.ConditionalSelect(wideFilter, filteredQ0, samples[7]);
        samples[8] = Vector128.ConditionalSelect(wideFilter, filteredQ1, samples[8]);
        samples[9] = Vector128.ConditionalSelect(wideFilter, filteredQ2, samples[9]);
        samples[10] = Vector128.ConditionalSelect(wideFilter, filteredQ3, samples[10]);
        samples[11] = Vector128.ConditionalSelect(wideFilter, filteredQ4, samples[11]);
        samples[12] = Vector128.ConditionalSelect(wideFilter, filteredQ5, samples[12]);
    }

    /// <summary>
    /// Determines whether a scalar lane satisfies the four-tap AV1 filter mask.
    /// </summary>
    /// <param name="samples">The scalar p6..p0,q0..q6 sample window.</param>
    /// <param name="limit">The scaled adjacent-sample threshold.</param>
    /// <param name="boundaryLimit">The scaled edge-discontinuity threshold.</param>
    /// <returns><see langword="true"/> when the edge satisfies the filter mask.</returns>
    private static bool IsFilter2Enabled(ReadOnlySpan<int> samples, int limit, int boundaryLimit)
        => Math.Abs(samples[5] - samples[6]) <= limit
            && Math.Abs(samples[8] - samples[7]) <= limit
            && ((2 * Math.Abs(samples[6] - samples[7])) + (Math.Abs(samples[5] - samples[8]) / 2)) <= boundaryLimit;

    /// <summary>
    /// Determines whether a scalar lane satisfies the eight- and fourteen-tap AV1 filter mask.
    /// </summary>
    /// <param name="samples">The scalar p6..p0,q0..q6 sample window.</param>
    /// <param name="limit">The scaled adjacent-sample threshold.</param>
    /// <param name="boundaryLimit">The scaled edge-discontinuity threshold.</param>
    /// <returns><see langword="true"/> when the edge satisfies the filter mask.</returns>
    private static bool IsFilterEnabled(ReadOnlySpan<int> samples, int limit, int boundaryLimit)
        => Math.Abs(samples[3] - samples[4]) <= limit
            && Math.Abs(samples[4] - samples[5]) <= limit
            && Math.Abs(samples[5] - samples[6]) <= limit
            && Math.Abs(samples[8] - samples[7]) <= limit
            && Math.Abs(samples[9] - samples[8]) <= limit
            && Math.Abs(samples[10] - samples[9]) <= limit
            && ((2 * Math.Abs(samples[6] - samples[7])) + (Math.Abs(samples[5] - samples[8]) / 2)) <= boundaryLimit;

    /// <summary>
    /// Determines whether a scalar lane satisfies the six-tap chroma filter mask.
    /// </summary>
    /// <param name="samples">The scalar p6..p0,q0..q6 sample window.</param>
    /// <param name="limit">The scaled adjacent-sample threshold.</param>
    /// <param name="boundaryLimit">The scaled edge-discontinuity threshold.</param>
    /// <returns><see langword="true"/> when the edge satisfies the filter mask.</returns>
    private static bool IsChromaFilterEnabled(ReadOnlySpan<int> samples, int limit, int boundaryLimit)
        => Math.Abs(samples[4] - samples[5]) <= limit
            && Math.Abs(samples[5] - samples[6]) <= limit
            && Math.Abs(samples[8] - samples[7]) <= limit
            && Math.Abs(samples[9] - samples[8]) <= limit
            && ((2 * Math.Abs(samples[6] - samples[7])) + (Math.Abs(samples[5] - samples[8]) / 2)) <= boundaryLimit;

    /// <summary>
    /// Determines whether a scalar lane satisfies the inner flatness mask.
    /// </summary>
    /// <param name="samples">The scalar p6..p0,q0..q6 sample window.</param>
    /// <param name="threshold">The scaled flatness threshold.</param>
    /// <returns><see langword="true"/> when the edge satisfies the flatness mask.</returns>
    private static bool IsFlat(ReadOnlySpan<int> samples, int threshold)
        => Math.Abs(samples[5] - samples[6]) <= threshold
            && Math.Abs(samples[8] - samples[7]) <= threshold
            && Math.Abs(samples[4] - samples[6]) <= threshold
            && Math.Abs(samples[9] - samples[7]) <= threshold
            && Math.Abs(samples[3] - samples[6]) <= threshold
            && Math.Abs(samples[10] - samples[7]) <= threshold;

    /// <summary>
    /// Determines whether a scalar lane satisfies the chroma flatness mask.
    /// </summary>
    /// <param name="samples">The scalar p6..p0,q0..q6 sample window.</param>
    /// <param name="threshold">The scaled flatness threshold.</param>
    /// <returns><see langword="true"/> when the edge satisfies the flatness mask.</returns>
    private static bool IsChromaFlat(ReadOnlySpan<int> samples, int threshold)
        => Math.Abs(samples[5] - samples[6]) <= threshold
            && Math.Abs(samples[8] - samples[7]) <= threshold
            && Math.Abs(samples[4] - samples[6]) <= threshold
            && Math.Abs(samples[9] - samples[7]) <= threshold;

    /// <summary>
    /// Determines whether a scalar lane satisfies the outer fourteen-tap flatness mask.
    /// </summary>
    /// <param name="samples">The scalar p6..p0,q0..q6 sample window.</param>
    /// <param name="threshold">The scaled flatness threshold.</param>
    /// <returns><see langword="true"/> when the edge satisfies the flatness mask.</returns>
    private static bool IsOuterFlat(ReadOnlySpan<int> samples, int threshold)
        => Math.Abs(samples[1] - samples[6]) <= threshold
            && Math.Abs(samples[11] - samples[7]) <= threshold
            && Math.Abs(samples[2] - samples[6]) <= threshold
            && Math.Abs(samples[12] - samples[7]) <= threshold
            && Math.Abs(samples[0] - samples[6]) <= threshold
            && Math.Abs(samples[13] - samples[7]) <= threshold;

    /// <summary>
    /// Applies the scalar narrow signed-saturating AV1 edge adjustment.
    /// </summary>
    /// <param name="samples">The scalar p6..p0,q0..q6 sample window.</param>
    /// <param name="filterEnabled">Whether the edge satisfies the filter mask.</param>
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
        bool highEdgeVariance = Math.Abs(samples[5] - samples[6]) > highEdgeVarianceThreshold
            || Math.Abs(samples[8] - samples[7]) > highEdgeVarianceThreshold;

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
    /// Applies the scalar six-tap chroma filter or its four-tap fallback.
    /// </summary>
    /// <param name="samples">The scalar p6..p0,q0..q6 sample window.</param>
    /// <param name="filterEnabled">Whether the edge satisfies the filter mask.</param>
    /// <param name="flat">Whether the edge satisfies the inner-flatness mask.</param>
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
    /// Applies the scalar eight-tap luma filter or its four-tap fallback.
    /// </summary>
    /// <param name="samples">The scalar p6..p0,q0..q6 sample window.</param>
    /// <param name="filterEnabled">Whether the edge satisfies the filter mask.</param>
    /// <param name="flat">Whether the edge satisfies the inner-flatness mask.</param>
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
    /// Applies the scalar fourteen-tap luma filter or its eight- and four-tap fallbacks.
    /// </summary>
    /// <param name="samples">The scalar p6..p0,q0..q6 sample window.</param>
    /// <param name="filterEnabled">Whether the edge satisfies the filter mask.</param>
    /// <param name="flat">Whether the edge satisfies the inner-flatness mask.</param>
    /// <param name="outerFlat">Whether the edge satisfies the outer-flatness mask.</param>
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

            // The repeated endpoints extend the normative 13-tap window without reading beyond p6 and q6.
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
    /// Rounds a packed integer while dividing by a power of two.
    /// </summary>
    /// <param name="value">The packed integers to round.</param>
    /// <param name="bitCount">The base-two divisor exponent.</param>
    /// <returns>The rounded packed quotients.</returns>
    private static Vector128<int> RoundPowerOfTwo(Vector128<int> value, int bitCount)
        => (value + Vector128.Create(1 << (bitCount - 1))) >> bitCount;

    /// <summary>
    /// Rounds a scalar integer while dividing by a power of two.
    /// </summary>
    /// <param name="value">The integer to round.</param>
    /// <param name="bitCount">The base-two divisor exponent.</param>
    /// <returns>The rounded quotient.</returns>
    private static int RoundPowerOfTwo(int value, int bitCount)
        => (value + (1 << (bitCount - 1))) >> bitCount;
}
