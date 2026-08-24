// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;

/// <summary>
/// Accumulates subsampled luma samples and derives the zero-mean Q3 predictor surface used by AV1 chroma-from-luma prediction.
/// </summary>
internal class Av1ChromaFromLumaContext
{
    /// <summary>
    /// The fixed row stride and maximum dimension, in chroma samples, of the luma predictor buffer.
    /// </summary>
    private const int BufferLine = 32;

    /// <summary>
    /// The number of initialized predictor rows currently stored in <see cref="Q3Buffer"/>.
    /// </summary>
    private int bufferHeight;

    /// <summary>
    /// The number of initialized predictor columns currently stored in <see cref="Q3Buffer"/>.
    /// </summary>
    private int bufferWidth;

    /// <summary>
    /// Whether luma is subsampled by two along the horizontal axis for the chroma planes.
    /// </summary>
    private readonly bool subX;

    /// <summary>
    /// Whether luma is subsampled by two along the vertical axis for the chroma planes.
    /// </summary>
    private readonly bool subY;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1ChromaFromLumaContext"/> class.
    /// </summary>
    /// <param name="colorConfig">The AV1 color configuration that supplies chroma subsampling.</param>
    public Av1ChromaFromLumaContext(ObuColorConfig colorConfig)
    {
        this.subX = colorConfig.SubSamplingX;
        this.subY = colorConfig.SubSamplingY;
        this.Q3Buffer = new short[BufferLine * BufferLine];
    }

    /// <summary>
    /// Gets the fixed-stride luma predictor samples in signed Q3 fixed-point representation.
    /// </summary>
    public short[] Q3Buffer { get; }

    /// <summary>
    /// Gets a value indicating whether edge padding and mean subtraction have been applied to the current samples.
    /// </summary>
    public bool AreParametersComputed { get; private set; }

    /// <summary>
    /// Stores one reconstructed luma transform region in the chroma-resolution Q3 predictor buffer.
    /// </summary>
    /// <typeparam name="T">The integer sample type of the reconstructed luma plane.</typeparam>
    /// <param name="input">The reconstructed luma samples for the transform region.</param>
    /// <param name="inputStride">The distance, in samples, between consecutive input rows.</param>
    /// <param name="row">The transform row relative to the chroma-from-luma block, in mode-info units.</param>
    /// <param name="column">The transform column relative to the chroma-from-luma block, in mode-info units.</param>
    /// <param name="transformSize">The luma transform dimensions.</param>
    /// <param name="blockSize">The coded luma block size used to resolve shared sub-8x8 chroma ownership.</param>
    /// <param name="modeInfoRow">The frame-relative luma row in 4x4 mode-info units.</param>
    /// <param name="modeInfoColumn">The frame-relative luma column in 4x4 mode-info units.</param>
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

        // New luma samples invalidate the previously padded, zero-mean surface.
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

    /// <summary>
    /// Pads the populated predictor extent to the transform dimensions and subtracts its rounded mean.
    /// </summary>
    /// <param name="transformSize">The chroma prediction transform dimensions.</param>
    public void ComputeParameters(Av1TransformSize transformSize)
    {
        Guard.IsFalse(this.AreParametersComputed, nameof(this.AreParametersComputed), "Do not call cfl_compute_parameters multiple time on the same values.");
        this.Pad(transformSize.GetWidth(), transformSize.GetHeight());
        this.SubtractAverage(transformSize);
        this.AreParametersComputed = true;
    }

    /// <summary>
    /// Extends the last initialized column and row to cover the requested predictor dimensions.
    /// </summary>
    /// <param name="width">The required predictor width in chroma samples.</param>
    /// <param name="height">The required predictor height in chroma samples.</param>
    private void Pad(int width, int height)
    {
        int differenceWidth = width - this.bufferWidth;
        int differenceHeight = height - this.bufferHeight;

        if (differenceWidth > 0)
        {
            int minimumHeight = height - differenceHeight;

            // AV1 CfL edge extension repeats the final available sample when the coded luma extent is narrower.
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
            // Missing bottom rows repeat the last available row after horizontal extension is complete.
            for (int y = this.bufferHeight; y < height; y++)
            {
                int rowOffset = y * BufferLine;
                this.Q3Buffer.AsSpan(rowOffset - BufferLine, width).CopyTo(this.Q3Buffer.AsSpan(rowOffset, width));
            }

            this.bufferHeight = height;
        }
    }

    /// <summary>
    /// Subtracts the rounded Q3 average from each predictor sample, leaving the AC contribution used by CfL.
    /// </summary>
    /// <param name="transformSize">The populated predictor dimensions.</param>
    /// <remarks>SVT-AV1: <c>svt_subtract_average_c</c>.</remarks>
    private void SubtractAverage(Av1TransformSize transformSize)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();

        // Transform dimensions are powers of two, so division by the sample count is an exact right shift.
        // Half the sample count is accumulated first to round the signed Q3 mean to the nearest integer.
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
