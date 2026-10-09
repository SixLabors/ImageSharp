// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <summary>
/// Applies the normative AV1 self-guided restoration filter and projection.
/// </summary>
/// <remarks>
/// Every stage walks each row with the widest available register first and finishes with one column at a time. The window sums come
/// from padded integral images kept in the caller-provided filter storage, beside the local coefficients and the two filtered planes.
/// </remarks>
internal static partial class Av1SelfGuidedFilter
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
    /// The base-two exponent used to align work-buffer rows to sixteen 32-bit lanes.
    /// </summary>
    private const int BufferAlignmentLog2 = 4;

    /// <summary>
    /// The extra columns separating the active integral-image rows.
    /// </summary>
    private const int BufferPadding = 16;

    /// <summary>
    /// The complete fixed-point self-guided blend range.
    /// </summary>
    private const int SelfGuidedScale = 1 << SelfGuidedBits;

    /// <summary>
    /// Names the 3x3 coefficient kernel of a filtered row.
    /// </summary>
    private enum FilterKind
    {
        /// <summary>
        /// The radius-one kernel over the complete coefficient grid.
        /// </summary>
        RadiusOne,

        /// <summary>
        /// The radius-two kernel of an even row, over the coefficient rows above and below.
        /// </summary>
        RadiusTwoEven,

        /// <summary>
        /// The radius-two kernel of an odd row, over its own coefficient row.
        /// </summary>
        RadiusTwoOdd,
    }

    /// <summary>
    /// Gets the radii selected by each of the sixteen self-guided parameter sets.
    /// </summary>
    public static ReadOnlySpan<int> ParameterRadii =>
    [
        2, 1, 2, 1, 2, 1, 2, 1,
        2, 1, 2, 1, 2, 1, 2, 1,
        2, 1, 2, 1, 0, 1, 0, 1,
        0, 1, 0, 1, 2, 0, 2, 0,
    ];

    /// <summary>
    /// Gets the variance scales selected by each of the sixteen self-guided parameter sets. A value of -1 marks a radius that the set does not use.
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
    private static ReadOnlySpan<int> XByXPlusOne =>
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
    /// Gets the Q12 reciprocals for every supported square-window area, indexed by the area less one.
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
    /// <returns>The required filter storage length.</returns>
    public static int GetFilterStorageLength(int width, int height)
    {
        int filteredLength = width * height;
        int bufferLength = GetBufferLength(width, height);
        return (filteredLength * 2) + (bufferLength * 4);
    }

    /// <summary>
    /// Filters one processing unit from a source rectangle containing the required three-sample borders.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
    /// <param name="source">The source rectangle beginning three samples above and left of the processing unit.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="destination">The destination span beginning at the restored processing-unit origin.</param>
    /// <param name="destinationStride">The number of samples between destination rows.</param>
    /// <param name="width">The processing-unit width in plane samples.</param>
    /// <param name="height">The processing-unit height in plane samples.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    /// <param name="parameterSetIndex">The decoded self-guided parameter-set index.</param>
    /// <param name="projectionCoefficients">The two transmitted projection coefficients.</param>
    /// <param name="filterStorage">Integer storage sized according to <see cref="GetFilterStorageLength"/>.</param>
    public static void FilterBlock<TSample>(
        ReadOnlySpan<TSample> source,
        int sourceStride,
        Span<TSample> destination,
        int destinationStride,
        int width,
        int height,
        int bitDepth,
        int parameterSetIndex,
        ReadOnlySpan<int> projectionCoefficients,
        Span<int> filterStorage)
        where TSample : unmanaged
    {
        int filteredLength = width * height;
        Span<int> filtered0 = filterStorage[..filteredLength];
        Span<int> filtered1 = filterStorage.Slice(filteredLength, filteredLength);
        GenerateFilters(source, sourceStride, width, height, bitDepth, parameterSetIndex, filtered0, filtered1, filterStorage[(filteredLength * 2)..]);

        ReadOnlySpan<int> radii = ParameterRadii.Slice(parameterSetIndex * 2, 2);
        DecodeProjectionCoefficients(radii, projectionCoefficients, out int projection0, out int projection1);
        ref TSample sourceBase = ref MemoryMarshal.GetReference(source);
        ref TSample destinationBase = ref MemoryMarshal.GetReference(destination);
        ref int filtered0Base = ref MemoryMarshal.GetReference(filtered0);
        ref int filtered1Base = ref MemoryMarshal.GetReference(filtered1);
        ProjectionParameters parameters = new(radii[0] > 0 ? projection0 : 0, radii[1] > 0 ? projection1 : 0, (1 << bitDepth) - 1);
        for (int row = 0; row < height; row++)
        {
            int sourceRowOffset = ((row + Border) * sourceStride) + Border;
            int destinationRowOffset = row * destinationStride;
            int filteredRowOffset = row * width;
            int column = 0;
            if (Vector512.IsHardwareAccelerated)
            {
                for (; column <= width - Vector512<int>.Count; column += Vector512<int>.Count)
                {
                    Project<TSample, Vector512<int>, Vector512LaneOperator>(
                        ref sourceBase, sourceRowOffset + column, ref destinationBase, destinationRowOffset + column, ref filtered0Base, ref filtered1Base, filteredRowOffset + column, parameters);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; column <= width - Vector256<int>.Count; column += Vector256<int>.Count)
                {
                    Project<TSample, Vector256<int>, Vector256LaneOperator>(
                        ref sourceBase, sourceRowOffset + column, ref destinationBase, destinationRowOffset + column, ref filtered0Base, ref filtered1Base, filteredRowOffset + column, parameters);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; column <= width - Vector128<int>.Count; column += Vector128<int>.Count)
                {
                    Project<TSample, Vector128<int>, Vector128LaneOperator>(
                        ref sourceBase, sourceRowOffset + column, ref destinationBase, destinationRowOffset + column, ref filtered0Base, ref filtered1Base, filteredRowOffset + column, parameters);
                }
            }

            for (; column < width; column++)
            {
                Project<TSample, int, ScalarLaneOperator>(
                    ref sourceBase, sourceRowOffset + column, ref destinationBase, destinationRowOffset + column, ref filtered0Base, ref filtered1Base, filteredRowOffset + column, parameters);
            }
        }
    }

    /// <summary>
    /// Produces the two unprojected fixed-point filter results for a processing unit.
    /// </summary>
    /// <typeparam name="TSample">The physical component sample type.</typeparam>
    /// <param name="source">The source unit including its three-sample border.</param>
    /// <param name="sourceStride">The source row stride in samples.</param>
    /// <param name="width">The visible unit width.</param>
    /// <param name="height">The visible unit height.</param>
    /// <param name="bitDepth">The component precision.</param>
    /// <param name="parameterSetIndex">The radius and smoothing parameter pair.</param>
    /// <param name="filtered0">The packed radius-two results, written only when that radius is enabled.</param>
    /// <param name="filtered1">The packed radius-one results, written only when that radius is enabled.</param>
    /// <param name="filterStorage">The coefficient and integral-image workspace, excluding the two filtered planes.</param>
    public static void GenerateFilters<TSample>(
        ReadOnlySpan<TSample> source,
        int sourceStride,
        int width,
        int height,
        int bitDepth,
        int parameterSetIndex,
        Span<int> filtered0,
        Span<int> filtered1,
        Span<int> filterStorage)
        where TSample : unmanaged
    {
        // Search retains these fixed-point values while it changes the projection coefficients.
        // Decoder output uses the same calculation and applies projection once afterward.
        int bufferLength = GetBufferLength(width, height);
        int bufferStride = GetBufferStride(width);
        Span<int> blendFactors = filterStorage[..bufferLength];
        Span<int> localMeans = filterStorage.Slice(bufferLength, bufferLength);
        Span<int> squareIntegral = filterStorage.Slice(bufferLength * 2, bufferLength);
        Span<int> sumIntegral = filterStorage.Slice(bufferLength * 3, bufferLength);

        BuildIntegralImages(source, sourceStride, width + (Border * 2), height + (Border * 2), bufferStride, squareIntegral, sumIntegral);

        int parameterOffset = parameterSetIndex * 2;
        ReadOnlySpan<int> radii = ParameterRadii.Slice(parameterOffset, 2);
        ReadOnlySpan<int> scales = ParameterScales.Slice(parameterOffset, 2);
        if (radii[0] > 0)
        {
            CalculateIntermediateCoefficients(width, height, bitDepth, radii[0], scales[0], true, bufferStride, squareIntegral, sumIntegral, blendFactors, localMeans);
            CalculateFilter(source, sourceStride, width, height, bufferStride, true, blendFactors, localMeans, filtered0);
        }

        if (radii[1] > 0)
        {
            CalculateIntermediateCoefficients(width, height, bitDepth, radii[1], scales[1], false, bufferStride, squareIntegral, sumIntegral, blendFactors, localMeans);
            CalculateFilter(source, sourceStride, width, height, bufferStride, false, blendFactors, localMeans, filtered1);
        }
    }

    /// <summary>
    /// Builds the summed-area tables of the samples and of their squares.
    /// </summary>
    /// <remarks>
    /// Each row is a prefix scan across the batch plus the row above. The carry holds the running row prefix
    /// between batches and passes from each register width to the next. A table entry can wrap in 32 bits for a
    /// twelve-bit unit, but every window sum is a difference of four entries and fits, so it stays exact.
    /// </remarks>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
    /// <param name="source">The complete bordered source rectangle.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="width">The bordered source width.</param>
    /// <param name="height">The bordered source height.</param>
    /// <param name="bufferStride">The padded work-buffer row stride.</param>
    /// <param name="squareIntegral">The destination integral image of squared samples.</param>
    /// <param name="sumIntegral">The destination integral image of samples.</param>
    private static void BuildIntegralImages<TSample>(
        ReadOnlySpan<TSample> source,
        int sourceStride,
        int width,
        int height,
        int bufferStride,
        Span<int> squareIntegral,
        Span<int> sumIntegral)
        where TSample : unmanaged
    {
        squareIntegral[..(width + 1)].Clear();
        sumIntegral[..(width + 1)].Clear();
        ref TSample sourceBase = ref MemoryMarshal.GetReference(source);
        ref int squareBase = ref MemoryMarshal.GetReference(squareIntegral);
        ref int sumBase = ref MemoryMarshal.GetReference(sumIntegral);
        for (int row = 0; row < height; row++)
        {
            IntegralRow integralRow = new(row * sourceStride, (row * bufferStride) + 1, ((row + 1) * bufferStride) + 1);
            squareIntegral[integralRow.Current - 1] = 0;
            sumIntegral[integralRow.Current - 1] = 0;
            int sumCarry = 0;
            int squareCarry = 0;
            int column = 0;
            if (Vector512.IsHardwareAccelerated)
            {
                for (; column <= width - Vector512<int>.Count; column += Vector512<int>.Count)
                {
                    AccumulateIntegral<TSample, Vector512<int>, Vector512LaneOperator>(ref sourceBase, ref sumBase, ref squareBase, integralRow, column, ref sumCarry, ref squareCarry);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; column <= width - Vector256<int>.Count; column += Vector256<int>.Count)
                {
                    AccumulateIntegral<TSample, Vector256<int>, Vector256LaneOperator>(ref sourceBase, ref sumBase, ref squareBase, integralRow, column, ref sumCarry, ref squareCarry);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; column <= width - Vector128<int>.Count; column += Vector128<int>.Count)
                {
                    AccumulateIntegral<TSample, Vector128<int>, Vector128LaneOperator>(ref sourceBase, ref sumBase, ref squareBase, integralRow, column, ref sumCarry, ref squareCarry);
                }
            }

            for (; column < width; column++)
            {
                AccumulateIntegral<TSample, int, ScalarLaneOperator>(ref sourceBase, ref sumBase, ref squareBase, integralRow, column, ref sumCarry, ref squareCarry);
            }
        }
    }

    /// <summary>
    /// Adds one batch of samples to the current integral-image rows.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
    /// <typeparam name="TLanes">The lane type.</typeparam>
    /// <typeparam name="TOperator">The lane arithmetic.</typeparam>
    /// <param name="source">The first sample of the source rectangle.</param>
    /// <param name="sumIntegral">The first entry of the sample integral image.</param>
    /// <param name="squareIntegral">The first entry of the squared-sample integral image.</param>
    /// <param name="row">The offsets of the row.</param>
    /// <param name="column">The first column of the batch.</param>
    /// <param name="sumCarry">The running sample prefix of the row.</param>
    /// <param name="squareCarry">The running squared-sample prefix of the row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AccumulateIntegral<TSample, TLanes, TOperator>(
        ref TSample source,
        ref int sumIntegral,
        ref int squareIntegral,
        IntegralRow row,
        int column,
        ref int sumCarry,
        ref int squareCarry)
        where TSample : unmanaged
        where TLanes : unmanaged
        where TOperator : struct, ILaneOperator<TLanes>
    {
        TLanes samples = TOperator.LoadSamples(ref source, (nuint)(row.Source + column));
        TLanes squares = TOperator.Multiply(samples, samples);
        TLanes sumsAbove = TOperator.Load(ref sumIntegral, (nuint)(row.Previous + column));
        TLanes squaresAbove = TOperator.Load(ref squareIntegral, (nuint)(row.Previous + column));
        TLanes rowSums = TOperator.Add(TOperator.Add(TOperator.Scan(samples), sumsAbove), TOperator.Create(sumCarry));
        TLanes rowSquares = TOperator.Add(TOperator.Add(TOperator.Scan(squares), squaresAbove), TOperator.Create(squareCarry));
        TOperator.Store(rowSums, ref sumIntegral, (nuint)(row.Current + column));
        TOperator.Store(rowSquares, ref squareIntegral, (nuint)(row.Current + column));

        // The last entry less the last entry above is the row prefix through this batch. It is the carry for the next batch.
        sumCarry = TOperator.Last(rowSums) - TOperator.Last(sumsAbove);
        squareCarry = TOperator.Last(rowSquares) - TOperator.Last(squaresAbove);
    }

    /// <summary>
    /// Calculates the local blend factor and mean for the requested filter radius.
    /// </summary>
    /// <param name="width">The processing-unit width in samples.</param>
    /// <param name="height">The processing-unit height in samples.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    /// <param name="radius">The square-window radius.</param>
    /// <param name="scale">The variance scale for the selected parameter set and radius.</param>
    /// <param name="skipAlternateRows">Whether only the rows consumed by the radius-two filter are calculated.</param>
    /// <param name="bufferStride">The padded work-buffer row stride.</param>
    /// <param name="squareIntegral">The integral image of squared samples.</param>
    /// <param name="sumIntegral">The integral image of samples.</param>
    /// <param name="blendFactors">The destination local blend factors.</param>
    /// <param name="localMeans">The destination scaled local means.</param>
    private static void CalculateIntermediateCoefficients(
        int width,
        int height,
        int bitDepth,
        int radius,
        int scale,
        bool skipAlternateRows,
        int bufferStride,
        ReadOnlySpan<int> squareIntegral,
        ReadOnlySpan<int> sumIntegral,
        Span<int> blendFactors,
        Span<int> localMeans)
    {
        int windowDiameter = (radius * 2) + 1;
        int windowArea = windowDiameter * windowDiameter;
        CoefficientParameters parameters = new(bufferStride, radius, bitDepth, windowArea, scale, OneByX[windowArea - 1]);
        int rowStep = skipAlternateRows ? 2 : 1;

        // The work-buffer origin skips the zero row and column of the integral image and the three-sample border.
        int bufferOrigin = (Border + 1) * (bufferStride + 1);
        ref int sumBase = ref MemoryMarshal.GetReference(sumIntegral);
        ref int squareBase = ref MemoryMarshal.GetReference(squareIntegral);
        ref int blendBase = ref MemoryMarshal.GetReference(blendFactors);
        ref int meanBase = ref MemoryMarshal.GetReference(localMeans);

        // The coefficients cover one column and one row beyond each side of the unit, which the 3x3 filter reads.
        for (int row = -1; row < height + 1; row += rowStep)
        {
            int rowOffset = bufferOrigin + (row * bufferStride);
            int column = -1;
            int end = width + 1;
            if (Vector512.IsHardwareAccelerated)
            {
                for (; column <= end - Vector512<int>.Count; column += Vector512<int>.Count)
                {
                    CalculateCoefficients<Vector512<int>, Vector512LaneOperator>(ref sumBase, ref squareBase, ref blendBase, ref meanBase, rowOffset + column, parameters);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; column <= end - Vector256<int>.Count; column += Vector256<int>.Count)
                {
                    CalculateCoefficients<Vector256<int>, Vector256LaneOperator>(ref sumBase, ref squareBase, ref blendBase, ref meanBase, rowOffset + column, parameters);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; column <= end - Vector128<int>.Count; column += Vector128<int>.Count)
                {
                    CalculateCoefficients<Vector128<int>, Vector128LaneOperator>(ref sumBase, ref squareBase, ref blendBase, ref meanBase, rowOffset + column, parameters);
                }
            }

            for (; column < end; column++)
            {
                CalculateCoefficients<int, ScalarLaneOperator>(ref sumBase, ref squareBase, ref blendBase, ref meanBase, rowOffset + column, parameters);
            }
        }
    }

    /// <summary>
    /// Calculates the blend factors and means of one batch of window centers.
    /// </summary>
    /// <remarks>
    /// High bit depths round both window sums to the eight-bit scale before the variance. Rounding can put the squared mean one step
    /// above the mean square. AV1 sets the variance to zero in that case, and the Max operation does the same. The scaled variance and
    /// the mean product are unsigned 32-bit values whose legal range can set the sign bit, so they shift logically.
    /// </remarks>
    /// <typeparam name="TLanes">The lane type.</typeparam>
    /// <typeparam name="TOperator">The lane arithmetic.</typeparam>
    /// <param name="sumIntegral">The first entry of the sample integral image.</param>
    /// <param name="squareIntegral">The first entry of the squared-sample integral image.</param>
    /// <param name="blendFactors">The first entry of the blend factors.</param>
    /// <param name="localMeans">The first entry of the local means.</param>
    /// <param name="centerOffset">The work-buffer offset of the first window center.</param>
    /// <param name="parameters">The window and scale parameters.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateCoefficients<TLanes, TOperator>(
        ref int sumIntegral,
        ref int squareIntegral,
        ref int blendFactors,
        ref int localMeans,
        int centerOffset,
        CoefficientParameters parameters)
        where TLanes : unmanaged
        where TOperator : struct, ILaneOperator<TLanes>
    {
        TLanes sums = BoxSum<TLanes, TOperator>(ref sumIntegral, centerOffset, parameters.Stride, parameters.Radius);
        TLanes squareSums = BoxSum<TLanes, TOperator>(ref squareIntegral, centerOffset, parameters.Stride, parameters.Radius);
        TLanes normalizedSums = sums;
        if (parameters.BitDepth > 8)
        {
            int depthShift = parameters.BitDepth - 8;
            squareSums = TOperator.ShiftRightLogical(TOperator.Add(squareSums, TOperator.Create(1 << ((depthShift * 2) - 1))), depthShift * 2);
            normalizedSums = TOperator.ShiftRightLogical(TOperator.Add(sums, TOperator.Create(1 << (depthShift - 1))), depthShift);
        }

        TLanes squareOfSums = TOperator.Multiply(normalizedSums, normalizedSums);
        TLanes scaledSquareSums = TOperator.Multiply(squareSums, TOperator.Create(parameters.WindowArea));
        TLanes variance = TOperator.Subtract(TOperator.Max(scaledSquareSums, squareOfSums), squareOfSums);

        // The scaled variance rounds away its 20 scale bits and clamps to the last table index.
        TLanes indices = TOperator.MinUnsigned(
            TOperator.ShiftRightLogical(
                TOperator.Add(TOperator.Multiply(variance, TOperator.Create(parameters.Scale)), TOperator.Create(1 << (ScaleBits - 1))),
                ScaleBits),
            TOperator.Create(255));

        // The zero-variance table entry is deliberately one rather than zero. This keeps the complementary factor below
        // 256 and the scaled mean inside its proven range.
        TLanes factors = TOperator.Gather(ref MemoryMarshal.GetReference(XByXPlusOne), indices);
        TLanes meanProducts = TOperator.Multiply(
            TOperator.Multiply(TOperator.Subtract(TOperator.Create(SelfGuidedScale), factors), TOperator.Create(parameters.Reciprocal)),
            sums);

        TLanes means = TOperator.ShiftRightLogical(TOperator.Add(meanProducts, TOperator.Create(1 << (ReciprocalBits - 1))), ReciprocalBits);
        TOperator.Store(factors, ref blendFactors, (nuint)centerOffset);
        TOperator.Store(means, ref localMeans, (nuint)centerOffset);
    }

    /// <summary>
    /// Calculates adjacent square-window sums from one integral image.
    /// </summary>
    /// <typeparam name="TLanes">The lane type.</typeparam>
    /// <typeparam name="TOperator">The lane arithmetic.</typeparam>
    /// <param name="integral">The first entry of the integral image.</param>
    /// <param name="centerOffset">The integral-image offset corresponding to the first window center.</param>
    /// <param name="stride">The integral-image row stride.</param>
    /// <param name="radius">The square-window radius.</param>
    /// <returns>The window sums.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TLanes BoxSum<TLanes, TOperator>(ref int integral, int centerOffset, int stride, int radius)
        where TLanes : unmanaged
        where TOperator : struct, ILaneOperator<TLanes>
    {
        // The four corner entries of the integral image give the window sum by inclusion and exclusion.
        int upperOffset = centerOffset - ((radius + 1) * stride);
        int lowerOffset = centerOffset + (radius * stride);
        TLanes topLeft = TOperator.Load(ref integral, (nuint)(upperOffset - radius - 1));
        TLanes topRight = TOperator.Load(ref integral, (nuint)(upperOffset + radius));
        TLanes bottomLeft = TOperator.Load(ref integral, (nuint)(lowerOffset - radius - 1));
        TLanes bottomRight = TOperator.Load(ref integral, (nuint)(lowerOffset + radius));
        return TOperator.Subtract(TOperator.Subtract(bottomRight, bottomLeft), TOperator.Subtract(topRight, topLeft));
    }

    /// <summary>
    /// Produces the filtered values of one radius from its coefficient grid. The radius-two filter uses the fast form, which computes
    /// coefficients on alternate rows only.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
    /// <param name="source">The bordered processing-unit source rectangle.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="width">The processing-unit width in samples.</param>
    /// <param name="height">The processing-unit height in samples.</param>
    /// <param name="bufferStride">The padded coefficient-buffer row stride.</param>
    /// <param name="radiusTwo">Whether the coefficients belong to the radius-two filter, which fills alternate rows.</param>
    /// <param name="blendFactors">The local sample blend factors.</param>
    /// <param name="localMeans">The scaled local means.</param>
    /// <param name="filtered">The destination fixed-point filtered values.</param>
    private static void CalculateFilter<TSample>(
        ReadOnlySpan<TSample> source,
        int sourceStride,
        int width,
        int height,
        int bufferStride,
        bool radiusTwo,
        ReadOnlySpan<int> blendFactors,
        ReadOnlySpan<int> localMeans,
        Span<int> filtered)
        where TSample : unmanaged
    {
        int bufferOrigin = (Border + 1) * (bufferStride + 1);
        ref TSample sourceBase = ref MemoryMarshal.GetReference(source);
        ref int blendBase = ref MemoryMarshal.GetReference(blendFactors);
        ref int meanBase = ref MemoryMarshal.GetReference(localMeans);
        ref int filteredBase = ref MemoryMarshal.GetReference(filtered);
        for (int row = 0; row < height; row++)
        {
            // The radius-one weights and the even radius-two weights add up to 32. The odd radius-two weights add up to 16.
            // The rounding shift removes that weight sum and the blend-factor scale, and leaves the result in Q4.
            FilterKind kind = !radiusTwo ? FilterKind.RadiusOne : (row & 1) == 0 ? FilterKind.RadiusTwoEven : FilterKind.RadiusTwoOdd;
            FilterRow filterRow = new(
                ((row + Border) * sourceStride) + Border,
                bufferOrigin + (row * bufferStride),
                row * width,
                bufferStride,
                SelfGuidedBits + (kind == FilterKind.RadiusTwoOdd ? 4 : 5) - RestorationBits,
                kind);

            int column = 0;
            if (Vector512.IsHardwareAccelerated)
            {
                for (; column <= width - Vector512<int>.Count; column += Vector512<int>.Count)
                {
                    Filter<TSample, Vector512<int>, Vector512LaneOperator>(ref sourceBase, ref blendBase, ref meanBase, ref filteredBase, filterRow, column);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; column <= width - Vector256<int>.Count; column += Vector256<int>.Count)
                {
                    Filter<TSample, Vector256<int>, Vector256LaneOperator>(ref sourceBase, ref blendBase, ref meanBase, ref filteredBase, filterRow, column);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; column <= width - Vector128<int>.Count; column += Vector128<int>.Count)
                {
                    Filter<TSample, Vector128<int>, Vector128LaneOperator>(ref sourceBase, ref blendBase, ref meanBase, ref filteredBase, filterRow, column);
                }
            }

            for (; column < width; column++)
            {
                Filter<TSample, int, ScalarLaneOperator>(ref sourceBase, ref blendBase, ref meanBase, ref filteredBase, filterRow, column);
            }
        }
    }

    /// <summary>
    /// Filters one batch of samples with the weighted neighborhoods of their coefficients.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
    /// <typeparam name="TLanes">The lane type.</typeparam>
    /// <typeparam name="TOperator">The lane arithmetic.</typeparam>
    /// <param name="source">The first sample of the source rectangle.</param>
    /// <param name="blendFactors">The first entry of the blend factors.</param>
    /// <param name="localMeans">The first entry of the local means.</param>
    /// <param name="filtered">The first entry of the filtered plane.</param>
    /// <param name="row">The offsets and kernel of the row.</param>
    /// <param name="column">The first column of the batch.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Filter<TSample, TLanes, TOperator>(
        ref TSample source,
        ref int blendFactors,
        ref int localMeans,
        ref int filtered,
        FilterRow row,
        int column)
        where TSample : unmanaged
        where TLanes : unmanaged
        where TOperator : struct, ILaneOperator<TLanes>
    {
        int offset = row.Coefficients + column;
        TLanes factors = CrossSum<TLanes, TOperator>(ref blendFactors, offset, row.Stride, row.Kind);
        TLanes means = CrossSum<TLanes, TOperator>(ref localMeans, offset, row.Stride, row.Kind);
        TLanes samples = TOperator.LoadSamples(ref source, (nuint)(row.Source + column));
        TLanes value = TOperator.Add(TOperator.Add(TOperator.Multiply(factors, samples), means), TOperator.Create(1 << (row.RoundingBits - 1)));
        TOperator.Store(TOperator.ShiftRightArithmetic(value, row.RoundingBits), ref filtered, (nuint)(row.Filtered + column));
    }

    /// <summary>
    /// Weighs the 3x3 coefficient neighborhoods of adjacent centers.
    /// </summary>
    /// <remarks>
    /// The radius-one kernel weighs the cross by four and the corners by three. The radius-two kernel reads only the coefficient rows
    /// that it computed. For an even row, it reads the rows above and below, with six for the centers and five for the corners. For an
    /// odd row, it reads its own row, with six for the center and five for the sides.
    /// </remarks>
    /// <typeparam name="TLanes">The lane type.</typeparam>
    /// <typeparam name="TOperator">The lane arithmetic.</typeparam>
    /// <param name="buffer">The first entry of the coefficient buffer.</param>
    /// <param name="offset">The first center coefficient.</param>
    /// <param name="stride">The coefficient-buffer row stride.</param>
    /// <param name="kind">The kernel of the row.</param>
    /// <returns>The weighted sums.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TLanes CrossSum<TLanes, TOperator>(ref int buffer, int offset, int stride, FilterKind kind)
        where TLanes : unmanaged
        where TOperator : struct, ILaneOperator<TLanes>
    {
        if (kind == FilterKind.RadiusTwoOdd)
        {
            TLanes center = TOperator.Load(ref buffer, (nuint)offset);
            TLanes combined = TOperator.Add(TOperator.Add(TOperator.Load(ref buffer, (nuint)(offset - 1)), center), TOperator.Load(ref buffer, (nuint)(offset + 1)));

            // Five times the three entries plus one more center gives six for the center and five for the sides.
            return TOperator.Add(TOperator.Add(TOperator.ShiftLeft(combined, 2), combined), center);
        }

        TLanes topLeft = TOperator.Load(ref buffer, (nuint)(offset - stride - 1));
        TLanes top = TOperator.Load(ref buffer, (nuint)(offset - stride));
        TLanes topRight = TOperator.Load(ref buffer, (nuint)(offset - stride + 1));
        TLanes bottomLeft = TOperator.Load(ref buffer, (nuint)(offset + stride - 1));
        TLanes bottom = TOperator.Load(ref buffer, (nuint)(offset + stride));
        TLanes bottomRight = TOperator.Load(ref buffer, (nuint)(offset + stride + 1));
        TLanes corners = TOperator.Add(TOperator.Add(topLeft, topRight), TOperator.Add(bottomLeft, bottomRight));
        if (kind == FilterKind.RadiusTwoEven)
        {
            TLanes centers = TOperator.Add(top, bottom);
            TLanes combinedRows = TOperator.Add(corners, centers);

            // Five times the six entries plus the centers again gives six for the centers and five for the corners.
            return TOperator.Add(TOperator.Add(TOperator.ShiftLeft(combinedRows, 2), combinedRows), centers);
        }

        TLanes middle = TOperator.Add(
            TOperator.Add(TOperator.Load(ref buffer, (nuint)(offset - 1)), TOperator.Load(ref buffer, (nuint)offset)),
            TOperator.Load(ref buffer, (nuint)(offset + 1)));

        TLanes remainder = TOperator.Add(TOperator.Add(top, bottom), middle);

        // Four times all nine entries less the corners gives four for the cross and three for the corners.
        return TOperator.Subtract(TOperator.ShiftLeft(TOperator.Add(corners, remainder), 2), corners);
    }

    /// <summary>
    /// Projects one batch of samples onto the two restored signals and stores the clipped result.
    /// </summary>
    /// <remarks>
    /// Filtered signals use Q4 precision. Projection applies the signaled Q7 weights to their difference from the
    /// unfiltered Q4 sample, then rounds by the combined eleven bits once before clipping. A disabled radius has a zero
    /// weight, which adds nothing.
    /// </remarks>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
    /// <typeparam name="TLanes">The lane type.</typeparam>
    /// <typeparam name="TOperator">The lane arithmetic.</typeparam>
    /// <param name="source">The first sample of the source rectangle.</param>
    /// <param name="sourceOffset">The source index of the first sample.</param>
    /// <param name="destination">The first sample of the destination.</param>
    /// <param name="destinationOffset">The destination index of the first sample.</param>
    /// <param name="filtered0">The first entry of the radius-two filtered plane.</param>
    /// <param name="filtered1">The first entry of the radius-one filtered plane.</param>
    /// <param name="filteredOffset">The filtered-plane index of the first sample.</param>
    /// <param name="parameters">The projection weights and sample range.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Project<TSample, TLanes, TOperator>(
        ref TSample source,
        int sourceOffset,
        ref TSample destination,
        int destinationOffset,
        ref int filtered0,
        ref int filtered1,
        int filteredOffset,
        ProjectionParameters parameters)
        where TSample : unmanaged
        where TLanes : unmanaged
        where TOperator : struct, ILaneOperator<TLanes>
    {
        const int projectionShift = ProjectionBits + RestorationBits;
        TLanes unfiltered = TOperator.ShiftLeft(TOperator.LoadSamples(ref source, (nuint)sourceOffset), RestorationBits);
        TLanes projected = TOperator.ShiftLeft(unfiltered, ProjectionBits);
        if (parameters.Projection0 != 0)
        {
            TLanes restored = TOperator.Load(ref filtered0, (nuint)filteredOffset);
            projected = TOperator.Add(projected, TOperator.Multiply(TOperator.Create(parameters.Projection0), TOperator.Subtract(restored, unfiltered)));
        }

        if (parameters.Projection1 != 0)
        {
            TLanes restored = TOperator.Load(ref filtered1, (nuint)filteredOffset);
            projected = TOperator.Add(projected, TOperator.Multiply(TOperator.Create(parameters.Projection1), TOperator.Subtract(restored, unfiltered)));
        }

        TLanes result = TOperator.ShiftRightArithmetic(TOperator.Add(projected, TOperator.Create(1 << (projectionShift - 1))), projectionShift);
        result = TOperator.Min(TOperator.Max(result, TOperator.Create(0)), TOperator.Create(parameters.MaximumSample));
        TOperator.StoreSamples(result, ref destination, (nuint)destinationOffset);
    }

    /// <summary>
    /// Decodes the transmitted projection coefficients for the active radius pair.
    /// </summary>
    /// <param name="radii">The two selected filter radii.</param>
    /// <param name="transmitted">The two transmitted projection coefficients.</param>
    /// <param name="projection0">The first decoded projection coefficient.</param>
    /// <param name="projection1">The second decoded projection coefficient.</param>
    private static void DecodeProjectionCoefficients(ReadOnlySpan<int> radii, ReadOnlySpan<int> transmitted, out int projection0, out int projection1)
    {
        if (radii[0] == 0)
        {
            projection0 = 0;
            projection1 = (1 << ProjectionBits) - transmitted[1];
        }
        else if (radii[1] == 0)
        {
            projection0 = transmitted[0];
            projection1 = 0;
        }
        else
        {
            projection0 = transmitted[0];
            projection1 = (1 << ProjectionBits) - projection0 - transmitted[1];
        }
    }

    /// <summary>
    /// Gets the padded row stride shared by coefficient and integral-image buffers.
    /// </summary>
    /// <param name="width">The filtered processing-unit width.</param>
    /// <returns>The aligned number of integers reserved for each work-buffer row.</returns>
    private static int GetBufferStride(int width)
    {
        // The stride includes the border samples and the row separation before it aligns to the widest batch.
        // The coefficient and integral views share this stride.
        return Av1Math.AlignPowerOf2(width + (Border * 2) + BufferPadding, BufferAlignmentLog2);
    }

    /// <summary>
    /// Gets the number of integers reserved for one padded work buffer.
    /// </summary>
    /// <param name="width">The filtered processing-unit width.</param>
    /// <param name="height">The filtered processing-unit height.</param>
    /// <returns>The required work-buffer length.</returns>
    private static int GetBufferLength(int width, int height)
        => GetBufferStride(width) * (height + (Border * 2) + 1);

    /// <summary>
    /// Holds the offsets of one integral-image row.
    /// </summary>
    private readonly struct IntegralRow
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IntegralRow"/> struct.
        /// </summary>
        /// <param name="source">The source index of the row's first sample.</param>
        /// <param name="previous">The integral-image index of the row above, at the entry after its zero column.</param>
        /// <param name="current">The integral-image index of the row, at the entry after its zero column.</param>
        public IntegralRow(int source, int previous, int current)
        {
            this.Source = source;
            this.Previous = previous;
            this.Current = current;
        }

        /// <summary>
        /// Gets the source index of the row's first sample.
        /// </summary>
        public int Source { get; }

        /// <summary>
        /// Gets the integral-image index of the row above, at the entry after its zero column.
        /// </summary>
        public int Previous { get; }

        /// <summary>
        /// Gets the integral-image index of the row, at the entry after its zero column.
        /// </summary>
        public int Current { get; }
    }

    /// <summary>
    /// Holds the window and scale parameters of one coefficient pass.
    /// </summary>
    private readonly struct CoefficientParameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CoefficientParameters"/> struct.
        /// </summary>
        /// <param name="stride">The work-buffer row stride.</param>
        /// <param name="radius">The square-window radius.</param>
        /// <param name="bitDepth">The encoded sample bit depth.</param>
        /// <param name="windowArea">The square-window area.</param>
        /// <param name="scale">The variance scale.</param>
        /// <param name="reciprocal">The fixed-point reciprocal of the window area.</param>
        public CoefficientParameters(int stride, int radius, int bitDepth, int windowArea, int scale, int reciprocal)
        {
            this.Stride = stride;
            this.Radius = radius;
            this.BitDepth = bitDepth;
            this.WindowArea = windowArea;
            this.Scale = scale;
            this.Reciprocal = reciprocal;
        }

        /// <summary>
        /// Gets the work-buffer row stride.
        /// </summary>
        public int Stride { get; }

        /// <summary>
        /// Gets the square-window radius.
        /// </summary>
        public int Radius { get; }

        /// <summary>
        /// Gets the encoded sample bit depth.
        /// </summary>
        public int BitDepth { get; }

        /// <summary>
        /// Gets the square-window area.
        /// </summary>
        public int WindowArea { get; }

        /// <summary>
        /// Gets the variance scale.
        /// </summary>
        public int Scale { get; }

        /// <summary>
        /// Gets the fixed-point reciprocal of the window area.
        /// </summary>
        public int Reciprocal { get; }
    }

    /// <summary>
    /// Holds the offsets and kernel of one filtered row.
    /// </summary>
    private readonly struct FilterRow
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FilterRow"/> struct.
        /// </summary>
        /// <param name="source">The source index of the row's first unit sample.</param>
        /// <param name="coefficients">The coefficient index of the row's first center.</param>
        /// <param name="filtered">The filtered-plane index of the row's first value.</param>
        /// <param name="stride">The coefficient-buffer row stride.</param>
        /// <param name="roundingBits">The rounding shift of the row's kernel.</param>
        /// <param name="kind">The kernel of the row.</param>
        public FilterRow(int source, int coefficients, int filtered, int stride, int roundingBits, FilterKind kind)
        {
            this.Source = source;
            this.Coefficients = coefficients;
            this.Filtered = filtered;
            this.Stride = stride;
            this.RoundingBits = roundingBits;
            this.Kind = kind;
        }

        /// <summary>
        /// Gets the source index of the row's first unit sample.
        /// </summary>
        public int Source { get; }

        /// <summary>
        /// Gets the coefficient index of the row's first center.
        /// </summary>
        public int Coefficients { get; }

        /// <summary>
        /// Gets the filtered-plane index of the row's first value.
        /// </summary>
        public int Filtered { get; }

        /// <summary>
        /// Gets the coefficient-buffer row stride.
        /// </summary>
        public int Stride { get; }

        /// <summary>
        /// Gets the rounding shift of the row's kernel.
        /// </summary>
        public int RoundingBits { get; }

        /// <summary>
        /// Gets the kernel of the row.
        /// </summary>
        public FilterKind Kind { get; }
    }

    /// <summary>
    /// Holds the decoded projection weights and the sample range.
    /// </summary>
    private readonly struct ProjectionParameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectionParameters"/> struct.
        /// </summary>
        /// <param name="projection0">The radius-two weight, or zero when that radius is disabled.</param>
        /// <param name="projection1">The radius-one weight, or zero when that radius is disabled.</param>
        /// <param name="maximumSample">The largest sample value.</param>
        public ProjectionParameters(int projection0, int projection1, int maximumSample)
        {
            this.Projection0 = projection0;
            this.Projection1 = projection1;
            this.MaximumSample = maximumSample;
        }

        /// <summary>
        /// Gets the radius-two weight, or zero when that radius is disabled.
        /// </summary>
        public int Projection0 { get; }

        /// <summary>
        /// Gets the radius-one weight, or zero when that radius is disabled.
        /// </summary>
        public int Projection1 { get; }

        /// <summary>
        /// Gets the largest sample value.
        /// </summary>
        public int MaximumSample { get; }
    }
}
