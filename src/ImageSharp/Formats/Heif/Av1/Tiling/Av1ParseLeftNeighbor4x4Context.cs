// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores entropy, partition, and transform contexts for 4-by-4 blocks left of the current block.
/// </summary>
internal class Av1ParseLeftNeighbor4x4Context
{
    /// <summary>
    /// Stores DC-sign and cumulative coefficient-level contexts for each plane left of the current block.
    /// </summary>
    private readonly int[][] leftContext = new int[Av1Constants.MaxPlanes][];

    /// <summary>
    /// Stores segmentation-prediction contexts for the current superblock row.
    /// </summary>
    private readonly int[] leftSegmentIdPredictionContext;

    /// <summary>
    /// Stores compound-reference group contexts for the current superblock row.
    /// </summary>
    private readonly int[] leftCompGroupIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1ParseLeftNeighbor4x4Context"/> class.
    /// </summary>
    /// <param name="planesCount">The number of color planes.</param>
    /// <param name="superblockModeInfoSize">The superblock height in 4x4 mode-information rows.</param>
    public Av1ParseLeftNeighbor4x4Context(int planesCount, int superblockModeInfoSize)
    {
        this.LeftTransformHeight = new int[superblockModeInfoSize];
        this.LeftPartitionHeight = new int[superblockModeInfoSize];
        for (int i = 0; i < planesCount; i++)
        {
            this.leftContext[i] = new int[superblockModeInfoSize];
        }

        this.leftSegmentIdPredictionContext = new int[superblockModeInfoSize];
        this.leftCompGroupIndex = new int[superblockModeInfoSize];
    }

    /// <summary>
    /// Gets a buffer holding the partition context of the left 4x4 blocks corresponding
    /// to the current super block row.
    /// </summary>
    public int[] LeftPartitionHeight { get; }

    /// <summary>
    /// Gets a buffer holding the transform sizes of the left 4x4 blocks corresponding
    /// to the current super block row.
    /// </summary>
    public int[] LeftTransformHeight { get; }

    /// <summary>
    /// Resets all left-neighbor state for a new superblock row.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header describing the superblock size and color planes.</param>
    public void Clear(ObuSequenceHeader sequenceHeader)
    {
        int blockCount = sequenceHeader.SuperblockModeInfoSize;
        int planeCount = sequenceHeader.ColorConfig.PlaneCount;
        int neighbor4x4Count = sequenceHeader.SuperblockModeInfoSize;
        Array.Fill(this.LeftTransformHeight, Av1TransformSize.Size64x64.GetHeight(), 0, blockCount);
        Array.Fill(this.LeftPartitionHeight, 0, 0, blockCount);
        for (int i = 0; i < planeCount; i++)
        {
            Array.Fill(this.leftContext[i], 0, 0, blockCount);
        }

        Array.Fill(this.leftSegmentIdPredictionContext, 0, 0, blockCount);
        Array.Fill(this.leftCompGroupIndex, 0, 0, blockCount);
    }

    /// <summary>
    /// Updates the left partition context for every 4x4 row covered by a block.
    /// </summary>
    /// <param name="modeInfoLocation">The block origin in frame mode-information units.</param>
    /// <param name="superblockInfo">The active superblock location.</param>
    /// <param name="subSize">The size produced by the decoded partition.</param>
    /// <param name="blockSize">The parent block size.</param>
    public void UpdatePartition(Point modeInfoLocation, Av1SuperblockInfo superblockInfo, Av1BlockSize subSize, Av1BlockSize blockSize)
    {
        // The left context is reused for each superblock row, so address it relative to the superblock origin.
        int startIndex = (modeInfoLocation.Y - superblockInfo.ModeInfoPosition.Y) & Av1PartitionContext.Mask;
        int bh = blockSize.Get4x4HighCount();
        int value = Av1PartitionContext.GetLeftContext(subSize);
        DebugGuard.MustBeLessThanOrEqualTo(startIndex, this.LeftPartitionHeight.Length - bh, nameof(startIndex));
        Array.Fill(this.LeftPartitionHeight, value, startIndex, bh);
    }

    /// <summary>
    /// Updates the left transform-size context for every 4x4 row covered by a block.
    /// </summary>
    /// <param name="modeInfoLocation">The block origin in frame mode-information units.</param>
    /// <param name="superblockInfo">The active superblock location.</param>
    /// <param name="transformSize">The selected transform size.</param>
    /// <param name="blockSize">The decoded block size.</param>
    /// <param name="skip">A value indicating whether the block omits residual coefficients.</param>
    public void UpdateTransformation(Point modeInfoLocation, Av1SuperblockInfo superblockInfo, Av1TransformSize transformSize, Av1BlockSize blockSize, bool skip)
    {
        int startIndex = modeInfoLocation.Y - superblockInfo.ModeInfoPosition.Y;
        int transformHeight = transformSize.GetHeight();
        int n4h = blockSize.Get4x4HighCount();
        if (skip)
        {
            // Skipped blocks expose the full block height as their effective transform extent.
            transformHeight = n4h << Av1Constants.ModeInfoSizeLog2;
        }

        DebugGuard.MustBeLessThanOrEqualTo(startIndex, this.LeftTransformHeight.Length - n4h, nameof(startIndex));
        Array.Fill(this.LeftTransformHeight, transformHeight, startIndex, n4h);
    }

    /// <summary>
    /// Clears a range of left coefficient contexts for one plane.
    /// </summary>
    /// <param name="plane">The zero-based plane index.</param>
    /// <param name="offset">The first context index to clear.</param>
    /// <param name="length">The number of context entries to clear.</param>
    internal void ClearContext(int plane, int offset, int length)
        => Array.Fill(this.leftContext[plane], 0, offset, length);

    /// <summary>
    /// Gets the coefficient context column for the specified plane.
    /// </summary>
    /// <param name="plane">The zero-based plane index.</param>
    /// <returns>The coefficient contexts for the plane.</returns>
    internal int[] GetContext(int plane) => this.leftContext[plane];
}
