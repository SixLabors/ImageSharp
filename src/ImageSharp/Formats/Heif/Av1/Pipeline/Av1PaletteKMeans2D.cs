// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Assigns paired chroma samples to AV1 palette colors and refines two-dimensional palette centroids.
/// </summary>
internal static class Av1PaletteKMeans2D
{
    /// <summary>
    /// Assigns every chroma pair to its nearest palette color.
    /// </summary>
    /// <param name="firstSamples">The active first-plane samples.</param>
    /// <param name="secondSamples">The active second-plane samples.</param>
    /// <param name="firstCentroids">The first-plane palette colors.</param>
    /// <param name="secondCentroids">The second-plane palette colors.</param>
    /// <param name="indices">The destination palette indices.</param>
    /// <returns>The sum of squared two-plane sample-to-centroid distances.</returns>
    public static long AssignIndices(
        ReadOnlySpan<short> firstSamples,
        ReadOnlySpan<short> secondSamples,
        ReadOnlySpan<short> firstCentroids,
        ReadOnlySpan<short> secondCentroids,
        Span<byte> indices)
    {
        Span<int> distanceScratch = stackalloc int[Vector512<short>.Count];
        Span<int> indexScratch = stackalloc int[Vector512<short>.Count];
        ref short firstSampleBase = ref MemoryMarshal.GetReference(firstSamples);
        ref short secondSampleBase = ref MemoryMarshal.GetReference(secondSamples);
        int offset = 0;
        long distortion = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            nuint vectorCount = Numerics.Vector512Count(firstSamples);
            for (; vectorCount > 0; vectorCount--, offset += Vector512<short>.Count)
            {
                Vector512<short> firstSample = Vector512.LoadUnsafe(ref firstSampleBase, (nuint)offset);
                Vector512<short> secondSample = Vector512.LoadUnsafe(ref secondSampleBase, (nuint)offset);
                Vector512<short> firstDifference = firstSample - Vector512.Create(firstCentroids[0]);
                Vector512<short> secondDifference = secondSample - Vector512.Create(secondCentroids[0]);
                (Vector512<int> firstLower, Vector512<int> firstUpper) = Vector512.Widen(firstDifference);
                (Vector512<int> secondLower, Vector512<int> secondUpper) = Vector512.Widen(secondDifference);
                Vector512<int> bestDistanceLower = (firstLower * firstLower) + (secondLower * secondLower);
                Vector512<int> bestDistanceUpper = (firstUpper * firstUpper) + (secondUpper * secondUpper);
                Vector512<int> bestIndexLower = Vector512<int>.Zero;
                Vector512<int> bestIndexUpper = Vector512<int>.Zero;
                for (int centroidIndex = 1; centroidIndex < firstCentroids.Length; centroidIndex++)
                {
                    firstDifference = firstSample - Vector512.Create(firstCentroids[centroidIndex]);
                    secondDifference = secondSample - Vector512.Create(secondCentroids[centroidIndex]);
                    (firstLower, firstUpper) = Vector512.Widen(firstDifference);
                    (secondLower, secondUpper) = Vector512.Widen(secondDifference);
                    Vector512<int> distanceLower = (firstLower * firstLower) + (secondLower * secondLower);
                    Vector512<int> distanceUpper = (firstUpper * firstUpper) + (secondUpper * secondUpper);
                    Vector512<int> replaceLower = Vector512.LessThan(distanceLower, bestDistanceLower);
                    Vector512<int> replaceUpper = Vector512.LessThan(distanceUpper, bestDistanceUpper);
                    bestDistanceLower = Vector512.ConditionalSelect(replaceLower, distanceLower, bestDistanceLower);
                    bestDistanceUpper = Vector512.ConditionalSelect(replaceUpper, distanceUpper, bestDistanceUpper);
                    bestIndexLower = Vector512.ConditionalSelect(
                        replaceLower,
                        Vector512.Create(centroidIndex),
                        bestIndexLower);

                    bestIndexUpper = Vector512.ConditionalSelect(
                        replaceUpper,
                        Vector512.Create(centroidIndex),
                        bestIndexUpper);
                }

                bestDistanceLower.CopyTo(distanceScratch);
                bestDistanceUpper.CopyTo(distanceScratch[Vector512<int>.Count..]);
                bestIndexLower.CopyTo(indexScratch);
                bestIndexUpper.CopyTo(indexScratch[Vector512<int>.Count..]);
                for (int lane = 0; lane < Vector512<short>.Count; lane++)
                {
                    indices[offset + lane] = (byte)indexScratch[lane];
                    distortion += distanceScratch[lane];
                }
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint vectorCount = Numerics.Vector256Count(firstSamples[offset..]);
            for (; vectorCount > 0; vectorCount--, offset += Vector256<short>.Count)
            {
                Vector256<short> firstSample = Vector256.LoadUnsafe(ref firstSampleBase, (nuint)offset);
                Vector256<short> secondSample = Vector256.LoadUnsafe(ref secondSampleBase, (nuint)offset);
                Vector256<short> firstDifference = firstSample - Vector256.Create(firstCentroids[0]);
                Vector256<short> secondDifference = secondSample - Vector256.Create(secondCentroids[0]);
                (Vector256<int> firstLower, Vector256<int> firstUpper) = Vector256.Widen(firstDifference);
                (Vector256<int> secondLower, Vector256<int> secondUpper) = Vector256.Widen(secondDifference);
                Vector256<int> bestDistanceLower = (firstLower * firstLower) + (secondLower * secondLower);
                Vector256<int> bestDistanceUpper = (firstUpper * firstUpper) + (secondUpper * secondUpper);
                Vector256<int> bestIndexLower = Vector256<int>.Zero;
                Vector256<int> bestIndexUpper = Vector256<int>.Zero;
                for (int centroidIndex = 1; centroidIndex < firstCentroids.Length; centroidIndex++)
                {
                    firstDifference = firstSample - Vector256.Create(firstCentroids[centroidIndex]);
                    secondDifference = secondSample - Vector256.Create(secondCentroids[centroidIndex]);
                    (firstLower, firstUpper) = Vector256.Widen(firstDifference);
                    (secondLower, secondUpper) = Vector256.Widen(secondDifference);
                    Vector256<int> distanceLower = (firstLower * firstLower) + (secondLower * secondLower);
                    Vector256<int> distanceUpper = (firstUpper * firstUpper) + (secondUpper * secondUpper);
                    Vector256<int> replaceLower = Vector256.LessThan(distanceLower, bestDistanceLower);
                    Vector256<int> replaceUpper = Vector256.LessThan(distanceUpper, bestDistanceUpper);
                    bestDistanceLower = Vector256.ConditionalSelect(replaceLower, distanceLower, bestDistanceLower);
                    bestDistanceUpper = Vector256.ConditionalSelect(replaceUpper, distanceUpper, bestDistanceUpper);
                    bestIndexLower = Vector256.ConditionalSelect(
                        replaceLower,
                        Vector256.Create(centroidIndex),
                        bestIndexLower);

                    bestIndexUpper = Vector256.ConditionalSelect(
                        replaceUpper,
                        Vector256.Create(centroidIndex),
                        bestIndexUpper);
                }

                bestDistanceLower.CopyTo(distanceScratch);
                bestDistanceUpper.CopyTo(distanceScratch[Vector256<int>.Count..]);
                bestIndexLower.CopyTo(indexScratch);
                bestIndexUpper.CopyTo(indexScratch[Vector256<int>.Count..]);
                for (int lane = 0; lane < Vector256<short>.Count; lane++)
                {
                    indices[offset + lane] = (byte)indexScratch[lane];
                    distortion += distanceScratch[lane];
                }
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint vectorCount = Numerics.Vector128Count(firstSamples[offset..]);
            for (; vectorCount > 0; vectorCount--, offset += Vector128<short>.Count)
            {
                Vector128<short> firstSample = Vector128.LoadUnsafe(ref firstSampleBase, (nuint)offset);
                Vector128<short> secondSample = Vector128.LoadUnsafe(ref secondSampleBase, (nuint)offset);
                Vector128<short> firstDifference = firstSample - Vector128.Create(firstCentroids[0]);
                Vector128<short> secondDifference = secondSample - Vector128.Create(secondCentroids[0]);
                (Vector128<int> firstLower, Vector128<int> firstUpper) = Vector128.Widen(firstDifference);
                (Vector128<int> secondLower, Vector128<int> secondUpper) = Vector128.Widen(secondDifference);
                Vector128<int> bestDistanceLower = (firstLower * firstLower) + (secondLower * secondLower);
                Vector128<int> bestDistanceUpper = (firstUpper * firstUpper) + (secondUpper * secondUpper);
                Vector128<int> bestIndexLower = Vector128<int>.Zero;
                Vector128<int> bestIndexUpper = Vector128<int>.Zero;
                for (int centroidIndex = 1; centroidIndex < firstCentroids.Length; centroidIndex++)
                {
                    firstDifference = firstSample - Vector128.Create(firstCentroids[centroidIndex]);
                    secondDifference = secondSample - Vector128.Create(secondCentroids[centroidIndex]);
                    (firstLower, firstUpper) = Vector128.Widen(firstDifference);
                    (secondLower, secondUpper) = Vector128.Widen(secondDifference);
                    Vector128<int> distanceLower = (firstLower * firstLower) + (secondLower * secondLower);
                    Vector128<int> distanceUpper = (firstUpper * firstUpper) + (secondUpper * secondUpper);
                    Vector128<int> replaceLower = Vector128.LessThan(distanceLower, bestDistanceLower);
                    Vector128<int> replaceUpper = Vector128.LessThan(distanceUpper, bestDistanceUpper);
                    bestDistanceLower = Vector128.ConditionalSelect(replaceLower, distanceLower, bestDistanceLower);
                    bestDistanceUpper = Vector128.ConditionalSelect(replaceUpper, distanceUpper, bestDistanceUpper);
                    bestIndexLower = Vector128.ConditionalSelect(
                        replaceLower,
                        Vector128.Create(centroidIndex),
                        bestIndexLower);

                    bestIndexUpper = Vector128.ConditionalSelect(
                        replaceUpper,
                        Vector128.Create(centroidIndex),
                        bestIndexUpper);
                }

                bestDistanceLower.CopyTo(distanceScratch);
                bestDistanceUpper.CopyTo(distanceScratch[Vector128<int>.Count..]);
                bestIndexLower.CopyTo(indexScratch);
                bestIndexUpper.CopyTo(indexScratch[Vector128<int>.Count..]);
                for (int lane = 0; lane < Vector128<short>.Count; lane++)
                {
                    indices[offset + lane] = (byte)indexScratch[lane];
                    distortion += distanceScratch[lane];
                }
            }
        }

        for (; offset < firstSamples.Length; offset++)
        {
            int firstDifference = firstSamples[offset] - firstCentroids[0];
            int secondDifference = secondSamples[offset] - secondCentroids[0];
            int bestDistance = (firstDifference * firstDifference) + (secondDifference * secondDifference);
            int bestIndex = 0;
            for (int centroidIndex = 1; centroidIndex < firstCentroids.Length; centroidIndex++)
            {
                firstDifference = firstSamples[offset] - firstCentroids[centroidIndex];
                secondDifference = secondSamples[offset] - secondCentroids[centroidIndex];
                int distance = (firstDifference * firstDifference) + (secondDifference * secondDifference);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = centroidIndex;
                }
            }

            indices[offset] = (byte)bestIndex;
            distortion += bestDistance;
        }

        return distortion;
    }

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
