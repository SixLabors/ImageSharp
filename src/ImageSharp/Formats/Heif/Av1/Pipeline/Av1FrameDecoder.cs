// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantification;
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
        this.blockDecoder = new(this.sequenceHeader, this.frameHeader, this.frameBuffer);
    }

    /// <summary>
    /// Reconstructs every coded tile of the frame; in-loop post-processing stages remain disabled until implemented.
    /// </summary>
    public void DecodeFrame()
    {
        // Tile columns are the outer traversal because each call walks that column's tile rows and their superblocks.
        for (int column = 0; column < this.frameHeader.TilesInfo.TileColumnCount; column++)
        {
            this.DecodeFrameTiles(column);
        }

        bool doLoopFilterFlag = false;
        bool doLoopRestoration = false;
        bool doUpscale = false;

        // These flags remain false until the corresponding normative stages have complete scalar implementations
        // and independent still-image vectors; silently running partial filters would corrupt reconstructed pixels.
        if (doLoopFilterFlag)
        {
            this.DecodeLoopFilterForFrame();
        }

        if (doLoopRestoration)
        {
            // LoopRestorationSaveBoundaryLines(false);
        }

        // DecodeCdef();
        // SuperResolutionUpscaling(doUpscale);
        if (doLoopRestoration && doUpscale)
        {
            // LoopRestorationSaveBoundaryLines(true);
        }

        // DecodeLoopRestoration(doLoopRestoration);
        // PadPicture();
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

    /// <summary>
    /// Traverses frame superblocks in the order required by the not-yet-implemented deblocking stage.
    /// </summary>
    private void DecodeLoopFilterForFrame()
    {
        int superblockSizeLog2 = this.sequenceHeader.SuperblockSizeLog2;
        int pictureWidthInSuperblocks = Av1Math.DivideLog2Ceiling(this.frameHeader.FrameSize.FrameWidth, this.sequenceHeader.SuperblockSizeLog2);
        int pictureHeightInSuperblocks = Av1Math.DivideLog2Ceiling(this.frameHeader.FrameSize.FrameHeight, this.sequenceHeader.SuperblockSizeLog2);

        // Deblocking uses raster traversal so each block can consume already reconstructed top and left edges.
        for (int superblockIndexY = 0; superblockIndexY < pictureHeightInSuperblocks; ++superblockIndexY)
        {
            for (int superblockIndexX = 0; superblockIndexX < pictureWidthInSuperblocks; ++superblockIndexX)
            {
                int superblockOriginX = superblockIndexX << superblockSizeLog2;
                int superblockOriginY = superblockIndexY << superblockSizeLog2;
                bool endOfRowFlag = superblockIndexX == pictureWidthInSuperblocks - 1;

                Point superblockPoint = new(superblockOriginX, superblockOriginY);
                Av1SuperblockInfo superblockInfo = this.frameInfo.GetSuperblock(superblockPoint);

                // Superblock filtering remains disabled until its complete plane and edge-strength implementation is available.
                /*
                DecodeLoopFilterForSuperblock(
                    superblockInfo,
                    this.frameHeader,
                    this.sequenceHeader,
                    reconstructionFrameBuffer,
                    loopFilterContext,
                    superblockOriginY >> 2,
                    superblockOriginX >> 2,
                    Av1Plane.Y,
                    3,
                    endOfRowFlag,
                    superblockInfo.SuperblockDeltaLoopFilter);
                */
            }
        }
    }
}
