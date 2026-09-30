// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Group.BlockLoader;

internal sealed class JxlEncoderBlockLoader : IJxlGetBlock
{
    private readonly IReadOnlyList<IJxlDctAcImage> quantizedAc;
    private readonly int[][] rows;
    private readonly uint[] shiftForPass;

    private int offset;

    public JxlEncoderBlockLoader(
        IReadOnlyList<IJxlDctAcImage> quantizedAc,
        int[][] rows,
        uint[] shiftForPass)
    {
        this.quantizedAc = quantizedAc;
        this.rows = rows;
        this.shiftForPass = shiftForPass;
        this.offset = 0;
    }

    public void StartRow(int by)
    {
    }

    public bool TryLoadBlock(
        int bx,
        int by,
        JxlAcStrategy acs,
        int size,
        int log2CoveredBlocks,
        JxlDctAcPointer block0,
        JxlDctAcPointer block1,
        JxlDctAcPointer block2,
        JxlDctAcType acType)
    {
        if (acType != JxlDctAcType.Ac32)
        {
            return false;
        }

        for (int c = 0; c < 3; c++)
        {
            JxlDctAcPointer block = c switch
            {
                0 => block0,
                1 => block1,
                _ => block2
            };

            for (int i = 0; i < this.quantizedAc.Count; i++)
            {
                Span<int> block32 = block.Pointer32;

                int shift = (int)this.shiftForPass[i];
                int scale = 1 << shift;
                ReadOnlySpan<int> row = this.rows[(i * 3) + c].AsSpan();

                for (int k = 0; k < size; k++)
                {
                    block32[k] += row[this.offset + k] * scale;
                }
            }
        }

        this.offset += size;
        return true;
    }

    public static JxlEncoderBlockLoader Create(
        IReadOnlyList<IJxlDctAcImage> ac,
        int groupIndex,
        ReadOnlySpan<uint> shiftForPass)
    {
        int[][] rows = new int[ac.Count * 3][];

        for (int i = 0; i < ac.Count; i++)
        {
            if (ac[i].Type != JxlDctAcType.Ac32)
            {
                throw new InvalidOperationException("AC image must use 32-bit coefficients.");
            }

            for (int c = 0; c < 3; c++)
            {
                rows[(i * 3) + c] = ac[i].GetPlaneRow(c, groupIndex, 0).Pointer32.ToArray();
            }
        }

        return new JxlEncoderBlockLoader(
            ac,
            rows,
            shiftForPass.ToArray());
    }
}
