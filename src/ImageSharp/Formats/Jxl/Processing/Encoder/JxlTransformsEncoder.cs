// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

internal static class JxlTransformsEncoder
{
    private const int MaxBlocks = 32;

    public static void ReinterpretingInverseDct(
        int dctRows,
        int dctCols,
        int lfRows,
        int lfCols,
        int rows,
        int cols,
        Span<float> input,
        int inputStride,
        Span<float> output,
        int outputStride,
        Span<float> scratch)
    {
        if (rows < cols)
        {
            ReadOnlySpan<float> sp1 = JxlDctScales.GetResampleScales(dctRows, rows);
            ReadOnlySpan<float> sp2 = JxlDctScales.GetResampleScales(dctCols, cols);

            for (int y = 0; y < lfRows; y++)
            {
                int yCols = y * cols;
                int yInputStride = y * inputStride;

                for (int x = 0; x < lfCols; x++)
                {
                    scratch[yCols + x] = input[yInputStride + x] * sp1[y] * sp2[y];
                }
            }
        }
        else
        {
            ReadOnlySpan<float> sp2 = JxlDctScales.GetResampleScales(dctRows, rows);
            ReadOnlySpan<float> sp1 = JxlDctScales.GetResampleScales(dctCols, cols);

            for (int y = 0; y < lfCols; y++)
            {
                int yRows = y * rows;
                int yInputStride = y * inputStride;

                for (int x = 0; x < lfRows; x++)
                {
                    scratch[yRows + x] = input[yInputStride + x] * sp1[y] * sp2[y];
                }
            }
        }

        Span<float> scratchSpace = scratch[(MaxBlocks * MaxBlocks)..];
        JxlDct.ComputeScaledInverseDct(rows, cols, scratch, new JxlDctOutput(output, outputStride), scratchSpace);
    }
}
