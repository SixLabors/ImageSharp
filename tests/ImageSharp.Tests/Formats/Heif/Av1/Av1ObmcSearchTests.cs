// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the OBMC search measures and the OBMC search target against the scalar definitions of libaom,
/// across sample precision, block geometry and intrinsic tiers.
/// </summary>
[Trait("Format", "Avif")]
public class Av1ObmcSearchTests
{
    /// <summary>
    /// The hardware configurations that run every register width and the scalar overloads.
    /// </summary>
    private const HwIntrinsics Configurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// The block widths of the OBMC motion modes.
    /// </summary>
    private static readonly int[] Widths = [8, 16, 32, 64, 128];

    /// <summary>
    /// Verifies the sum of rounded absolute differences against obmc_sad(), the signed and squared sums against
    /// obmc_variance(), including the largest twelve-bit block where a thirty-two-bit total of the squares would
    /// overflow, and every step of the OBMC search target against calc_target_weighted_pred().
    /// </summary>
    [Fact]
    public void SearchMeasuresMatchReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateSearchMeasures, Configurations);

    private static void ValidateSearchMeasures()
    {
        ValidateSumAbsoluteDifferences();
        ValidateMoments();
        ValidateLargestMoments();
        ValidateTarget();
    }

    private static void ValidateSumAbsoluteDifferences()
    {
        Random random = new(0x0B3C);
        foreach (int bitDepth in new[] { 8, 10, 12 })
        {
            foreach (int width in Widths)
            {
                foreach (int height in new[] { 8, 16, width })
                {
                    int stride = width + 13;
                    ushort[] prediction = CreateSamples(random, (stride * height) + 3, bitDepth);
                    int[] weightedSource = CreateWeightedSource(random, width * height, bitDepth);
                    int[] mask = CreateMask(random, width * height);
                    int expected = SumAbsoluteDifferencesReference(prediction, stride, weightedSource, mask, width, height);
                    int actual = bitDepth == 8
                        ? Av1ObmcSearch.SumAbsoluteDifferences<byte, Av1ObmcSearch.ByteOperator>(ToBytes(prediction), stride, weightedSource, mask, width, height)
                        : Av1ObmcSearch.SumAbsoluteDifferences<ushort, Av1ObmcSearch.UInt16Operator>(prediction, stride, weightedSource, mask, width, height);

                    Assert.Equal(expected, actual);
                }
            }
        }
    }

    private static void ValidateMoments()
    {
        Random random = new(0x0B3D);
        foreach (int bitDepth in new[] { 8, 10, 12 })
        {
            foreach (int width in Widths)
            {
                foreach (int height in new[] { 8, 16, width })
                {
                    int stride = width + 7;
                    ushort[] prediction = CreateSamples(random, stride * height, bitDepth);
                    int[] weightedSource = CreateWeightedSource(random, width * height, bitDepth);
                    int[] mask = CreateMask(random, width * height);
                    AssertMoments(prediction, stride, weightedSource, mask, width, height, bitDepth);
                }
            }
        }
    }

    private static void ValidateLargestMoments()
    {
        const int size = 128;
        ushort[] prediction = new ushort[size * size];
        int[] weightedSource = new int[size * size];
        int[] mask = new int[size * size];
        for (int i = 0; i < weightedSource.Length; i++)
        {
            // Alternate signs keep the signed total small while every square is the largest possible.
            weightedSource[i] = (i & 1) == 0 ? 4095 * 4096 : -4095 * 4096;
            mask[i] = 4096;
        }

        AssertMoments(prediction, size, weightedSource, mask, size, size, 12);
    }

    private static void ValidateTarget()
    {
        Random random = new(0x0B3E);
        foreach (int bitDepth in new[] { 8, 10, 12 })
        {
            int maximum = (1 << bitDepth) - 1;
            foreach (int width in Widths)
            {
                ushort[] samples = CreateSamples(random, width, bitDepth);

                // Above-neighbor row.
                int weight = random.Next(0, 65);
                int[] expectedSource = new int[width];
                int[] expectedMask = new int[width];
                int[] actualSource = new int[width];
                int[] actualMask = new int[width];
                for (int x = 0; x < width; x++)
                {
                    expectedSource[x] = (64 - weight) * samples[x];
                    expectedMask[x] = weight;
                }

                if (bitDepth == 8)
                {
                    Av1ObmcSearch.WeightAbove<byte, Av1ObmcSearch.ByteOperator>(ToBytes(samples), weight, actualSource, actualMask, width);
                }
                else
                {
                    Av1ObmcSearch.WeightAbove<ushort, Av1ObmcSearch.UInt16Operator>(samples, weight, actualSource, actualMask, width);
                }

                Assert.Equal(expectedSource, actualSource);
                Assert.Equal(expectedMask, actualMask);

                // Scale pass.
                int[] source = new int[width];
                int[] weights = new int[width];
                for (int x = 0; x < width; x++)
                {
                    source[x] = random.Next(0, (64 * maximum) + 1);
                    weights[x] = random.Next(0, 65);
                    expectedSource[x] = source[x] * 64;
                    expectedMask[x] = weights[x] * 64;
                }

                Av1ObmcSearch.Scale(source, weights);

                Assert.Equal(expectedSource, source);
                Assert.Equal(expectedMask, weights);

                // Left-neighbor row, over the overlap of this block width.
                int overlap = Math.Min(width, 64) >> 1;
                ReadOnlySpan<byte> columnWeights = Av1ObmcMask.Get(overlap);
                int[] leftSource = new int[width];
                int[] leftMask = new int[width];
                for (int x = 0; x < width; x++)
                {
                    leftSource[x] = random.Next(0, (64 * maximum) + 1) * 64;
                    leftMask[x] = random.Next(0, 65) * 64;
                    expectedSource[x] = leftSource[x];
                    expectedMask[x] = leftMask[x];
                }

                for (int x = 0; x < overlap; x++)
                {
                    int columnWeight = columnWeights[x];
                    expectedSource[x] = ((expectedSource[x] >> 6) * columnWeight) + ((samples[x] << 6) * (64 - columnWeight));
                    expectedMask[x] = (expectedMask[x] >> 6) * columnWeight;
                }

                if (bitDepth == 8)
                {
                    Av1ObmcSearch.WeightLeft<byte, Av1ObmcSearch.ByteOperator>(ToBytes(samples), columnWeights, leftSource, leftMask);
                }
                else
                {
                    Av1ObmcSearch.WeightLeft<ushort, Av1ObmcSearch.UInt16Operator>(samples, columnWeights, leftSource, leftMask);
                }

                Assert.Equal(expectedSource, leftSource);
                Assert.Equal(expectedMask, leftMask);

                // Source pass.
                int[] target = new int[width];
                for (int x = 0; x < width; x++)
                {
                    target[x] = random.Next(0, (4096 * maximum) + 1);
                    expectedSource[x] = (samples[x] * 64 * 64) - target[x];
                }

                if (bitDepth == 8)
                {
                    Av1ObmcSearch.SubtractFromSource<byte, Av1ObmcSearch.ByteOperator>(ToBytes(samples), target);
                }
                else
                {
                    Av1ObmcSearch.SubtractFromSource<ushort, Av1ObmcSearch.UInt16Operator>(samples, target);
                }

                Assert.Equal(expectedSource, target);
            }
        }
    }

    private static void AssertMoments(ushort[] prediction, int stride, int[] weightedSource, int[] mask, int width, int height, int bitDepth)
    {
        GetMomentsReference(prediction, stride, weightedSource, mask, width, height, out long expectedSum, out ulong expectedSquares);
        int actualSum;
        ulong actualSquares;
        if (bitDepth == 8)
        {
            Av1ObmcSearch.GetMoments<byte, Av1ObmcSearch.ByteOperator>(ToBytes(prediction), stride, weightedSource, mask, width, height, out actualSum, out actualSquares);
        }
        else
        {
            Av1ObmcSearch.GetMoments<ushort, Av1ObmcSearch.UInt16Operator>(prediction, stride, weightedSource, mask, width, height, out actualSum, out actualSquares);
        }

        Assert.Equal(expectedSum, actualSum);
        Assert.Equal(expectedSquares, actualSquares);
    }

    /// <summary>
    /// The scalar definition of obmc_sad().
    /// </summary>
    private static int SumAbsoluteDifferencesReference(ushort[] prediction, int stride, int[] weightedSource, int[] mask, int width, int height)
    {
        uint sad = 0;
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int position = (row * width) + column;
                sad += (uint)((Math.Abs(weightedSource[position] - (prediction[(row * stride) + column] * mask[position])) + 2048) >> 12);
            }
        }

        return (int)sad;
    }

    /// <summary>
    /// The scalar definition of the accumulation of obmc_variance(), with ROUND_POWER_OF_TWO_SIGNED().
    /// </summary>
    private static void GetMomentsReference(ushort[] prediction, int stride, int[] weightedSource, int[] mask, int width, int height, out long sum, out ulong squares)
    {
        sum = 0;
        squares = 0;
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int position = (row * width) + column;
                int value = weightedSource[position] - (prediction[(row * stride) + column] * mask[position]);
                int difference = value < 0 ? -((-value + 2048) >> 12) : (value + 2048) >> 12;
                sum += difference;
                squares += (ulong)((long)difference * difference);
            }
        }
    }

    private static ushort[] CreateSamples(Random random, int length, int bitDepth)
    {
        ushort[] samples = new ushort[length];
        for (int i = 0; i < length; i++)
        {
            samples[i] = (ushort)random.Next(0, 1 << bitDepth);
        }

        return samples;
    }

    /// <summary>
    /// Creates weighted source values over the full range of calc_target_weighted_pred(): the source
    /// scaled by 64 * 64 minus a neighbor term of up to the same size.
    /// </summary>
    private static int[] CreateWeightedSource(Random random, int length, int bitDepth)
    {
        int limit = ((1 << bitDepth) - 1) * 4096;
        int[] values = new int[length];
        for (int i = 0; i < length; i++)
        {
            values[i] = random.Next(-limit, limit + 1);
        }

        return values;
    }

    private static int[] CreateMask(Random random, int length)
    {
        int[] values = new int[length];
        for (int i = 0; i < length; i++)
        {
            values[i] = random.Next(0, 4097);
        }

        return values;
    }

    private static byte[] ToBytes(ushort[] samples)
    {
        byte[] bytes = new byte[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            bytes[i] = (byte)samples[i];
        }

        return bytes;
    }
}
