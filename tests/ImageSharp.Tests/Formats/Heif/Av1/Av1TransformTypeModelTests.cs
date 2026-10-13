// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the residual energy features of the inter transform-type pruning model.
/// </summary>
[Trait("Format", "Avif")]
public class Av1TransformTypeModelTests
{
    /// <summary>
    /// The hardware configurations covering every vector tier and the scalar fallback.
    /// </summary>
    private const HwIntrinsics Configurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies the energy projections, bit for bit, against a scalar transcription of
    /// get_energy_distribution_finer() for every model block size and sample precision.
    /// </summary>
    [Fact]
    public void EnergyDistributionMatchesReferenceAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertEnergyDistribution, Configurations);

    private static void AssertEnergyDistribution()
    {
        (int Width, int Height)[] sizes = [(4, 4), (8, 8), (16, 16), (4, 8), (8, 4), (8, 16), (16, 8), (4, 16), (16, 4)];
        foreach ((int width, int height) in sizes)
        {
            foreach (int bitDepth in new[] { 8, 10, 12 })
            {
                int maximum = (1 << bitDepth) - 1;
                int stride = width + 3;
                short[] residual = new short[stride * height];
                for (int pattern = 0; pattern < 5; pattern++)
                {
                    for (int i = 0; i < residual.Length; i++)
                    {
                        residual[i] = (short)(pattern switch
                        {
                            0 => 0,
                            1 => maximum,
                            2 => (i & 1) == 0 ? maximum : -maximum,
                            3 => ((i * 7919) % ((2 * maximum) + 1)) - maximum,
                            _ => ((i * 104729) % 97) - 48
                        });
                    }

                    float[] expectedHorizontal = new float[16];
                    float[] expectedVertical = new float[16];
                    float[] actualHorizontal = new float[16];
                    float[] actualVertical = new float[16];
                    ReferenceEnergyDistribution(residual, stride, width, height, expectedHorizontal, expectedVertical);
                    Av1IntraSuperblockEncoder.GetEnergyDistribution(residual, stride, width, height, actualHorizontal, actualVertical);
                    Assert.Equal(expectedHorizontal, actualHorizontal);
                    Assert.Equal(expectedVertical, actualVertical);
                }
            }
        }
    }

    private static void ReferenceEnergyDistribution(short[] diff, int stride, int bw, int bh, float[] hordist, float[] verdist)
    {
        uint[] esq = new uint[256];
        int wShift = bw <= 8 ? 0 : 1;
        int hShift = bh <= 8 ? 0 : 1;
        int esqW = bw >> wShift;
        int esqH = bh >> hShift;
        for (int i = 0; i < bh; i++)
        {
            for (int j = 0; j < bw; j++)
            {
                esq[((i >> hShift) * esqW) + (j >> wShift)] += (uint)(diff[(i * stride) + j] * diff[(i * stride) + j]);
            }
        }

        ulong total = 0;
        for (int i = 0; i < esqW * esqH; i++)
        {
            total += esq[i];
        }

        if (total == 0)
        {
            for (int j = 0; j < esqW - 1; j++)
            {
                hordist[j] = 1.0f / esqW;
            }

            for (int i = 0; i < esqH - 1; i++)
            {
                verdist[i] = 1.0f / esqH;
            }

            return;
        }

        float reciprocal = 1.0f / total;
        int row;
        for (row = 0; row < esqH - 1; row++)
        {
            int column;
            for (column = 0; column < esqW - 1; column++)
            {
                hordist[column] += esq[(row * esqW) + column];
                verdist[row] += esq[(row * esqW) + column];
            }

            verdist[row] += esq[(row * esqW) + column];
        }

        for (int column = 0; column < esqW - 1; column++)
        {
            hordist[column] += esq[(row * esqW) + column];
        }

        for (int column = 0; column < esqW - 1; column++)
        {
            hordist[column] *= reciprocal;
        }

        for (int i = 0; i < esqH - 1; i++)
        {
            verdist[i] *= reciprocal;
        }
    }
}
