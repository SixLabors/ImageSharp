// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 constrained directional enhancement filtering across sample precision, block geometry, and intrinsic tiers.
/// </summary>
[Trait("Format", "Avif")]
public class Av1CdefFilterTests
{
    /// <summary>
    /// The hardware configurations required to exercise packed filtering and the scalar fallback.
    /// </summary>
    private const HwIntrinsics Configurations = HwIntrinsics.AllowAll | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// The row stride of the bordered source plane used by the filter tests.
    /// </summary>
    private const int SourceStride = 16;

    /// <summary>
    /// The number of unavailable samples surrounding the test image.
    /// </summary>
    private const int SourceBorder = 2;

    /// <summary>
    /// Verifies direction selection and variance against an independent scalar definition.
    /// </summary>
    [Fact]
    public void FindDirectionMatchesIndependentDefinitionAcrossIntrinsicTiers()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateDirections, Configurations);

    /// <summary>
    /// Verifies every CDEF block geometry and strength mode against an independent scalar definition.
    /// </summary>
    [Fact]
    public void FilterBlockMatchesIndependentDefinitionAcrossIntrinsicTiers()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateFilters, Configurations);

    /// <summary>
    /// Verifies the complete asymmetric chroma direction mappings and the unchanged symmetric mappings.
    /// </summary>
    [Fact]
    public void ConvertDirectionMatchesSubsamplingGeometry()
    {
        int[] horizontalSubsampling = [7, 0, 2, 4, 5, 6, 6, 6];
        int[] verticalSubsampling = [1, 2, 2, 2, 3, 4, 6, 0];
        for (int direction = 0; direction < 8; direction++)
        {
            Assert.Equal(horizontalSubsampling[direction], Av1CdefFilter.ConvertDirection(direction, 1, 0));
            Assert.Equal(verticalSubsampling[direction], Av1CdefFilter.ConvertDirection(direction, 0, 1));
            Assert.Equal(direction, Av1CdefFilter.ConvertDirection(direction, 0, 0));
            Assert.Equal(direction, Av1CdefFilter.ConvertDirection(direction, 1, 1));
        }
    }

    /// <summary>
    /// Verifies luma strength adjustment at zero, logarithmic-class boundaries, and the capped variance class.
    /// </summary>
    [Fact]
    public void AdjustStrengthMatchesIndependentDefinition()
    {
        foreach (int strength in new[] { 0, 4, 15, 60 })
        {
            foreach (int variance in new[] { 0, 1, 63, 64, 255, 4096, 1 << 20 })
            {
                int varianceClass = variance >> 6;
                int adjustment = varianceClass == 0 ? 0 : Math.Min(BitOperations.Log2((uint)varianceClass), 12);
                int expected = variance == 0 ? 0 : ((strength * (4 + adjustment)) + 8) >> 4;
                Assert.Equal(expected, Av1CdefFilter.AdjustStrength(strength, variance));
            }
        }
    }

    /// <summary>
    /// Verifies that the deterministic direction corpus exercises every selected-direction branch.
    /// </summary>
    [Fact]
    public void DirectionCorpusCoversEveryDirection()
    {
        const int stride = 32;
        const int sourceOffset = (4 * stride) + 5;
        int secondSourceOffset = sourceOffset + 8;
        HashSet<int> observedDirections = [];
        for (int pattern = 0; pattern < 8; pattern++)
        {
            ushort[] source = new ushort[stride * 16];
            PopulateDirectionSource(source, sourceOffset, stride, pattern, 8);
            observedDirections.Add(FindDirectionReference(source, sourceOffset, stride, 0, out _));
            observedDirections.Add(FindDirectionReference(source, secondSourceOffset, stride, 0, out _));
        }

        Assert.Equal(Enumerable.Range(0, 8), observedDirections.Order());
    }

    /// <summary>
    /// Exercises direction search with multiple source patterns at every supported sample precision.
    /// </summary>
    private static void ValidateDirections()
    {
        const int stride = 32;
        const int sourceOffset = (4 * stride) + 5;

        foreach (int bitDepth in new[] { 8, 10, 12 })
        {
            int coefficientShift = bitDepth - 8;
            for (int pattern = 0; pattern < 8; pattern++)
            {
                ushort[] source = new ushort[stride * 16];
                int secondSourceOffset = sourceOffset + 8;
                PopulateDirectionSource(source, sourceOffset, stride, pattern, bitDepth);

                int expectedDirection = FindDirectionReference(source, sourceOffset, stride, coefficientShift, out int expectedVariance);
                int actualDirection = Av1CdefFilter.FindDirection(source, sourceOffset, stride, coefficientShift, out int actualVariance);
                Assert.Equal(expectedDirection, actualDirection);
                Assert.Equal(expectedVariance, actualVariance);

                int secondExpectedDirection = FindDirectionReference(source, secondSourceOffset, stride, coefficientShift, out int secondExpectedVariance);
                Av1CdefFilter.FindDirections(
                    source,
                    sourceOffset,
                    secondSourceOffset,
                    stride,
                    coefficientShift,
                    out int firstActualDirection,
                    out int firstActualVariance,
                    out int secondActualDirection,
                    out int secondActualVariance);

                Assert.Equal(expectedDirection, firstActualDirection);
                Assert.Equal(expectedVariance, firstActualVariance);
                Assert.Equal(secondExpectedDirection, secondActualDirection);
                Assert.Equal(secondExpectedVariance, secondActualVariance);
            }
        }
    }

    /// <summary>
    /// Populates two adjacent 8x8 blocks with deterministic directional samples.
    /// </summary>
    /// <param name="source">The destination source plane.</param>
    /// <param name="sourceOffset">The first populated sample.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="pattern">The deterministic pattern index.</param>
    /// <param name="bitDepth">The sample precision.</param>
    private static void PopulateDirectionSource(Span<ushort> source, int sourceOffset, int sourceStride, int pattern, int bitDepth)
    {
        int coefficientShift = bitDepth - 8;
        int maximum = (1 << bitDepth) - 1;
        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 16; column++)
            {
                int localColumn = column & 7;
                int direction = column < 8 ? pattern : (pattern + 4) & 7;
                int line = GetDirectionLineReference(direction, row, localColumn);

                // Samples are constant along the requested geometric line and vary between lines. Direction search
                // therefore minimizes reconstruction error in that direction while still exercising nonuniform values.
                int value = (24 + (line * 14)) << coefficientShift;
                source[sourceOffset + (row * sourceStride) + column] = (ushort)(value & maximum);
            }
        }
    }

    /// <summary>
    /// Maps one source coordinate to its line in an AV1 direction independently of the production implementation.
    /// </summary>
    /// <param name="direction">The zero-based AV1 direction index.</param>
    /// <param name="row">The source row.</param>
    /// <param name="column">The source column.</param>
    /// <returns>The zero-based line index.</returns>
    private static int GetDirectionLineReference(int direction, int row, int column) => direction switch
    {
        0 => row + column,
        1 => row + (column / 2),
        2 => row,
        3 => 3 + row - (column / 2),
        4 => 7 + row - column,
        5 => 3 - (row / 2) + column,
        6 => column,
        _ => (row / 2) + column
    };

    /// <summary>
    /// Exercises all normative block dimensions, directions, strength combinations, output types, and coded precisions.
    /// </summary>
    private static void ValidateFilters()
    {
        (int Width, int Height)[] dimensions = [(4, 4), (4, 8), (8, 4), (8, 8)];
        foreach (int bitDepth in new[] { 8, 10, 12 })
        {
            int coefficientShift = bitDepth - 8;
            int scale = 1 << coefficientShift;
            ushort[] source = CreateBorderedSource(bitDepth);
            int sourceOffset = (SourceBorder * SourceStride) + SourceBorder;
            (int Primary, int Secondary)[] strengths = [(0, 0), (4 * scale, 0), (0, 2 * scale), (5 * scale, 2 * scale)];

            foreach ((int blockWidth, int blockHeight) in dimensions)
            {
                foreach (int direction in Enumerable.Range(0, 8))
                {
                    foreach ((int primaryStrength, int secondaryStrength) in strengths)
                    {
                        if (bitDepth == 8)
                        {
                            AssertByteFilter(
                                source,
                                sourceOffset,
                                blockWidth,
                                blockHeight,
                                primaryStrength,
                                secondaryStrength,
                                direction,
                                coefficientShift);
                        }

                        AssertUInt16Filter(
                            source,
                            sourceOffset,
                            blockWidth,
                            blockHeight,
                            primaryStrength,
                            secondaryStrength,
                            direction,
                            coefficientShift);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Creates a deterministic image whose top-left output block touches the unavailable-neighbor border.
    /// </summary>
    /// <param name="bitDepth">The source sample precision.</param>
    /// <returns>The bordered 16-bit source plane.</returns>
    private static ushort[] CreateBorderedSource(int bitDepth)
    {
        ushort[] source = Enumerable.Repeat(Av1CdefFilter.VeryLarge, SourceStride * SourceStride).ToArray();
        int maximum = (1 << bitDepth) - 1;
        int scale = 1 << (bitDepth - 8);
        for (int row = SourceBorder; row < SourceStride - SourceBorder; row++)
        {
            for (int column = SourceBorder; column < SourceStride - SourceBorder; column++)
            {
                int localRow = row - SourceBorder;
                int localColumn = column - SourceBorder;
                int value = (72 + (localRow * 9) + (localColumn * 5) + ((localRow * localColumn) & 15)) * scale;
                source[(row * SourceStride) + column] = (ushort)Math.Min(value, maximum);
            }
        }

        return source;
    }

    /// <summary>
    /// Verifies one eight-bit output block while retaining untouched destination padding in the comparison.
    /// </summary>
    /// <param name="source">The bordered source plane.</param>
    /// <param name="sourceOffset">The first source sample in the block.</param>
    /// <param name="blockWidth">The output block width.</param>
    /// <param name="blockHeight">The output block height.</param>
    /// <param name="primaryStrength">The primary filter strength.</param>
    /// <param name="secondaryStrength">The secondary filter strength.</param>
    /// <param name="direction">The primary filter direction.</param>
    /// <param name="coefficientShift">The source precision shift.</param>
    private static void AssertByteFilter(
        ushort[] source,
        int sourceOffset,
        int blockWidth,
        int blockHeight,
        int primaryStrength,
        int secondaryStrength,
        int direction,
        int coefficientShift)
    {
        const int destinationStride = 12;
        const int destinationOffset = destinationStride + 1;
        byte[] expected = Enumerable.Repeat((byte)231, destinationStride * 10).ToArray();
        byte[] actual = (byte[])expected.Clone();
        int damping = 5 + coefficientShift;

        FilterReference(
            source,
            sourceOffset,
            SourceStride,
            expected,
            destinationOffset,
            destinationStride,
            primaryStrength,
            secondaryStrength,
            direction,
            damping,
            coefficientShift,
            blockWidth,
            blockHeight);

        Av1CdefFilter.FilterBlock(
            source,
            sourceOffset,
            SourceStride,
            actual,
            destinationOffset,
            destinationStride,
            primaryStrength,
            secondaryStrength,
            direction,
            damping,
            damping,
            coefficientShift,
            blockWidth,
            blockHeight);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Verifies one 16-bit output block while retaining untouched destination padding in the comparison.
    /// </summary>
    /// <param name="source">The bordered source plane.</param>
    /// <param name="sourceOffset">The first source sample in the block.</param>
    /// <param name="blockWidth">The output block width.</param>
    /// <param name="blockHeight">The output block height.</param>
    /// <param name="primaryStrength">The primary filter strength.</param>
    /// <param name="secondaryStrength">The secondary filter strength.</param>
    /// <param name="direction">The primary filter direction.</param>
    /// <param name="coefficientShift">The source precision shift.</param>
    private static void AssertUInt16Filter(
        ushort[] source,
        int sourceOffset,
        int blockWidth,
        int blockHeight,
        int primaryStrength,
        int secondaryStrength,
        int direction,
        int coefficientShift)
    {
        const int destinationStride = 12;
        const int destinationOffset = destinationStride + 1;
        ushort[] expected = Enumerable.Repeat((ushort)60000, destinationStride * 10).ToArray();
        ushort[] actual = (ushort[])expected.Clone();
        int damping = 5 + coefficientShift;

        FilterReference(
            source,
            sourceOffset,
            SourceStride,
            expected,
            destinationOffset,
            destinationStride,
            primaryStrength,
            secondaryStrength,
            direction,
            damping,
            coefficientShift,
            blockWidth,
            blockHeight);

        Av1CdefFilter.FilterBlock(
            source,
            sourceOffset,
            SourceStride,
            actual,
            destinationOffset,
            destinationStride,
            primaryStrength,
            secondaryStrength,
            direction,
            damping,
            damping,
            coefficientShift,
            blockWidth,
            blockHeight);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Finds one direction and variance using the scalar AV1 definition independently of the production layouts.
    /// </summary>
    /// <param name="source">The source plane.</param>
    /// <param name="sourceOffset">The first sample in the 8x8 block.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="coefficientShift">The source precision shift.</param>
    /// <param name="variance">Receives the directional variance.</param>
    /// <returns>The selected direction.</returns>
    private static int FindDirectionReference(
        ReadOnlySpan<ushort> source,
        int sourceOffset,
        int sourceStride,
        int coefficientShift,
        out int variance)
    {
        int[,] partials = new int[8, 15];
        int[] costs = new int[8];
        int[] divisions = [0, 840, 420, 280, 210, 168, 140, 120, 105];
        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 8; column++)
            {
                int value = (source[sourceOffset + (row * sourceStride) + column] >> coefficientShift) - 128;
                partials[0, row + column] += value;
                partials[1, row + (column / 2)] += value;
                partials[2, row] += value;
                partials[3, 3 + row - (column / 2)] += value;
                partials[4, 7 + row - column] += value;
                partials[5, 3 - (row / 2) + column] += value;
                partials[6, column] += value;
                partials[7, (row / 2) + column] += value;
            }
        }

        for (int line = 0; line < 8; line++)
        {
            costs[2] += partials[2, line] * partials[2, line];
            costs[6] += partials[6, line] * partials[6, line];
        }

        costs[2] *= divisions[8];
        costs[6] *= divisions[8];
        for (int line = 0; line < 7; line++)
        {
            costs[0] += ((partials[0, line] * partials[0, line]) + (partials[0, 14 - line] * partials[0, 14 - line])) * divisions[line + 1];
            costs[4] += ((partials[4, line] * partials[4, line]) + (partials[4, 14 - line] * partials[4, 14 - line])) * divisions[line + 1];
        }

        costs[0] += partials[0, 7] * partials[0, 7] * divisions[8];
        costs[4] += partials[4, 7] * partials[4, 7] * divisions[8];
        for (int direction = 1; direction < 8; direction += 2)
        {
            for (int line = 0; line < 5; line++)
            {
                costs[direction] += partials[direction, 3 + line] * partials[direction, 3 + line];
            }

            costs[direction] *= divisions[8];
            for (int line = 0; line < 3; line++)
            {
                costs[direction] += ((partials[direction, line] * partials[direction, line])
                    + (partials[direction, 10 - line] * partials[direction, 10 - line])) * divisions[(2 * line) + 2];
            }
        }

        int bestCost = 0;
        int bestDirection = 0;
        for (int direction = 0; direction < 8; direction++)
        {
            if (costs[direction] > bestCost)
            {
                bestCost = costs[direction];
                bestDirection = direction;
            }
        }

        variance = (bestCost - costs[(bestDirection + 4) & 7]) >> 10;
        return bestDirection;
    }

    /// <summary>
    /// Applies the independent scalar filter definition to eight-bit output storage.
    /// </summary>
    private static void FilterReference(
        ReadOnlySpan<ushort> source,
        int sourceOffset,
        int sourceStride,
        Span<byte> destination,
        int destinationOffset,
        int destinationStride,
        int primaryStrength,
        int secondaryStrength,
        int direction,
        int damping,
        int coefficientShift,
        int blockWidth,
        int blockHeight)
    {
        for (int row = 0; row < blockHeight; row++)
        {
            for (int column = 0; column < blockWidth; column++)
            {
                destination[destinationOffset + (row * destinationStride) + column] = (byte)FilterSampleReference(
                    source,
                    sourceOffset + (row * sourceStride) + column,
                    sourceStride,
                    primaryStrength,
                    secondaryStrength,
                    direction,
                    damping,
                    coefficientShift);
            }
        }
    }

    /// <summary>
    /// Applies the independent scalar filter definition to 16-bit output storage.
    /// </summary>
    private static void FilterReference(
        ReadOnlySpan<ushort> source,
        int sourceOffset,
        int sourceStride,
        Span<ushort> destination,
        int destinationOffset,
        int destinationStride,
        int primaryStrength,
        int secondaryStrength,
        int direction,
        int damping,
        int coefficientShift,
        int blockWidth,
        int blockHeight)
    {
        for (int row = 0; row < blockHeight; row++)
        {
            for (int column = 0; column < blockWidth; column++)
            {
                destination[destinationOffset + (row * destinationStride) + column] = (ushort)FilterSampleReference(
                    source,
                    sourceOffset + (row * sourceStride) + column,
                    sourceStride,
                    primaryStrength,
                    secondaryStrength,
                    direction,
                    damping,
                    coefficientShift);
            }
        }
    }

    /// <summary>
    /// Computes one independently filtered sample from its primary and secondary neighbors.
    /// </summary>
    private static int FilterSampleReference(
        ReadOnlySpan<ushort> source,
        int sourceIndex,
        int sourceStride,
        int primaryStrength,
        int secondaryStrength,
        int direction,
        int damping,
        int coefficientShift)
    {
        bool enablePrimary = primaryStrength != 0;
        bool enableSecondary = secondaryStrength != 0;
        bool clippingRequired = enablePrimary && enableSecondary;
        int primaryTapSet = (primaryStrength >> coefficientShift) & 1;
        int sample = source[sourceIndex];
        int sum = 0;
        int minimum = sample;
        int maximum = sample;

        for (int tap = 0; tap < 2; tap++)
        {
            if (enablePrimary)
            {
                int offset = GetDirectionOffsetReference(direction, tap, sourceStride);
                int neighbor0 = source[sourceIndex + offset];
                int neighbor1 = source[sourceIndex - offset];
                int weight = primaryTapSet == 0 ? (tap == 0 ? 4 : 2) : 3;
                sum += weight * ConstrainReference(neighbor0 - sample, primaryStrength, damping);
                sum += weight * ConstrainReference(neighbor1 - sample, primaryStrength, damping);

                if (clippingRequired)
                {
                    maximum = neighbor0 != Av1CdefFilter.VeryLarge ? Math.Max(maximum, neighbor0) : maximum;
                    maximum = neighbor1 != Av1CdefFilter.VeryLarge ? Math.Max(maximum, neighbor1) : maximum;
                    minimum = Math.Min(minimum, Math.Min(neighbor0, neighbor1));
                }
            }

            if (enableSecondary)
            {
                int offset0 = GetDirectionOffsetReference((direction + 2) & 7, tap, sourceStride);
                int offset1 = GetDirectionOffsetReference((direction + 6) & 7, tap, sourceStride);
                int neighbor0 = source[sourceIndex + offset0];
                int neighbor1 = source[sourceIndex - offset0];
                int neighbor2 = source[sourceIndex + offset1];
                int neighbor3 = source[sourceIndex - offset1];
                int weight = tap == 0 ? 2 : 1;
                sum += weight * ConstrainReference(neighbor0 - sample, secondaryStrength, damping);
                sum += weight * ConstrainReference(neighbor1 - sample, secondaryStrength, damping);
                sum += weight * ConstrainReference(neighbor2 - sample, secondaryStrength, damping);
                sum += weight * ConstrainReference(neighbor3 - sample, secondaryStrength, damping);

                if (clippingRequired)
                {
                    maximum = neighbor0 != Av1CdefFilter.VeryLarge ? Math.Max(maximum, neighbor0) : maximum;
                    maximum = neighbor1 != Av1CdefFilter.VeryLarge ? Math.Max(maximum, neighbor1) : maximum;
                    maximum = neighbor2 != Av1CdefFilter.VeryLarge ? Math.Max(maximum, neighbor2) : maximum;
                    maximum = neighbor3 != Av1CdefFilter.VeryLarge ? Math.Max(maximum, neighbor3) : maximum;
                    minimum = Math.Min(minimum, Math.Min(Math.Min(neighbor0, neighbor1), Math.Min(neighbor2, neighbor3)));
                }
            }
        }

        int filtered = sample + ((8 + sum - (sum < 0 ? 1 : 0)) >> 4);
        return clippingRequired ? Math.Clamp(filtered, minimum, maximum) : filtered;
    }

    /// <summary>
    /// Applies the scalar AV1 constrain equation independently of the production implementation.
    /// </summary>
    private static int ConstrainReference(int difference, int threshold, int damping)
    {
        int shift = Math.Max(0, damping - BitOperations.Log2((uint)threshold));
        int magnitude = Math.Abs(difference);
        int constrained = Math.Clamp(threshold - (magnitude >> shift), 0, magnitude);
        return difference < 0 ? -constrained : constrained;
    }

    /// <summary>
    /// Converts a direction and tap to a signed source offset independently of the production implementation.
    /// </summary>
    private static int GetDirectionOffsetReference(int direction, int tap, int stride)
    {
        int[] x0 = [1, 1, 1, 1, 1, 0, 0, 0];
        int[] y0 = [-1, 0, 0, 0, 1, 1, 1, 1];
        int[] x1 = [2, 2, 2, 2, 2, 1, 0, -1];
        int[] y1 = [-2, -1, 0, 1, 2, 2, 2, 2];
        return tap == 0 ? (y0[direction] * stride) + x0[direction] : (y1[direction] * stride) + x1[direction];
    }
}
