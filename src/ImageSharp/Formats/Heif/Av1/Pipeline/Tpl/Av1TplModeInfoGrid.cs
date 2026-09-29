// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// The mode-information fields that the model reads from the encoder's block records at the top-left position of each
/// 16x16 block. The model borrows the frame's mode-information allocation, so it reads what the previous coded frame
/// left there (the last value any final or trial decision wrote), and what earlier model blocks of the same run wrote.
/// It reads the partition of its own block, which selects the top-right and bottom-left availability tables, and the
/// luma and chroma modes of the neighbors above and to the left, which select the intra edge filter.
/// Reference: the mi_alloc entries that set_mode_info_offsets() lends to mode_estimation().
/// </summary>
internal sealed class Av1TplModeInfoGrid
{
    /// <summary>
    /// The partition field of each record. Reference: mbmi->partition.
    /// </summary>
    private readonly byte[] partition;

    /// <summary>
    /// The luma mode field of each record. Reference: mbmi->mode.
    /// </summary>
    private readonly byte[] lumaMode;

    /// <summary>
    /// The chroma mode field of each record. Reference: mbmi->uv_mode.
    /// </summary>
    private readonly byte[] chromaMode;

    /// <summary>
    /// Whether the first reference field of each record names an inter reference. Reference: ref_frame[0].
    /// </summary>
    private readonly bool[] isInter;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TplModeInfoGrid"/> class with zeroed records.
    /// </summary>
    /// <param name="columns">The number of 16x16 block columns.</param>
    /// <param name="rows">The number of 16x16 block rows.</param>
    public Av1TplModeInfoGrid(int columns, int rows)
    {
        this.Columns = columns;
        this.Rows = rows;
        int count = columns * rows;
        this.partition = new byte[count];
        this.lumaMode = new byte[count];
        this.chromaMode = new byte[count];
        this.isInter = new bool[count];
    }

    /// <summary>
    /// Gets the number of 16x16 block columns.
    /// </summary>
    public int Columns { get; }

    /// <summary>
    /// Gets the number of 16x16 block rows.
    /// </summary>
    public int Rows { get; }

    /// <summary>
    /// Zeroes every record, which is the state of a freshly allocated encoder before its first frame.
    /// Reference: enc_setup_mi().
    /// </summary>
    public void Reset()
    {
        Array.Clear(this.partition);
        Array.Clear(this.lumaMode);
        Array.Clear(this.chromaMode);
        Array.Clear(this.isInter);
    }

    /// <summary>
    /// Records the fields that the previous coded frame left at a 16x16-aligned position.
    /// </summary>
    /// <param name="column">The 16x16 block column.</param>
    /// <param name="row">The 16x16 block row.</param>
    /// <param name="partitionType">The partition field. Reference: mbmi->partition.</param>
    /// <param name="mode">The luma mode field. Reference: mbmi->mode.</param>
    /// <param name="uvMode">The chroma mode field. Reference: mbmi->uv_mode.</param>
    /// <param name="inter">Whether the first reference field names an inter reference. Reference: ref_frame[0].</param>
    public void Set(int column, int row, Av1PartitionType partitionType, Av1PredictionMode mode, Av1ChromaPredictionMode uvMode, bool inter)
    {
        int index = (row * this.Columns) + column;
        this.partition[index] = (byte)partitionType;
        this.lumaMode[index] = (byte)mode;
        this.chromaMode[index] = (byte)uvMode;
        this.isInter[index] = inter;
    }

    /// <summary>
    /// Gets the partition field of a block.
    /// </summary>
    /// <param name="column">The 16x16 block column.</param>
    /// <param name="row">The 16x16 block row.</param>
    /// <returns>The partition type.</returns>
    public Av1PartitionType GetPartition(int column, int row) => (Av1PartitionType)this.partition[(row * this.Columns) + column];

    /// <summary>
    /// Writes the luma mode field of a block. The compound search of the model writes NEW_NEWMV.
    /// </summary>
    /// <param name="column">The 16x16 block column.</param>
    /// <param name="row">The 16x16 block row.</param>
    /// <param name="mode">The mode.</param>
    public void SetMode(int column, int row, Av1PredictionMode mode) => this.lumaMode[(row * this.Columns) + column] = (byte)mode;

    /// <summary>
    /// Writes whether the first reference field of a block names an inter reference.
    /// </summary>
    /// <param name="column">The 16x16 block column.</param>
    /// <param name="row">The 16x16 block row.</param>
    /// <param name="inter">Whether the block holds an inter reference.</param>
    public void SetInter(int column, int row, bool inter) => this.isInter[(row * this.Columns) + column] = inter;

    /// <summary>
    /// Returns whether a neighbor selects the smooth intra edge filter for a plane. Reference: is_smooth().
    /// </summary>
    /// <param name="column">The 16x16 block column of the neighbor.</param>
    /// <param name="row">The 16x16 block row of the neighbor.</param>
    /// <param name="plane">The plane, zero for luma.</param>
    /// <returns><see langword="true"/> when the neighbor uses a smooth mode.</returns>
    public bool IsSmooth(int column, int row, int plane)
    {
        int index = (row * this.Columns) + column;
        if (plane == 0)
        {
            Av1PredictionMode mode = (Av1PredictionMode)this.lumaMode[index];
            return mode is Av1PredictionMode.Smooth or Av1PredictionMode.SmoothVertical or Av1PredictionMode.SmoothHorizontal;
        }

        // The chroma mode is not set for inter blocks, so an inter reference field excludes it.
        if (this.isInter[index])
        {
            return false;
        }

        Av1ChromaPredictionMode uvMode = (Av1ChromaPredictionMode)this.chromaMode[index];
        return uvMode is Av1ChromaPredictionMode.Smooth or Av1ChromaPredictionMode.SmoothVertical or Av1ChromaPredictionMode.SmoothHorizontal;
    }
}
