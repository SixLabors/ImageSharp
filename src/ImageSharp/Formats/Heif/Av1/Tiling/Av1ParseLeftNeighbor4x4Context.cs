// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

internal class Av1ParseLeftNeighbor4x4Context
{
    /* Buffer holding the transform sizes of the left 4x4 blocks corresponding
         to the current super block row. */
    private readonly int[] leftTransformHeight;

    /* Buffer holding the partition context of the left 4x4 blocks corresponding
     to the current super block row. */
    private int[] leftPartitionHeight;

    /* Buffer holding the sign of the DC coefficients and the
       cumulative sum of the coefficient levels of the left 4x4
       blocks corresponding to the current super block row. */
    private int[][] leftContext = new int[Av1Constants.MaxPlanes][];

    /* Buffer holding the seg_id_predicted of the previous 4x4 block row. */
    private int[] leftSegmentIdPredictionContext;

    /* Value of base colors for Y, U, and V */
    private int[][] leftPaletteColors = new int[Av1Constants.MaxPlanes][];

    private int[] leftCompGroupIndex;

    public Av1ParseLeftNeighbor4x4Context(int planesCount, int superblockModeInfoSize)
    {
        this.leftTransformHeight = new int[superblockModeInfoSize];
        this.leftPartitionHeight = new int[superblockModeInfoSize];
        for (int i = 0; i < planesCount; i++)
        {
            this.leftContext[i] = new int[superblockModeInfoSize];
            this.leftPaletteColors[i] = new int[superblockModeInfoSize * Av1Constants.PaletteMaxSize];
        }

        this.leftSegmentIdPredictionContext = new int[superblockModeInfoSize];
        this.leftCompGroupIndex = new int[superblockModeInfoSize];
    }

    public int[] LeftPartitionHeight => this.leftPartitionHeight;

    public int[] LeftTransformHeight => this.leftTransformHeight;

    public void Clear(ObuSequenceHeader sequenceHeader)
    {
        int planeCount = sequenceHeader.ColorConfig.ChannelCount;
        Array.Fill(this.leftTransformHeight, Av1TransformSize.Size64x64.GetHeight());
        Array.Fill(this.leftPartitionHeight, 0);
        for (int i = 0; i < planeCount; i++)
        {
            Array.Fill(this.leftContext[i], 0);
            Array.Fill(this.leftPaletteColors[i], 0);
        }

        Array.Fill(this.leftSegmentIdPredictionContext, 0);
        Array.Fill(this.leftCompGroupIndex, 0);
    }

    public void UpdatePartition(Point modeInfoLocation, Av1SuperblockInfo superblockInfo, Av1BlockSize subSize, Av1BlockSize blockSize)
    {
        int startIndex = (modeInfoLocation.Y - superblockInfo.Position.Y) & Av1PartitionContext.Mask;
        int bh = blockSize.Get4x4HighCount();
        int value = Av1PartitionContext.GetLeftContext(subSize);
        for (int i = 0; i < bh; i++)
        {
            this.leftPartitionHeight[startIndex + i] = value;
        }
    }

    public void UpdateTransformation(Point modeInfoLocation, Av1SuperblockInfo superblockInfo, Av1TransformSize transformSize, Av1BlockSize blockSize, bool skip)
    {
        int startIndex = modeInfoLocation.Y - superblockInfo.Position.Y;
        int transformHeight = transformSize.GetHeight();
        int n4h = blockSize.Get4x4HighCount();
        if (skip)
        {
            transformHeight = n4h * (1 << Av1Constants.ModeInfoSizeLog2);
        }

        for (int i = 0; i < n4h; i++)
        {
            this.leftTransformHeight[startIndex + i] = transformHeight;
        }
    }

    internal void ClearContext(int plane, int offset, int length)
        => Array.Fill(this.leftContext[plane], 0, offset, length);

    internal int[] GetContext(int plane) => this.leftContext[plane];
}
