// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;

internal class Av1ChromaFromLumaContext
{
    private const int BufferLine = 32;

    private int bufferHeight;
    private int bufferWidth;
    private readonly bool subX;
    private readonly bool subY;

    public Av1ChromaFromLumaContext(ObuColorConfig colorConfig)
    {
        this.subX = colorConfig.SubSamplingX;
        this.subY = colorConfig.SubSamplingY;
        this.Q3Buffer = new short[BufferLine * BufferLine];
    }

    public short[] Q3Buffer { get; }

    public bool AreParametersComputed { get; private set; }

    public void Store<T>(
        Span<T> input,
        int inputStride,
        int row,
        int column,
        Av1TransformSize transformSize,
        Av1BlockSize blockSize,
        int modeInfoRow,
        int modeInfoColumn)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (blockSize.GetHeight() == 4 || blockSize.GetWidth() == 4)
        {
            // Subsampled chroma shares one CfL surface across the adjacent sub-8x8 luma blocks.
            if ((modeInfoRow & 1) != 0 && this.subY)
            {
                row++;
            }

            if ((modeInfoColumn & 1) != 0 && this.subX)
            {
                column++;
            }
        }

        int subX = this.subX ? 1 : 0;
        int subY = this.subY ? 1 : 0;
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int storeRow = row << (Av1Constants.ModeInfoSizeLog2 - subY);
        int storeColumn = column << (Av1Constants.ModeInfoSizeLog2 - subX);
        int storeWidth = width >> subX;
        int storeHeight = height >> subY;
        this.AreParametersComputed = false;

        if (column == 0 && row == 0)
        {
            this.bufferWidth = storeWidth;
            this.bufferHeight = storeHeight;
        }
        else
        {
            this.bufferWidth = Math.Max(storeColumn + storeWidth, this.bufferWidth);
            this.bufferHeight = Math.Max(storeRow + storeHeight, this.bufferHeight);
        }

        int outputOffset = (storeRow * BufferLine) + storeColumn;
        if (!this.subX)
        {
            // A direct luma sample is multiplied by eight to produce the Q3 representation used by CfL.
            for (int y = 0; y < height; y++)
            {
                int inputRow = y * inputStride;
                int outputRow = outputOffset + (y * BufferLine);
                for (int x = 0; x < width; x++)
                {
                    this.Q3Buffer[outputRow + x] = (short)(int.CreateChecked(input[inputRow + x]) << 3);
                }
            }
        }
        else if (!this.subY)
        {
            // The pair sum is multiplied by four, which is the Q3 representation of its horizontal average.
            for (int y = 0; y < height; y++)
            {
                int inputRow = y * inputStride;
                int outputRow = outputOffset + (y * BufferLine);
                for (int x = 0; x < width; x += 2)
                {
                    int sum = int.CreateChecked(input[inputRow + x]) + int.CreateChecked(input[inputRow + x + 1]);
                    this.Q3Buffer[outputRow + (x >> 1)] = (short)(sum << 2);
                }
            }
        }
        else
        {
            // The 2x2 sum is multiplied by two, which is the Q3 representation of its four-sample average.
            for (int y = 0; y < height; y += 2)
            {
                int inputRow = y * inputStride;
                int nextInputRow = inputRow + inputStride;
                int outputRow = outputOffset + ((y >> 1) * BufferLine);
                for (int x = 0; x < width; x += 2)
                {
                    int sum = int.CreateChecked(input[inputRow + x]) +
                        int.CreateChecked(input[inputRow + x + 1]) +
                        int.CreateChecked(input[nextInputRow + x]) +
                        int.CreateChecked(input[nextInputRow + x + 1]);

                    this.Q3Buffer[outputRow + (x >> 1)] = (short)(sum << 1);
                }
            }
        }
    }

    public void ComputeParameters(Av1TransformSize transformSize)
    {
        Guard.IsFalse(this.AreParametersComputed, nameof(this.AreParametersComputed), "Do not call cfl_compute_parameters multiple time on the same values.");
        this.Pad(transformSize.GetWidth(), transformSize.GetHeight());
        this.SubtractAverage(transformSize);
        this.AreParametersComputed = true;
    }

    private void Pad(int width, int height)
    {
        int differenceWidth = width - this.bufferWidth;
        int differenceHeight = height - this.bufferHeight;

        if (differenceWidth > 0)
        {
            int minimumHeight = height - differenceHeight;
            for (int y = 0; y < minimumHeight; y++)
            {
                int rowOffset = y * BufferLine;
                short lastPixel = this.Q3Buffer[rowOffset + this.bufferWidth - 1];
                this.Q3Buffer.AsSpan(rowOffset + this.bufferWidth, differenceWidth).Fill(lastPixel);
            }

            this.bufferWidth = width;
        }

        if (differenceHeight > 0)
        {
            for (int y = this.bufferHeight; y < height; y++)
            {
                int rowOffset = y * BufferLine;
                this.Q3Buffer.AsSpan(rowOffset - BufferLine, width).CopyTo(this.Q3Buffer.AsSpan(rowOffset, width));
            }

            this.bufferHeight = height;
        }
    }

    /************************************************************************************************
    * svt_subtract_average_c
    * Calculate the DC value by averaging over all sample. Subtract DC value to get AC values In C
    ************************************************************************************************/
    private void SubtractAverage(Av1TransformSize transformSize)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int roundOffset = (width * height) >> 1;
        int pelCountLog2 = transformSize.GetBlockWidthLog2() + transformSize.GetBlockHeightLog2();
        int sumQ3 = roundOffset;
        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * BufferLine;
            for (int x = 0; x < width; x++)
            {
                sumQ3 += this.Q3Buffer[rowOffset + x];
            }
        }

        int averageQ3 = sumQ3 >> pelCountLog2;

        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * BufferLine;
            for (int x = 0; x < width; x++)
            {
                this.Q3Buffer[rowOffset + x] -= (short)averageQ3;
            }
        }
    }
}
