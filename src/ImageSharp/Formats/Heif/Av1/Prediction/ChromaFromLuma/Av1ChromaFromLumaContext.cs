// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;

internal class Av1ChromaFromLumaContext
{
    private const int BufferLine = 32;

    private int bufferHeight;
    private int bufferWidth;
    private readonly bool subX;
    private readonly bool subY;

    public Av1ChromaFromLumaContext(Configuration configuration, ObuColorConfig colorConfig)
    {
        this.subX = colorConfig.SubSamplingX;
        this.subY = colorConfig.SubSamplingY;
        this.Q3Buffer = configuration.MemoryAllocator.Allocate2D<short>(new Size(32, 32), AllocationOptions.Clean);
    }

    public Buffer2D<short> Q3Buffer { get; private set; }

    public bool AreParametersComputed { get; private set; }

    public void ComputeParameters(Av1TransformSize transformSize)
    {
        Guard.IsFalse(this.AreParametersComputed, nameof(this.AreParametersComputed), "Do not call cfl_compute_parameters multiple time on the same values.");
        this.Pad(transformSize.GetWidth(), transformSize.GetHeight());
        SubtractAverage(ref this.Q3Buffer[0, 0], transformSize);
        this.AreParametersComputed = true;
    }

    private void Pad(int width, int height)
    {
        int diff_width = width - this.bufferWidth;
        int diff_height = height - this.bufferHeight;

        if (diff_width > 0)
        {
            int min_height = height - diff_height;
            ref short recon_buf_q3 = ref this.Q3Buffer[width - diff_width, 0];
            for (int j = 0; j < min_height; j++)
            {
                short last_pixel = Unsafe.Subtract(ref recon_buf_q3, 1);
                Guard.IsTrue(Unsafe.IsAddressLessThan(ref Unsafe.Add(ref recon_buf_q3, diff_width), ref this.Q3Buffer[BufferLine, BufferLine]), nameof(recon_buf_q3), "Shall stay within bounds.");
                for (int i = 0; i < diff_width; i++)
                {
                    Unsafe.Add(ref recon_buf_q3, i) = last_pixel;
                }

                recon_buf_q3 += BufferLine;
            }

            this.bufferWidth = width;
        }

        if (diff_height > 0)
        {
            ref short recon_buf_q3 = ref this.Q3Buffer[0, height - diff_height];
            for (int j = 0; j < diff_height; j++)
            {
                ref short last_row_q3 = ref Unsafe.Subtract(ref recon_buf_q3, BufferLine);
                Guard.IsTrue(Unsafe.IsAddressLessThan(ref Unsafe.Add(ref recon_buf_q3, diff_width), ref this.Q3Buffer[BufferLine, BufferLine]), nameof(recon_buf_q3), "Shall stay within bounds.");
                for (int i = 0; i < width; i++)
                {
                    Unsafe.Add(ref recon_buf_q3, i) = Unsafe.Add(ref last_row_q3, i);
                }

                recon_buf_q3 += BufferLine;
            }

            this.bufferHeight = height;
        }
    }

    /************************************************************************************************
    * svt_subtract_average_c
    * Calculate the DC value by averaging over all sample. Subtract DC value to get AC values In C
    ************************************************************************************************/
    private static void SubtractAverage(ref short pred_buf_q3, Av1TransformSize transformSize)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int roundOffset = (width * height) >> 1;
        int pelCountLog2 = transformSize.GetBlockWidthLog2() + transformSize.GetBlockHeightLog2();
        int sum_q3 = 0;
        ref short pred_buf = ref pred_buf_q3;
        for (int j = 0; j < height; j++)
        {
            // assert(pred_buf_q3 + tx_width <= cfl->pred_buf_q3 + CFL_BUF_SQUARE);
            for (int i = 0; i < width; i++)
            {
                sum_q3 += Unsafe.Add(ref pred_buf, i);
            }

            pred_buf += BufferLine;
        }

        int avg_q3 = (sum_q3 + roundOffset) >> pelCountLog2;

        // Loss is never more than 1/2 (in Q3)
        // assert(abs((avg_q3 * (1 << num_pel_log2)) - sum_q3) <= 1 << num_pel_log2 >>
        //       1);
        for (int j = 0; j < height; j++)
        {
            for (int i = 0; i < width; i++)
            {
                Unsafe.Add(ref pred_buf_q3, i) -= (short)avg_q3;
            }

            pred_buf_q3 += BufferLine;
        }
    }
}
