// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1ParseLeftNeighbor4x4Context
{
    /* Buffer holding the sign of the DC coefficients and the
       cumulative sum of the coefficient levels of the left 4x4
       blocks corresponding to the current super block row. */
    private readonly int[][] leftContext = new int[Av1Constants.MaxPlanes][];

    /* Buffer holding the seg_id_predicted of the previous 4x4 block row. */
    private readonly int[] leftSegmentIdPredictionContext;

    /* Value of base colors for Y, U, and V */
    private readonly int[][] leftPaletteColors = new int[Av1Constants.MaxPlanes][];

    private readonly int[] leftCompGroupIndex;

    public Av1ParseLeftNeighbor4x4Context(int planesCount, int superblockModeInfoSize)
    {
        this.LeftTransformHeight = new int[superblockModeInfoSize];
        this.LeftPartitionHeight = new int[superblockModeInfoSize];
        for (int i = 0; i < planesCount; i++)
        {
            this.leftContext[i] = new int[superblockModeInfoSize];
            this.leftPaletteColors[i] = new int[superblockModeInfoSize * Av1Constants.PaletteMaxSize];
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
            Array.Fill(this.leftPaletteColors[i], 0, 0, blockCount);
        }

        Array.Fill(this.leftSegmentIdPredictionContext, 0, 0, blockCount);
        Array.Fill(this.leftCompGroupIndex, 0, 0, blockCount);
    }

    public void UpdatePartition(Point modeInfoLocation, Av1SuperblockInfo superblockInfo, Av1BlockSize subSize, Av1BlockSize blockSize)
    {
        int startIndex = (modeInfoLocation.Y - superblockInfo.ModeInfoPosition.Y) & Av1PartitionContext.Mask;
        int bh = blockSize.Get4x4HighCount();
        int value = Av1PartitionContext.GetLeftContext(subSize);
        DebugGuard.MustBeLessThanOrEqualTo(startIndex, this.LeftTransformHeight.Length - bh, nameof(startIndex));
        Array.Fill(this.LeftPartitionHeight, value, startIndex, bh);
    }

    public void UpdateTransformation(Point modeInfoLocation, Av1SuperblockInfo superblockInfo, Av1TransformSize transformSize, Av1BlockSize blockSize, bool skip)
    {
        int startIndex = modeInfoLocation.Y - superblockInfo.ModeInfoPosition.Y;
        int transformHeight = transformSize.GetHeight();
        int n4h = blockSize.Get4x4HighCount();
        if (skip)
        {
            transformHeight = n4h << Av1Constants.ModeInfoSizeLog2;
        }

        DebugGuard.MustBeLessThanOrEqualTo(startIndex, this.LeftTransformHeight.Length - n4h, nameof(startIndex));
        Array.Fill(this.LeftTransformHeight, transformHeight, startIndex, n4h);
    }

    internal void ClearContext(int plane, int offset, int length)
        => Array.Fill(this.leftContext[plane], 0, offset, length);

    internal int[] GetContext(int plane) => this.leftContext[plane];
}
