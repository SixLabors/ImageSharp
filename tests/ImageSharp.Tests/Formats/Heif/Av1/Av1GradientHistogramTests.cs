// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the Sobel gradients and histogram bins of the intra-mode gradient histogram.
/// </summary>
[Trait("Format", "Avif")]
public class Av1GradientHistogramTests
{
    /// <summary>
    /// The hardware configurations covering every vector tier and the scalar fallback.
    /// </summary>
    private const HwIntrinsics Configurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    private static readonly int[] Thresholds =
    [
        -1334015, -441798, -261605, -183158, -138560, -109331, -88359, -72303,
        -59392, -48579, -39272, -30982, -23445, -16400, -9715, -3194,
        3227, 9748, 16433, 23478, 31015, 39305, 48611, 59425,
        72336, 88392, 109364, 138593, 183191, 261638, 441831, int.MaxValue
    ];

    /// <summary>
    /// Verifies every interior sample's magnitude and bin against the per-sample definitions of
    /// lowbd_generate_hog() and get_hist_bin_idx(), for every block width and sample precision.
    /// </summary>
    [Fact]
    public void GradientRowsMatchReferenceAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertRows, Configurations);

    private static void AssertRows()
    {
        foreach (int width in new[] { 4, 8, 16, 32, 64, 128, 12, 37, 77 })
        {
            foreach (int bitDepth in new[] { 8, 10, 12 })
            {
                int maximum = (1 << bitDepth) - 1;
                for (int pattern = 0; pattern < 4; pattern++)
                {
                    short[] block = new short[width * 3];
                    for (int i = 0; i < block.Length; i++)
                    {
                        block[i] = (short)(pattern switch
                        {
                            0 => (i * 7919) % (maximum + 1),
                            1 => (i & 1) == 0 ? maximum : 0,
                            2 => (i / width) == 1 ? maximum : 0,
                            _ => ((i % width) * 37) % (maximum + 1)
                        });
                    }

                    short[] magnitudes = new short[128];
                    int[] bins = new int[128];
                    Av1GradientHistogram.ComputeRow(block, width, 1, magnitudes, bins, new short[128], new short[128]);
                    for (int column = 1; column < width - 1; column++)
                    {
                        int dx = block[column + 1] + (2 * block[width + column + 1]) + block[(2 * width) + column + 1]
                            - block[column - 1] - (2 * block[width + column - 1]) - block[(2 * width) + column - 1];

                        int dy = block[(2 * width) + column - 1] + (2 * block[(2 * width) + column]) + block[(2 * width) + column + 1]
                            - block[column - 1] - (2 * block[column]) - block[column + 1];

                        Assert.Equal(Math.Abs(dx) + Math.Abs(dy), magnitudes[column - 1]);
                        int expectedBin = Av1GradientHistogram.VerticalBin;
                        if (dx != 0)
                        {
                            int ratio = (dy * (1 << 16)) / dx;
                            expectedBin = 0;
                            while (ratio > Thresholds[expectedBin])
                            {
                                expectedBin++;
                            }
                        }

                        Assert.Equal(expectedBin, bins[column - 1]);
                    }
                }
            }
        }
    }
}
