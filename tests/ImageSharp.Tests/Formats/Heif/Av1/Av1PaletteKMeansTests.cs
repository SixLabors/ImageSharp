// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 palette clustering against independent scalar results at every intrinsic tier.
/// </summary>
[Trait("Format", "Avif")]
public class Av1PaletteKMeansTests
{
    /// <summary>
    /// The hardware configurations covering every descending SIMD width and the scalar fallback.
    /// </summary>
    private const HwIntrinsics Configurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies nearest-color indices, squared distortion, tie order, and destination bounds.
    /// </summary>
    [Fact]
    public void AssignIndicesMatchesScalarAtEveryIntrinsicTier()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateAssignment, Configurations);

    /// <summary>
    /// Verifies the per-color counts and component sums of the centroid update for one and two components, every
    /// palette size, and lengths that end inside a vector.
    /// </summary>
    [Fact]
    public void SumByIndexMatchesDefinitionAtEveryIntrinsicTier()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateSums, Configurations);

    /// <summary>
    /// Compares the counts and sums with a direct tally of every sample.
    /// </summary>
    private static void ValidateSums()
    {
        foreach (int sampleCount in new[] { 7, 16, 95, 4096 })
        {
            short[] first = new short[sampleCount];
            short[] second = new short[sampleCount];
            byte[] indices = new byte[sampleCount];
            for (int colorCount = 2; colorCount <= 8; colorCount++)
            {
                for (int index = 0; index < sampleCount; index++)
                {
                    first[index] = (short)(((index * 977) + (index * index * 17)) & 4095);
                    second[index] = (short)((index * 131) & 4095);
                    indices[index] = (byte)(((index * 7) + (index >> 3)) % colorCount);
                }

                int[] expectedCounts = new int[colorCount];
                int[] expectedFirst = new int[colorCount];
                int[] expectedSecond = new int[colorCount];
                for (int index = 0; index < sampleCount; index++)
                {
                    expectedCounts[indices[index]]++;
                    expectedFirst[indices[index]] += first[index];
                    expectedSecond[indices[index]] += second[index];
                }

                int[] counts = new int[colorCount];
                int[] firstSums = new int[colorCount];
                int[] secondSums = new int[colorCount];
                Av1PaletteKMeans.SumByIndex(first, second, indices, counts, firstSums, secondSums);
                Assert.Equal(expectedCounts, counts);
                Assert.Equal(expectedFirst, firstSums);
                Assert.Equal(expectedSecond, secondSums);

                Array.Clear(counts);
                Array.Clear(firstSums);
                Av1PaletteKMeans.SumByIndex(first, default, indices, counts, firstSums, default);
                Assert.Equal(expectedCounts, counts);
                Assert.Equal(expectedFirst, firstSums);
            }
        }
    }

    /// <summary>
    /// Compares production assignment with a scalar equation over a length that exercises every available remainder path.
    /// </summary>
    private static void ValidateAssignment()
    {
        const int SampleCount = 95;
        short[] centroids = [0, 512, 1024, 2048, 3072, 4095];
        short[] samples = new short[SampleCount];
        for (int index = 0; index < samples.Length; index++)
        {
            samples[index] = (short)(((index * 977) + (index * index * 17)) & 4095);
        }

        // This sample is equidistant from the first two colors and must retain the lower palette index.
        samples[0] = 256;
        byte[] expected = new byte[SampleCount];
        long expectedDistortion = AssignReference(samples, centroids, expected);
        byte[] actual = Enumerable.Repeat(byte.MaxValue, SampleCount + 7).ToArray();

        long actualDistortion = Av1PaletteKMeans.AssignIndices(samples, centroids, actual);

        Assert.Equal(expectedDistortion, actualDistortion);
        Assert.Equal(expected, actual.AsSpan(..SampleCount).ToArray());
        Assert.All(actual[SampleCount..], value => Assert.Equal(byte.MaxValue, value));
    }

    /// <summary>
    /// Applies the scalar nearest-color rule independently of the production SIMD traversal.
    /// </summary>
    private static long AssignReference(
        ReadOnlySpan<short> samples,
        ReadOnlySpan<short> centroids,
        Span<byte> indices)
    {
        long distortion = 0;
        for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++)
        {
            int bestDistance = Math.Abs(samples[sampleIndex] - centroids[0]);
            int bestIndex = 0;
            for (int centroidIndex = 1; centroidIndex < centroids.Length; centroidIndex++)
            {
                int distance = Math.Abs(samples[sampleIndex] - centroids[centroidIndex]);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = centroidIndex;
                }
            }

            indices[sampleIndex] = (byte)bestIndex;
            distortion += (long)bestDistance * bestDistance;
        }

        return distortion;
    }
}
