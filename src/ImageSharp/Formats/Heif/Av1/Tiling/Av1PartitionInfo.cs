// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Describes a decoded AV1 partition's block geometry, neighbors, and frame-boundary availability.
/// </summary>
internal ref struct Av1PartitionInfo
{
    /// <summary>
    /// The luma block width in samples.
    /// </summary>
    private int lumaWidthInPixels;

    /// <summary>
    /// The shared chroma block width in samples.
    /// </summary>
    private int chromaWidthInPixels;

    /// <summary>
    /// The luma block height in samples.
    /// </summary>
    private int lumaHeightInPixels;

    /// <summary>
    /// The shared chroma block height in samples.
    /// </summary>
    private int chromaHeightInPixels;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1PartitionInfo"/> structure.
    /// </summary>
    /// <param name="modeInfo">The decoded mode information for the partition block.</param>
    /// <param name="superblockInfo">The containing superblock.</param>
    /// <param name="isChroma">A value indicating whether the partition has chroma samples.</param>
    /// <param name="partitionType">The partition type that produced the block.</param>
    public Av1PartitionInfo(Av1BlockModeInfo modeInfo, Av1SuperblockInfo superblockInfo, bool isChroma, Av1PartitionType partitionType)
    {
        this.ModeInfo = modeInfo;
        this.SuperblockInfo = superblockInfo;
        this.IsChroma = isChroma;
        this.Type = partitionType;
    }

    /// <summary>
    /// Gets the decoded block mode information.
    /// </summary>
    public Av1BlockModeInfo ModeInfo { get; }

    /// <summary>
    /// Gets the <see cref="Av1SuperblockInfo"/> this partition resides inside.
    /// </summary>
    public Av1SuperblockInfo SuperblockInfo { get; }

    /// <summary>
    /// Gets a value indicating whether the partition has chroma samples at its current luma position.
    /// </summary>
    public bool IsChroma { get; }

    /// <summary>
    /// Gets the partition type that produced the block.
    /// </summary>
    public Av1PartitionType Type { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the information from the block above can be used on the luma plane.
    /// </summary>
    public bool AvailableAbove { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the information from the block left can be used on the luma plane.
    /// </summary>
    public bool AvailableLeft { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the information from the block above can be used on the chroma plane.
    /// </summary>
    public bool AvailableAboveForChroma { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the information from the block left can be used on the chroma plane.
    /// </summary>
    public bool AvailableLeftForChroma { get; set; }

    /// <summary>
    /// Gets or sets the horizontal location of the block in units of 4x4 luma samples.
    /// </summary>
    public int ColumnIndex { get; set; }

    /// <summary>
    /// Gets or sets the vertical location of the block in units of 4x4 luma samples.
    /// </summary>
    public int RowIndex { get; set; }

    /// <summary>
    /// Gets or sets the mode information covering the immediately above luma neighbor.
    /// </summary>
    public Av1BlockModeInfo? AboveModeInfo { get; set; }

    /// <summary>
    /// Gets or sets the mode information covering the immediately left luma neighbor.
    /// </summary>
    public Av1BlockModeInfo? LeftModeInfo { get; set; }

    /// <summary>
    /// Gets or sets the mode information covering the above chroma neighbor.
    /// </summary>
    public Av1BlockModeInfo? AboveModeInfoForChroma { get; set; }

    /// <summary>
    /// Gets or sets the mode information covering the left chroma neighbor.
    /// </summary>
    public Av1BlockModeInfo? LeftModeInfoForChroma { get; set; }

    /// <summary>
    /// Gets the reference-frame types selected for the block.
    /// </summary>
    public Span<Av1ReferenceFrameType> ReferenceFrames => this.ModeInfo.ReferenceFrames;

    /// <summary>
    /// Gets the signed distance from the block to the left frame edge in one-eighth-sample units.
    /// </summary>
    public int ModeBlockToLeftEdge { get; private set; }

    /// <summary>
    /// Gets the signed distance from the block to the right frame edge in one-eighth-sample units.
    /// </summary>
    public int ModeBlockToRightEdge { get; private set; }

    /// <summary>
    /// Gets the signed distance from the block to the top frame edge in one-eighth-sample units.
    /// </summary>
    public int ModeBlockToTopEdge { get; private set; }

    /// <summary>
    /// Gets the signed distance from the block to the bottom frame edge in one-eighth-sample units.
    /// </summary>
    public int ModeBlockToBottomEdge { get; private set; }

    /// <summary>
    /// Gets or sets the neighboring luma samples used by chroma-from-luma prediction.
    /// </summary>
    public Av1ChromaFromLumaContext? ChromaFromLumaContext { get; set; }

    /// <summary>
    /// Gets the block width in samples for a color plane.
    /// </summary>
    /// <param name="plane">The luma, blue-difference, or red-difference plane.</param>
    /// <returns>The block width in samples for the requested plane.</returns>
    public int GetWidthInPixels(Av1Plane plane) => plane == Av1Plane.Y ? this.lumaWidthInPixels : this.chromaWidthInPixels;

    /// <summary>
    /// Gets the block height in samples for a color plane.
    /// </summary>
    /// <param name="plane">The luma, blue-difference, or red-difference plane.</param>
    /// <returns>The block height in samples for the requested plane.</returns>
    public int GetHeightInPixels(Av1Plane plane) => plane == Av1Plane.Y ? this.lumaHeightInPixels : this.chromaHeightInPixels;

    /// <summary>
    /// Computes tile-neighbor availability, frame-edge distances, and per-plane block dimensions.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header describing color subsampling.</param>
    /// <param name="frameHeader">The frame header describing coded dimensions.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    public void ComputeBoundaryOffsets(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader, Av1TileInfo tileInfo)
    {
        Av1BlockSize blockSize = this.ModeInfo.BlockSize;
        int bw4 = blockSize.Get4x4WideCount();
        int bh4 = blockSize.Get4x4HighCount();
        int subX = sequenceHeader.ColorConfig.SubSamplingX ? 1 : 0;
        int subY = sequenceHeader.ColorConfig.SubSamplingY ? 1 : 0;
        this.AvailableAbove = this.RowIndex > tileInfo.ModeInfoRowStart;
        this.AvailableLeft = this.ColumnIndex > tileInfo.ModeInfoColumnStart;
        this.AvailableAboveForChroma = this.AvailableAbove;
        this.AvailableLeftForChroma = this.AvailableLeft;

        int shift = Av1Constants.ModeInfoSizeLog2 + 3;
        this.ModeBlockToLeftEdge = -this.ColumnIndex << shift;
        this.ModeBlockToRightEdge = (frameHeader.ModeInfoColumnCount - bw4 - this.ColumnIndex) << shift;
        this.ModeBlockToTopEdge = -this.RowIndex << shift;
        this.ModeBlockToBottomEdge = (frameHeader.ModeInfoRowCount - bh4 - this.RowIndex) << shift;

        // The bitstream expresses block size on the luma grid. Chroma dimensions are derived by
        // subsampling that grid while retaining at least one 4x4 chroma unit for narrow blocks.
        const int modeInfoSize = 1 << Av1Constants.ModeInfoSizeLog2;
        this.lumaWidthInPixels = bw4 * modeInfoSize;
        this.lumaHeightInPixels = bh4 * modeInfoSize;
        this.chromaWidthInPixels = Math.Max(1, bw4 >> subX) * modeInfoSize;
        this.chromaHeightInPixels = Math.Max(1, bh4 >> subY) * modeInfoSize;
    }

    /// <summary>
    /// Resolves the decoded luma and chroma mode information for available above and left neighbors.
    /// </summary>
    /// <param name="colorConfig">The color-plane subsampling configuration.</param>
    public void PopulateModeInfoNeighbors(ObuColorConfig colorConfig)
    {
        if (this.AvailableAbove)
        {
            this.AboveModeInfo = this.SuperblockInfo.GetModeInfoAt(new Point(this.ColumnIndex, this.RowIndex - 1));
        }

        if (this.AvailableLeft)
        {
            this.LeftModeInfo = this.SuperblockInfo.GetModeInfoAt(new Point(this.ColumnIndex - 1, this.RowIndex));
        }

        if (!this.IsChroma)
        {
            return;
        }

        int subX = colorConfig.SubSamplingX ? 1 : 0;
        int subY = colorConfig.SubSamplingY ? 1 : 0;
        int chromaBaseColumn = this.ColumnIndex - (this.ColumnIndex & subX);
        int chromaBaseRow = this.RowIndex - (this.RowIndex & subY);

        // Chroma neighbors refer to the bottom-right luma mode covered by each adjacent chroma block.
        if (this.AvailableAboveForChroma)
        {
            this.AboveModeInfoForChroma = this.SuperblockInfo.GetModeInfoAt(new Point(chromaBaseColumn + subX, chromaBaseRow - 1));
        }

        if (this.AvailableLeftForChroma)
        {
            this.LeftModeInfoForChroma = this.SuperblockInfo.GetModeInfoAt(new Point(chromaBaseColumn - 1, chromaBaseRow + subY));
        }
    }

    /// <summary>
    /// Gets the luma transform type covering a transform position from a subsampled chroma plane.
    /// </summary>
    /// <param name="planePosition">The transform position in 4x4 units of the target plane.</param>
    /// <param name="subX">Indicates whether the target plane is horizontally subsampled.</param>
    /// <param name="subY">Indicates whether the target plane is vertically subsampled.</param>
    /// <returns>The transform type decoded at the corresponding luma-grid position.</returns>
    public Av1TransformType GetLumaTransformType(Point planePosition, bool subX, bool subY)
    {
        int lumaColumn = planePosition.X << (subX ? 1 : 0);
        int lumaRow = planePosition.Y << (subY ? 1 : 0);
        int first = this.ModeInfo.GetFirstTransformLocation(Av1Plane.Y);
        int count = this.ModeInfo.GetTransformUnitCount(Av1Plane.Y);
        Span<Av1TransformInfo> lumaTransforms = this.SuperblockInfo.GetTransformInfoY().Slice(first, count);
        foreach (Av1TransformInfo transform in lumaTransforms)
        {
            int width = transform.Size.Get4x4WideCount();
            int height = transform.Size.Get4x4HighCount();
            if (lumaColumn >= transform.OffsetX && lumaColumn < transform.OffsetX + width &&
                lumaRow >= transform.OffsetY && lumaRow < transform.OffsetY + height)
            {
                return transform.Type;
            }
        }

        throw new InvalidImageContentException("Missing luma transform for inter-predicted chroma.");
    }

    /// <summary>
    /// Gets the block width clipped to the right frame edge.
    /// </summary>
    /// <param name="blockSize">The luma block size.</param>
    /// <param name="subX">A value indicating whether the target plane is horizontally subsampled.</param>
    /// <returns>The clipped width in 4x4 units of the target plane.</returns>
    public int GetMaxBlockWide(Av1BlockSize blockSize, bool subX)
    {
        int maxBlockWide = blockSize.GetWidth();
        if (this.ModeBlockToRightEdge < 0)
        {
            int shift = subX ? 4 : 3;
            maxBlockWide += this.ModeBlockToRightEdge >> shift;
        }

        return maxBlockWide >> 2;
    }

    /// <summary>
    /// Gets the block height clipped to the bottom frame edge.
    /// </summary>
    /// <param name="blockSize">The luma block size.</param>
    /// <param name="subY">A value indicating whether the target plane is vertically subsampled.</param>
    /// <returns>The clipped height in 4x4 units of the target plane.</returns>
    public int GetMaxBlockHigh(Av1BlockSize blockSize, bool subY)
    {
        int maxBlockHigh = blockSize.GetHeight();
        if (this.ModeBlockToBottomEdge < 0)
        {
            int shift = subY ? 4 : 3;
            maxBlockHigh += this.ModeBlockToBottomEdge >> shift;
        }

        return maxBlockHigh >> 2;
    }

    /// <summary>
    /// Determines whether the current block can use the block at its top-right search position.
    /// </summary>
    /// <param name="superblockModeInfoSize">The superblock width in 4x4 mode-information units.</param>
    /// <returns><see langword="true"/> when the top-right block has already been decoded; otherwise, <see langword="false"/>.</returns>
    public bool HasTopRight(int superblockModeInfoSize)
    {
        int width = this.ModeInfo.BlockSize.Get4x4WideCount();
        int height = this.ModeInfo.BlockSize.Get4x4HighCount();
        int blockSize = Math.Max(width, height);
        if (blockSize > 16)
        {
            return false;
        }

        int row = this.RowIndex & (superblockModeInfoSize - 1);
        int column = this.ColumnIndex & (superblockModeInfoSize - 1);
        bool hasTopRight = !((row & blockSize) != 0 && (column & blockSize) != 0);
        int traversalSize = blockSize;

        // Split partitions decode three quadrants before the bottom-right quadrant. Walking the enclosing split levels
        // excludes a right-hand block whenever traversal has not reached that block yet.
        while (traversalSize < superblockModeInfoSize)
        {
            if ((column & traversalSize) == 0)
            {
                break;
            }

            if ((column & (traversalSize << 1)) != 0 && (row & (traversalSize << 1)) != 0)
            {
                hasTopRight = false;
                break;
            }

            traversalSize <<= 1;
        }

        // Rectangular partitions override the square traversal rule because their sub-blocks are decoded along the
        // long axis. Earlier vertical rectangles have a completed row above; later horizontal rectangles do not.
        if (width < height && ((this.ColumnIndex + width) & (height - 1)) != 0)
        {
            hasTopRight = true;
        }

        if (width > height && (this.RowIndex & (width - 1)) != 0)
        {
            hasTopRight = false;
        }

        // The lower-left square of a vertical-A partition precedes its right-hand rectangle in bitstream order.
        if (this.Type == Av1PartitionType.VerticalA && width == height && (row & traversalSize) != 0)
        {
            hasTopRight = false;
        }

        return hasTopRight;
    }
}
