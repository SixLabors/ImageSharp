// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1ParseAboveNeighbor4x4Context
{
    /* Buffer holding the sign of the DC coefficients and the
       cumulative sum of the coefficient levels of the above 4x4
       blocks corresponding to the current super block row. */
    private readonly int[][] aboveContext = new int[Av1Constants.MaxPlanes][];

    /* Buffer holding the seg_id_predicted of the previous 4x4 block row. */
    private readonly int[] aboveSegmentIdPredictionContext;

    /* Value of base colors for Y, U, and V */
    private readonly int[][] abovePaletteColors = new int[Av1Constants.MaxPlanes][];

    private readonly int[] aboveCompGroupIndex;

    public Av1ParseAboveNeighbor4x4Context(int planesCount, int modeInfoColumnCount)
    {
        int wide64x64Count = Av1BlockSize.Block64x64.Get4x4WideCount();
        this.AboveTransformWidth = new int[modeInfoColumnCount];
        this.AbovePartitionWidth = new int[modeInfoColumnCount];
        for (int i = 0; i < planesCount; i++)
        {
            this.aboveContext[i] = new int[modeInfoColumnCount];
            this.abovePaletteColors[i] = new int[wide64x64Count * Av1Constants.PaletteMaxSize];
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

    public int[] GetContext(int plane) => this.aboveContext[plane];

    public void Clear(ObuSequenceHeader sequenceHeader, int modeInfoColumnStart, int modeInfoColumnEnd)
    {
        int planeCount = sequenceHeader.ColorConfig.PlaneCount;
        int width = modeInfoColumnEnd - modeInfoColumnStart;
        Array.Fill(this.AboveTransformWidth, Av1TransformSize.Size64x64.GetWidth(), 0, width);
        Array.Fill(this.AbovePartitionWidth, 0, 0, width);
        for (int i = 0; i < planeCount; i++)
        {
            Array.Fill(this.aboveContext[i], 0, 0, width);
            Array.Fill(this.abovePaletteColors[i], 0, 0, width);
        }

        Array.Fill(this.aboveSegmentIdPredictionContext, 0, 0, width);
        Array.Fill(this.aboveCompGroupIndex, 0, 0, width);
    }

    public void UpdatePartition(Point modeInfoLocation, Av1TileInfo tileInfo, Av1BlockSize subSize, Av1BlockSize blockSize)
    {
        int startIndex = modeInfoLocation.X - tileInfo.ModeInfoColumnStart;
        int bw = blockSize.Get4x4WideCount();
        int value = Av1PartitionContext.GetAboveContext(subSize);

        DebugGuard.MustBeLessThanOrEqualTo(startIndex, this.AboveTransformWidth.Length - bw, nameof(startIndex));
        Array.Fill(this.AbovePartitionWidth, value, startIndex, bw);
    }

    public void UpdateTransformation(Point modeInfoLocation, Av1TileInfo tileInfo, Av1TransformSize transformSize, Av1BlockSize blockSize, bool skip)
    {
        int startIndex = modeInfoLocation.X - tileInfo.ModeInfoColumnStart;
        int transformWidth = transformSize.GetWidth();
        int n4w = blockSize.Get4x4WideCount();
        if (skip)
        {
            transformWidth = n4w << Av1Constants.ModeInfoSizeLog2;
        }

        DebugGuard.MustBeLessThanOrEqualTo(startIndex, this.AboveTransformWidth.Length - n4w, nameof(startIndex));
        Array.Fill(this.AboveTransformWidth, transformWidth, startIndex, n4w);
    }

    internal void ClearContext(int plane, int offset, int length)
        => Array.Fill(this.aboveContext[plane], 0, offset, length);
}
