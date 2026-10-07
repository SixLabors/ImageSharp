// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// The temporal dependency state of one frame of the model: the block statistics, the grid geometry, the base rate
/// multiplier and the reference mapping. The source and reconstructed pictures of the frame are held by the model.
/// Reference: TplDepFrame.
/// </summary>
internal sealed class Av1TplFrameStatistics
{
    /// <summary>
    /// The block statistics, or empty memory for a reference-slot frame. The model owns the storage; one
    /// pool entry is lent to each processed frame of a golden group. Reference: tpl_stats_ptr.
    /// </summary>
    private Memory<Av1TplBlockStatistics> statistics;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TplFrameStatistics"/> class.
    /// </summary>
    /// <param name="modeInfoRows">The number of mode-information rows of the frame.</param>
    /// <param name="modeInfoColumns">The number of mode-information columns of the frame.</param>
    public Av1TplFrameStatistics(int modeInfoRows, int modeInfoColumns)
    {
        // The grid covers the frame rounded up to whole 128x128 superblocks, in 16x16 units. Reference: the width and
        // height setup of av1_setup_tpl_buffers(), with ALIGN_POWER_OF_TWO(mi_cols, MAX_MIB_SIZE_LOG2).
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
    /// Reference: is_valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets the number of 16x16 blocks per statistics row. Reference: stride.
    /// </summary>
    public int Stride { get; }

    /// <summary>
    /// Gets the number of 16x16 block columns. Reference: width.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the number of 16x16 block rows. Reference: height.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the number of mode-information rows of the frame. Reference: mi_rows.
    /// </summary>
    public int ModeInfoRows { get; }

    /// <summary>
    /// Gets the number of mode-information columns of the frame. Reference: mi_cols.
    /// </summary>
    public int ModeInfoColumns { get; }

    /// <summary>
    /// Gets or sets the rate multiplier at the model quantizer, divided by six. Reference: base_rdmult.
    /// </summary>
    public int BaseRateMultiplier { get; set; }

    /// <summary>
    /// Gets or sets the display index of the frame. Reference: frame_display_index.
    /// </summary>
    public int DisplayIndex { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether mode decisions compare absolute differences instead of transform costs.
    /// Reference: use_pred_sad.
    /// </summary>
    public bool UsePredictionSad { get; set; }

    /// <summary>
    /// Gets the model frame index of each named reference, where negative indices name reference slots: slot i is
    /// index -i-1. Reference: ref_map_index.
    /// </summary>
    public int[] ReferenceMapIndex { get; }

    /// <summary>
    /// Gets a value indicating whether block statistics storage is attached.
    /// </summary>
    public bool HasStatistics => !this.statistics.IsEmpty;

    /// <summary>
    /// Gets the block statistics in raster order of 16x16 blocks. Reference: tpl_stats_ptr.
    /// </summary>
    public Span<Av1TplBlockStatistics> Statistics => this.statistics.Span;

    /// <summary>
    /// Returns the storage index of a mode-information position. Reference: av1_tpl_ptr_pos().
    /// </summary>
    /// <param name="modeInfoRow">The mode-information row.</param>
    /// <param name="modeInfoColumn">The mode-information column.</param>
    /// <param name="stride">The number of blocks per statistics row.</param>
    /// <param name="rightShift">The base-two logarithm of the block size in mode-information units.</param>
    /// <returns>The storage index.</returns>
    public static int GetPosition(int modeInfoRow, int modeInfoColumn, int stride, int rightShift)
        => ((modeInfoRow >> rightShift) * stride) + (modeInfoColumn >> rightShift);

    /// <summary>
    /// Attaches pooled block statistics storage, or detaches it. Reference: the tpl_stats_ptr assignment of
    /// init_gop_frames_for_tpl().
    /// </summary>
    /// <param name="storage">The pool entry, or empty memory to detach.</param>
    public void AttachStatistics(Memory<Av1TplBlockStatistics> storage) => this.statistics = storage;
}
