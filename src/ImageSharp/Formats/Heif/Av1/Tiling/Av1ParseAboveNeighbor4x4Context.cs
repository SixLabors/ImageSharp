// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores entropy, partition, and transform contexts for 4-by-4 blocks above the current block.
/// </summary>
internal class Av1ParseAboveNeighbor4x4Context
{
    /// <summary>
    /// Stores DC-sign and cumulative coefficient-level contexts for each plane above the current block.
    /// </summary>
    private readonly int[][] aboveContext = new int[Av1Constants.MaxPlanes][];

    /// <summary>
    /// Stores segmentation-prediction contexts from the preceding 4x4 row.
    /// </summary>
    private readonly int[] aboveSegmentIdPredictionContext;

    /// <summary>
    /// Stores compound-reference group contexts from the preceding 4x4 row.
    /// </summary>
    private readonly int[] aboveCompGroupIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1ParseAboveNeighbor4x4Context"/> class.
    /// </summary>
    /// <param name="planesCount">The number of color planes.</param>
    /// <param name="modeInfoColumnCount">The frame width in 4x4 mode-information columns.</param>
    public Av1ParseAboveNeighbor4x4Context(int planesCount, int modeInfoColumnCount)
    {
        this.AboveTransformWidth = new int[modeInfoColumnCount];
        this.AbovePartitionWidth = new int[modeInfoColumnCount];
        for (int i = 0; i < planesCount; i++)
        {
            this.aboveContext[i] = new int[modeInfoColumnCount];
        }

        this.aboveSegmentIdPredictionContext = new int[modeInfoColumnCount];
        this.aboveCompGroupIndex = new int[modeInfoColumnCount];
    }

    /// <summary>
    /// Gets a buffer holding the partition context of the previous 4x4 block row.
    /// </summary>
    public int[] AbovePartitionWidth { get; }

    /// <summary>
    /// Gets a buffer holding the transform sizes of the previous 4x4 block row.
    /// </summary>
    public int[] AboveTransformWidth { get; }

    /// <summary>
    /// Gets the coefficient context row for the specified plane.
    /// </summary>
    /// <param name="plane">The zero-based plane index.</param>
    /// <returns>The coefficient contexts for the plane.</returns>
    public int[] GetContext(int plane) => this.aboveContext[plane];

    /// <summary>
    /// Resets above-neighbor state for the active tile-column range.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header describing the color planes.</param>
    /// <param name="modeInfoColumnStart">The first mode-information column in the tile.</param>
    /// <param name="modeInfoColumnEnd">The exclusive end mode-information column in the tile.</param>
    public void Clear(ObuSequenceHeader sequenceHeader, int modeInfoColumnStart, int modeInfoColumnEnd)
    {
        int planeCount = sequenceHeader.ColorConfig.PlaneCount;
        int width = modeInfoColumnEnd - modeInfoColumnStart;
        Array.Fill(this.AboveTransformWidth, Av1TransformSize.Size64x64.GetWidth(), 0, width);
        Array.Fill(this.AbovePartitionWidth, 0, 0, width);
        for (int i = 0; i < planeCount; i++)
        {
            Array.Fill(this.aboveContext[i], 0, 0, width);
        }

        Array.Fill(this.aboveSegmentIdPredictionContext, 0, 0, width);
        Array.Fill(this.aboveCompGroupIndex, 0, 0, width);
    }

    /// <summary>
    /// Updates the above partition context for every 4x4 column covered by a block.
    /// </summary>
    /// <param name="modeInfoLocation">The block origin in frame mode-information units.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="subSize">The size produced by the decoded partition.</param>
    /// <param name="blockSize">The parent block size.</param>
    public void UpdatePartition(Point modeInfoLocation, Av1TileInfo tileInfo, Av1BlockSize subSize, Av1BlockSize blockSize)
    {
        // Above contexts are tile-local even though block positions are frame-relative.
        int startIndex = modeInfoLocation.X - tileInfo.ModeInfoColumnStart;
        int bw = blockSize.Get4x4WideCount();
        int value = Av1PartitionContext.GetAboveContext(subSize);

        DebugGuard.MustBeLessThanOrEqualTo(startIndex, this.AboveTransformWidth.Length - bw, nameof(startIndex));
        Array.Fill(this.AbovePartitionWidth, value, startIndex, bw);
    }

    /// <summary>
    /// Updates the above transform-size context for every 4x4 column covered by a block.
    /// </summary>
    /// <param name="modeInfoLocation">The block origin in frame mode-information units.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="transformSize">The selected transform size.</param>
    /// <param name="blockSize">The decoded block size.</param>
    /// <param name="skip">A value indicating whether the block omits residual coefficients.</param>
    public void UpdateTransformation(Point modeInfoLocation, Av1TileInfo tileInfo, Av1TransformSize transformSize, Av1BlockSize blockSize, bool skip)
    {
        int startIndex = modeInfoLocation.X - tileInfo.ModeInfoColumnStart;
        int transformWidth = transformSize.GetWidth();
        int n4w = blockSize.Get4x4WideCount();
        if (skip)
        {
            // Skipped blocks expose the full block width as their effective transform extent.
            transformWidth = n4w << Av1Constants.ModeInfoSizeLog2;
        }

        DebugGuard.MustBeLessThanOrEqualTo(startIndex, this.AboveTransformWidth.Length - n4w, nameof(startIndex));
        Array.Fill(this.AboveTransformWidth, transformWidth, startIndex, n4w);
    }

    /// <summary>
    /// Clears a range of above coefficient contexts for one plane.
    /// </summary>
    /// <param name="plane">The zero-based plane index.</param>
    /// <param name="offset">The first context index to clear.</param>
    /// <param name="length">The number of context entries to clear.</param>
    internal void ClearContext(int plane, int offset, int length)
        => Array.Fill(this.aboveContext[plane], 0, offset, length);
}
