// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Assigns luma samples to AV1 palette colors and refines one-dimensional palette centroids.
/// </summary>
internal static class Av1PaletteKMeans
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
    /// <returns>The sum of squared sample-to-centroid distances.</returns>
    public static long AssignIndices(
        ReadOnlySpan<short> samples,
        ReadOnlySpan<short> centroids,
        Span<byte> indices)
    {
        Span<short> distanceScratch = stackalloc short[Vector512<short>.Count];
        Span<short> indexScratch = stackalloc short[Vector512<short>.Count];
        ref short sampleBase = ref MemoryMarshal.GetReference(samples);
        int offset = 0;
        long distortion = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            nuint vectorCount = Numerics.Vector512Count(samples);
            for (; vectorCount > 0; vectorCount--, offset += Vector512<short>.Count)
            {
                Vector512<short> sample = Vector512.LoadUnsafe(ref sampleBase, (nuint)offset);
                Vector512<short> bestDistance = Vector512.Abs(sample - Vector512.Create(centroids[0]));
                Vector512<short> bestIndex = Vector512<short>.Zero;
                for (int centroidIndex = 1; centroidIndex < centroids.Length; centroidIndex++)
                {
                    Vector512<short> distance = Vector512.Abs(sample - Vector512.Create(centroids[centroidIndex]));
                    Vector512<short> replace = Vector512.LessThan(distance, bestDistance);
                    bestDistance = Vector512.ConditionalSelect(replace, distance, bestDistance);
                    bestIndex = Vector512.ConditionalSelect(replace, Vector512.Create((short)centroidIndex), bestIndex);
                }

                // Strict comparison preserves the first centroid on ties, matching scalar AV1 palette selection.
                bestDistance.CopyTo(distanceScratch);
                bestIndex.CopyTo(indexScratch);
                for (int lane = 0; lane < Vector512<short>.Count; lane++)
                {
                    indices[offset + lane] = (byte)indexScratch[lane];
                    long distance = distanceScratch[lane];
                    distortion += distance * distance;
                }
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint vectorCount = Numerics.Vector256Count(samples[offset..]);
            for (; vectorCount > 0; vectorCount--, offset += Vector256<short>.Count)
            {
                Vector256<short> sample = Vector256.LoadUnsafe(ref sampleBase, (nuint)offset);
                Vector256<short> bestDistance = Vector256.Abs(sample - Vector256.Create(centroids[0]));
                Vector256<short> bestIndex = Vector256<short>.Zero;
                for (int centroidIndex = 1; centroidIndex < centroids.Length; centroidIndex++)
                {
                    Vector256<short> distance = Vector256.Abs(sample - Vector256.Create(centroids[centroidIndex]));
                    Vector256<short> replace = Vector256.LessThan(distance, bestDistance);
                    bestDistance = Vector256.ConditionalSelect(replace, distance, bestDistance);
                    bestIndex = Vector256.ConditionalSelect(replace, Vector256.Create((short)centroidIndex), bestIndex);
                }

                bestDistance.CopyTo(distanceScratch);
                bestIndex.CopyTo(indexScratch);
                for (int lane = 0; lane < Vector256<short>.Count; lane++)
                {
                    indices[offset + lane] = (byte)indexScratch[lane];
                    long distance = distanceScratch[lane];
                    distortion += distance * distance;
                }
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint vectorCount = Numerics.Vector128Count(samples[offset..]);
            for (; vectorCount > 0; vectorCount--, offset += Vector128<short>.Count)
            {
                Vector128<short> sample = Vector128.LoadUnsafe(ref sampleBase, (nuint)offset);
                Vector128<short> bestDistance = Vector128.Abs(sample - Vector128.Create(centroids[0]));
                Vector128<short> bestIndex = Vector128<short>.Zero;
                for (int centroidIndex = 1; centroidIndex < centroids.Length; centroidIndex++)
                {
                    Vector128<short> distance = Vector128.Abs(sample - Vector128.Create(centroids[centroidIndex]));
                    Vector128<short> replace = Vector128.LessThan(distance, bestDistance);
                    bestDistance = Vector128.ConditionalSelect(replace, distance, bestDistance);
                    bestIndex = Vector128.ConditionalSelect(replace, Vector128.Create((short)centroidIndex), bestIndex);
                }

                bestDistance.CopyTo(distanceScratch);
                bestIndex.CopyTo(indexScratch);
                for (int lane = 0; lane < Vector128<short>.Count; lane++)
                {
                    indices[offset + lane] = (byte)indexScratch[lane];
                    long distance = distanceScratch[lane];
                    distortion += distance * distance;
                }
            }
        }

        for (; offset < samples.Length; offset++)
        {
            int bestDistance = Math.Abs(samples[offset] - centroids[0]);
            int bestIndex = 0;
            for (int centroidIndex = 1; centroidIndex < centroids.Length; centroidIndex++)
            {
                int distance = Math.Abs(samples[offset] - centroids[centroidIndex]);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = centroidIndex;
                }
            }

            indices[offset] = (byte)bestIndex;
            distortion += (long)bestDistance * bestDistance;
        }

        return distortion;
    }

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
        Span<int> counts = stackalloc int[Av1Constants.PaletteMaxSize];
        Span<int> sums = stackalloc int[Av1Constants.PaletteMaxSize];
        counts = counts[..centroids.Length];
        sums = sums[..centroids.Length];
        counts.Clear();
        sums.Clear();
        for (int index = 0; index < samples.Length; index++)
        {
            int centroidIndex = indices[index];
            counts[centroidIndex]++;
            sums[centroidIndex] += samples[index];
        }

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
