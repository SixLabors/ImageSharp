// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;

/// <summary>
/// Accumulates subsampled luma samples and derives the zero-mean Q3 predictor surface used by AV1 chroma-from-luma prediction.
/// </summary>
internal sealed partial class Av1ChromaFromLumaContext
{
    /// <summary>
    /// The fixed row stride and maximum dimension, in chroma samples, of the luma predictor buffer.
    /// </summary>
    public const int BufferLine = 32;

    /// <summary>
    /// The number of samples in the fixed-stride chroma-from-luma workspace.
    /// </summary>
    public const int BufferLength = BufferLine * BufferLine;

    /// <summary>
    /// The caller-owned fixed-stride luma predictor workspace.
    /// </summary>
    private readonly Memory<short> q3Buffer;

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
        : this(colorConfig, new short[BufferLength])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1ChromaFromLumaContext"/> class over caller-owned workspace.
    /// </summary>
    /// <param name="colorConfig">The AV1 color configuration that supplies chroma subsampling.</param>
    /// <param name="q3Buffer">The fixed-stride signed Q3 workspace retained for the context lifetime.</param>
    public Av1ChromaFromLumaContext(ObuColorConfig colorConfig, Memory<short> q3Buffer)
    {
        this.subX = colorConfig.SubSamplingX;
        this.subY = colorConfig.SubSamplingY;
        this.q3Buffer = q3Buffer;
    }

    /// <summary>
    /// Gets the fixed-stride luma predictor samples in signed Q3 fixed-point representation.
    /// </summary>
    public Span<short> Q3Buffer => this.q3Buffer.Span;

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
        => this.Store(input, inputStride, row, column, transformSize.GetWidth(), transformSize.GetHeight(), blockSize, modeInfoRow, modeInfoColumn);

    /// <summary>
    /// Stores a reconstructed luma region in the chroma-resolution Q3 predictor buffer.
    /// </summary>
    /// <typeparam name="T">The integer sample type of the reconstructed luma plane.</typeparam>
    /// <param name="input">The reconstructed luma samples for the region.</param>
    /// <param name="inputStride">The distance, in samples, between consecutive input rows.</param>
    /// <param name="row">The region row relative to the chroma-from-luma block, in mode-info units.</param>
    /// <param name="column">The region column relative to the chroma-from-luma block, in mode-info units.</param>
    /// <param name="width">The width of the luma region in samples.</param>
    /// <param name="height">The height of the luma region in samples.</param>
    /// <param name="blockSize">The coded luma block size used to resolve shared sub-8x8 chroma ownership.</param>
    /// <param name="modeInfoRow">The frame-relative luma row in 4x4 mode-info units.</param>
    /// <param name="modeInfoColumn">The frame-relative luma column in 4x4 mode-info units.</param>
    public void Store<T>(
        Span<T> input,
        int inputStride,
        int row,
        int column,
        int width,
        int height,
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

        // Reconstruction reaches this method only through the byte and short decoder pipelines. Dispatching once
        // here keeps sample conversion out of the row kernels and lets the JIT specialize both storage layouts.
        if (typeof(T) == typeof(byte))
        {
            StoreSamples(
                MemoryMarshal.Cast<T, byte>(input),
                inputStride,
                outputOffset,
                width,
                height,
                this.Q3Buffer,
                this.subX,
                this.subY);
        }
        else
        {
            StoreSamples(
                MemoryMarshal.Cast<T, short>(input),
                inputStride,
                outputOffset,
                width,
                height,
                this.Q3Buffer,
                this.subX,
                this.subY);
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
        SubtractAverage(this.Q3Buffer, transformSize);
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
        Span<short> q3Buffer = this.Q3Buffer;

        if (differenceWidth > 0)
        {
            int minimumHeight = height - differenceHeight;

            // AV1 CfL edge extension repeats the final available sample when the coded luma extent is narrower.
            for (int y = 0; y < minimumHeight; y++)
            {
                int rowOffset = y * BufferLine;
                short lastPixel = q3Buffer[rowOffset + this.bufferWidth - 1];
                q3Buffer.Slice(rowOffset + this.bufferWidth, differenceWidth).Fill(lastPixel);
            }

            this.bufferWidth = width;
        }

        if (differenceHeight > 0)
        {
            // Missing bottom rows repeat the last available row after horizontal extension is complete.
            for (int y = this.bufferHeight; y < height; y++)
            {
                int rowOffset = y * BufferLine;
                q3Buffer.Slice(rowOffset - BufferLine, width).CopyTo(q3Buffer.Slice(rowOffset, width));
            }

            this.bufferHeight = height;
        }
    }
}
