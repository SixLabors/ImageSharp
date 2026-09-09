// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 self-guided restoration against a direct-window definition across the supported hardware-intrinsic configurations.
/// </summary>
[Trait("Format", "Heif")]
public class Av1SelfGuidedFilterTests
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
    /// The number of fractional bits used by the projection coefficients.
    /// </summary>
    private const int ProjectionBits = 7;

    /// <summary>
    /// The hardware configurations required to exercise AVX2-assisted 256-bit, portable 256-bit, 128-bit, and scalar execution.
    /// </summary>
    private const HwIntrinsics Configurations =
        HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Gets the radii selected by each of the sixteen normative parameter sets.
    /// </summary>
    private static ReadOnlySpan<int> ParameterRadii =>
    [
        2, 1, 2, 1, 2, 1, 2, 1,
        2, 1, 2, 1, 2, 1, 2, 1,
        2, 1, 2, 1, 0, 1, 0, 1,
        0, 1, 0, 1, 2, 0, 2, 0,
    ];

    /// <summary>
    /// Gets the variance scales selected by each of the sixteen normative parameter sets.
    /// </summary>
    private static ReadOnlySpan<int> ParameterScales =>
    [
        140, 3236, 112, 2158, 93, 1618, 80, 1438,
        70, 1295, 58, 1177, 47, 1079, 37, 996,
        30, 925, 25, 863, -1, 2589, -1, 1618,
        -1, 1177, -1, 925, 56, -1, 22, -1,
    ];

    /// <summary>
    /// Gets processing-unit dimensions covering narrow chroma units, odd frame edges, and both vector remainder widths.
    /// </summary>
    private static ReadOnlySpan<int> ProcessingUnitDimensions =>
    [
        1, 1,
        3, 5,
        7, 4,
        13, 9,
        29, 6,
        32, 32,
        64, 64,
    ];

    /// <summary>
    /// Verifies every normative parameter set, sample precision, and processing-unit tail against the direct-window definition.
    /// </summary>
    [Fact]
    public void FilterMatchesReference()
    {
        ValidateFilters();
        FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateFilters, Configurations);
    }

    /// <summary>
    /// Validates the complete self-guided parameter matrix in the active hardware-intrinsic configuration.
    /// </summary>
    private static void ValidateFilters()
    {
        foreach (int bitDepth in new[] { 8, 10, 12 })
        {
            int maximumSample = (1 << bitDepth) - 1;
            ReadOnlySpan<int> processingUnitDimensions = ProcessingUnitDimensions;
            for (int dimensionIndex = 0; dimensionIndex < processingUnitDimensions.Length; dimensionIndex += 2)
            {
                int width = processingUnitDimensions[dimensionIndex];
                int height = processingUnitDimensions[dimensionIndex + 1];
                int sourceStride = width + (Border * 2) + 5;
                int destinationStride = width + 7;
                ushort[] source = new ushort[sourceStride * (height + (Border * 2))];
                ushort[] expected = new ushort[destinationStride * height];
                ushort[] actual = new ushort[destinationStride * height];
                byte[] byteSource = new byte[source.Length];
                byte[] byteActual = new byte[actual.Length + 2];
                byte[] byteExpected = new byte[actual.Length + 2];
                int scratchLength = Av1SelfGuidedFilter.GetScratchLength(width, height);
                int[] scratchStorage = new int[scratchLength + 2];
                Span<int> scratch = scratchStorage.AsSpan(1, scratchLength);
                int[] projectionCoefficients = new int[2];

                FillSource(source, sourceStride, maximumSample);
                for (int index = 0; index < source.Length; index++)
                {
                    byteSource[index] = (byte)source[index];
                }

                for (int parameterSetIndex = 0; parameterSetIndex < 16; parameterSetIndex++)
                {
                    expected.AsSpan().Fill(ushort.MaxValue);
                    actual.AsSpan().Fill(ushort.MaxValue);
                    scratchStorage.AsSpan().Fill(int.MinValue);
                    projectionCoefficients[0] = -96 + ((parameterSetIndex * 17) & 127);
                    projectionCoefficients[1] = -32 + ((parameterSetIndex * 29) & 127);

                    FilterReference(
                        source,
                        sourceStride,
                        expected,
                        destinationStride,
                        width,
                        height,
                        bitDepth,
                        parameterSetIndex,
                        projectionCoefficients);

                    Av1SelfGuidedFilter.FilterBlock(
                        source,
                        sourceStride,
                        actual,
                        destinationStride,
                        width,
                        height,
                        bitDepth,
                        parameterSetIndex,
                        projectionCoefficients,
                        scratch);

                    AssertBlockEqual(expected, actual, destinationStride, width, height, bitDepth, parameterSetIndex);
                    Assert.Equal(int.MinValue, scratchStorage[0]);
                    Assert.Equal(int.MinValue, scratchStorage[^1]);

                    if (bitDepth == 8)
                    {
                        // Reuse the independent window result for physical byte storage. The leading,
                        // trailing, and row-padding sentinels detect stores wider than the result lanes.
                        byteActual.AsSpan().Fill(byte.MaxValue);
                        byteExpected.AsSpan().Fill(byte.MaxValue);
                        for (int index = 0; index < expected.Length; index++)
                        {
                            byteExpected[index + 1] = (byte)expected[index];
                        }

                        scratchStorage.AsSpan().Fill(int.MinValue);
                        Av1SelfGuidedFilter.FilterBlock<byte>(
                            byteSource,
                            sourceStride,
                            byteActual.AsSpan(1, actual.Length),
                            destinationStride,
                            width,
                            height,
                            bitDepth,
                            parameterSetIndex,
                            projectionCoefficients,
                            scratch);

                        Assert.True(
                            byteExpected.AsSpan().SequenceEqual(byteActual),
                            $"Byte width={width}, height={height}, parameterSet={parameterSetIndex}");

                        Assert.Equal(int.MinValue, scratchStorage[0]);
                        Assert.Equal(int.MinValue, scratchStorage[^1]);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Populates the bordered source with deterministic values spanning the selected sample precision.
    /// </summary>
    /// <param name="source">The complete bordered source storage.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="maximumSample">The largest encoded sample value.</param>
    private static void FillSource(Span<ushort> source, int sourceStride, int maximumSample)
    {
        int rowCount = source.Length / sourceStride;
        for (int row = 0; row < rowCount; row++)
        {
            for (int column = 0; column < sourceStride; column++)
            {
                int value = (row * 239) + (column * 101) + (row * column * 17) + (((row + column) & 3) * (maximumSample / 3));
                source[(row * sourceStride) + column] = (ushort)(value & maximumSample);
            }
        }

        // Exact endpoints make clipping and the full local-variance range observable without depending on random input.
        source[0] = 0;
        source[^1] = (ushort)maximumSample;
    }

    /// <summary>
    /// Applies the normative projection to direct-window self-guided results.
    /// </summary>
    /// <param name="source">The source rectangle beginning three samples above and left of the processing unit.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="destination">The destination storage beginning at the restored processing-unit origin.</param>
    /// <param name="destinationStride">The number of samples between destination rows.</param>
    /// <param name="width">The processing-unit width.</param>
    /// <param name="height">The processing-unit height.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    /// <param name="parameterSetIndex">The self-guided parameter-set index.</param>
    /// <param name="projectionCoefficients">The two transmitted projection coefficients.</param>
    private static void FilterReference(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        int bitDepth,
        int parameterSetIndex,
        ReadOnlySpan<int> projectionCoefficients)
    {
        int parameterOffset = parameterSetIndex * 2;
        int radius0 = ParameterRadii[parameterOffset];
        int radius1 = ParameterRadii[parameterOffset + 1];
        int scale0 = ParameterScales[parameterOffset];
        int scale1 = ParameterScales[parameterOffset + 1];
        int projection0;
        int projection1;

        if (radius0 == 0)
        {
            projection0 = 0;
            projection1 = (1 << ProjectionBits) - projectionCoefficients[1];
        }
        else if (radius1 == 0)
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
            for (int column = 0; column < width; column++)
            {
                int unfiltered = source[sourceRowOffset + column + Border] << RestorationBits;
                int projected = unfiltered << ProjectionBits;

                if (radius0 > 0)
                {
                    int filtered0 = CalculateFilteredSample(source, sourceStride, column, row, bitDepth, radius0, scale0);
                    projected += projection0 * (filtered0 - unfiltered);
                }

                if (radius1 > 0)
                {
                    int filtered1 = CalculateFilteredSample(source, sourceStride, column, row, bitDepth, radius1, scale1);
                    projected += projection1 * (filtered1 - unfiltered);
                }

                destination[destinationRowOffset + column] =
                    (ushort)Math.Clamp(RoundPowerOfTwo(projected, ProjectionBits + RestorationBits), 0, maximumSample);
            }
        }
    }

    /// <summary>
    /// Calculates one fixed-point filtered sample directly from its local coefficient windows.
    /// </summary>
    /// <param name="source">The bordered source rectangle.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="column">The processing-unit column.</param>
    /// <param name="row">The processing-unit row.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    /// <param name="radius">The selected filter radius.</param>
    /// <param name="scale">The selected variance scale.</param>
    /// <returns>The filtered sample with four fractional bits.</returns>
    private static int CalculateFilteredSample(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int column,
        int row,
        int bitDepth,
        int radius,
        int scale)
    {
        int blendFactor = 0;
        int localMean = 0;
        int roundingBits;

        if (radius == 2 && (row & 1) == 0)
        {
            for (int coefficientRow = row - 1; coefficientRow <= row + 1; coefficientRow += 2)
            {
                for (int coefficientColumn = column - 1; coefficientColumn <= column + 1; coefficientColumn++)
                {
                    int weight = coefficientColumn == column ? 6 : 5;
                    (int localBlendFactor, int localMeanValue) =
                        CalculateCoefficient(source, sourceStride, coefficientColumn, coefficientRow, bitDepth, radius, scale);

                    blendFactor += weight * localBlendFactor;
                    localMean += weight * localMeanValue;
                }
            }

            roundingBits = 9;
        }
        else if (radius == 2)
        {
            for (int coefficientColumn = column - 1; coefficientColumn <= column + 1; coefficientColumn++)
            {
                int weight = coefficientColumn == column ? 6 : 5;
                (int localBlendFactor, int localMeanValue) =
                    CalculateCoefficient(source, sourceStride, coefficientColumn, row, bitDepth, radius, scale);

                blendFactor += weight * localBlendFactor;
                localMean += weight * localMeanValue;
            }

            roundingBits = 8;
        }
        else
        {
            for (int coefficientRow = row - 1; coefficientRow <= row + 1; coefficientRow++)
            {
                for (int coefficientColumn = column - 1; coefficientColumn <= column + 1; coefficientColumn++)
                {
                    int weight = coefficientRow == row || coefficientColumn == column ? 4 : 3;
                    (int localBlendFactor, int localMeanValue) =
                        CalculateCoefficient(source, sourceStride, coefficientColumn, coefficientRow, bitDepth, radius, scale);

                    blendFactor += weight * localBlendFactor;
                    localMean += weight * localMeanValue;
                }
            }

            roundingBits = 9;
        }

        int sample = source[((row + Border) * sourceStride) + column + Border];
        return RoundPowerOfTwo((blendFactor * sample) + localMean, roundingBits);
    }

    /// <summary>
    /// Calculates the blend factor and scaled local mean for one coefficient location by visiting every window sample.
    /// </summary>
    /// <param name="source">The bordered source rectangle.</param>
    /// <param name="sourceStride">The number of samples between source rows.</param>
    /// <param name="column">The coefficient column relative to the processing unit.</param>
    /// <param name="row">The coefficient row relative to the processing unit.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    /// <param name="radius">The square-window radius.</param>
    /// <param name="scale">The variance scale.</param>
    /// <returns>The local blend factor and scaled mean.</returns>
    private static (int BlendFactor, int LocalMean) CalculateCoefficient(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int column,
        int row,
        int bitDepth,
        int radius,
        int scale)
    {
        int centerX = column + Border;
        int centerY = row + Border;
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

        int diameter = (radius * 2) + 1;
        int windowArea = diameter * diameter;
        int normalizedSquareSum = RoundPowerOfTwo(squareSum, 2 * (bitDepth - 8));
        int normalizedSum = RoundPowerOfTwo(sum, bitDepth - 8);
        uint squareOfSum = (uint)normalizedSum * (uint)normalizedSum;
        uint scaledSquareSum = (uint)normalizedSquareSum * (uint)windowArea;
        uint variance = scaledSquareSum < squareOfSum ? 0 : scaledSquareSum - squareOfSum;
        uint varianceIndex = Math.Min(RoundPowerOfTwo(variance * (uint)scale, 20), 255U);

        // The endpoint exceptions are part of the normative table. The middle values are the rounded x / (x + 1) ratio in Q8.
        int blendFactor = varianceIndex switch
        {
            0 => 1,
            255 => 256,
            _ => (int)(((varianceIndex << 8) + ((varianceIndex + 1) >> 1)) / (varianceIndex + 1)),
        };

        uint reciprocal = radius == 1 ? 455U : 164U;
        uint meanProduct = (uint)(256 - blendFactor) * (uint)sum * reciprocal;
        int localMean = (int)RoundPowerOfTwo(meanProduct, 12);
        return (blendFactor, localMean);
    }

    /// <summary>
    /// Verifies visible samples and confirms that the filter does not overwrite destination-row padding.
    /// </summary>
    /// <param name="expected">The direct-window output.</param>
    /// <param name="actual">The production output.</param>
    /// <param name="stride">The number of samples between destination rows.</param>
    /// <param name="width">The processing-unit width.</param>
    /// <param name="height">The processing-unit height.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    /// <param name="parameterSetIndex">The self-guided parameter-set index.</param>
    private static void AssertBlockEqual(
        ReadOnlySpan<ushort> expected,
        ReadOnlySpan<ushort> actual,
        int stride,
        int width,
        int height,
        int bitDepth,
        int parameterSetIndex)
    {
        for (int row = 0; row < height; row++)
        {
            int rowOffset = row * stride;
            for (int column = 0; column < width; column++)
            {
                if (expected[rowOffset + column] != actual[rowOffset + column])
                {
                    Assert.Fail(
                        $"Self-guided parameter {parameterSetIndex}, {bitDepth}-bit block differs at ({column}, {row}): "
                        + $"expected {expected[rowOffset + column]}, actual {actual[rowOffset + column]}.");
                }
            }

            for (int column = width; column < stride; column++)
            {
                Assert.Equal(ushort.MaxValue, actual[rowOffset + column]);
            }
        }
    }

    /// <summary>
    /// Rounds a signed fixed-point value to the requested lower precision.
    /// </summary>
    /// <param name="value">The signed fixed-point value.</param>
    /// <param name="bitCount">The number of low bits to discard.</param>
    /// <returns>The rounded signed value.</returns>
    private static int RoundPowerOfTwo(int value, int bitCount)
        => bitCount == 0 ? value : (value + (1 << (bitCount - 1))) >> bitCount;

    /// <summary>
    /// Rounds an unsigned fixed-point value to the requested lower precision.
    /// </summary>
    /// <param name="value">The unsigned fixed-point value.</param>
    /// <param name="bitCount">The number of low bits to discard.</param>
    /// <returns>The rounded unsigned value.</returns>
    private static uint RoundPowerOfTwo(uint value, int bitCount)
        => bitCount == 0 ? value : (value + (1U << (bitCount - 1))) >> bitCount;
}
