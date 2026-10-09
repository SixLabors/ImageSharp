// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// The temporal dependency state of one frame of the model: the block statistics, the grid geometry, the base rate
/// multiplier and the reference mapping. The model holds the source and reconstructed pictures of the frame.
/// </summary>
internal sealed class Av1TplFrameStatistics
{
    /// <summary>
    /// The block statistics, or empty memory for a reference-slot frame. The model owns the storage. It lends one pool
    /// entry to each processed frame of a golden group.
    /// </summary>
    private Memory<Av1TplBlockStatistics> statistics;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TplFrameStatistics"/> class.
    /// </summary>
    /// <param name="modeInfoRows">The number of mode-information rows of the frame.</param>
    /// <param name="modeInfoColumns">The number of mode-information columns of the frame.</param>
    public Av1TplFrameStatistics(int modeInfoRows, int modeInfoColumns)
    {
        // The grid covers the frame rounded up to whole 128x128 superblocks, in 16x16 units.
        int alignedColumns = Av1Math.AlignPowerOf2(modeInfoColumns, Av1TplModelConstants.MaximumModeInfoSizeLog2);
        int alignedRows = Av1Math.AlignPowerOf2(modeInfoRows, Av1TplModelConstants.MaximumModeInfoSizeLog2);
        this.Width = alignedColumns >> Av1TplModelConstants.BlockModeInfoLog2;
        this.Height = alignedRows >> Av1TplModelConstants.BlockModeInfoLog2;
        this.Stride = this.Width;
        this.ModeInfoRows = modeInfoRows;
        this.ModeInfoColumns = modeInfoColumns;
        this.ReferenceMapIndex = new int[Av1TplModelConstants.ReferenceFrameSlotCount];
    }

    /// <summary>
    /// Gets or sets a value indicating whether the statistics were measured for the current golden group.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets the number of 16x16 blocks per statistics row.
    /// </summary>
    public int Stride { get; }

    /// <summary>
    /// Gets the number of 16x16 block columns.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the number of 16x16 block rows.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the number of mode-information rows of the frame.
    /// </summary>
    public int ModeInfoRows { get; }

    /// <summary>
    /// Gets the number of mode-information columns of the frame.
    /// </summary>
    public int ModeInfoColumns { get; }

    /// <summary>
    /// Gets or sets the rate multiplier at the model quantizer, divided by six.
    /// </summary>
    public int BaseRateMultiplier { get; set; }

    /// <summary>
    /// Gets or sets the display index of the frame.
    /// </summary>
    public int DisplayIndex { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether mode decisions compare absolute differences instead of transform costs.
    /// </summary>
    public bool UsePredictionSad { get; set; }

    /// <summary>
    /// Gets the model frame index of each named reference. Negative indices name reference slots: slot i is index -i-1.
    /// </summary>
    public int[] ReferenceMapIndex { get; }

    /// <summary>
    /// Gets the block statistics in raster order of 16x16 blocks.
    /// </summary>
    public Span<Av1TplBlockStatistics> Statistics => this.statistics.Span;

    /// <summary>
    /// Returns the storage index of a mode-information position.
    /// </summary>
    /// <param name="modeInfoRow">The mode-information row.</param>
    /// <param name="modeInfoColumn">The mode-information column.</param>
    /// <param name="stride">The number of blocks per statistics row.</param>
    /// <param name="rightShift">The base-two logarithm of the block size in mode-information units.</param>
    /// <returns>The storage index.</returns>
    public static int GetPosition(int modeInfoRow, int modeInfoColumn, int stride, int rightShift)
        => ((modeInfoRow >> rightShift) * stride) + (modeInfoColumn >> rightShift);

    /// <summary>
    /// Attaches pooled block statistics storage, or detaches it.
    /// </summary>
    /// <param name="storage">The pool entry, or empty memory to detach.</param>
    public void AttachStatistics(Memory<Av1TplBlockStatistics> storage) => this.statistics = storage;
}
