// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the first-pass wavelet energy of 8x8 blocks.
/// </summary>
[Trait("Format", "Avif")]
public class Av1WaveletEnergyTests
{
    /// <summary>
    /// The hardware configurations covering every vector tier and the scalar fallback.
    /// </summary>
    private const HwIntrinsics Configurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies every block energy against haar_ac_sad_8x8_uint8_input(), for block counts that end in every
    /// vector tail and for eight- and twelve-bit samples.
    /// </summary>
    [Fact]
    public void BlockEnergiesMatchReferenceAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertBlockEnergies, Configurations);

    private static void AssertBlockEnergies()
    {
        int seed = 17;
        foreach (int blockCount in new[] { 1, 3, 4, 7, 8, 15, 16, 17, 37 })
        {
            int stride = (blockCount * 8) + 5;
            byte[] samples = new byte[(8 * stride) + 3];
            ushort[] wideSamples = new ushort[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                seed = (seed * 1103515245) + 12345;
                int value = (seed >>> 8) & 0xFFFF;

                // Alternate smooth and noisy blocks so the bands carry both small and large values.
                int column = (i % stride) / 8;
                samples[i] = (byte)((column & 1) == 0 ? (i % stride) * 3 : value);
                wideSamples[i] = (ushort)((column & 1) == 0 ? ((i % stride) * 51) & 4095 : value & 4095);
            }

            int[] energies = new int[blockCount];
            Av1FirstPass<byte, Av1FirstPassOperator.ByteOperator>.GetWaveletEnergies(samples, 3, stride, energies);
            for (int block = 0; block < blockCount; block++)
            {
                Assert.Equal(GetHaarAcSad(samples, 3 + (block * 8), stride), energies[block]);
            }

            Av1FirstPass<ushort, Av1FirstPassOperator.UInt16Operator>.GetWaveletEnergies(wideSamples, 3, stride, energies);
            for (int block = 0; block < blockCount; block++)
            {
                Assert.Equal(GetHaarAcSad(wideSamples, 3 + (block * 8), stride), energies[block]);
            }
        }
    }

    private static int GetHaarAcSad<T>(T[] samples, int index, int stride)
        where T : unmanaged, IConvertible
    {
        int[] c = new int[64];
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                c[(i * 8) + j] = samples[index + (i * stride) + j].ToInt32(null) << 2;
            }
        }

        int[] buffer = new int[16];
        int hh = 8;
        int hw = 8;
        for (int level = 0; level < 4; level++)
        {
            int nh = hh;
            hh = (hh + 1) >> 1;
            int nw = hw;
            hw = (hw + 1) >> 1;
            if (nh < 2 || nw < 2)
            {
                break;
            }

            for (int i = 0; i < nh; i++)
            {
                Array.Copy(c, i * 8, buffer, 0, nw);
                AnalyzeRow(nw, buffer, 0, c, i * 8, (i * 8) + hw);
            }

            for (int j = 0; j < nw; j++)
            {
                for (int i = 0; i < nh; i++)
                {
                    buffer[i + nh] = c[(i * 8) + j];
                }

                AnalyzeColumn(nh, buffer, nh, 0, hh);
                for (int i = 0; i < nh; i++)
                {
                    c[(i * 8) + j] = buffer[i];
                }
            }
        }

        int sad = 0;
        for (int r = 0; r < 8; r++)
        {
            for (int column = 0; column < 8; column++)
            {
                if (r >= 4 || column >= 4)
                {
                    sad += Math.Abs(c[(r * 8) + column]);
                }
            }
        }

        return sad;
    }

    // A transcription of analysis_53_row() with pointers as indices.
    private static void AnalyzeRow(int length, int[] x, int xi, int[] c, int low, int high)
    {
        int n = length >> 1;
        int a = low;
        int b = high;
        int r;
        while (--n != 0)
        {
            r = x[xi++];
            c[a++] = r * 2;
            c[b++] = x[xi] - ((r + x[xi + 1] + 1) >> 1);
            xi++;
        }

        r = x[xi++];
        c[a] = r * 2;
        c[b] = x[xi] - r;

        n = length >> 1;
        a = low;
        b = high;
        r = c[high];
        while (n-- != 0)
        {
            c[a++] += (r + c[b] + 1) >> 1;
            r = c[b++];
        }
    }

    // A transcription of analysis_53_col() with pointers as indices into one buffer.
    private static void AnalyzeColumn(int length, int[] buffer, int xi, int low, int high)
    {
        int n = length >> 1;
        int a = low;
        int b = high;
        int r;
        while (--n != 0)
        {
            r = buffer[xi++];
            buffer[a++] = r;
            buffer[b++] = ((buffer[xi] * 2) - (r + buffer[xi + 1]) + 2) >> 2;
            xi++;
        }

        r = buffer[xi++];
        buffer[a] = r;
        buffer[b] = (buffer[xi] - r + 1) >> 1;

        n = length >> 1;
        a = low;
        b = high;
        r = buffer[high];
        while (n-- != 0)
        {
            buffer[a++] += (r + buffer[b] + 1) >> 1;
            r = buffer[b++];
        }
    }
}
