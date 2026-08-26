// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <content>
/// Provides the 128- and 256-bit self-guided restoration pipelines. Consecutive lanes represent neighboring output
/// columns throughout integral-image construction, coefficient generation, filtering, and projection. Each vector
/// loop passes its final horizontal prefix to the scalar tail, preserving one continuous summed-area row without
/// recomputing already processed samples.
/// </content>
internal static partial class Av1SelfGuidedFilter
{
    /// <summary>
    /// Applies self-guided restoration with the AVX2 traversal used by libaom.
    /// </summary>
    /// <param name="source">The bordered processing-unit source rectangle.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="destination">The destination processing-unit rectangle.</param>
    /// <param name="destinationStride">The number of samples between destination rows.</param>
    /// <param name="width">The processing-unit width in samples.</param>
    /// <param name="height">The processing-unit height in samples.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    /// <param name="parameterSetIndex">The decoded self-guided parameter-set index.</param>
    /// <param name="projectionCoefficients">The two transmitted projection coefficients.</param>
    /// <param name="scratch">The caller-owned work storage.</param>
    /// <param name="vector">The overload-selection value.</param>
    private static void FilterBlock(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        int bitDepth,
        int parameterSetIndex,
        ReadOnlySpan<int> projectionCoefficients,
        Span<int> scratch,
        Vector256<int> vector)
    {
        // The caller-owned span contains two visible filtered planes followed by four identically strided coefficient
        // planes. Keeping these regions disjoint allows projection to read either radius after the integral buffers
        // have been reused as immutable inputs, without per-unit allocation or copying.
        int filteredLength = width * height;
        int bufferLength = GetBufferLength(width, height);
        int bufferStride = GetBufferStride(width);
        Span<int> filtered0 = scratch[..filteredLength];
        Span<int> filtered1 = scratch.Slice(filteredLength, filteredLength);
        Span<int> blendFactors = scratch.Slice(filteredLength * 2, bufferLength);
        Span<int> localMeans = scratch.Slice((filteredLength * 2) + bufferLength, bufferLength);
        Span<int> squareIntegral = scratch.Slice((filteredLength * 2) + (bufferLength * 2), bufferLength);
        Span<int> sumIntegral = scratch.Slice((filteredLength * 2) + (bufferLength * 3), bufferLength);

        BuildIntegralImages(source, sourceStride, width + (Border * 2), height + (Border * 2), bufferStride, squareIntegral, sumIntegral, vector);

        int parameterOffset = parameterSetIndex * 2;
        ReadOnlySpan<int> radii = ParameterRadii.Slice(parameterOffset, 2);
        ReadOnlySpan<int> scales = ParameterScales.Slice(parameterOffset, 2);
        if (radii[0] > 0)
        {
            CalculateIntermediateCoefficients(
                width,
                height,
                bitDepth,
                radii[0],
                scales[0],
                skipAlternateRows: true,
                bufferStride,
                squareIntegral,
                sumIntegral,
                blendFactors,
                localMeans,
                vector);

            CalculateRadiusTwoFilter(source, sourceStride, width, height, bufferStride, blendFactors, localMeans, filtered0, vector);
        }

        if (radii[1] > 0)
        {
            CalculateIntermediateCoefficients(
                width,
                height,
                bitDepth,
                radii[1],
                scales[1],
                skipAlternateRows: false,
                bufferStride,
                squareIntegral,
                sumIntegral,
                blendFactors,
                localMeans,
                vector);

            CalculateRadiusOneFilter(source, sourceStride, width, height, bufferStride, blendFactors, localMeans, filtered1, vector);
        }

        DecodeProjectionCoefficients(radii, projectionCoefficients, out int projection0, out int projection1);
        Project(
            source,
            sourceStride,
            destination,
            destinationStride,
            width,
            height,
            bitDepth,
            radii,
            projection0,
            projection1,
            filtered0,
            filtered1,
            vector);
    }

    /// <summary>
    /// Applies self-guided restoration with the cross-platform 128-bit traversal.
    /// </summary>
    /// <param name="source">The bordered processing-unit source rectangle.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="destination">The destination processing-unit rectangle.</param>
    /// <param name="destinationStride">The number of samples between destination rows.</param>
    /// <param name="width">The processing-unit width in samples.</param>
    /// <param name="height">The processing-unit height in samples.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    /// <param name="parameterSetIndex">The decoded self-guided parameter-set index.</param>
    /// <param name="projectionCoefficients">The two transmitted projection coefficients.</param>
    /// <param name="scratch">The caller-owned work storage.</param>
    /// <param name="vector">The overload-selection value.</param>
    private static void FilterBlock(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        int bitDepth,
        int parameterSetIndex,
        ReadOnlySpan<int> projectionCoefficients,
        Span<int> scratch,
        Vector128<int> vector)
    {
        // Use the same scratch partition as the 256-bit path. Vector width changes only the number of adjacent columns
        // advanced by each stage; all offsets and fixed-point representations remain identical.
        int filteredLength = width * height;
        int bufferLength = GetBufferLength(width, height);
        int bufferStride = GetBufferStride(width);
        Span<int> filtered0 = scratch[..filteredLength];
        Span<int> filtered1 = scratch.Slice(filteredLength, filteredLength);
        Span<int> blendFactors = scratch.Slice(filteredLength * 2, bufferLength);
        Span<int> localMeans = scratch.Slice((filteredLength * 2) + bufferLength, bufferLength);
        Span<int> squareIntegral = scratch.Slice((filteredLength * 2) + (bufferLength * 2), bufferLength);
        Span<int> sumIntegral = scratch.Slice((filteredLength * 2) + (bufferLength * 3), bufferLength);

        BuildIntegralImages(source, sourceStride, width + (Border * 2), height + (Border * 2), bufferStride, squareIntegral, sumIntegral, vector);

        int parameterOffset = parameterSetIndex * 2;
        ReadOnlySpan<int> radii = ParameterRadii.Slice(parameterOffset, 2);
        ReadOnlySpan<int> scales = ParameterScales.Slice(parameterOffset, 2);
        if (radii[0] > 0)
        {
            CalculateIntermediateCoefficients(
                width,
                height,
                bitDepth,
                radii[0],
                scales[0],
                skipAlternateRows: true,
                bufferStride,
                squareIntegral,
                sumIntegral,
                blendFactors,
                localMeans,
                vector);

            CalculateRadiusTwoFilter(source, sourceStride, width, height, bufferStride, blendFactors, localMeans, filtered0, vector);
        }

        if (radii[1] > 0)
        {
            CalculateIntermediateCoefficients(
                width,
                height,
                bitDepth,
                radii[1],
                scales[1],
                skipAlternateRows: false,
                bufferStride,
                squareIntegral,
                sumIntegral,
                blendFactors,
                localMeans,
                vector);

            CalculateRadiusOneFilter(source, sourceStride, width, height, bufferStride, blendFactors, localMeans, filtered1, vector);
        }

        DecodeProjectionCoefficients(radii, projectionCoefficients, out int projection0, out int projection1);
        Project(
            source,
            sourceStride,
            destination,
            destinationStride,
            width,
            height,
            bitDepth,
            radii,
            projection0,
            projection1,
            filtered0,
            filtered1,
            vector);
    }

    /// <summary>
    /// Builds the summed-area tables consumed by the AVX2 coefficient stage.
    /// </summary>
    /// <param name="source">The complete bordered source rectangle.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="width">The bordered source width.</param>
    /// <param name="height">The bordered source height.</param>
    /// <param name="bufferStride">The padded work-buffer row stride.</param>
    /// <param name="squareIntegral">The destination integral image of squared samples.</param>
    /// <param name="sumIntegral">The destination integral image of samples.</param>
    /// <param name="vector">The overload-selection value.</param>
    private static void BuildIntegralImages(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int width,
        int height,
        int bufferStride,
        Span<int> squareIntegral,
        Span<int> sumIntegral,
        Vector256<int> vector)
    {
        squareIntegral[..(width + 1)].Clear();
        sumIntegral[..(width + 1)].Clear();
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref int squareBase = ref MemoryMarshal.GetReference(squareIntegral);
        ref int sumBase = ref MemoryMarshal.GetReference(sumIntegral);

        for (int row = 0; row < height; row++)
        {
            int sourceRowOffset = row * sourceStride;
            int previousRowOffset = row * bufferStride;
            int currentRowOffset = previousRowOffset + bufferStride;
            squareIntegral[currentRowOffset] = 0;
            sumIntegral[currentRowOffset] = 0;
            Vector256<int> squareCarry = Vector256<int>.Zero;
            Vector256<int> sumCarry = Vector256<int>.Zero;
            int column = 0;
            int vectorEnd = width - Vector256<int>.Count;
            for (; column <= vectorEnd; column += Vector256<int>.Count)
            {
                // Eight packed 16-bit samples become eight 32-bit lanes. The prefix scans mirror
                // libaom's scan_32, and the replicated carry joins consecutive vector batches.
                Vector128<ushort> packed = Vector128.LoadUnsafe(ref sourceBase, (nuint)(sourceRowOffset + column));
                Vector256<int> samples = Avx2.ConvertToVector256Int32(packed);
                Vector256<int> squares = samples * samples;
                Vector256<int> scannedSums = Scan(samples);
                Vector256<int> scannedSquares = Scan(squares);
                Vector256<int> sumsAbove = Vector256.LoadUnsafe(ref sumBase, (nuint)(previousRowOffset + column + 1));
                Vector256<int> squaresAbove = Vector256.LoadUnsafe(ref squareBase, (nuint)(previousRowOffset + column + 1));
                Vector256<int> rowSums = scannedSums + sumsAbove + sumCarry;
                Vector256<int> rowSquares = scannedSquares + squaresAbove + squareCarry;
                rowSums.StoreUnsafe(ref sumBase, (nuint)(currentRowOffset + column + 1));
                rowSquares.StoreUnsafe(ref squareBase, (nuint)(currentRowOffset + column + 1));

                sumCarry = Vector256.Create(rowSums.GetElement(Vector256<int>.Count - 1) - sumsAbove.GetElement(Vector256<int>.Count - 1));
                squareCarry = Vector256.Create(rowSquares.GetElement(Vector256<int>.Count - 1) - squaresAbove.GetElement(Vector256<int>.Count - 1));
            }

            int runningSum = sumCarry.GetElement(0);
            int runningSquareSum = squareCarry.GetElement(0);
            for (; column < width; column++)
            {
                int sample = source[sourceRowOffset + column];
                runningSum += sample;
                runningSquareSum += sample * sample;
                sumIntegral[currentRowOffset + column + 1] = sumIntegral[previousRowOffset + column + 1] + runningSum;
                squareIntegral[currentRowOffset + column + 1] = squareIntegral[previousRowOffset + column + 1] + runningSquareSum;
            }
        }
    }

    /// <summary>
    /// Builds the summed-area tables consumed by the cross-platform coefficient stage.
    /// </summary>
    /// <param name="source">The complete bordered source rectangle.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="width">The bordered source width.</param>
    /// <param name="height">The bordered source height.</param>
    /// <param name="bufferStride">The padded work-buffer row stride.</param>
    /// <param name="squareIntegral">The destination integral image of squared samples.</param>
    /// <param name="sumIntegral">The destination integral image of samples.</param>
    /// <param name="vector">The overload-selection value.</param>
    private static void BuildIntegralImages(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int width,
        int height,
        int bufferStride,
        Span<int> squareIntegral,
        Span<int> sumIntegral,
        Vector128<int> vector)
    {
        squareIntegral[..(width + 1)].Clear();
        sumIntegral[..(width + 1)].Clear();
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref int squareBase = ref MemoryMarshal.GetReference(squareIntegral);
        ref int sumBase = ref MemoryMarshal.GetReference(sumIntegral);

        for (int row = 0; row < height; row++)
        {
            int sourceRowOffset = row * sourceStride;
            int previousRowOffset = row * bufferStride;
            int currentRowOffset = previousRowOffset + bufferStride;
            squareIntegral[currentRowOffset] = 0;
            sumIntegral[currentRowOffset] = 0;
            Vector128<int> squareCarry = Vector128<int>.Zero;
            Vector128<int> sumCarry = Vector128<int>.Zero;
            int column = 0;
            int vectorEnd = width - Vector128<int>.Count;
            for (; column <= vectorEnd; column += Vector128<int>.Count)
            {
                // Loading through Vector64 avoids reading beyond the four samples owned by this
                // batch. Widening is normalized by the runtime for both x86 and Arm64 targets.
                ref ushort sourceReference = ref Unsafe.Add(ref sourceBase, sourceRowOffset + column);
                Vector64<ushort> packed = Unsafe.As<ushort, Vector64<ushort>>(ref sourceReference);
                Vector128<int> samples = Vector128.WidenLower(Vector128.Create(packed, Vector64<ushort>.Zero)).AsInt32();
                Vector128<int> squares = samples * samples;
                Vector128<int> scannedSums = Scan(samples);
                Vector128<int> scannedSquares = Scan(squares);
                Vector128<int> sumsAbove = Vector128.LoadUnsafe(ref sumBase, (nuint)(previousRowOffset + column + 1));
                Vector128<int> squaresAbove = Vector128.LoadUnsafe(ref squareBase, (nuint)(previousRowOffset + column + 1));
                Vector128<int> rowSums = scannedSums + sumsAbove + sumCarry;
                Vector128<int> rowSquares = scannedSquares + squaresAbove + squareCarry;
                rowSums.StoreUnsafe(ref sumBase, (nuint)(currentRowOffset + column + 1));
                rowSquares.StoreUnsafe(ref squareBase, (nuint)(currentRowOffset + column + 1));

                sumCarry = Vector128.Create(rowSums.GetElement(Vector128<int>.Count - 1) - sumsAbove.GetElement(Vector128<int>.Count - 1));
                squareCarry = Vector128.Create(rowSquares.GetElement(Vector128<int>.Count - 1) - squaresAbove.GetElement(Vector128<int>.Count - 1));
            }

            int runningSum = sumCarry.GetElement(0);
            int runningSquareSum = squareCarry.GetElement(0);
            for (; column < width; column++)
            {
                int sample = source[sourceRowOffset + column];
                runningSum += sample;
                runningSquareSum += sample * sample;
                sumIntegral[currentRowOffset + column + 1] = sumIntegral[previousRowOffset + column + 1] + runningSum;
                squareIntegral[currentRowOffset + column + 1] = squareIntegral[previousRowOffset + column + 1] + runningSquareSum;
            }
        }
    }

    /// <summary>
    /// Computes inclusive prefix sums for eight 32-bit lanes.
    /// </summary>
    /// <param name="values">The independent input values.</param>
    /// <returns>The inclusive prefix sum in each lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> Scan(Vector256<int> values)
    {
        // AVX2 byte shifts operate independently on the two 128-bit halves. After the two
        // within-half scans, the lower-half total is added to every lane of the upper half.
        Vector256<int> scan = values + Avx2.ShiftLeftLogical128BitLane(values.AsByte(), sizeof(int)).AsInt32();
        scan += Avx2.ShiftLeftLogical128BitLane(scan.AsByte(), sizeof(int) * 2).AsInt32();
        Vector256<int> lowerTotal = Vector256.Create(Vector128<int>.Zero, Vector128.Create(scan.GetElement(3)));
        return scan + lowerTotal;
    }

    /// <summary>
    /// Computes inclusive prefix sums for four 32-bit lanes.
    /// </summary>
    /// <param name="values">The independent input values.</param>
    /// <returns>The inclusive prefix sum in each lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> Scan(Vector128<int> values)
    {
        // The portable shuffle is required here because its out-of-range indices produce zero.
        // ShuffleNative may mask those indices and wrap them back into the input on some ISAs.
        Vector128<int> scan = values + Vector128.Shuffle(values, Vector128.Create(4, 0, 1, 2));
        return scan + Vector128.Shuffle(scan, Vector128.Create(4, 4, 0, 1));
    }

    /// <summary>
    /// Calculates the coefficient grid in eight-sample AVX2 batches.
    /// </summary>
    /// <param name="width">The processing-unit width in samples.</param>
    /// <param name="height">The processing-unit height in samples.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    /// <param name="radius">The square-window radius.</param>
    /// <param name="scale">The variance scale for the selected parameter set.</param>
    /// <param name="skipAlternateRows">Whether only alternate coefficient rows are required.</param>
    /// <param name="bufferStride">The padded work-buffer row stride.</param>
    /// <param name="squareIntegral">The integral image of squared samples.</param>
    /// <param name="sumIntegral">The integral image of samples.</param>
    /// <param name="blendFactors">The destination local blend factors.</param>
    /// <param name="localMeans">The destination scaled local means.</param>
    /// <param name="vector">The overload-selection value.</param>
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
        Span<int> localMeans,
        Vector256<int> vector)
    {
        int windowDiameter = (radius * 2) + 1;
        int windowArea = windowDiameter * windowDiameter;
        int reciprocal = OneByX[windowArea - 1];
        int rowStep = skipAlternateRows ? 2 : 1;
        int bufferOrigin = (Border + 1) * (bufferStride + 1);
        Vector256<int> windowAreaVector = Vector256.Create(windowArea);
        Vector256<uint> scaleVector = Vector256.Create((uint)scale);
        Vector256<int> reciprocalVector = Vector256.Create(reciprocal);
        Vector256<uint> varianceRounding = Vector256.Create(1U << (ScaleBits - 1));
        Vector256<uint> meanRounding = Vector256.Create(1U << (ReciprocalBits - 1));
        Vector256<uint> maximumTableIndex = Vector256.Create(255U);
        Vector256<int> selfGuidedScale = Vector256.Create(SelfGuidedScale);
        ref int blendBase = ref MemoryMarshal.GetReference(blendFactors);
        ref int meanBase = ref MemoryMarshal.GetReference(localMeans);

        for (int row = -1; row < height + 1; row += rowStep)
        {
            int column = -1;
            int remaining = width + 2;
            for (; remaining >= Vector256<int>.Count; column += Vector256<int>.Count, remaining -= Vector256<int>.Count)
            {
                Vector256<int> sums = BoxSum(sumIntegral, bufferOrigin + (row * bufferStride) + column, bufferStride, radius, vector);
                Vector256<int> squareSums = BoxSum(squareIntegral, bufferOrigin + (row * bufferStride) + column, bufferStride, radius, vector);
                Vector256<uint> variance = CalculateVariance(sums, squareSums, bitDepth, windowAreaVector, vector);

                // The fixed-point product is intentionally unsigned. Its legal range can set the
                // sign bit even though the normative value remains a non-negative 32-bit integer.
                Vector256<uint> tableIndices = Vector256.Min(
                    Vector256.ShiftRightLogical((variance * scaleVector) + varianceRounding, ScaleBits),
                    maximumTableIndex);

                Vector256<int> factors = LookupBlendFactors(tableIndices);
                Vector256<uint> meanProducts = ((selfGuidedScale - factors) * reciprocalVector * sums).AsUInt32();
                Vector256<int> means = Vector256.ShiftRightLogical(meanProducts + meanRounding, ReciprocalBits).AsInt32();
                int coefficientOffset = bufferOrigin + (row * bufferStride) + column;
                factors.StoreUnsafe(ref blendBase, (nuint)coefficientOffset);
                means.StoreUnsafe(ref meanBase, (nuint)coefficientOffset);
            }

            for (; remaining > 0; column++, remaining--)
            {
                CalculateIntermediateCoefficient(
                    squareIntegral,
                    sumIntegral,
                    bufferOrigin + (row * bufferStride) + column,
                    bufferStride,
                    bitDepth,
                    radius,
                    windowArea,
                    scale,
                    reciprocal,
                    out int blendFactor,
                    out int localMean);

                int coefficientOffset = bufferOrigin + (row * bufferStride) + column;
                blendFactors[coefficientOffset] = blendFactor;
                localMeans[coefficientOffset] = localMean;
            }
        }
    }

    /// <summary>
    /// Calculates the coefficient grid in four-sample cross-platform batches.
    /// </summary>
    /// <param name="width">The processing-unit width in samples.</param>
    /// <param name="height">The processing-unit height in samples.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    /// <param name="radius">The square-window radius.</param>
    /// <param name="scale">The variance scale for the selected parameter set.</param>
    /// <param name="skipAlternateRows">Whether only alternate coefficient rows are required.</param>
    /// <param name="bufferStride">The padded work-buffer row stride.</param>
    /// <param name="squareIntegral">The integral image of squared samples.</param>
    /// <param name="sumIntegral">The integral image of samples.</param>
    /// <param name="blendFactors">The destination local blend factors.</param>
    /// <param name="localMeans">The destination scaled local means.</param>
    /// <param name="vector">The overload-selection value.</param>
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
        Span<int> localMeans,
        Vector128<int> vector)
    {
        int windowDiameter = (radius * 2) + 1;
        int windowArea = windowDiameter * windowDiameter;
        int reciprocal = OneByX[windowArea - 1];
        int rowStep = skipAlternateRows ? 2 : 1;
        int bufferOrigin = (Border + 1) * (bufferStride + 1);
        Vector128<int> windowAreaVector = Vector128.Create(windowArea);
        Vector128<uint> scaleVector = Vector128.Create((uint)scale);
        Vector128<int> reciprocalVector = Vector128.Create(reciprocal);
        Vector128<uint> varianceRounding = Vector128.Create(1U << (ScaleBits - 1));
        Vector128<uint> meanRounding = Vector128.Create(1U << (ReciprocalBits - 1));
        Vector128<uint> maximumTableIndex = Vector128.Create(255U);
        Vector128<int> selfGuidedScale = Vector128.Create(SelfGuidedScale);
        ref int blendBase = ref MemoryMarshal.GetReference(blendFactors);
        ref int meanBase = ref MemoryMarshal.GetReference(localMeans);

        for (int row = -1; row < height + 1; row += rowStep)
        {
            int column = -1;
            int remaining = width + 2;
            for (; remaining >= Vector128<int>.Count; column += Vector128<int>.Count, remaining -= Vector128<int>.Count)
            {
                Vector128<int> sums = BoxSum(sumIntegral, bufferOrigin + (row * bufferStride) + column, bufferStride, radius, vector);
                Vector128<int> squareSums = BoxSum(squareIntegral, bufferOrigin + (row * bufferStride) + column, bufferStride, radius, vector);
                Vector128<uint> variance = CalculateVariance(sums, squareSums, bitDepth, windowAreaVector, vector);
                Vector128<uint> tableIndices = Vector128.Min(
                    Vector128.ShiftRightLogical((variance * scaleVector) + varianceRounding, ScaleBits),
                    maximumTableIndex);

                Vector128<int> factors = LookupBlendFactors(tableIndices);
                Vector128<uint> meanProducts = ((selfGuidedScale - factors) * reciprocalVector * sums).AsUInt32();
                Vector128<int> means = Vector128.ShiftRightLogical(meanProducts + meanRounding, ReciprocalBits).AsInt32();
                int coefficientOffset = bufferOrigin + (row * bufferStride) + column;
                factors.StoreUnsafe(ref blendBase, (nuint)coefficientOffset);
                means.StoreUnsafe(ref meanBase, (nuint)coefficientOffset);
            }

            for (; remaining > 0; column++, remaining--)
            {
                CalculateIntermediateCoefficient(
                    squareIntegral,
                    sumIntegral,
                    bufferOrigin + (row * bufferStride) + column,
                    bufferStride,
                    bitDepth,
                    radius,
                    windowArea,
                    scale,
                    reciprocal,
                    out int blendFactor,
                    out int localMean);

                int coefficientOffset = bufferOrigin + (row * bufferStride) + column;
                blendFactors[coefficientOffset] = blendFactor;
                localMeans[coefficientOffset] = localMean;
            }
        }
    }

    /// <summary>
    /// Calculates eight adjacent box sums from one integral image.
    /// </summary>
    /// <param name="integral">The source integral image.</param>
    /// <param name="centerOffset">The integral-image offset corresponding to the first box center.</param>
    /// <param name="stride">The integral-image row stride.</param>
    /// <param name="radius">The square-box radius.</param>
    /// <param name="vector">The overload-selection value.</param>
    /// <returns>The eight adjacent box sums.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> BoxSum(ReadOnlySpan<int> integral, int centerOffset, int stride, int radius, Vector256<int> vector)
    {
        ref int integralBase = ref MemoryMarshal.GetReference(integral);
        int upperOffset = centerOffset - ((radius + 1) * stride);
        int lowerOffset = centerOffset + (radius * stride);
        Vector256<int> topLeft = Vector256.LoadUnsafe(ref integralBase, (nuint)(upperOffset - radius - 1));
        Vector256<int> topRight = Vector256.LoadUnsafe(ref integralBase, (nuint)(upperOffset + radius));
        Vector256<int> bottomLeft = Vector256.LoadUnsafe(ref integralBase, (nuint)(lowerOffset - radius - 1));
        Vector256<int> bottomRight = Vector256.LoadUnsafe(ref integralBase, (nuint)(lowerOffset + radius));
        return (bottomRight - bottomLeft) - (topRight - topLeft);
    }

    /// <summary>
    /// Calculates four adjacent box sums from one integral image.
    /// </summary>
    /// <param name="integral">The source integral image.</param>
    /// <param name="centerOffset">The integral-image offset corresponding to the first box center.</param>
    /// <param name="stride">The integral-image row stride.</param>
    /// <param name="radius">The square-box radius.</param>
    /// <param name="vector">The overload-selection value.</param>
    /// <returns>The four adjacent box sums.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> BoxSum(ReadOnlySpan<int> integral, int centerOffset, int stride, int radius, Vector128<int> vector)
    {
        ref int integralBase = ref MemoryMarshal.GetReference(integral);
        int upperOffset = centerOffset - ((radius + 1) * stride);
        int lowerOffset = centerOffset + (radius * stride);
        Vector128<int> topLeft = Vector128.LoadUnsafe(ref integralBase, (nuint)(upperOffset - radius - 1));
        Vector128<int> topRight = Vector128.LoadUnsafe(ref integralBase, (nuint)(upperOffset + radius));
        Vector128<int> bottomLeft = Vector128.LoadUnsafe(ref integralBase, (nuint)(lowerOffset - radius - 1));
        Vector128<int> bottomRight = Vector128.LoadUnsafe(ref integralBase, (nuint)(lowerOffset + radius));
        return (bottomRight - bottomLeft) - (topRight - topLeft);
    }

    /// <summary>
    /// Converts eight window sums into the bounded variance measure defined by AV1.
    /// </summary>
    /// <param name="sums">The sample sums.</param>
    /// <param name="squareSums">The squared-sample sums.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    /// <param name="windowArea">The replicated square-window area.</param>
    /// <param name="vector">The overload-selection value.</param>
    /// <returns>The non-negative variance measure.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<uint> CalculateVariance(
        Vector256<int> sums,
        Vector256<int> squareSums,
        int bitDepth,
        Vector256<int> windowArea,
        Vector256<int> vector)
    {
        if (bitDepth > 8)
        {
            int depthShift = bitDepth - 8;
            squareSums = Vector256.ShiftRightLogical(
                squareSums.AsUInt32() + Vector256.Create(1U << ((depthShift * 2) - 1)), depthShift * 2).AsInt32();

            sums = Vector256.ShiftRightLogical(sums.AsUInt32() + Vector256.Create(1U << (depthShift - 1)), depthShift).AsInt32();
        }

        Vector256<int> squareOfSums = sums * sums;
        Vector256<int> scaledSquareSums = squareSums * windowArea;

        // Rounding high-bit-depth inputs can put the squared mean one step above
        // the mean square. AV1 saturates that artifact before applying the scale.
        return Vector256.Max(scaledSquareSums, squareOfSums).AsUInt32() - squareOfSums.AsUInt32();
    }

    /// <summary>
    /// Converts four window sums into the bounded variance measure defined by AV1.
    /// </summary>
    /// <param name="sums">The sample sums.</param>
    /// <param name="squareSums">The squared-sample sums.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    /// <param name="windowArea">The replicated square-window area.</param>
    /// <param name="vector">The overload-selection value.</param>
    /// <returns>The non-negative variance measure.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> CalculateVariance(
        Vector128<int> sums,
        Vector128<int> squareSums,
        int bitDepth,
        Vector128<int> windowArea,
        Vector128<int> vector)
    {
        if (bitDepth > 8)
        {
            int depthShift = bitDepth - 8;
            squareSums = Vector128.ShiftRightLogical(
                squareSums.AsUInt32() + Vector128.Create(1U << ((depthShift * 2) - 1)), depthShift * 2).AsInt32();

            sums = Vector128.ShiftRightLogical(sums.AsUInt32() + Vector128.Create(1U << (depthShift - 1)), depthShift).AsInt32();
        }

        Vector128<int> squareOfSums = sums * sums;
        Vector128<int> scaledSquareSums = squareSums * windowArea;
        return Vector128.Max(scaledSquareSums, squareOfSums).AsUInt32() - squareOfSums.AsUInt32();
    }

    /// <summary>
    /// Maps eight bounded variance indices to their normative blend factors.
    /// </summary>
    /// <param name="indices">The table indices.</param>
    /// <returns>The gathered blend factors.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe Vector256<int> LookupBlendFactors(Vector256<uint> indices)
    {
        // Variance normalization bounds every index to the 256-entry table. AVX2 gather keeps the eight independent
        // column lookups in the vector pipeline instead of materializing an intermediate scalar scale buffer.
        fixed (int* table = XByXPlusOne)
        {
            return Avx2.GatherVector256(table, indices.AsInt32(), sizeof(int));
        }
    }

    /// <summary>
    /// Maps four bounded variance indices to their normative blend factors.
    /// </summary>
    /// <param name="indices">The table indices.</param>
    /// <returns>The gathered blend factors.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> LookupBlendFactors(Vector128<uint> indices)
    {
        // Portable 128-bit APIs do not provide indexed loads. Four bounded scalar reads are assembled directly into
        // the result vector, avoiding both an allocation and a second pass over the coefficient row.
        ReadOnlySpan<int> table = XByXPlusOne;
        return Vector128.Create(
            table[(int)indices.GetElement(0)],
            table[(int)indices.GetElement(1)],
            table[(int)indices.GetElement(2)],
            table[(int)indices.GetElement(3)]);
    }

    /// <summary>
    /// Calculates one coefficient pair for a vector remainder.
    /// </summary>
    /// <param name="squareIntegral">The integral image of squared samples.</param>
    /// <param name="sumIntegral">The integral image of samples.</param>
    /// <param name="centerOffset">The integral-image offset corresponding to the box center.</param>
    /// <param name="stride">The integral-image row stride.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    /// <param name="radius">The square-window radius.</param>
    /// <param name="windowArea">The square-window area.</param>
    /// <param name="scale">The variance scale for the selected parameter set.</param>
    /// <param name="reciprocal">The fixed-point reciprocal of the window area.</param>
    /// <param name="blendFactor">The calculated local blend factor.</param>
    /// <param name="localMean">The calculated scaled local mean.</param>
    private static void CalculateIntermediateCoefficient(
        ReadOnlySpan<int> squareIntegral,
        ReadOnlySpan<int> sumIntegral,
        int centerOffset,
        int stride,
        int bitDepth,
        int radius,
        int windowArea,
        int scale,
        int reciprocal,
        out int blendFactor,
        out int localMean)
    {
        int sum = BoxSum(sumIntegral, centerOffset, stride, radius);
        int squareSum = BoxSum(squareIntegral, centerOffset, stride, radius);
        int normalizedSquareSum = RoundPowerOfTwo(squareSum, 2 * (bitDepth - 8));
        int normalizedSum = RoundPowerOfTwo(sum, bitDepth - 8);
        uint squareOfSum = (uint)normalizedSum * (uint)normalizedSum;
        uint scaledSquareSum = (uint)normalizedSquareSum * (uint)windowArea;
        uint variance = scaledSquareSum < squareOfSum ? 0 : scaledSquareSum - squareOfSum;
        uint varianceIndex = RoundPowerOfTwo(variance * (uint)scale, ScaleBits);
        blendFactor = XByXPlusOne[(int)Math.Min(varianceIndex, 255U)];
        uint meanProduct = (uint)(SelfGuidedScale - blendFactor) * (uint)reciprocal * (uint)sum;
        localMean = (int)RoundPowerOfTwo(meanProduct, ReciprocalBits);
    }

    /// <summary>
    /// Calculates one square-window sum from an integral image.
    /// </summary>
    /// <param name="integral">The source integral image.</param>
    /// <param name="centerOffset">The integral-image offset corresponding to the box center.</param>
    /// <param name="stride">The integral-image row stride.</param>
    /// <param name="radius">The square-box radius.</param>
    /// <returns>The square-window sum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int BoxSum(ReadOnlySpan<int> integral, int centerOffset, int stride, int radius)
    {
        int upperOffset = centerOffset - ((radius + 1) * stride);
        int lowerOffset = centerOffset + (radius * stride);
        int top = integral[upperOffset + radius] - integral[upperOffset - radius - 1];
        int bottom = integral[lowerOffset + radius] - integral[lowerOffset - radius - 1];
        return bottom - top;
    }

    /// <summary>
    /// Produces the radius-two filtered values in eight-sample AVX2 batches.
    /// </summary>
    /// <param name="source">The bordered processing-unit source rectangle.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="width">The processing-unit width in samples.</param>
    /// <param name="height">The processing-unit height in samples.</param>
    /// <param name="bufferStride">The padded coefficient-buffer row stride.</param>
    /// <param name="blendFactors">The local sample blend factors.</param>
    /// <param name="localMeans">The scaled local means.</param>
    /// <param name="filtered">The destination fixed-point filtered values.</param>
    /// <param name="vector">The overload-selection value.</param>
    private static void CalculateRadiusTwoFilter(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int width,
        int height,
        int bufferStride,
        ReadOnlySpan<int> blendFactors,
        ReadOnlySpan<int> localMeans,
        Span<int> filtered,
        Vector256<int> vector)
    {
        int bufferOrigin = (Border + 1) * (bufferStride + 1);
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref int filteredBase = ref MemoryMarshal.GetReference(filtered);

        for (int row = 0; row < height; row++)
        {
            int sourceRowOffset = ((row + Border) * sourceStride) + Border;
            int filteredRowOffset = row * width;
            int coefficientRowOffset = bufferOrigin + (row * bufferStride);
            int roundingBits = SelfGuidedBits + ((row & 1) == 0 ? 5 : 4) - RestorationBits;
            Vector256<int> rounding = Vector256.Create(1 << (roundingBits - 1));
            int column = 0;
            int vectorEnd = width - Vector256<int>.Count;

            // Filtered signals use Q4 precision. Projection applies the signaled Q7 weights to their difference from
            // the unfiltered Q4 sample, then performs the combined Q11 rounding shift once before clipping.
            for (; column <= vectorEnd; column += Vector256<int>.Count)
            {
                Vector256<int> factors = CrossSum(blendFactors, coefficientRowOffset + column, bufferStride, row, vector);
                Vector256<int> means = CrossSum(localMeans, coefficientRowOffset + column, bufferStride, row, vector);
                Vector128<ushort> packed = Vector128.LoadUnsafe(ref sourceBase, (nuint)(sourceRowOffset + column));
                Vector256<int> samples = Avx2.ConvertToVector256Int32(packed);
                Vector256<int> values = Vector256.ShiftRightArithmetic((factors * samples) + means + rounding, roundingBits);
                values.StoreUnsafe(ref filteredBase, (nuint)(filteredRowOffset + column));
            }

            for (; column < width; column++)
            {
                int factors = CrossSum(blendFactors, coefficientRowOffset + column, bufferStride, row);
                int means = CrossSum(localMeans, coefficientRowOffset + column, bufferStride, row);
                int value = (factors * source[sourceRowOffset + column]) + means;
                filtered[filteredRowOffset + column] = RoundPowerOfTwo(value, roundingBits);
            }
        }
    }

    /// <summary>
    /// Produces the radius-two filtered values in four-sample cross-platform batches.
    /// </summary>
    /// <param name="source">The bordered processing-unit source rectangle.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="width">The processing-unit width in samples.</param>
    /// <param name="height">The processing-unit height in samples.</param>
    /// <param name="bufferStride">The padded coefficient-buffer row stride.</param>
    /// <param name="blendFactors">The local sample blend factors.</param>
    /// <param name="localMeans">The scaled local means.</param>
    /// <param name="filtered">The destination fixed-point filtered values.</param>
    /// <param name="vector">The overload-selection value.</param>
    private static void CalculateRadiusTwoFilter(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int width,
        int height,
        int bufferStride,
        ReadOnlySpan<int> blendFactors,
        ReadOnlySpan<int> localMeans,
        Span<int> filtered,
        Vector128<int> vector)
    {
        int bufferOrigin = (Border + 1) * (bufferStride + 1);
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref int filteredBase = ref MemoryMarshal.GetReference(filtered);

        for (int row = 0; row < height; row++)
        {
            int sourceRowOffset = ((row + Border) * sourceStride) + Border;
            int filteredRowOffset = row * width;
            int coefficientRowOffset = bufferOrigin + (row * bufferStride);
            int roundingBits = SelfGuidedBits + ((row & 1) == 0 ? 5 : 4) - RestorationBits;
            Vector128<int> rounding = Vector128.Create(1 << (roundingBits - 1));
            int column = 0;
            int vectorEnd = width - Vector128<int>.Count;

            // The 128-bit path uses the same Q4/Q7 projection equation. The four-sample load and store are deliberately
            // 64 bits wide so a tightly strided destination row never requires writable padding.
            for (; column <= vectorEnd; column += Vector128<int>.Count)
            {
                Vector128<int> factors = CrossSum(blendFactors, coefficientRowOffset + column, bufferStride, row, vector);
                Vector128<int> means = CrossSum(localMeans, coefficientRowOffset + column, bufferStride, row, vector);
                ref ushort sourceReference = ref Unsafe.Add(ref sourceBase, sourceRowOffset + column);
                Vector64<ushort> packed = Unsafe.As<ushort, Vector64<ushort>>(ref sourceReference);
                Vector128<int> samples = Vector128.WidenLower(Vector128.Create(packed, Vector64<ushort>.Zero)).AsInt32();
                Vector128<int> values = Vector128.ShiftRightArithmetic((factors * samples) + means + rounding, roundingBits);
                values.StoreUnsafe(ref filteredBase, (nuint)(filteredRowOffset + column));
            }

            for (; column < width; column++)
            {
                int factors = CrossSum(blendFactors, coefficientRowOffset + column, bufferStride, row);
                int means = CrossSum(localMeans, coefficientRowOffset + column, bufferStride, row);
                int value = (factors * source[sourceRowOffset + column]) + means;
                filtered[filteredRowOffset + column] = RoundPowerOfTwo(value, roundingBits);
            }
        }
    }

    /// <summary>
    /// Produces the radius-one filtered values in eight-sample AVX2 batches.
    /// </summary>
    /// <param name="source">The bordered processing-unit source rectangle.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="width">The processing-unit width in samples.</param>
    /// <param name="height">The processing-unit height in samples.</param>
    /// <param name="bufferStride">The padded coefficient-buffer row stride.</param>
    /// <param name="blendFactors">The local sample blend factors.</param>
    /// <param name="localMeans">The scaled local means.</param>
    /// <param name="filtered">The destination fixed-point filtered values.</param>
    /// <param name="vector">The overload-selection value.</param>
    private static void CalculateRadiusOneFilter(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int width,
        int height,
        int bufferStride,
        ReadOnlySpan<int> blendFactors,
        ReadOnlySpan<int> localMeans,
        Span<int> filtered,
        Vector256<int> vector)
    {
        int bufferOrigin = (Border + 1) * (bufferStride + 1);
        int roundingBits = SelfGuidedBits + 5 - RestorationBits;
        Vector256<int> rounding = Vector256.Create(1 << (roundingBits - 1));
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref int filteredBase = ref MemoryMarshal.GetReference(filtered);

        for (int row = 0; row < height; row++)
        {
            int sourceRowOffset = ((row + Border) * sourceStride) + Border;
            int filteredRowOffset = row * width;
            int coefficientRowOffset = bufferOrigin + (row * bufferStride);
            int column = 0;
            int vectorEnd = width - Vector256<int>.Count;
            for (; column <= vectorEnd; column += Vector256<int>.Count)
            {
                Vector256<int> factors = CrossSum(blendFactors, coefficientRowOffset + column, bufferStride, vector);
                Vector256<int> means = CrossSum(localMeans, coefficientRowOffset + column, bufferStride, vector);
                Vector128<ushort> packed = Vector128.LoadUnsafe(ref sourceBase, (nuint)(sourceRowOffset + column));
                Vector256<int> samples = Avx2.ConvertToVector256Int32(packed);
                Vector256<int> values = Vector256.ShiftRightArithmetic((factors * samples) + means + rounding, roundingBits);
                values.StoreUnsafe(ref filteredBase, (nuint)(filteredRowOffset + column));
            }

            for (; column < width; column++)
            {
                int factors = CrossSum(blendFactors, coefficientRowOffset + column, bufferStride);
                int means = CrossSum(localMeans, coefficientRowOffset + column, bufferStride);
                int value = (factors * source[sourceRowOffset + column]) + means;
                filtered[filteredRowOffset + column] = RoundPowerOfTwo(value, roundingBits);
            }
        }
    }

    /// <summary>
    /// Produces the radius-one filtered values in four-sample cross-platform batches.
    /// </summary>
    /// <param name="source">The bordered processing-unit source rectangle.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="width">The processing-unit width in samples.</param>
    /// <param name="height">The processing-unit height in samples.</param>
    /// <param name="bufferStride">The padded coefficient-buffer row stride.</param>
    /// <param name="blendFactors">The local sample blend factors.</param>
    /// <param name="localMeans">The scaled local means.</param>
    /// <param name="filtered">The destination fixed-point filtered values.</param>
    /// <param name="vector">The overload-selection value.</param>
    private static void CalculateRadiusOneFilter(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int width,
        int height,
        int bufferStride,
        ReadOnlySpan<int> blendFactors,
        ReadOnlySpan<int> localMeans,
        Span<int> filtered,
        Vector128<int> vector)
    {
        int bufferOrigin = (Border + 1) * (bufferStride + 1);
        int roundingBits = SelfGuidedBits + 5 - RestorationBits;
        Vector128<int> rounding = Vector128.Create(1 << (roundingBits - 1));
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref int filteredBase = ref MemoryMarshal.GetReference(filtered);

        for (int row = 0; row < height; row++)
        {
            int sourceRowOffset = ((row + Border) * sourceStride) + Border;
            int filteredRowOffset = row * width;
            int coefficientRowOffset = bufferOrigin + (row * bufferStride);
            int column = 0;
            int vectorEnd = width - Vector128<int>.Count;
            for (; column <= vectorEnd; column += Vector128<int>.Count)
            {
                Vector128<int> factors = CrossSum(blendFactors, coefficientRowOffset + column, bufferStride, vector);
                Vector128<int> means = CrossSum(localMeans, coefficientRowOffset + column, bufferStride, vector);
                ref ushort sourceReference = ref Unsafe.Add(ref sourceBase, sourceRowOffset + column);
                Vector64<ushort> packed = Unsafe.As<ushort, Vector64<ushort>>(ref sourceReference);
                Vector128<int> samples = Vector128.WidenLower(Vector128.Create(packed, Vector64<ushort>.Zero)).AsInt32();
                Vector128<int> values = Vector128.ShiftRightArithmetic((factors * samples) + means + rounding, roundingBits);
                values.StoreUnsafe(ref filteredBase, (nuint)(filteredRowOffset + column));
            }

            for (; column < width; column++)
            {
                int factors = CrossSum(blendFactors, coefficientRowOffset + column, bufferStride);
                int means = CrossSum(localMeans, coefficientRowOffset + column, bufferStride);
                int value = (factors * source[sourceRowOffset + column]) + means;
                filtered[filteredRowOffset + column] = RoundPowerOfTwo(value, roundingBits);
            }
        }
    }

    /// <summary>
    /// Calculates eight radius-one weighted cross sums.
    /// </summary>
    /// <param name="buffer">The coefficient buffer.</param>
    /// <param name="offset">The first center coefficient.</param>
    /// <param name="stride">The coefficient-buffer row stride.</param>
    /// <param name="vector">The overload-selection value.</param>
    /// <returns>The weighted cross sums.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> CrossSum(ReadOnlySpan<int> buffer, int offset, int stride, Vector256<int> vector)
    {
        ref int bufferBase = ref MemoryMarshal.GetReference(buffer);
        Vector256<int> topLeft = Vector256.LoadUnsafe(ref bufferBase, (nuint)(offset - stride - 1));
        Vector256<int> top = Vector256.LoadUnsafe(ref bufferBase, (nuint)(offset - stride));
        Vector256<int> topRight = Vector256.LoadUnsafe(ref bufferBase, (nuint)(offset - stride + 1));
        Vector256<int> left = Vector256.LoadUnsafe(ref bufferBase, (nuint)(offset - 1));
        Vector256<int> center = Vector256.LoadUnsafe(ref bufferBase, (nuint)offset);
        Vector256<int> right = Vector256.LoadUnsafe(ref bufferBase, (nuint)(offset + 1));
        Vector256<int> bottomLeft = Vector256.LoadUnsafe(ref bufferBase, (nuint)(offset + stride - 1));
        Vector256<int> bottom = Vector256.LoadUnsafe(ref bufferBase, (nuint)(offset + stride));
        Vector256<int> bottomRight = Vector256.LoadUnsafe(ref bufferBase, (nuint)(offset + stride + 1));
        Vector256<int> corners = topLeft + topRight + bottomLeft + bottomRight;
        Vector256<int> remainder = left + top + center + right + bottom;
        return Vector256.ShiftLeft(corners + remainder, 2) - corners;
    }

    /// <summary>
    /// Calculates four radius-one weighted cross sums.
    /// </summary>
    /// <param name="buffer">The coefficient buffer.</param>
    /// <param name="offset">The first center coefficient.</param>
    /// <param name="stride">The coefficient-buffer row stride.</param>
    /// <param name="vector">The overload-selection value.</param>
    /// <returns>The weighted cross sums.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> CrossSum(ReadOnlySpan<int> buffer, int offset, int stride, Vector128<int> vector)
    {
        ref int bufferBase = ref MemoryMarshal.GetReference(buffer);
        Vector128<int> topLeft = Vector128.LoadUnsafe(ref bufferBase, (nuint)(offset - stride - 1));
        Vector128<int> top = Vector128.LoadUnsafe(ref bufferBase, (nuint)(offset - stride));
        Vector128<int> topRight = Vector128.LoadUnsafe(ref bufferBase, (nuint)(offset - stride + 1));
        Vector128<int> left = Vector128.LoadUnsafe(ref bufferBase, (nuint)(offset - 1));
        Vector128<int> center = Vector128.LoadUnsafe(ref bufferBase, (nuint)offset);
        Vector128<int> right = Vector128.LoadUnsafe(ref bufferBase, (nuint)(offset + 1));
        Vector128<int> bottomLeft = Vector128.LoadUnsafe(ref bufferBase, (nuint)(offset + stride - 1));
        Vector128<int> bottom = Vector128.LoadUnsafe(ref bufferBase, (nuint)(offset + stride));
        Vector128<int> bottomRight = Vector128.LoadUnsafe(ref bufferBase, (nuint)(offset + stride + 1));
        Vector128<int> corners = topLeft + topRight + bottomLeft + bottomRight;
        Vector128<int> remainder = left + top + center + right + bottom;
        return Vector128.ShiftLeft(corners + remainder, 2) - corners;
    }

    /// <summary>
    /// Calculates eight radius-two weighted cross sums from the required coefficient rows.
    /// </summary>
    /// <param name="buffer">The coefficient buffer.</param>
    /// <param name="offset">The first center coefficient.</param>
    /// <param name="stride">The coefficient-buffer row stride.</param>
    /// <param name="row">The destination row selecting the even or odd kernel.</param>
    /// <param name="vector">The overload-selection value.</param>
    /// <returns>The weighted cross sums.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> CrossSum(ReadOnlySpan<int> buffer, int offset, int stride, int row, Vector256<int> vector)
    {
        ref int bufferBase = ref MemoryMarshal.GetReference(buffer);
        if ((row & 1) != 0)
        {
            Vector256<int> left = Vector256.LoadUnsafe(ref bufferBase, (nuint)(offset - 1));
            Vector256<int> center = Vector256.LoadUnsafe(ref bufferBase, (nuint)offset);
            Vector256<int> right = Vector256.LoadUnsafe(ref bufferBase, (nuint)(offset + 1));
            Vector256<int> combined = left + center + right;
            return Vector256.ShiftLeft(combined, 2) + combined + center;
        }

        Vector256<int> topLeft = Vector256.LoadUnsafe(ref bufferBase, (nuint)(offset - stride - 1));
        Vector256<int> top = Vector256.LoadUnsafe(ref bufferBase, (nuint)(offset - stride));
        Vector256<int> topRight = Vector256.LoadUnsafe(ref bufferBase, (nuint)(offset - stride + 1));
        Vector256<int> bottomLeft = Vector256.LoadUnsafe(ref bufferBase, (nuint)(offset + stride - 1));
        Vector256<int> bottom = Vector256.LoadUnsafe(ref bufferBase, (nuint)(offset + stride));
        Vector256<int> bottomRight = Vector256.LoadUnsafe(ref bufferBase, (nuint)(offset + stride + 1));
        Vector256<int> centers = top + bottom;
        Vector256<int> combinedRows = topLeft + topRight + bottomLeft + bottomRight + centers;
        return Vector256.ShiftLeft(combinedRows, 2) + combinedRows + centers;
    }

    /// <summary>
    /// Calculates four radius-two weighted cross sums from the required coefficient rows.
    /// </summary>
    /// <param name="buffer">The coefficient buffer.</param>
    /// <param name="offset">The first center coefficient.</param>
    /// <param name="stride">The coefficient-buffer row stride.</param>
    /// <param name="row">The destination row selecting the even or odd kernel.</param>
    /// <param name="vector">The overload-selection value.</param>
    /// <returns>The weighted cross sums.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> CrossSum(ReadOnlySpan<int> buffer, int offset, int stride, int row, Vector128<int> vector)
    {
        ref int bufferBase = ref MemoryMarshal.GetReference(buffer);
        if ((row & 1) != 0)
        {
            Vector128<int> left = Vector128.LoadUnsafe(ref bufferBase, (nuint)(offset - 1));
            Vector128<int> center = Vector128.LoadUnsafe(ref bufferBase, (nuint)offset);
            Vector128<int> right = Vector128.LoadUnsafe(ref bufferBase, (nuint)(offset + 1));
            Vector128<int> combined = left + center + right;
            return Vector128.ShiftLeft(combined, 2) + combined + center;
        }

        Vector128<int> topLeft = Vector128.LoadUnsafe(ref bufferBase, (nuint)(offset - stride - 1));
        Vector128<int> top = Vector128.LoadUnsafe(ref bufferBase, (nuint)(offset - stride));
        Vector128<int> topRight = Vector128.LoadUnsafe(ref bufferBase, (nuint)(offset - stride + 1));
        Vector128<int> bottomLeft = Vector128.LoadUnsafe(ref bufferBase, (nuint)(offset + stride - 1));
        Vector128<int> bottom = Vector128.LoadUnsafe(ref bufferBase, (nuint)(offset + stride));
        Vector128<int> bottomRight = Vector128.LoadUnsafe(ref bufferBase, (nuint)(offset + stride + 1));
        Vector128<int> centers = top + bottom;
        Vector128<int> combinedRows = topLeft + topRight + bottomLeft + bottomRight + centers;
        return Vector128.ShiftLeft(combinedRows, 2) + combinedRows + centers;
    }

    /// <summary>
    /// Calculates one scalar radius-one weighted cross sum.
    /// </summary>
    /// <param name="buffer">The coefficient buffer.</param>
    /// <param name="offset">The center coefficient.</param>
    /// <param name="stride">The coefficient-buffer row stride.</param>
    /// <returns>The weighted cross sum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CrossSum(ReadOnlySpan<int> buffer, int offset, int stride)
    {
        int corners = buffer[offset - stride - 1] + buffer[offset - stride + 1] + buffer[offset + stride - 1] + buffer[offset + stride + 1];
        int remainder = buffer[offset - 1] + buffer[offset - stride] + buffer[offset] + buffer[offset + 1] + buffer[offset + stride];
        return ((corners + remainder) << 2) - corners;
    }

    /// <summary>
    /// Calculates one scalar radius-two weighted cross sum.
    /// </summary>
    /// <param name="buffer">The coefficient buffer.</param>
    /// <param name="offset">The center coefficient.</param>
    /// <param name="stride">The coefficient-buffer row stride.</param>
    /// <param name="row">The destination row selecting the even or odd kernel.</param>
    /// <returns>The weighted cross sum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CrossSum(ReadOnlySpan<int> buffer, int offset, int stride, int row)
    {
        if ((row & 1) != 0)
        {
            int center = buffer[offset];
            int combined = buffer[offset - 1] + center + buffer[offset + 1];
            return (combined << 2) + combined + center;
        }

        int centers = buffer[offset - stride] + buffer[offset + stride];
        int combinedRows = buffer[offset - stride - 1] + buffer[offset - stride + 1]
            + buffer[offset + stride - 1] + buffer[offset + stride + 1] + centers;

        return (combinedRows << 2) + combinedRows + centers;
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
    /// Projects the two restored signals in eight-sample AVX2 batches.
    /// </summary>
    /// <param name="source">The bordered processing-unit source rectangle.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="destination">The destination processing-unit rectangle.</param>
    /// <param name="destinationStride">The number of samples between destination rows.</param>
    /// <param name="width">The processing-unit width in samples.</param>
    /// <param name="height">The processing-unit height in samples.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    /// <param name="radii">The two selected filter radii.</param>
    /// <param name="projection0">The first decoded projection coefficient.</param>
    /// <param name="projection1">The second decoded projection coefficient.</param>
    /// <param name="filtered0">The first fixed-point restored signal.</param>
    /// <param name="filtered1">The second fixed-point restored signal.</param>
    /// <param name="vector">The overload-selection value.</param>
    private static void Project(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        int bitDepth,
        ReadOnlySpan<int> radii,
        int projection0,
        int projection1,
        ReadOnlySpan<int> filtered0,
        ReadOnlySpan<int> filtered1,
        Vector256<int> vector)
    {
        const int projectionShift = ProjectionBits + RestorationBits;
        Vector256<int> projection0Vector = Vector256.Create(projection0);
        Vector256<int> projection1Vector = Vector256.Create(projection1);
        Vector256<int> rounding = Vector256.Create(1 << (projectionShift - 1));
        Vector256<int> maximumSample = Vector256.Create((1 << bitDepth) - 1);
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        ref int filtered0Base = ref MemoryMarshal.GetReference(filtered0);
        ref int filtered1Base = ref MemoryMarshal.GetReference(filtered1);

        for (int row = 0; row < height; row++)
        {
            int sourceRowOffset = ((row + Border) * sourceStride) + Border;
            int destinationRowOffset = row * destinationStride;
            int filteredRowOffset = row * width;
            int column = 0;
            int vectorEnd = width - Vector256<int>.Count;
            for (; column <= vectorEnd; column += Vector256<int>.Count)
            {
                Vector128<ushort> packed = Vector128.LoadUnsafe(ref sourceBase, (nuint)(sourceRowOffset + column));
                Vector256<int> samples = Avx2.ConvertToVector256Int32(packed);
                Vector256<int> unfiltered = Vector256.ShiftLeft(samples, RestorationBits);
                Vector256<int> projected = Vector256.ShiftLeft(unfiltered, ProjectionBits);
                if (radii[0] > 0)
                {
                    Vector256<int> restored = Vector256.LoadUnsafe(ref filtered0Base, (nuint)(filteredRowOffset + column));
                    projected += projection0Vector * (restored - unfiltered);
                }

                if (radii[1] > 0)
                {
                    Vector256<int> restored = Vector256.LoadUnsafe(ref filtered1Base, (nuint)(filteredRowOffset + column));
                    projected += projection1Vector * (restored - unfiltered);
                }

                Vector256<int> result = Vector256.ShiftRightArithmetic(projected + rounding, projectionShift);
                result = Vector256.Min(Vector256.Max(result, Vector256<int>.Zero), maximumSample);

                // Narrowing the result with a zero upper vector places the eight ordered samples
                // in the lower 128 bits, which can be stored without the AVX2 pack permutation.
                Vector128<ushort> narrowed = Vector256.Narrow(result.AsUInt32(), Vector256<uint>.Zero).GetLower();
                narrowed.StoreUnsafe(ref destinationBase, (nuint)(destinationRowOffset + column));
            }

            for (; column < width; column++)
            {
                int filteredOffset = filteredRowOffset + column;
                int sample = source[sourceRowOffset + column];
                int restored0 = radii[0] > 0 ? filtered0[filteredOffset] : 0;
                int restored1 = radii[1] > 0 ? filtered1[filteredOffset] : 0;
                destination[destinationRowOffset + column] = ProjectSample(
                    sample,
                    bitDepth,
                    radii,
                    projection0,
                    projection1,
                    restored0,
                    restored1);
            }
        }
    }

    /// <summary>
    /// Projects the two restored signals in four-sample cross-platform batches.
    /// </summary>
    /// <param name="source">The bordered processing-unit source rectangle.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="destination">The destination processing-unit rectangle.</param>
    /// <param name="destinationStride">The number of samples between destination rows.</param>
    /// <param name="width">The processing-unit width in samples.</param>
    /// <param name="height">The processing-unit height in samples.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    /// <param name="radii">The two selected filter radii.</param>
    /// <param name="projection0">The first decoded projection coefficient.</param>
    /// <param name="projection1">The second decoded projection coefficient.</param>
    /// <param name="filtered0">The first fixed-point restored signal.</param>
    /// <param name="filtered1">The second fixed-point restored signal.</param>
    /// <param name="vector">The overload-selection value.</param>
    private static void Project(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        int bitDepth,
        ReadOnlySpan<int> radii,
        int projection0,
        int projection1,
        ReadOnlySpan<int> filtered0,
        ReadOnlySpan<int> filtered1,
        Vector128<int> vector)
    {
        const int projectionShift = ProjectionBits + RestorationBits;
        Vector128<int> projection0Vector = Vector128.Create(projection0);
        Vector128<int> projection1Vector = Vector128.Create(projection1);
        Vector128<int> rounding = Vector128.Create(1 << (projectionShift - 1));
        Vector128<int> maximumSample = Vector128.Create((1 << bitDepth) - 1);
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        ref int filtered0Base = ref MemoryMarshal.GetReference(filtered0);
        ref int filtered1Base = ref MemoryMarshal.GetReference(filtered1);

        for (int row = 0; row < height; row++)
        {
            int sourceRowOffset = ((row + Border) * sourceStride) + Border;
            int destinationRowOffset = row * destinationStride;
            int filteredRowOffset = row * width;
            int column = 0;
            int vectorEnd = width - Vector128<int>.Count;
            for (; column <= vectorEnd; column += Vector128<int>.Count)
            {
                ref ushort sourceReference = ref Unsafe.Add(ref sourceBase, sourceRowOffset + column);
                Vector64<ushort> packed = Unsafe.As<ushort, Vector64<ushort>>(ref sourceReference);
                Vector128<int> samples = Vector128.WidenLower(Vector128.Create(packed, Vector64<ushort>.Zero)).AsInt32();
                Vector128<int> unfiltered = Vector128.ShiftLeft(samples, RestorationBits);
                Vector128<int> projected = Vector128.ShiftLeft(unfiltered, ProjectionBits);
                if (radii[0] > 0)
                {
                    Vector128<int> restored = Vector128.LoadUnsafe(ref filtered0Base, (nuint)(filteredRowOffset + column));
                    projected += projection0Vector * (restored - unfiltered);
                }

                if (radii[1] > 0)
                {
                    Vector128<int> restored = Vector128.LoadUnsafe(ref filtered1Base, (nuint)(filteredRowOffset + column));
                    projected += projection1Vector * (restored - unfiltered);
                }

                Vector128<int> result = Vector128.ShiftRightArithmetic(projected + rounding, projectionShift);
                result = Vector128.Min(Vector128.Max(result, Vector128<int>.Zero), maximumSample);
                Vector64<ushort> narrowed = Vector128.Narrow(result.AsUInt32(), Vector128<uint>.Zero).GetLower();
                ref ushort destinationReference = ref Unsafe.Add(ref destinationBase, destinationRowOffset + column);
                Unsafe.As<ushort, Vector64<ushort>>(ref destinationReference) = narrowed;
            }

            for (; column < width; column++)
            {
                int filteredOffset = filteredRowOffset + column;
                int sample = source[sourceRowOffset + column];
                int restored0 = radii[0] > 0 ? filtered0[filteredOffset] : 0;
                int restored1 = radii[1] > 0 ? filtered1[filteredOffset] : 0;
                destination[destinationRowOffset + column] = ProjectSample(
                    sample,
                    bitDepth,
                    radii,
                    projection0,
                    projection1,
                    restored0,
                    restored1);
            }
        }
    }

    /// <summary>
    /// Projects one scalar remainder sample from the active restored signals.
    /// </summary>
    /// <param name="sample">The unfiltered source sample.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    /// <param name="radii">The two selected filter radii.</param>
    /// <param name="projection0">The first decoded projection coefficient.</param>
    /// <param name="projection1">The second decoded projection coefficient.</param>
    /// <param name="filtered0">The first fixed-point restored value.</param>
    /// <param name="filtered1">The second fixed-point restored value.</param>
    /// <returns>The clipped projected sample.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort ProjectSample(
        int sample,
        int bitDepth,
        ReadOnlySpan<int> radii,
        int projection0,
        int projection1,
        int filtered0,
        int filtered1)
    {
        int unfiltered = sample << RestorationBits;
        int projected = unfiltered << ProjectionBits;
        if (radii[0] > 0)
        {
            projected += projection0 * (filtered0 - unfiltered);
        }

        if (radii[1] > 0)
        {
            projected += projection1 * (filtered1 - unfiltered);
        }

        return (ushort)Av1Math.Clip3(0, (1 << bitDepth) - 1, RoundPowerOfTwo(projected, ProjectionBits + RestorationBits));
    }
}
