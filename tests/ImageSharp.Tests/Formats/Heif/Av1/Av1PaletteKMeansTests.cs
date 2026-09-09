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
    /// Verifies the deterministic centroid sequence on three separated sample groups.
    /// </summary>
    [Fact]
    public void ClusterMatchesReferenceFixture()
    {
        short[] samples = [0, 1, 2, 100, 101, 102, 200, 201, 202];
        short[] centroids = [33, 100, 167];
        byte[] indices = new byte[samples.Length];
        short[] alternateCentroids = new short[centroids.Length];
        byte[] alternateIndices = new byte[samples.Length];

        long distortion = Av1PaletteKMeans.Cluster(
            samples,
            centroids,
            indices,
            alternateCentroids,
            alternateIndices);

        Assert.Equal([1, 101, 201], centroids);
        Assert.Equal([0, 0, 0, 1, 1, 1, 2, 2, 2], indices);
        Assert.Equal(6, distortion);
    }

    /// <summary>
    /// Verifies the integer interval midpoints used to seed palette refinement.
    /// </summary>
    [Fact]
    public void InitializeCentroidsMatchesReferenceIntegerOrder()
    {
        short[] centroids = new short[3];

        Av1PaletteKMeans.InitializeCentroids(10, 250, centroids);

        Assert.Equal([50, 130, 210], centroids);
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
