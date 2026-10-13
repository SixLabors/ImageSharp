// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the Wiener and self-guided statistics of the loop-restoration search.
/// </summary>
[Trait("Format", "Avif")]
public class Av1LoopRestorationStatisticsTests
{
    /// <summary>
    /// The hardware configurations covering every vector tier and the scalar fallback.
    /// </summary>
    private const HwIntrinsics Configurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies the moments against calc_proj_params_r0_r1_c() and the error against av1_lowbd_pixel_proj_error() and
    /// av1_highbd_pixel_proj_error(), for every radius combination and row lengths that end in every vector tail.
    /// </summary>
    [Fact]
    public void ProjectionRowsMatchReferenceAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertProjectionRows, Configurations);

    /// <summary>
    /// Verifies the centered products against the accumulation of compute_stats(), with and without row downsampling,
    /// and the sample moments against aom_var_2d_u8() and aom_var_2d_u16().
    /// </summary>
    [Fact]
    public void WienerStatisticsMatchReferenceAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertWienerStatistics, Configurations);

    private static void AssertProjectionRows()
    {
        int seed = 23;
        foreach (int bitDepth in new[] { 8, 12 })
        {
            int maximum = (1 << bitDepth) - 1;
            foreach (int width in new[] { 1, 3, 4, 7, 8, 15, 16, 17, 33, 64, 99 })
            {
                ushort[] original = new ushort[width];
                ushort[] reconstructed = new ushort[width];
                int[] first = new int[width];
                int[] second = new int[width];
                for (int i = 0; i < width; i++)
                {
                    original[i] = (ushort)(Next(ref seed) & maximum);
                    reconstructed[i] = (ushort)(Next(ref seed) & maximum);

                    // Filtered values span the asserted range of the reference, below 2^15 in magnitude.
                    first[i] = (Next(ref seed) & 0xFFFF) - 32767;
                    second[i] = (Next(ref seed) & 0xFFFF) - 32767;
                }

                // Eight-bit samples are already below 256, so the byte copies hold the same values.
                byte[] byteOriginal = Array.ConvertAll(original, value => (byte)value);
                byte[] byteReconstructed = Array.ConvertAll(reconstructed, value => (byte)value);
                foreach ((bool radiusTwo, bool radiusOne) in new[] { (true, true), (true, false), (false, true) })
                {
                    int firstWeight = radiusTwo ? -70 : 0;
                    int secondWeight = radiusOne ? 128 - firstWeight - 55 : 0;
                    long[] expected = new long[5];
                    long expectedError = 0;
                    for (int i = 0; i < width; i++)
                    {
                        int center = reconstructed[i] << 4;
                        int target = (original[i] << 4) - center;
                        int a = radiusTwo ? first[i] - center : 0;
                        int b = radiusOne ? second[i] - center : 0;
                        expected[0] += (long)a * a;
                        expected[1] += (long)a * b;
                        expected[2] += (long)b * b;
                        expected[3] += (long)a * target;
                        expected[4] += (long)b * target;

                        int v = center << 7;
                        v += (firstWeight * (first[i] - center)) + (secondWeight * (second[i] - center));
                        long e = ((v + (1 << 10)) >> 11) - original[i];
                        expectedError += e * e;
                    }

                    long[] moments = new long[5];
                    long error = bitDepth == 8
                        ? Av1LoopRestorationEncoder.MeasureProjectionRow<byte>(
                            byteOriginal, byteReconstructed, first, second, radiusTwo, radiusOne, firstWeight, secondWeight, moments)
                        : Av1LoopRestorationEncoder.MeasureProjectionRow<ushort>(
                            original, reconstructed, first, second, radiusTwo, radiusOne, firstWeight, secondWeight, moments);

                    Assert.Equal(expectedError, error);
                    Assert.Equal(expected, moments);
                }
            }
        }
    }

    private static void AssertWienerStatistics()
    {
        int seed = 41;
        foreach (int bitDepth in new[] { 8, 12 })
        {
            int maximum = (1 << bitDepth) - 1;
            foreach ((int width, int height) in new[] { (1, 1), (7, 5), (16, 9), (33, 13), (70, 64), (99, 3) })
            {
                int stride = width + 11;
                ushort[] samples = new ushort[stride * (height + 2)];
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = (ushort)(Next(ref seed) & maximum);
                }

                byte[] byteSamples = Array.ConvertAll(samples, value => (byte)value);
                int average = maximum / 3;
                foreach (int rowStep in new[] { 1, 4 })
                {
                    // The second region starts one row and two columns later, as a neighboring window tap does.
                    int secondStart = stride + 2;
                    long expected = 0;
                    for (int row = 0; row < height; row += rowStep)
                    {
                        long rowTotal = 0;
                        for (int column = 0; column < width; column++)
                        {
                            rowTotal += (long)(samples[(row * stride) + column] - average) * (samples[secondStart + (row * stride) + column] - average);
                        }

                        expected += rowTotal * Math.Min(rowStep, height - row);
                    }

                    long actual = bitDepth == 8
                        ? Av1LoopRestorationEncoder.MeasureCorrelation<byte>(byteSamples, stride, byteSamples.AsSpan(secondStart), stride, width, height, average, rowStep)
                        : Av1LoopRestorationEncoder.MeasureCorrelation<ushort>(samples, stride, samples.AsSpan(secondStart), stride, width, height, average, rowStep);

                    Assert.Equal(expected, actual);
                }

                long expectedSum = 0;
                long expectedSquares = 0;
                for (int row = 1; row <= height; row++)
                {
                    for (int column = 3; column < width + 3; column++)
                    {
                        int value = samples[(row * stride) + column];
                        expectedSum += value;
                        expectedSquares += (long)value * value;
                    }
                }

                Rectangle bounds = new(3, 1, width, height);
                long squares;
                long sum = bitDepth == 8
                    ? Av1LoopRestorationEncoder.MeasureSampleMoments(new Av1PlaneRegion<byte>(byteSamples, stride, bounds), out squares)
                    : Av1LoopRestorationEncoder.MeasureSampleMoments(new Av1PlaneRegion<ushort>(samples, stride, bounds), out squares);

                Assert.Equal(expectedSum, sum);
                Assert.Equal(expectedSquares, squares);
            }
        }
    }

    private static int Next(ref int seed)
    {
        seed = (seed * 1103515245) + 12345;
        return (seed >>> 8) & 0xFFFFFF;
    }
}
