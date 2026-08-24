// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.FilmGrain;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantification;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.SuperResolution;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Reconstructs the coded blocks of one AV1 still-image frame into planar sample buffers.
/// </summary>
internal class Av1FrameDecoder : IAv1FrameDecoder
{
    /// <summary>
    /// The sequence-level superblock and color configuration.
    /// </summary>
    private readonly ObuSequenceHeader sequenceHeader;

    /// <summary>
    /// The frame-level tile, quantization, and reconstruction configuration.
    /// </summary>
    private readonly ObuFrameHeader frameHeader;

    /// <summary>
    /// The parsed superblock and block-mode information for the frame.
    /// </summary>
    private readonly Av1FrameInfo frameInfo;

    /// <summary>
    /// The destination planar sample buffers for reconstructed pixels.
    /// </summary>
    private readonly Av1FrameBuffer<byte> frameBuffer;

    /// <summary>
    /// The coefficient inverse-quantization stage shared across superblocks.
    /// </summary>
    private readonly Av1InverseQuantizer inverseQuantizer;

    /// <summary>
    /// The frame's base per-segment and per-plane dequantization values.
    /// </summary>
    private readonly Av1DeQuantizationContext deQuants;

    /// <summary>
    /// The transform-size map populated during reconstruction and consumed by deblocking.
    /// </summary>
    private readonly Av1LoopFilterContext loopFilterContext;

    /// <summary>
    /// The block reconstruction stage that applies prediction and inverse transforms.
    /// </summary>
    private readonly Av1BlockDecoder blockDecoder;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1FrameDecoder"/> class.
    /// </summary>
    /// <param name="sequenceHeader">The parsed AV1 sequence header.</param>
    /// <param name="frameHeader">The parsed AV1 frame header.</param>
    /// <param name="frameInfo">The parsed superblock and block-mode information.</param>
    /// <param name="frameBuffer">The destination planar sample buffers.</param>
    public Av1FrameDecoder(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader, Av1FrameInfo frameInfo, Av1FrameBuffer<byte> frameBuffer)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;
        this.frameInfo = frameInfo;
        this.frameBuffer = frameBuffer;
        this.inverseQuantizer = new(sequenceHeader, frameHeader);
        this.deQuants = new(sequenceHeader, frameHeader);
        this.loopFilterContext = new(sequenceHeader);
        this.blockDecoder = new(this.sequenceHeader, this.frameHeader, this.frameBuffer, this.loopFilterContext);
    }

    /// <summary>
    /// Reconstructs every coded tile and applies the implemented in-loop frame stages in normative order.
    /// </summary>
    public void DecodeFrame()
    {
        // Tile columns are the outer traversal because each call walks that column's tile rows and their superblocks.
        for (int column = 0; column < this.frameHeader.TilesInfo.TileColumnCount; column++)
        {
            this.DecodeFrameTiles(column);
        }

        bool doLoopRestoration = this.frameHeader.LoopRestorationParameters.UsesLoopRestoration;

        Av1LoopFilterDecoder loopFilterDecoder = new(
            this.sequenceHeader,
            this.frameHeader,
            this.frameInfo,
            this.frameBuffer,
            this.loopFilterContext);

        loopFilterDecoder.DecodeFrame();

        using Av1LoopRestorationBoundary? restorationBoundary = doLoopRestoration
            ? new(this.sequenceHeader, this.frameHeader, this.frameBuffer)
            : null;

        if (restorationBoundary is not null)
        {
            restorationBoundary.SaveDeblockedRows();
        }

        Av1CdefDecoder cdefDecoder = new(this.sequenceHeader, this.frameHeader, this.frameInfo, this.frameBuffer);
        cdefDecoder.DecodeFrame();

        Av1SuperResolutionDecoder superResolutionDecoder = new(this.sequenceHeader, this.frameHeader, this.frameBuffer);
        superResolutionDecoder.DecodeFrame();

        if (restorationBoundary is not null)
        {
            restorationBoundary.SaveFrameEdgeRows();
            Av1LoopRestorationDecoder loopRestorationDecoder = new(
                this.sequenceHeader,
                this.frameHeader,
                this.frameInfo,
                this.frameBuffer,
                restorationBoundary);

            loopRestorationDecoder.DecodeFrame();
        }

        // Film grain belongs to the displayed image rather than the reference reconstruction, so it
        // follows every in-loop filter. This decoder owns no retained reference frames.
        Av1FilmGrainDecoder filmGrainDecoder = new(this.sequenceHeader, this.frameHeader, this.frameBuffer);
        filmGrainDecoder.DecodeFrame();

        // Extending reference-frame borders is sequence playback state and is deliberately outside
        // this still-image decoder's scope.
    }

    /// <summary>
    /// Reconstructs every tile row in one tile column.
    /// </summary>
    /// <param name="tileColumn">The zero-based tile-column index.</param>
    /// <remarks>SVT-AV1: <c>decode_tile</c>.</remarks>
    private void DecodeFrameTiles(int tileColumn)
    {
        ObuTileGroupHeader tileInfo = this.frameHeader.TilesInfo;
        for (int tileRow = 0; tileRow < tileInfo.TileRowCount; tileRow++)
        {
            // Tile boundaries are expressed in 4x4 mode-info units. Walk every superblock row between consecutive
            // boundaries; using only the tile-row index would reconstruct one row and leave taller tiles incomplete.
            int modeInfoRowStart = tileInfo.TileRowStartModeInfo[tileRow];
            int modeInfoRowEnd = tileInfo.TileRowStartModeInfo[tileRow + 1];
            for (int modeInfoRow = modeInfoRowStart;
                modeInfoRow < modeInfoRowEnd;
                modeInfoRow += this.sequenceHeader.SuperblockModeInfoSize)
            {
                int superblockRow = modeInfoRow / this.sequenceHeader.SuperblockModeInfoSize;
                this.DecodeTileSuperblockRow(tileRow, tileColumn, modeInfoRow, superblockRow);
            }
        }
    }

    /// <summary>
    /// Reconstructs one superblock row within a tile from left to right.
    /// </summary>
    /// <param name="tileRow">The zero-based tile-row index.</param>
    /// <param name="tileColumn">The zero-based tile-column index.</param>
    /// <param name="modeInfoRow">The frame-relative row in 4x4 mode-info units.</param>
    /// <param name="superblockRow">The frame-relative superblock row.</param>
    /// <remarks>SVT-AV1: <c>decode_tile_row</c>.</remarks>
    private void DecodeTileSuperblockRow(int tileRow, int tileColumn, int modeInfoRow, int superblockRow)
    {
        ObuTileGroupHeader tileInfo = this.frameHeader.TilesInfo;
        for (int modeInfoColumn = tileInfo.TileColumnStartModeInfo[tileColumn]; modeInfoColumn < tileInfo.TileColumnStartModeInfo[tileColumn + 1];
             modeInfoColumn += this.sequenceHeader.SuperblockModeInfoSize)
        {
            // Convert the signaled 4x4 mode-info column to the frame-level superblock index used by Av1FrameInfo.
            int superblockColumn = modeInfoColumn << Av1Constants.ModeInfoSizeLog2 >> this.sequenceHeader.SuperblockSizeLog2;

            Av1SuperblockInfo superblockInfo = this.frameInfo.GetSuperblock(new Point(superblockColumn, superblockRow));

            Point modeInfoPosition = new(modeInfoColumn, modeInfoRow);
            this.DecodeSuperblock(modeInfoPosition, superblockInfo, new Av1TileInfo(tileRow, tileColumn, this.frameHeader));
        }
    }

    /// <summary>
    /// Reconstructs one superblock after applying its block state and delta-Q context.
    /// </summary>
    /// <param name="modeInfoPosition">The superblock's top-left position in 4x4 mode-info units.</param>
    /// <param name="superblockInfo">The decoded syntax and block modes for the superblock.</param>
    /// <param name="tileInfo">The tile that contains the superblock.</param>
    /// <remarks>SVT-AV1: <c>svt_aom_decode_super_block</c>.</remarks>
    public void DecodeSuperblock(Point modeInfoPosition, Av1SuperblockInfo superblockInfo, Av1TileInfo tileInfo)
    {
        this.blockDecoder.UpdateSuperblock(superblockInfo);
        this.inverseQuantizer.UpdateDequant(this.deQuants, superblockInfo);
        this.DecodePartition(modeInfoPosition, superblockInfo, tileInfo);
    }

    /// <summary>
    /// Reconstructs each decoded block in a superblock partition.
    /// </summary>
    /// <param name="modeInfoPosition">The superblock's frame-relative origin in 4x4 mode-info units.</param>
    /// <param name="superblockInfo">The superblock whose block modes are traversed.</param>
    /// <param name="tileInfo">The tile boundary information used by intra prediction.</param>
    /// <remarks>SVT-AV1: <c>decode_partition</c>.</remarks>
    private void DecodePartition(Point modeInfoPosition, Av1SuperblockInfo superblockInfo, Av1TileInfo tileInfo)
    {
        foreach (Av1BlockModeInfo modeInfo in superblockInfo.GetModeInfos())
        {
            Point subPosition = modeInfo.PositionInSuperblock;
            Av1BlockSize subSize = modeInfo.BlockSize;
            Point globalPosition = new(modeInfoPosition.X, modeInfoPosition.Y);

            // Block positions are stored relative to the superblock; prediction and reconstruction require frame-relative mode-info coordinates.
            globalPosition.Offset(subPosition);
            this.blockDecoder.DecodeBlock(modeInfo, globalPosition, subSize, superblockInfo, tileInfo);
        }
    }
}
