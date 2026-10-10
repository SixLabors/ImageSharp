// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Assigns paired chroma samples to AV1 palette colors and refines two-dimensional palette centroids.
/// </summary>
internal static partial class Av1PaletteKMeans2D
{
    /// <summary>
    /// Assigns every chroma pair to its nearest palette color.
    /// </summary>
    /// <param name="firstSamples">The active first-plane samples.</param>
    /// <param name="secondSamples">The active second-plane samples.</param>
    /// <param name="firstCentroids">The first-plane palette colors.</param>
    /// <param name="secondCentroids">The second-plane palette colors.</param>
    /// <param name="indices">The destination palette indices.</param>
    /// <returns>The sum of the squared two-plane sample-to-color distances.</returns>
    public static long AssignIndices(
        ReadOnlySpan<short> firstSamples,
        ReadOnlySpan<short> secondSamples,
        ReadOnlySpan<short> firstCentroids,
        ReadOnlySpan<short> secondCentroids,
        Span<byte> indices)
        => Assign<NearestOperator>.Apply(firstSamples, secondSamples, firstCentroids, secondCentroids, indices);

    /// <summary>
    /// Refines initialized paired colors with a deterministic k-means sequence.
    /// </summary>
    /// <remarks>
    /// Each iteration recalculates the centroids and then assigns the pairs again. The sequence stops when the centroids
    /// do not change, when the distortion increases, or after <see cref="Av1PaletteKMeans.MaximumIterations"/> iterations.
    /// The method keeps the last centroids that did not increase the distortion.
    /// </remarks>
    /// <param name="firstSamples">The active first-plane samples.</param>
    /// <param name="secondSamples">The active second-plane samples.</param>
    /// <param name="firstCentroids">The initialized first-plane colors.</param>
    /// <param name="secondCentroids">The initialized second-plane colors.</param>
    /// <param name="indices">The palette indices belonging to the retained colors.</param>
    /// <param name="alternateFirstCentroids">Reusable storage for the next first-plane centroid iteration.</param>
    /// <param name="alternateSecondCentroids">Reusable storage for the next second-plane centroid iteration.</param>
    /// <param name="alternateIndices">Reusable storage for the next index iteration.</param>
    /// <returns>The retained sum of squared two-plane distances.</returns>
    public static long Cluster(
        ReadOnlySpan<short> firstSamples,
        ReadOnlySpan<short> secondSamples,
        Span<short> firstCentroids,
        Span<short> secondCentroids,
        Span<byte> indices,
        Span<short> alternateFirstCentroids,
        Span<short> alternateSecondCentroids,
        Span<byte> alternateIndices)
    {
        alternateFirstCentroids = alternateFirstCentroids[..firstCentroids.Length];
        alternateSecondCentroids = alternateSecondCentroids[..secondCentroids.Length];
        alternateIndices = alternateIndices[..firstSamples.Length];
        long distortion = AssignIndices(
            firstSamples,
            secondSamples,
            firstCentroids,
            secondCentroids,
            indices);

        bool currentIsAlternate = false;
        for (int iteration = 0; iteration < Av1PaletteKMeans.MaximumIterations; iteration++)
        {
            ReadOnlySpan<short> currentFirstCentroids = currentIsAlternate
                ? alternateFirstCentroids
                : firstCentroids;

            ReadOnlySpan<short> currentSecondCentroids = currentIsAlternate
                ? alternateSecondCentroids
                : secondCentroids;

            ReadOnlySpan<byte> currentIndices = currentIsAlternate ? alternateIndices : indices;
            Span<short> nextFirstCentroids = currentIsAlternate ? firstCentroids : alternateFirstCentroids;
            Span<short> nextSecondCentroids = currentIsAlternate ? secondCentroids : alternateSecondCentroids;
            Span<byte> nextIndices = currentIsAlternate ? indices : alternateIndices;
            CalculateCentroids(
                firstSamples,
                secondSamples,
                currentIndices,
                nextFirstCentroids,
                nextSecondCentroids);

            if (nextFirstCentroids.SequenceEqual(currentFirstCentroids) &&
                nextSecondCentroids.SequenceEqual(currentSecondCentroids))
            {
                break;
            }

            long nextDistortion = AssignIndices(
                firstSamples,
                secondSamples,
                nextFirstCentroids,
                nextSecondCentroids,
                nextIndices);

            if (nextDistortion > distortion)
            {
                break;
            }

            distortion = nextDistortion;
            currentIsAlternate = !currentIsAlternate;
        }

        if (currentIsAlternate)
        {
            alternateFirstCentroids.CopyTo(firstCentroids);
            alternateSecondCentroids.CopyTo(secondCentroids);
            alternateIndices.CopyTo(indices);
        }

        return distortion;
    }

    /// <summary>
    /// Places paired initial colors at the midpoints of equal intervals that span the sample range of each plane.
    /// </summary>
    /// <param name="firstMinimum">The smallest first-plane sample value.</param>
    /// <param name="firstMaximum">The largest first-plane sample value.</param>
    /// <param name="secondMinimum">The smallest second-plane sample value.</param>
    /// <param name="secondMaximum">The largest second-plane sample value.</param>
    /// <param name="firstCentroids">The first-plane palette colors to initialize.</param>
    /// <param name="secondCentroids">The second-plane palette colors to initialize.</param>
    public static void InitializeCentroids(
        short firstMinimum,
        short firstMaximum,
        short secondMinimum,
        short secondMaximum,
        Span<short> firstCentroids,
        Span<short> secondCentroids)
    {
        int firstRange = firstMaximum - firstMinimum;
        int secondRange = secondMaximum - secondMinimum;
        for (int index = 0; index < firstCentroids.Length; index++)
        {
            firstCentroids[index] = (short)(firstMinimum + (((2 * index) + 1) * firstRange / firstCentroids.Length / 2));
            secondCentroids[index] = (short)(secondMinimum + (((2 * index) + 1) * secondRange / secondCentroids.Length / 2));
        }
    }

    /// <summary>
    /// Recalculates each paired centroid as the rounded mean of its assigned pairs.
    /// </summary>
    /// <param name="firstSamples">The active first-plane samples.</param>
    /// <param name="secondSamples">The active second-plane samples.</param>
    /// <param name="indices">The palette index of each pair.</param>
    /// <param name="firstCentroids">Receives the recalculated first-plane colors.</param>
    /// <param name="secondCentroids">Receives the recalculated second-plane colors.</param>
    private static void CalculateCentroids(
        ReadOnlySpan<short> firstSamples,
        ReadOnlySpan<short> secondSamples,
        ReadOnlySpan<byte> indices,
        Span<short> firstCentroids,
        Span<short> secondCentroids)
    {
        // One pass per color counts it and sums both of its components with the same index comparison.
        InlineArray8<int> countStorage = default;
        InlineArray8<int> firstSumStorage = default;
        InlineArray8<int> secondSumStorage = default;
        Span<int> counts = ((Span<int>)countStorage)[..firstCentroids.Length];
        Span<int> firstSums = ((Span<int>)firstSumStorage)[..firstCentroids.Length];
        Span<int> secondSums = ((Span<int>)secondSumStorage)[..firstCentroids.Length];
        Av1PaletteKMeans.SumByIndex(firstSamples, secondSamples, indices, counts, firstSums, secondSums);

        uint randomState = (uint)firstSamples[0];
        for (int centroidIndex = 0; centroidIndex < firstCentroids.Length; centroidIndex++)
        {
            int count = counts[centroidIndex];
            if (count == 0)
            {
                // An empty paired cluster copies both components from one pseudo-random sample. The seeded sequence is the same
                // on every platform, so the palette choices are reproducible.
                randomState = unchecked((randomState * 1103515245U) + 12345U);
                uint random = (randomState / 65536U) % 32768U;
                int sampleIndex = (int)(random % (uint)firstSamples.Length);
                firstCentroids[centroidIndex] = firstSamples[sampleIndex];
                secondCentroids[centroidIndex] = secondSamples[sampleIndex];
            }
            else
            {
                firstCentroids[centroidIndex] = (short)((firstSums[centroidIndex] + (count / 2)) / count);
                secondCentroids[centroidIndex] = (short)((secondSums[centroidIndex] + (count / 2)) / count);
            }
        }
    }
}
