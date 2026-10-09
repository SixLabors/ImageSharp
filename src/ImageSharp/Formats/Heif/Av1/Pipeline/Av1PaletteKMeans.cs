// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Assigns luma samples to AV1 palette colors and refines one-dimensional palette centroids.
/// </summary>
internal static partial class Av1PaletteKMeans
{
    /// <summary>
    /// The iteration limit used by the reference encoder for palette clustering.
    /// </summary>
    public const int MaximumIterations = 50;

    /// <summary>
    /// Assigns every sample to its nearest palette color.
    /// </summary>
    /// <param name="samples">The active block samples.</param>
    /// <param name="centroids">The candidate palette colors.</param>
    /// <param name="indices">The destination palette indices.</param>
    /// <returns>The sum of the squared sample-to-color distances.</returns>
    public static long AssignIndices(
        ReadOnlySpan<short> samples,
        ReadOnlySpan<short> centroids,
        Span<byte> indices)
        => Assign<NearestOperator>.Apply(samples, centroids, indices);

    /// <summary>
    /// Refines initialized palette colors through the reference encoder's deterministic clustering sequence.
    /// </summary>
    /// <param name="samples">The active block samples.</param>
    /// <param name="centroids">The initialized colors, replaced with the best refined colors.</param>
    /// <param name="indices">The palette indices belonging to the retained colors.</param>
    /// <param name="alternateCentroids">Reusable storage for the next centroid iteration.</param>
    /// <param name="alternateIndices">Reusable storage for the next index iteration.</param>
    /// <returns>The retained sum of squared distances.</returns>
    public static long Cluster(
        ReadOnlySpan<short> samples,
        Span<short> centroids,
        Span<byte> indices,
        Span<short> alternateCentroids,
        Span<byte> alternateIndices)
    {
        alternateCentroids = alternateCentroids[..centroids.Length];
        alternateIndices = alternateIndices[..samples.Length];
        long distortion = AssignIndices(samples, centroids, indices);
        bool currentIsAlternate = false;

        for (int iteration = 0; iteration < MaximumIterations; iteration++)
        {
            ReadOnlySpan<short> currentCentroids = currentIsAlternate ? alternateCentroids : centroids;
            ReadOnlySpan<byte> currentIndices = currentIsAlternate ? alternateIndices : indices;
            Span<short> nextCentroids = currentIsAlternate ? centroids : alternateCentroids;
            Span<byte> nextIndices = currentIsAlternate ? indices : alternateIndices;
            CalculateCentroids(samples, currentIndices, nextCentroids);
            if (nextCentroids.SequenceEqual(currentCentroids))
            {
                break;
            }

            long nextDistortion = AssignIndices(samples, nextCentroids, nextIndices);
            if (nextDistortion > distortion)
            {
                break;
            }

            distortion = nextDistortion;
            currentIsAlternate = !currentIsAlternate;
        }

        if (currentIsAlternate)
        {
            alternateCentroids.CopyTo(centroids);
            alternateIndices.CopyTo(indices);
        }

        return distortion;
    }

    /// <summary>
    /// Places initial colors at the midpoint of equal intervals spanning the sample range.
    /// </summary>
    /// <param name="minimum">The smallest sample value.</param>
    /// <param name="maximum">The largest sample value.</param>
    /// <param name="centroids">The palette colors to initialize.</param>
    public static void InitializeCentroids(short minimum, short maximum, Span<short> centroids)
    {
        int range = maximum - minimum;
        for (int index = 0; index < centroids.Length; index++)
        {
            centroids[index] = (short)(minimum + (((2 * index) + 1) * range / centroids.Length / 2));
        }
    }

    /// <summary>
    /// Recalculates each centroid from its assigned samples.
    /// </summary>
    private static void CalculateCentroids(
        ReadOnlySpan<short> samples,
        ReadOnlySpan<byte> indices,
        Span<short> centroids)
    {
        InlineArray8<int> countStorage = default;
        InlineArray8<int> sumStorage = default;
        Span<int> counts = ((Span<int>)countStorage)[..centroids.Length];
        Span<int> sums = ((Span<int>)sumStorage)[..centroids.Length];
        SumByIndex(samples, default, indices, counts, sums, default);

        uint randomState = (uint)samples[0];
        for (int centroidIndex = 0; centroidIndex < centroids.Length; centroidIndex++)
        {
            int count = counts[centroidIndex];
            if (count == 0)
            {
                // Empty clusters use the same seeded sequence on every platform so palette choices remain reproducible.
                randomState = unchecked((randomState * 1103515245U) + 12345U);
                uint random = (randomState / 65536U) % 32768U;
                centroids[centroidIndex] = samples[(int)(random % (uint)samples.Length)];
            }
            else
            {
                centroids[centroidIndex] = (short)((sums[centroidIndex] + (count / 2)) / count);
            }
        }
    }
}
