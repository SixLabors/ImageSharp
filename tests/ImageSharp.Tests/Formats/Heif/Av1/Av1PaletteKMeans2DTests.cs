// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies paired AV1 palette clustering against independent scalar results at every intrinsic tier.
/// </summary>
[Trait("Format", "Avif")]
public class Av1PaletteKMeans2DTests
{
    private const HwIntrinsics Configurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    [Fact]
    public void AssignIndicesMatchesScalarAtEveryIntrinsicTier()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateAssignment, Configurations);

    [Fact]
    public void ClusterMatchesReferenceFixture()
    {
        short[] firstSamples = [0, 2, 0, 100, 102, 100, 200, 202, 200];
        short[] secondSamples = [10, 10, 12, 110, 110, 112, 210, 210, 212];
        short[] firstCentroids = [20, 100, 180];
        short[] secondCentroids = [30, 110, 190];
        byte[] indices = new byte[firstSamples.Length];

        long distortion = Av1PaletteKMeans2D.Cluster(
            firstSamples,
            secondSamples,
            firstCentroids,
            secondCentroids,
            indices);

        Assert.Equal([1, 101, 201], firstCentroids);
        Assert.Equal([11, 111, 211], secondCentroids);
        Assert.Equal([0, 0, 0, 1, 1, 1, 2, 2, 2], indices);
        Assert.Equal(18, distortion);
    }

    [Fact]
    public void InitializeCentroidsMatchesReferenceIntegerOrder()
    {
        short[] firstCentroids = new short[3];
        short[] secondCentroids = new short[3];

        Av1PaletteKMeans2D.InitializeCentroids(10, 250, 20, 260, firstCentroids, secondCentroids);

        Assert.Equal([50, 130, 210], firstCentroids);
        Assert.Equal([60, 140, 220], secondCentroids);
    }

    private static void ValidateAssignment()
    {
        const int SampleCount = 95;
        short[] firstCentroids = [0, 512, 1024, 2048, 3072, 4095];
        short[] secondCentroids = [4094, 3072, 2048, 1024, 512, 0];
        short[] firstSamples = new short[SampleCount];
        short[] secondSamples = new short[SampleCount];
        for (int index = 0; index < SampleCount; index++)
        {
            firstSamples[index] = (short)(((index * 977) + (index * index * 17)) & 4095);
            secondSamples[index] = (short)(((index * 619) + (index * index * 29)) & 4095);
        }

        // The first sample is equidistant from the first two colors and must retain the first palette index.
        firstSamples[0] = 256;
        secondSamples[0] = 3583;
        byte[] expected = new byte[SampleCount];
        long expectedDistortion = AssignReference(
            firstSamples,
            secondSamples,
            firstCentroids,
            secondCentroids,
            expected);

        byte[] actual = Enumerable.Repeat(byte.MaxValue, SampleCount + 7).ToArray();
        long actualDistortion = Av1PaletteKMeans2D.AssignIndices(
            firstSamples,
            secondSamples,
            firstCentroids,
            secondCentroids,
            actual);

        Assert.Equal(expectedDistortion, actualDistortion);
        Assert.Equal(expected, actual.AsSpan(..SampleCount).ToArray());
        Assert.All(actual[SampleCount..], value => Assert.Equal(byte.MaxValue, value));
    }

    private static long AssignReference(
        ReadOnlySpan<short> firstSamples,
        ReadOnlySpan<short> secondSamples,
        ReadOnlySpan<short> firstCentroids,
        ReadOnlySpan<short> secondCentroids,
        Span<byte> indices)
    {
        long distortion = 0;
        for (int sampleIndex = 0; sampleIndex < firstSamples.Length; sampleIndex++)
        {
            int firstDifference = firstSamples[sampleIndex] - firstCentroids[0];
            int secondDifference = secondSamples[sampleIndex] - secondCentroids[0];
            int bestDistance = (firstDifference * firstDifference) + (secondDifference * secondDifference);
            int bestIndex = 0;
            for (int centroidIndex = 1; centroidIndex < firstCentroids.Length; centroidIndex++)
            {
                firstDifference = firstSamples[sampleIndex] - firstCentroids[centroidIndex];
                secondDifference = secondSamples[sampleIndex] - secondCentroids[centroidIndex];
                int distance = (firstDifference * firstDifference) + (secondDifference * secondDifference);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = centroidIndex;
                }
            }

            indices[sampleIndex] = (byte)bestIndex;
            distortion += bestDistance;
        }

        return distortion;
    }
}
