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
    /// Refines initialized paired colors through the reference encoder's deterministic clustering sequence.
    /// </summary>
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
    /// Places paired initial colors at the midpoints of equal intervals spanning each plane's sample range.
    /// </summary>
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

    private static void CalculateCentroids(
        ReadOnlySpan<short> firstSamples,
        ReadOnlySpan<short> secondSamples,
        ReadOnlySpan<byte> indices,
        Span<short> firstCentroids,
        Span<short> secondCentroids)
    {
        Span<int> counts = stackalloc int[Av1Constants.PaletteMaxSize];
        Span<int> firstSums = stackalloc int[Av1Constants.PaletteMaxSize];
        Span<int> secondSums = stackalloc int[Av1Constants.PaletteMaxSize];
        counts = counts[..firstCentroids.Length];
        firstSums = firstSums[..firstCentroids.Length];
        secondSums = secondSums[..secondCentroids.Length];
        counts.Clear();
        firstSums.Clear();
        secondSums.Clear();
        for (int index = 0; index < firstSamples.Length; index++)
        {
            int centroidIndex = indices[index];
            counts[centroidIndex]++;
            firstSums[centroidIndex] += firstSamples[index];
            secondSums[centroidIndex] += secondSamples[index];
        }

        uint randomState = (uint)firstSamples[0];
        for (int centroidIndex = 0; centroidIndex < firstCentroids.Length; centroidIndex++)
        {
            int count = counts[centroidIndex];
            if (count == 0)
            {
                // Empty paired clusters copy both components from the same deterministically selected sample.
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
