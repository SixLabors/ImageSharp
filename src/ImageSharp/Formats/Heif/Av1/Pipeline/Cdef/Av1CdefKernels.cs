// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;

/// <summary>
/// Provides the scalar constrained directional enhancement filter operations defined by AV1.
/// </summary>
internal static class Av1CdefKernels
{
    /// <summary>
    /// The sample value used for neighbors outside the coded frame.
    /// </summary>
    public const ushort VeryLarge = 0x4000;

    /// <summary>
    /// The horizontal offsets for the nearest primary or secondary taps in each direction.
    /// </summary>
    private static readonly int[] DirectionX1 = [1, 1, 1, 1, 1, 0, 0, 0];

    /// <summary>
    /// The vertical offsets for the nearest primary or secondary taps in each direction.
    /// </summary>
    private static readonly int[] DirectionY1 = [-1, 0, 0, 0, 1, 1, 1, 1];

    /// <summary>
    /// The horizontal offsets for the furthest primary or secondary taps in each direction.
    /// </summary>
    private static readonly int[] DirectionX2 = [2, 2, 2, 2, 2, 1, 0, -1];

    /// <summary>
    /// The vertical offsets for the furthest primary or secondary taps in each direction.
    /// </summary>
    private static readonly int[] DirectionY2 = [-2, -1, 0, 1, 2, 2, 2, 2];

    /// <summary>
    /// The primary-tap weights selected by the parity of the unscaled primary strength.
    /// </summary>
    private static readonly int[,] PrimaryTaps =
    {
        { 4, 2 },
        { 3, 3 }
    };

    /// <summary>
    /// The secondary-tap weights for the nearest and furthest samples.
    /// </summary>
    private static readonly int[] SecondaryTaps = [2, 1];

    /// <summary>
    /// The common multiples used to compare line variance without division.
    /// </summary>
    private static readonly int[] DivisionTable = [0, 840, 420, 280, 210, 168, 140, 120, 105];

    /// <summary>
    /// The direction mapping for horizontally subsampled, vertically full-resolution chroma.
    /// </summary>
    private static readonly int[] DirectionMap422 = [7, 0, 2, 4, 5, 6, 6, 6];

    /// <summary>
    /// The direction mapping for horizontally full-resolution, vertically subsampled chroma.
    /// </summary>
    private static readonly int[] DirectionMap440 = [1, 2, 2, 2, 3, 4, 6, 0];

    /// <summary>
    /// Finds the dominant direction of an 8x8 luma block and its directional variance.
    /// </summary>
    /// <param name="source">The bordered, deblocked source plane.</param>
    /// <param name="sourceOffset">The offset of the block's top-left sample.</param>
    /// <param name="sourceStride">The number of samples between adjacent source rows.</param>
    /// <param name="coefficientShift">The number of bits above the eight-bit analysis precision.</param>
    /// <param name="variance">Receives the variance difference between the selected and orthogonal directions.</param>
    /// <returns>The zero-based AV1 direction index.</returns>
    public static int FindDirection(
        ReadOnlySpan<ushort> source,
        int sourceOffset,
        int sourceStride,
        int coefficientShift,
        out int variance)
    {
        Span<int> partial = stackalloc int[8 * 15];
        Span<int> cost = stackalloc int[8];
        partial.Clear();
        cost.Clear();

        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 8; column++)
            {
                // Direction analysis deliberately reduces every source to eight-bit precision so its
                // strength selection is identical for 8-, 10-, and 12-bit coded images.
                int value = (source[sourceOffset + (row * sourceStride) + column] >> coefficientShift) - 128;
                partial[(0 * 15) + row + column] += value;
                partial[(1 * 15) + row + (column / 2)] += value;
                partial[(2 * 15) + row] += value;
                partial[(3 * 15) + 3 + row - (column / 2)] += value;
                partial[(4 * 15) + 7 + row - column] += value;
                partial[(5 * 15) + 3 - (row / 2) + column] += value;
                partial[(6 * 15) + column] += value;
                partial[(7 * 15) + (row / 2) + column] += value;
            }
        }

        for (int i = 0; i < 8; i++)
        {
            cost[2] += partial[(2 * 15) + i] * partial[(2 * 15) + i];
            cost[6] += partial[(6 * 15) + i] * partial[(6 * 15) + i];
        }

        cost[2] *= DivisionTable[8];
        cost[6] *= DivisionTable[8];
        for (int i = 0; i < 7; i++)
        {
            cost[0] += ((partial[(0 * 15) + i] * partial[(0 * 15) + i]) +
                (partial[(0 * 15) + 14 - i] * partial[(0 * 15) + 14 - i])) * DivisionTable[i + 1];
            cost[4] += ((partial[(4 * 15) + i] * partial[(4 * 15) + i]) +
                (partial[(4 * 15) + 14 - i] * partial[(4 * 15) + 14 - i])) * DivisionTable[i + 1];
        }

        cost[0] += partial[(0 * 15) + 7] * partial[(0 * 15) + 7] * DivisionTable[8];
        cost[4] += partial[(4 * 15) + 7] * partial[(4 * 15) + 7] * DivisionTable[8];
        for (int direction = 1; direction < 8; direction += 2)
        {
            for (int i = 0; i < 5; i++)
            {
                cost[direction] += partial[(direction * 15) + 3 + i] * partial[(direction * 15) + 3 + i];
            }

            cost[direction] *= DivisionTable[8];
            for (int i = 0; i < 3; i++)
            {
                cost[direction] += ((partial[(direction * 15) + i] * partial[(direction * 15) + i]) +
                    (partial[(direction * 15) + 10 - i] * partial[(direction * 15) + 10 - i])) * DivisionTable[(2 * i) + 2];
            }
        }

        int bestCost = 0;
        int bestDirection = 0;
        for (int direction = 0; direction < 8; direction++)
        {
            if (cost[direction] > bestCost)
            {
                bestCost = cost[direction];
                bestDirection = direction;
            }
        }

        // Both costs omit the same sum-of-squares term. Their scaled difference is the
        // directional variance consumed by AV1's luma strength adjustment.
        variance = (bestCost - cost[(bestDirection + 4) & 7]) >> 10;
        return bestDirection;
    }

    /// <summary>
    /// Adjusts a luma primary strength according to the directional variance of its 8x8 block.
    /// </summary>
    /// <param name="strength">The bit-depth-scaled primary strength.</param>
    /// <param name="variance">The directional variance returned by <see cref="FindDirection"/>.</param>
    /// <returns>The variance-adjusted primary strength.</returns>
    public static int AdjustStrength(int strength, int variance)
    {
        int varianceClass = variance >> 6;
        int adjustment = varianceClass != 0 ? Math.Min(Av1Math.MostSignificantBit((uint)varianceClass), 12) : 0;
        return variance != 0 ? ((strength * (4 + adjustment)) + 8) >> 4 : 0;
    }

    /// <summary>
    /// Converts a luma direction to the matching chroma direction for asymmetric subsampling.
    /// </summary>
    /// <param name="direction">The zero-based luma direction index.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <returns>The direction index in the chroma sample grid.</returns>
    public static int ConvertDirection(int direction, int subsamplingX, int subsamplingY)
    {
        if (subsamplingX == subsamplingY)
        {
            return direction;
        }

        return subsamplingX != 0 ? DirectionMap422[direction] : DirectionMap440[direction];
    }

    /// <summary>
    /// Filters one luma or chroma block from an immutable bordered source plane.
    /// </summary>
    /// <param name="source">The bordered, deblocked source plane.</param>
    /// <param name="sourceOffset">The offset of the block's top-left source sample.</param>
    /// <param name="sourceStride">The number of samples between adjacent source rows.</param>
    /// <param name="destination">The unbordered filtered destination plane.</param>
    /// <param name="destinationOffset">The offset of the block's top-left destination sample.</param>
    /// <param name="destinationStride">The number of samples between adjacent destination rows.</param>
    /// <param name="primaryStrength">The bit-depth-scaled primary strength.</param>
    /// <param name="secondaryStrength">The bit-depth-scaled secondary strength.</param>
    /// <param name="direction">The zero-based AV1 direction index.</param>
    /// <param name="primaryDamping">The damping value applied to primary taps.</param>
    /// <param name="secondaryDamping">The damping value applied to secondary taps.</param>
    /// <param name="coefficientShift">The number of bits above eight-bit sample precision.</param>
    /// <param name="blockWidth">The block width in plane samples.</param>
    /// <param name="blockHeight">The block height in plane samples.</param>
    public static void FilterBlock(
        ReadOnlySpan<ushort> source,
        int sourceOffset,
        int sourceStride,
        Span<ushort> destination,
        int destinationOffset,
        int destinationStride,
        int primaryStrength,
        int secondaryStrength,
        int direction,
        int primaryDamping,
        int secondaryDamping,
        int coefficientShift,
        int blockWidth,
        int blockHeight)
    {
        bool enablePrimary = primaryStrength != 0;
        bool enableSecondary = secondaryStrength != 0;
        bool clippingRequired = enablePrimary && enableSecondary;
        int primaryTapSet = (primaryStrength >> coefficientShift) & 1;

        for (int row = 0; row < blockHeight; row++)
        {
            for (int column = 0; column < blockWidth; column++)
            {
                int sourceIndex = sourceOffset + (row * sourceStride) + column;
                int sample = source[sourceIndex];
                int sum = 0;
                int minimum = sample;
                int maximum = sample;

                for (int tap = 0; tap < 2; tap++)
                {
                    if (enablePrimary)
                    {
                        int primaryDirectionOffset = GetDirectionOffset(direction, tap, sourceStride);
                        int neighbor0 = source[sourceIndex + primaryDirectionOffset];
                        int neighbor1 = source[sourceIndex - primaryDirectionOffset];
                        int weight = PrimaryTaps[primaryTapSet, tap];
                        sum += weight * Constrain(neighbor0 - sample, primaryStrength, primaryDamping);
                        sum += weight * Constrain(neighbor1 - sample, primaryStrength, primaryDamping);

                        if (clippingRequired)
                        {
                            maximum = neighbor0 != VeryLarge ? Math.Max(maximum, neighbor0) : maximum;
                            maximum = neighbor1 != VeryLarge ? Math.Max(maximum, neighbor1) : maximum;
                            minimum = Math.Min(minimum, neighbor0);
                            minimum = Math.Min(minimum, neighbor1);
                        }
                    }

                    if (enableSecondary)
                    {
                        int secondaryDirection0 = GetDirectionOffset((direction + 2) & 7, tap, sourceStride);
                        int secondaryDirection1 = GetDirectionOffset((direction + 6) & 7, tap, sourceStride);
                        int neighbor0 = source[sourceIndex + secondaryDirection0];
                        int neighbor1 = source[sourceIndex - secondaryDirection0];
                        int neighbor2 = source[sourceIndex + secondaryDirection1];
                        int neighbor3 = source[sourceIndex - secondaryDirection1];
                        int weight = SecondaryTaps[tap];

                        if (clippingRequired)
                        {
                            maximum = neighbor0 != VeryLarge ? Math.Max(maximum, neighbor0) : maximum;
                            maximum = neighbor1 != VeryLarge ? Math.Max(maximum, neighbor1) : maximum;
                            maximum = neighbor2 != VeryLarge ? Math.Max(maximum, neighbor2) : maximum;
                            maximum = neighbor3 != VeryLarge ? Math.Max(maximum, neighbor3) : maximum;
                            minimum = Math.Min(minimum, neighbor0);
                            minimum = Math.Min(minimum, neighbor1);
                            minimum = Math.Min(minimum, neighbor2);
                            minimum = Math.Min(minimum, neighbor3);
                        }

                        sum += weight * Constrain(neighbor0 - sample, secondaryStrength, secondaryDamping);
                        sum += weight * Constrain(neighbor1 - sample, secondaryStrength, secondaryDamping);
                        sum += weight * Constrain(neighbor2 - sample, secondaryStrength, secondaryDamping);
                        sum += weight * Constrain(neighbor3 - sample, secondaryStrength, secondaryDamping);
                    }
                }

                // The negative-sum correction preserves AV1's asymmetric signed rounding exactly.
                int filtered = sample + ((8 + sum - (sum < 0 ? 1 : 0)) >> 4);
                destination[destinationOffset + (row * destinationStride) + column] =
                    (ushort)(clippingRequired ? Av1Math.Clip3(minimum, maximum, filtered) : filtered);
            }
        }
    }

    /// <summary>
    /// Converts a direction and tap number to a signed plane-buffer offset.
    /// </summary>
    /// <param name="direction">The zero-based AV1 direction index.</param>
    /// <param name="tap">The zero-based distance index.</param>
    /// <param name="stride">The number of samples between adjacent rows.</param>
    /// <returns>The signed sample offset.</returns>
    private static int GetDirectionOffset(int direction, int tap, int stride)
        => tap == 0
            ? (DirectionY1[direction] * stride) + DirectionX1[direction]
            : (DirectionY2[direction] * stride) + DirectionX2[direction];

    /// <summary>
    /// Limits a neighbor difference according to a filter strength and damping value.
    /// </summary>
    /// <param name="difference">The signed difference from the current sample.</param>
    /// <param name="threshold">The bit-depth-scaled filter strength.</param>
    /// <param name="damping">The damping value.</param>
    /// <returns>The signed constrained difference.</returns>
    private static int Constrain(int difference, int threshold, int damping)
    {
        if (threshold == 0)
        {
            return 0;
        }

        // Stronger thresholds reduce the effective damping shift, matching the AV1 constrain function.
        int shift = Math.Max(0, damping - Av1Math.MostSignificantBit((uint)threshold));
        int magnitude = Math.Abs(difference);
        int constrained = Av1Math.Clip3(0, magnitude, threshold - (magnitude >> shift));
        return difference < 0 ? -constrained : constrained;
    }
}
