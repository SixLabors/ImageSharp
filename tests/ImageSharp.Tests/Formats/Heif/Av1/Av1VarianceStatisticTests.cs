// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the smoothing variance measure against libaom's C definition.
/// </summary>
[Trait("Format", "Avif")]
public class Av1VarianceStatisticTests
{
    /// <summary>
    /// The hardware configurations covering every vector tier and the scalar fallback.
    /// </summary>
    private const HwIntrinsics Configurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies every block shape at eight and twelve bits at every SIMD tier.
    /// </summary>
    [Fact]
    public void VarianceStatisticMatchesLibaomReferenceAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(Validate, Configurations);

    /// <summary>
    /// Compares every block shape from 4 to 128 samples at eight and twelve bits with the reference.
    /// </summary>
    private static void Validate()
    {
        ReadOnlySpan<int> sides = [4, 8, 16, 32, 64, 128];
        const int stride = 160;
        byte[] bytes = new byte[stride * 130];
        ushort[] words = new ushort[stride * 130];
        for (int i = 0; i < bytes.Length; i++)
        {
            // Edges, flat runs and noise, so every lane sees large and small differences.
            int value = ((i * 7919) ^ (i >> 5) * 31) & 0xFFF;
            words[i] = (ushort)value;
            bytes[i] = (byte)(value >> 4);
        }

        foreach (int width in sides)
        {
            foreach (int height in sides)
            {
                Assert.Equal(
                    Reference(bytes, stride, width, height),
                    Av1VarianceStatistic.Calculate<byte, Av1MotionVectorStatistics.ByteTextureOperator>(bytes, stride, width, height));

                Assert.Equal(
                    Reference(words, stride, width, height),
                    Av1VarianceStatistic.Calculate<ushort, Av1MotionVectorStatistics.UInt16TextureOperator>(words, stride, width, height));
            }
        }
    }

    /// <summary>
    /// Verifies that a block past the frame edge measures as the reference measures a source whose border repeats
    /// the last visible row and column.
    /// </summary>
    [Fact]
    public void VarianceStatisticWithBorderMatchesExtendedSource()
    {
        ReadOnlySpan<int> sides = [4, 8, 16, 32];
        const int stride = 40;
        byte[] bytes = new byte[stride * 40];
        ushort[] words = new ushort[stride * 40];
        for (int i = 0; i < bytes.Length; i++)
        {
            int value = ((i * 7919) ^ (i >> 5) * 31) & 0xFFF;
            words[i] = (ushort)value;
            bytes[i] = (byte)(value >> 4);
        }

        foreach (int width in sides)
        {
            foreach (int height in sides)
            {
                foreach (int visibleWidth in (ReadOnlySpan<int>)[1, 3, width])
                {
                    foreach (int visibleHeight in (ReadOnlySpan<int>)[1, 3, height])
                    {
                        if (visibleWidth > width || visibleHeight > height)
                        {
                            continue;
                        }

                        Assert.Equal(
                            Reference(Extend(bytes, stride, width, height, visibleWidth, visibleHeight), stride, width, height),
                            Av1VarianceStatistic.CalculateWithBorder<byte, Av1MotionVectorStatistics.ByteTextureOperator>(
                                bytes, stride, width, height, visibleWidth, visibleHeight));

                        Assert.Equal(
                            Reference(Extend(words, stride, width, height, visibleWidth, visibleHeight), stride, width, height),
                            Av1VarianceStatistic.CalculateWithBorder<ushort, Av1MotionVectorStatistics.UInt16TextureOperator>(
                                words, stride, width, height, visibleWidth, visibleHeight));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Copies a block and repeats its last visible row and column through the rest of it, as the reference extends
    /// its source border.
    /// </summary>
    /// <typeparam name="T">The sample type.</typeparam>
    /// <param name="source">The samples, starting at the block origin.</param>
    /// <param name="stride">The row stride.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="visibleWidth">The number of columns kept from the source.</param>
    /// <param name="visibleHeight">The number of rows kept from the source.</param>
    /// <returns>A copy with the source's stride, whose block repeats the last kept row and column.</returns>
    private static T[] Extend<T>(T[] source, int stride, int width, int height, int visibleWidth, int visibleHeight)
        where T : unmanaged
    {
        T[] extended = new T[source.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                extended[(y * stride) + x] = source[(Math.Min(y, visibleHeight - 1) * stride) + Math.Min(x, visibleWidth - 1)];
            }
        }

        return extended;
    }

    /// <summary>
    /// Mirrors aom_calc_variance_stat_c() and aom_highbd_calc_variance_stat_c(): a padded copy with repeated edges,
    /// then the 1-2-1 by 1-2-1 smoothing.
    /// </summary>
    /// <typeparam name="T">The sample type.</typeparam>
    /// <param name="source">The samples, starting at the block origin.</param>
    /// <param name="stride">The row stride.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <returns>The measure.</returns>
    private static long Reference<T>(T[] source, int stride, int width, int height)
        where T : unmanaged
    {
        int paddedStride = width + 2;
        int[] padded = new int[paddedStride * (height + 2)];
        for (int y = -1; y < height + 1; y++)
        {
            for (int x = -1; x < width + 1; x++)
            {
                int sourceY = Math.Clamp(y, 0, height - 1);
                int sourceX = Math.Clamp(x, 0, width - 1);
                padded[((y + 1) * paddedStride) + x + 1] = Convert.ToInt32(source[(sourceY * stride) + sourceX]);
            }
        }

        ReadOnlySpan<int> filter = [1, 2, 1, 2, 4, 2, 1, 2, 1];
        long total = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int sum = 0;
                for (int filterY = 0; filterY < 3; filterY++)
                {
                    for (int filterX = 0; filterX < 3; filterX++)
                    {
                        sum += padded[((y + filterY) * paddedStride) + x + filterX] * filter[(filterY * 3) + filterX];
                    }
                }

                long difference = padded[((y + 1) * paddedStride) + x + 1] - (sum >> 4);
                total += difference * difference;
            }
        }

        return total << 4;
    }
}
