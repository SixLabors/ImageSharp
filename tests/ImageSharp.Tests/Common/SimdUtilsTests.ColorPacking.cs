// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Common;

/// <summary>
/// Tests shared planar and packed color-buffer transformations.
/// </summary>
public partial class SimdUtilsTests
{
    private static readonly int[] Lengths = [0, 1, 2, 3, 4, 5, 7, 8, 15, 16, 17, 31, 32, 33, 129];

    /// <summary>
    /// Verifies every packing operation against its scalar definition with and without hardware intrinsics.
    /// </summary>
    [Fact]
    public void PackingOperationsMatchScalarDefinitions()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidatePackingOperations,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Exercises every SIMD transition and scalar remainder for the packing operations.
    /// </summary>
    private static void ValidatePackingOperations()
    {
        foreach (int length in Lengths)
        {
            ValidatePackedNormalizeInterleave3(length);
            ValidateUnpackDeinterleave3(length);
            ValidatePackedNormalizeInterleave4(length);
            ValidatePackedInvertNormalizeInterleave4(length);
        }
    }

    /// <summary>
    /// Compares normalized three-plane interleaving with the original scalar loop.
    /// </summary>
    /// <param name="length">The number of samples in each component plane.</param>
    private static void ValidatePackedNormalizeInterleave3(int length)
    {
        const float scale = 1F / 255F;
        float[] x = CreateSamples(length, 1);
        float[] y = CreateSamples(length, 2);
        float[] z = CreateSamples(length, 3);
        float[] expected = new float[length * 3];
        float[] actual = new float[length * 3];

        for (int i = 0; i < length; i++)
        {
            int packedOffset = i * 3;
            expected[packedOffset] = x[i] * scale;
            expected[packedOffset + 1] = y[i] * scale;
            expected[packedOffset + 2] = z[i] * scale;
        }

        SimdUtils.PackedNormalizeInterleave3(x, y, z, actual, scale);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Compares packed three-channel deinterleaving with the original scalar loop.
    /// </summary>
    /// <param name="length">The number of packed values.</param>
    private static void ValidateUnpackDeinterleave3(int length)
    {
        Vector3[] packed = new Vector3[length];
        float[] expectedX = CreateSamples(length, 1);
        float[] expectedY = CreateSamples(length, 2);
        float[] expectedZ = CreateSamples(length, 3);
        float[] actualX = new float[length];
        float[] actualY = new float[length];
        float[] actualZ = new float[length];

        for (int i = 0; i < length; i++)
        {
            packed[i] = new Vector3(expectedX[i], expectedY[i], expectedZ[i]);
        }

        SimdUtils.UnpackDeinterleave3(packed, actualX, actualY, actualZ);

        Assert.Equal(expectedX, actualX);
        Assert.Equal(expectedY, actualY);
        Assert.Equal(expectedZ, actualZ);
    }

    /// <summary>
    /// Compares normalized four-plane interleaving with the original scalar loop.
    /// </summary>
    /// <param name="length">The number of samples in each component plane.</param>
    private static void ValidatePackedNormalizeInterleave4(int length)
    {
        const float maximumValue = 255F;
        const float scale = 1F / maximumValue;
        float[] x = CreateSamples(length, 1);
        float[] y = CreateSamples(length, 2);
        float[] z = CreateSamples(length, 3);
        float[] w = CreateSamples(length, 4);
        float[] expected = new float[length * 4];
        float[] actual = new float[length * 4];

        for (int i = 0; i < length; i++)
        {
            int packedOffset = i * 4;
            expected[packedOffset] = x[i] * scale;
            expected[packedOffset + 1] = y[i] * scale;
            expected[packedOffset + 2] = z[i] * scale;
            expected[packedOffset + 3] = w[i] * scale;
        }

        SimdUtils.PackedNormalizeInterleave4(x, y, z, w, actual, maximumValue);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Compares inverted normalized four-plane interleaving with the original scalar loop.
    /// </summary>
    /// <param name="length">The number of samples in each component plane.</param>
    private static void ValidatePackedInvertNormalizeInterleave4(int length)
    {
        const float maximumValue = 255F;
        const float scale = 1F / maximumValue;
        float[] x = CreateSamples(length, 1);
        float[] y = CreateSamples(length, 2);
        float[] z = CreateSamples(length, 3);
        float[] w = CreateSamples(length, 4);
        float[] expected = new float[length * 4];
        float[] actual = new float[length * 4];

        for (int i = 0; i < length; i++)
        {
            int packedOffset = i * 4;
            expected[packedOffset] = (maximumValue - x[i]) * scale;
            expected[packedOffset + 1] = (maximumValue - y[i]) * scale;
            expected[packedOffset + 2] = (maximumValue - z[i]) * scale;
            expected[packedOffset + 3] = (maximumValue - w[i]) * scale;
        }

        SimdUtils.PackedInvertNormalizeInterleave4(x, y, z, w, actual, maximumValue);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Repeats explicit endpoint and fractional samples across the SIMD boundaries.
    /// </summary>
    /// <param name="length">The number of samples to create.</param>
    /// <param name="component">The one-based offset that gives each plane a different sequence.</param>
    /// <returns>The generated sample values.</returns>
    private static float[] CreateSamples(int length, int component)
    {
        // Seventeen mixed values avoid repeating at any 4-, 8-, or 16-lane boundary.
        ReadOnlySpan<float> source =
        [
            0F,
            255F,
            0.125F,
            127.5F,
            1F,
            254.875F,
            32.25F,
            96.5F,
            191.75F,
            7.125F,
            240F,
            19.5F,
            63.875F,
            128F,
            223.25F,
            48.5F,
            175.125F
        ];

        float[] samples = new float[length];

        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = source[(i + component) % source.Length];
        }

        return samples;
    }
}
