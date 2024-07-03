// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

internal class Av1ParseAboveNeighbor4x4Context
{
    /* Buffer holding the transform sizes of the previous 4x4 block row. */
    private readonly int[] aboveTransformWidth;

    /* Buffer holding the partition context of the previous 4x4 block row. */
    private int[] abovePartitionWidth;

    /* Buffer holding the sign of the DC coefficients and the
       cumulative sum of the coefficient levels of the above 4x4
       blocks corresponding to the current super block row. */
    private int[][] aboveContext = new int[Av1Constants.MaxPlanes][];

    /* Buffer holding the seg_id_predicted of the previous 4x4 block row. */
    private int[] aboveSegmentIdPredictionContext;

    /* Value of base colors for Y, U, and V */
    private int[][] abovePaletteColors = new int[Av1Constants.MaxPlanes][];

    private int[] aboveCompGroupIndex;

    public Av1ParseAboveNeighbor4x4Context(int planesCount, int modeInfoColumnCount)
    {
        int wide64x64Count = Av1BlockSize.Block64x64.Get4x4WideCount();
        this.aboveTransformWidth = new int[modeInfoColumnCount];
        this.abovePartitionWidth = new int[modeInfoColumnCount];
        for (int i = 0; i < planesCount; i++)
        {
            this.aboveContext[i] = new int[modeInfoColumnCount];
            this.abovePaletteColors[i] = new int[wide64x64Count * Av1Constants.PaletteMaxSize];
        }

        this.aboveSegmentIdPredictionContext = new int[modeInfoColumnCount];
        this.aboveCompGroupIndex = new int[modeInfoColumnCount];
    }

    public int[] AbovePartitionWidth => this.abovePartitionWidth;

    public int[] AboveTransformWidth => this.aboveTransformWidth;

    public void Clear(ObuSequenceHeader sequenceHeader)
    {
        int planeCount = sequenceHeader.ColorConfig.ChannelCount;
        Array.Fill(this.aboveTransformWidth, Av1TransformSize.Size64x64.GetWidth());
        Array.Fill(this.abovePartitionWidth, 0);
        for (int i = 0; i < planeCount; i++)
        {
            Array.Fill(this.aboveContext[i], 0);
            Array.Fill(this.abovePaletteColors[i], 0);
        }

        Array.Fill(this.aboveSegmentIdPredictionContext, 0);
        Array.Fill(this.aboveCompGroupIndex, 0);
    }

    public void UpdatePartition(Point modeInfoLocation, Av1TileInfo tileLoc, Av1BlockSize subSize, Av1BlockSize blockSize)
    {
        int startIndex = modeInfoLocation.X - tileLoc.ModeInfoColumnStart;
        int bw = blockSize.Get4x4WideCount();
        int value = Av1PartitionContext.GetAboveContext(subSize);
        for (int i = 0; i < bw; i++)
        {
            this.abovePartitionWidth[startIndex + i] = value;
        }
    }

    public void UpdateTransformation(Point modeInfoLocation, Av1TileInfo tileInfo, Av1TransformSize transformSize, Av1BlockSize blockSize, bool skip)
    {
        int startIndex = modeInfoLocation.X - tileInfo.ModeInfoColumnStart;
        int transformWidth = transformSize.GetWidth();
        int n4w = blockSize.Get4x4WideCount();
        if (skip)
        {
            transformWidth = n4w * (1 << Av1Constants.ModeInfoSizeLog2);
        }

        for (int i = 0; i < n4w; i++)
        {
            this.aboveTransformWidth[startIndex + i] = transformWidth;
        }
    }

    internal void ClearContext(int plane, int offset, int length)
        => Array.Fill(this.aboveContext[plane], 0, offset, length);
}
