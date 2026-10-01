// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// The encoder's mode-information records as the model sees them. The model borrows the frame's pointer grid and
/// record allocation: each 4x4 grid position points to the record of the coded block that covers it, as the previous
/// coded frame left them, and each model block lends its own record at its top-left position and writes into it. The
/// model reads the partition of its own record, and the luma and chroma modes of the records that the grid positions
/// above and to the left point to. Reference: mi_grid_base and mi_alloc, with set_mode_info_offsets() in
/// mode_estimation() and the above, left and chroma neighbors of set_mi_row_col().
/// </summary>
internal sealed class Av1TplModeInfoGrid
{
    /// <summary>
    /// The grid value of a position no record covers. Reference: a NULL mi_grid_base entry.
    /// </summary>
    private const int NoRecord = -1;

    /// <summary>
    /// The record index of each 4x4 position of the frame.
    /// </summary>
    private readonly int[] grid;

    private byte[] partition = [];
    private byte[] lumaMode = [];
    private byte[] chromaMode = [];
    private bool[] isInter = [];
    private bool[] intraBlockCopy = [];
    private int recordShift;
    private int recordStride;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TplModeInfoGrid"/> class with no records, the state of a
    /// freshly allocated encoder before its first frame.
    /// </summary>
    /// <param name="modeInfoColumns">The number of 4x4 columns of the frame.</param>
    /// <param name="modeInfoRows">The number of 4x4 rows of the frame.</param>
    public Av1TplModeInfoGrid(int modeInfoColumns, int modeInfoRows)
    {
        this.ModeInfoColumns = modeInfoColumns;
        this.ModeInfoRows = modeInfoRows;
        this.grid = new int[modeInfoColumns * modeInfoRows];
        this.AllocateRecords(0, modeInfoColumns, modeInfoRows);
        this.Reset();
    }

    /// <summary>
    /// Gets the number of 4x4 columns of the frame.
    /// </summary>
    public int ModeInfoColumns { get; }

    /// <summary>
    /// Gets the number of 4x4 rows of the frame.
    /// </summary>
    public int ModeInfoRows { get; }

    /// <summary>
    /// Clears the grid and zeroes every record. Reference: enc_setup_mi().
    /// </summary>
    public void Reset()
    {
        this.grid.AsSpan().Fill(NoRecord);
        Array.Clear(this.partition);
        Array.Clear(this.lumaMode);
        Array.Clear(this.chromaMode);
        Array.Clear(this.isInter);
        Array.Clear(this.intraBlockCopy);
    }

    /// <summary>
    /// Takes the grid and the records a coded frame left.
    /// </summary>
    /// <param name="picture">The coded picture.</param>
    public void Capture(Av1PictureControlSet picture)
    {
        int shift = picture.Disallow4x4AllFrames ? 1 : 0;
        int stride = picture.ModeInfoStride;
        this.AllocateRecords(shift, stride >> shift, (this.ModeInfoRows + shift) >> shift);

        ReadOnlySpan<int> pictureGrid = picture.ModeInfoGrid.Span;
        for (int row = 0; row < this.ModeInfoRows; row++)
        {
            pictureGrid.Slice(row * stride, this.ModeInfoColumns).CopyTo(this.grid.AsSpan(row * this.ModeInfoColumns, this.ModeInfoColumns));
        }

        ReadOnlySpan<Av1MacroBlockModeInfo> allocation = picture.ModeInfoAllocation.Span;
        int count = Math.Min(allocation.Length, this.partition.Length);
        for (int index = 0; index < count; index++)
        {
            ref readonly Av1EncoderBlockModeInfo block = ref allocation[index].Block;
            this.partition[index] = (byte)block.PartitionType;
            this.lumaMode[index] = (byte)block.Mode;
            this.chromaMode[index] = (byte)block.UvMode;
            this.isInter[index] = block.ReferenceFrame > Av1ReferenceFrameType.Intra;
            this.intraBlockCopy[index] = block.UseIntraBlockCopy;
        }
    }

    /// <summary>
    /// Points the grid position of a model block to the block's own record. Reference: set_mi_offsets().
    /// </summary>
    /// <param name="modeInfoRow">The 4x4 row of the block.</param>
    /// <param name="modeInfoColumn">The 4x4 column of the block.</param>
    public void LendRecord(int modeInfoRow, int modeInfoColumn)
        => this.grid[(modeInfoRow * this.ModeInfoColumns) + modeInfoColumn] = this.GetRecordIndex(modeInfoRow, modeInfoColumn);

    /// <summary>
    /// Gets the partition field of a model block's own record. Reference: mbmi->partition.
    /// </summary>
    /// <param name="modeInfoRow">The 4x4 row of the block.</param>
    /// <param name="modeInfoColumn">The 4x4 column of the block.</param>
    /// <returns>The partition type.</returns>
    public Av1PartitionType GetPartition(int modeInfoRow, int modeInfoColumn)
        => (Av1PartitionType)this.partition[this.GetRecordIndex(modeInfoRow, modeInfoColumn)];

    /// <summary>
    /// Writes the luma mode field of a model block's own record. The compound search of the model writes NEW_NEWMV.
    /// </summary>
    /// <param name="modeInfoRow">The 4x4 row of the block.</param>
    /// <param name="modeInfoColumn">The 4x4 column of the block.</param>
    /// <param name="mode">The mode.</param>
    public void SetMode(int modeInfoRow, int modeInfoColumn, Av1PredictionMode mode)
        => this.lumaMode[this.GetRecordIndex(modeInfoRow, modeInfoColumn)] = (byte)mode;

    /// <summary>
    /// Writes whether the first reference field of a model block's own record names an inter reference.
    /// </summary>
    /// <param name="modeInfoRow">The 4x4 row of the block.</param>
    /// <param name="modeInfoColumn">The 4x4 column of the block.</param>
    /// <param name="inter">Whether the block holds an inter reference.</param>
    public void SetInter(int modeInfoRow, int modeInfoColumn, bool inter)
        => this.isInter[this.GetRecordIndex(modeInfoRow, modeInfoColumn)] = inter;

    /// <summary>
    /// Returns whether the record a grid position points to selects the smooth intra edge filter for a plane. A
    /// position without a record does not. Reference: is_smooth() on the neighbor that get_intra_edge_filter_type()
    /// reads.
    /// </summary>
    /// <param name="modeInfoRow">The 4x4 row of the neighbor position.</param>
    /// <param name="modeInfoColumn">The 4x4 column of the neighbor position.</param>
    /// <param name="plane">The plane, zero for luma.</param>
    /// <returns><see langword="true"/> when the neighbor uses a smooth mode.</returns>
    public bool IsSmooth(int modeInfoRow, int modeInfoColumn, int plane)
    {
        if ((uint)modeInfoRow >= (uint)this.ModeInfoRows || (uint)modeInfoColumn >= (uint)this.ModeInfoColumns)
        {
            return false;
        }

        int index = this.grid[(modeInfoRow * this.ModeInfoColumns) + modeInfoColumn];
        if (index == NoRecord)
        {
            return false;
        }

        if (plane == 0)
        {
            Av1PredictionMode mode = (Av1PredictionMode)this.lumaMode[index];
            return mode is Av1PredictionMode.Smooth or Av1PredictionMode.SmoothVertical or Av1PredictionMode.SmoothHorizontal;
        }

        // The chroma mode is not set for inter blocks, so an inter reference field excludes it, as does the intra
        // block copy flag the model never writes. Reference: is_inter_block() with is_intrabc_block().
        if (this.isInter[index] || this.intraBlockCopy[index])
        {
            return false;
        }

        Av1ChromaPredictionMode uvMode = (Av1ChromaPredictionMode)this.chromaMode[index];
        return uvMode is Av1ChromaPredictionMode.Smooth or Av1ChromaPredictionMode.SmoothVertical or Av1ChromaPredictionMode.SmoothHorizontal;
    }

    /// <summary>
    /// Returns the record index of a 4x4 position. Reference: get_alloc_mi_idx().
    /// </summary>
    private int GetRecordIndex(int modeInfoRow, int modeInfoColumn)
        => ((modeInfoRow >> this.recordShift) * this.recordStride) + (modeInfoColumn >> this.recordShift);

    /// <summary>
    /// Sizes the record storage for an allocation granularity, keeping it when it already fits.
    /// </summary>
    private void AllocateRecords(int shift, int stride, int rows)
    {
        this.recordShift = shift;
        this.recordStride = stride;
        int count = stride * rows;
        if (this.partition.Length != count)
        {
            this.partition = new byte[count];
            this.lumaMode = new byte[count];
            this.chromaMode = new byte[count];
            this.isInter = new bool[count];
            this.intraBlockCopy = new bool[count];
        }
    }
}
