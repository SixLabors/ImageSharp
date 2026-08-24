// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Parses partition, mode, transform, and coefficient syntax for one AV1 tile.
/// </summary>
internal class Av1TileReader : IAv1TileReader
{
    /// <summary>
    /// The default self-guided restoration projection coefficients for each color plane.
    /// </summary>
    private static readonly int[] SgrprojXqdMid = [-32, 31];

    /// <summary>
    /// The default Wiener restoration taps retained between restoration units.
    /// </summary>
    private static readonly int[] WienerTapsMid = [3, -7, 15];

    /// <summary>
    /// Maps packed coefficient sign classes to their signed contribution to the DC context.
    /// </summary>
    private static readonly int[] Signs = [0, -1, 1];

    /// <summary>
    /// Maps the summed neighboring DC signs to the AV1 DC-sign entropy context.
    /// </summary>
    private static readonly int[] DcSignContexts = [
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2];

    /// <summary>
    /// Maps the minimum and union of luma neighbor levels to a transform-block skip context.
    /// </summary>
    private static readonly int[][] SkipContexts = [
        [1, 2, 2, 2, 3], [1, 4, 4, 4, 5], [1, 4, 4, 4, 5], [1, 4, 4, 4, 5], [1, 4, 4, 4, 6]];

    /// <summary>
    /// Stores the preceding self-guided restoration coefficients for each color plane.
    /// </summary>
    private int[][] referenceSgrXqd = [];

    /// <summary>
    /// Stores the preceding horizontal and vertical Wiener taps for each color plane.
    /// </summary>
    private int[][][] referenceLrWiener = [];

    /// <summary>
    /// Tracks entropy, partition, transform, and palette state above the current block.
    /// </summary>
    private readonly Av1ParseAboveNeighbor4x4Context aboveNeighborContext;

    /// <summary>
    /// Tracks entropy, partition, transform, and palette state left of the current block.
    /// </summary>
    private readonly Av1ParseLeftNeighbor4x4Context leftNeighborContext;

    /// <summary>
    /// The quantizer index carried between delta-quantized blocks in the current tile.
    /// </summary>
    private int currentQuantizerIndex;

    /// <summary>
    /// Stores the segment identifier covering each 4x4 frame position.
    /// </summary>
    private readonly int[][] segmentIds = [];

    /// <summary>
    /// Stores per-plane transform counts for each forced 64x64 residual region.
    /// </summary>
    private readonly int[][] transformUnitCount;

    /// <summary>
    /// Tracks the first unassigned transform-information index for luma and shared chroma storage.
    /// </summary>
    private readonly int[] firstTransformOffset = new int[2];

    /// <summary>
    /// Tracks the next coefficient slot for each color plane within the current superblock.
    /// </summary>
    private readonly int[] coefficientIndex = [];

    /// <summary>
    /// Provides allocator and decoder configuration to tile entropy decoding.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// Reconstructs each parsed superblock when pixel decoding is requested; otherwise, tile parsing is metadata-only.
    /// </summary>
    private readonly IAv1FrameDecoder? frameDecoder;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TileReader"/> class for syntax parsing without reconstruction.
    /// </summary>
    /// <param name="configuration">The decoder configuration.</param>
    /// <param name="sequenceHeader">The active AV1 sequence header.</param>
    /// <param name="frameHeader">The frame header whose tiles will be parsed.</param>
    public Av1TileReader(Configuration configuration, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        this.FrameHeader = frameHeader;
        this.configuration = configuration;
        this.SequenceHeader = sequenceHeader;

        // FrameInfo owns all traversal-order records and coefficient storage produced by the tile readers.
        this.FrameInfo = new(this.SequenceHeader);
        this.segmentIds = new int[this.FrameHeader.ModeInfoRowCount][];
        for (int y = 0; y < this.FrameHeader.ModeInfoRowCount; y++)
        {
            this.segmentIds[y] = new int[this.FrameHeader.ModeInfoColumnCount];
        }

        // Above contexts span the aligned frame width, while left contexts are reused for each superblock row.
        int planesCount = sequenceHeader.ColorConfig.PlaneCount;
        int superblockColumnCount =
            Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameWidth, sequenceHeader.SuperblockSizeLog2) >> sequenceHeader.SuperblockSizeLog2;
        int modeInfoWideColumnCount = superblockColumnCount * sequenceHeader.SuperblockModeInfoSize;
        modeInfoWideColumnCount = Av1Math.AlignPowerOf2(modeInfoWideColumnCount, sequenceHeader.SuperblockSizeLog2 - Av1Constants.ModeInfoSizeLog2);
        this.aboveNeighborContext = new Av1ParseAboveNeighbor4x4Context(planesCount, modeInfoWideColumnCount);
        this.leftNeighborContext = new Av1ParseLeftNeighbor4x4Context(planesCount, sequenceHeader.SuperblockModeInfoSize);
        this.transformUnitCount = new int[Av1Constants.MaxPlanes][];
        this.transformUnitCount[0] = new int[this.FrameInfo.ModeInfoCount];
        this.transformUnitCount[1] = new int[this.FrameInfo.ModeInfoCount];
        this.transformUnitCount[2] = new int[this.FrameInfo.ModeInfoCount];
        this.coefficientIndex = new int[Av1Constants.MaxPlanes];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TileReader"/> class that reconstructs parsed superblocks.
    /// </summary>
    /// <param name="configuration">The decoder configuration.</param>
    /// <param name="sequenceHeader">The active AV1 sequence header.</param>
    /// <param name="frameHeader">The frame header whose tiles will be parsed.</param>
    /// <param name="frameDecoder">The frame decoder that reconstructs each parsed superblock.</param>
    public Av1TileReader(Configuration configuration, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader, IAv1FrameDecoder frameDecoder)
        : this(configuration, sequenceHeader, frameHeader)
        => this.frameDecoder = frameDecoder;

    /// <summary>
    /// Gets the frame header whose tile syntax is being parsed.
    /// </summary>
    public ObuFrameHeader FrameHeader { get; }

    /// <summary>
    /// Gets the sequence header governing the frame.
    /// </summary>
    public ObuSequenceHeader SequenceHeader { get; }

    /// <summary>
    /// Gets the frame-owned mode, transform, coefficient, quantizer, and filter state populated by tile parsing.
    /// </summary>
    public Av1FrameInfo FrameInfo { get; }

    /// <summary>
    /// Parses one tile's partition, mode, transform, coefficient, and filter syntax in superblock order.
    /// </summary>
    /// <param name="tileData">The entropy-coded tile payload.</param>
    /// <param name="tileNum">The zero-based tile index in row-major order.</param>
    /// <remarks>Corresponds to <c>parse_tile</c> in SVT-AV1.</remarks>
    public void ReadTile(Span<byte> tileData, int tileNum)
    {
        Av1SymbolDecoder reader = new(this.configuration, tileData, this.FrameHeader.QuantizationParameters.BaseQIndex);
        int tileColumnIndex = tileNum % this.FrameHeader.TilesInfo.TileColumnCount;
        int tileRowIndex = tileNum / this.FrameHeader.TilesInfo.TileColumnCount;

        int modeInfoColumnStart = this.FrameHeader.TilesInfo.TileColumnStartModeInfo[tileColumnIndex];
        int modeInfoColumnEnd = this.FrameHeader.TilesInfo.TileColumnStartModeInfo[tileColumnIndex + 1];
        int modeInfoRowStart = this.FrameHeader.TilesInfo.TileRowStartModeInfo[tileRowIndex];
        int modeInfoRowEnd = this.FrameHeader.TilesInfo.TileRowStartModeInfo[tileRowIndex + 1];
        this.aboveNeighborContext.Clear(this.SequenceHeader, modeInfoColumnStart, modeInfoColumnEnd);
        this.ClearLoopFilterDelta();
        int planesCount = this.SequenceHeader.ColorConfig.PlaneCount;

        // Restoration coefficients are differentially coded, so each tile begins from the AV1 defaults.
        this.referenceSgrXqd = new int[planesCount][];
        this.referenceLrWiener = new int[planesCount][][];
        for (int plane = 0; plane < planesCount; plane++)
        {
            this.referenceSgrXqd[plane] = new int[2];
            Array.Copy(SgrprojXqdMid, this.referenceSgrXqd[plane], SgrprojXqdMid.Length);
            this.referenceLrWiener[plane] = new int[2][];
            for (int pass = 0; pass < 2; pass++)
            {
                this.referenceLrWiener[plane][pass] = new int[Av1Constants.WienerCoefficientCount];
                Array.Copy(WienerTapsMid, this.referenceLrWiener[plane][pass], WienerTapsMid.Length);
            }
        }

        Av1TileInfo tileInfo = new(tileRowIndex, tileColumnIndex, this.FrameHeader);
        Av1BlockSize superBlockSize = this.SequenceHeader.SuperblockSize;
        int superBlock4x4Size = this.SequenceHeader.SuperblockSize.Get4x4WideCount();
        int superBlockSizeLog2 = this.SequenceHeader.SuperblockSizeLog2;
        for (int row = modeInfoRowStart; row < modeInfoRowEnd; row += superBlock4x4Size)
        {
            int superBlockRow = (row << Av1Constants.ModeInfoSizeLog2) >> superBlockSizeLog2;
            this.leftNeighborContext.Clear(this.SequenceHeader);
            for (int column = modeInfoColumnStart; column < modeInfoColumnEnd; column += superBlock4x4Size)
            {
                int superBlockColumn = (column << Av1Constants.ModeInfoSizeLog2) >> superBlockSizeLog2;
                Point superblockPosition = new(superBlockColumn, superBlockRow);
                Av1SuperblockInfo superblockInfo = this.FrameInfo.GetSuperblock(superblockPosition);

                Point modeInfoPosition = new(column, row);
                this.FrameInfo.ClearCdef(superblockPosition);
                this.firstTransformOffset[0] = 0;
                this.firstTransformOffset[1] = 0;
                this.coefficientIndex.AsSpan().Clear();
                this.ReadLoopRestoration(modeInfoPosition, superBlockSize);
                this.ParsePartition(ref reader, modeInfoPosition, superBlockSize, superblockInfo, tileInfo);

                // Identify-only parsing omits a frame decoder but still populates the complete syntax model.
                this.frameDecoder?.DecodeSuperblock(modeInfoPosition, superblockInfo, tileInfo);
            }
        }
    }

    /// <summary>
    /// Resets all frame loop-filter delta state before parsing a tile.
    /// </summary>
    private void ClearLoopFilterDelta()
        => this.FrameInfo.ClearDeltaLoopFilter();

    /// <summary>
    /// Reads loop-restoration unit syntax that begins at a superblock location.
    /// </summary>
    /// <param name="modeInfoLocation">The superblock origin in 4x4 mode-information units.</param>
    /// <param name="superBlockSize">The superblock size.</param>
    /// <exception cref="NotImplementedException">A color plane signals a loop-restoration filter.</exception>
    private void ReadLoopRestoration(Point modeInfoLocation, Av1BlockSize superBlockSize)
    {
        int planesCount = this.SequenceHeader.ColorConfig.PlaneCount;
        for (int plane = 0; plane < planesCount; plane++)
        {
            if (this.FrameHeader.LoopRestorationParameters.Items[plane].Type != ObuRestorationType.None)
            {
                throw new NotImplementedException("No loop restoration filter support.");
            }
        }
    }

    /// <summary>
    /// Decodes AV1 partition syntax and recursively visits each resulting coding block.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="modeInfoLocation">The parent block origin in 4x4 mode-information units.</param>
    /// <param name="blockSize">The parent block size.</param>
    /// <param name="superblockInfo">The containing superblock.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <remarks>Implements AV1 section 5.11.4.</remarks>
    private void ParsePartition(ref Av1SymbolDecoder reader, Point modeInfoLocation, Av1BlockSize blockSize, Av1SuperblockInfo superblockInfo, Av1TileInfo tileInfo)
    {
        int columnIndex = modeInfoLocation.X;
        int rowIndex = modeInfoLocation.Y;
        if (modeInfoLocation.Y >= this.FrameHeader.ModeInfoRowCount || modeInfoLocation.X >= this.FrameHeader.ModeInfoColumnCount)
        {
            return;
        }

        int block4x4Size = blockSize.Get4x4WideCount();
        int halfBlock4x4Size = block4x4Size >> 1;
        int quarterBlock4x4Size = halfBlock4x4Size >> 1;
        bool hasRows = (modeInfoLocation.Y + halfBlock4x4Size) < this.FrameHeader.ModeInfoRowCount;
        bool hasColumns = (modeInfoLocation.X + halfBlock4x4Size) < this.FrameHeader.ModeInfoColumnCount;
        Av1PartitionType partitionType = Av1PartitionType.None;
        if (blockSize >= Av1BlockSize.Block8x8)
        {
            int ctx = this.GetPartitionPlaneContext(modeInfoLocation, blockSize, tileInfo, superblockInfo);
            partitionType = Av1PartitionType.Split;
            if (hasRows && hasColumns)
            {
                partitionType = reader.ReadPartitionType(ctx);
            }
            else if (hasColumns)
            {
                partitionType = reader.ReadSplitOrHorizontal(blockSize, ctx);
            }
            else if (hasRows)
            {
                partitionType = reader.ReadSplitOrVertical(blockSize, ctx);
            }
        }

        Av1BlockSize subSize = partitionType.GetBlockSubSize(blockSize);
        Av1BlockSize splitSize = Av1PartitionType.Split.GetBlockSubSize(blockSize);

        // Partition syntax is depth-first. The visit order here is also the order in which mode,
        // transform, and coefficient records are appended to their frame-owned arrays.
        switch (partitionType)
        {
            case Av1PartitionType.Split:
                Point loc1 = new(modeInfoLocation.X + halfBlock4x4Size, modeInfoLocation.Y);
                Point loc2 = new(modeInfoLocation.X, modeInfoLocation.Y + halfBlock4x4Size);
                Point loc3 = new(modeInfoLocation.X + halfBlock4x4Size, modeInfoLocation.Y + halfBlock4x4Size);
                this.ParsePartition(ref reader, modeInfoLocation, subSize, superblockInfo, tileInfo);
                this.ParsePartition(ref reader, loc1, subSize, superblockInfo, tileInfo);
                this.ParsePartition(ref reader, loc2, subSize, superblockInfo, tileInfo);
                this.ParsePartition(ref reader, loc3, subSize, superblockInfo, tileInfo);
                break;
            case Av1PartitionType.None:
                this.ParseBlock(ref reader, modeInfoLocation, subSize, superblockInfo, tileInfo, Av1PartitionType.None);
                break;
            case Av1PartitionType.Horizontal:
                this.ParseBlock(ref reader, modeInfoLocation, subSize, superblockInfo, tileInfo, Av1PartitionType.Horizontal);
                if (hasRows)
                {
                    Point halfLocation = new(columnIndex, rowIndex + halfBlock4x4Size);
                    this.ParseBlock(ref reader, halfLocation, subSize, superblockInfo, tileInfo, Av1PartitionType.Horizontal);
                }

                break;
            case Av1PartitionType.Vertical:
                this.ParseBlock(ref reader, modeInfoLocation, subSize, superblockInfo, tileInfo, Av1PartitionType.Vertical);
                if (hasColumns)
                {
                    Point halfLocation = new(columnIndex + halfBlock4x4Size, rowIndex);
                    this.ParseBlock(ref reader, halfLocation, subSize, superblockInfo, tileInfo, Av1PartitionType.Vertical);
                }

                break;
            case Av1PartitionType.HorizontalA:
                this.ParseBlock(ref reader, modeInfoLocation, splitSize, superblockInfo, tileInfo, Av1PartitionType.HorizontalA);
                Point locHorA1 = new(columnIndex + halfBlock4x4Size, rowIndex);
                this.ParseBlock(ref reader, locHorA1, splitSize, superblockInfo, tileInfo, Av1PartitionType.HorizontalA);
                Point locHorA2 = new(columnIndex, rowIndex + halfBlock4x4Size);
                this.ParseBlock(ref reader, locHorA2, subSize, superblockInfo, tileInfo, Av1PartitionType.HorizontalA);
                break;
            case Av1PartitionType.HorizontalB:
                this.ParseBlock(ref reader, modeInfoLocation, subSize, superblockInfo, tileInfo, Av1PartitionType.HorizontalB);
                Point locHorB1 = new(columnIndex, rowIndex + halfBlock4x4Size);
                this.ParseBlock(ref reader, locHorB1, splitSize, superblockInfo, tileInfo, Av1PartitionType.HorizontalB);
                Point locHorB2 = new(columnIndex + halfBlock4x4Size, rowIndex + halfBlock4x4Size);
                this.ParseBlock(ref reader, locHorB2, splitSize, superblockInfo, tileInfo, Av1PartitionType.HorizontalB);
                break;
            case Av1PartitionType.VerticalA:
                this.ParseBlock(ref reader, modeInfoLocation, splitSize, superblockInfo, tileInfo, Av1PartitionType.VerticalA);
                Point locVertA1 = new(columnIndex, rowIndex + halfBlock4x4Size);
                this.ParseBlock(ref reader, locVertA1, splitSize, superblockInfo, tileInfo, Av1PartitionType.VerticalA);
                Point locVertA2 = new(columnIndex + halfBlock4x4Size, rowIndex);
                this.ParseBlock(ref reader, locVertA2, subSize, superblockInfo, tileInfo, Av1PartitionType.VerticalA);
                break;
            case Av1PartitionType.VerticalB:
                this.ParseBlock(ref reader, modeInfoLocation, subSize, superblockInfo, tileInfo, Av1PartitionType.VerticalB);
                Point locVertB1 = new(columnIndex + halfBlock4x4Size, rowIndex);
                this.ParseBlock(ref reader, locVertB1, splitSize, superblockInfo, tileInfo, Av1PartitionType.VerticalB);
                Point locVertB2 = new(columnIndex + halfBlock4x4Size, rowIndex + halfBlock4x4Size);
                this.ParseBlock(ref reader, locVertB2, splitSize, superblockInfo, tileInfo, Av1PartitionType.VerticalB);
                break;
            case Av1PartitionType.Horizontal4:
                for (int i = 0; i < 4; i++)
                {
                    int currentBlockRow = rowIndex + (i * quarterBlock4x4Size);
                    if (i > 0 && currentBlockRow >= this.FrameHeader.ModeInfoRowCount)
                    {
                        break;
                    }

                    Point currentLocation = new(modeInfoLocation.X, currentBlockRow);
                    this.ParseBlock(ref reader, currentLocation, subSize, superblockInfo, tileInfo, Av1PartitionType.Horizontal4);
                }

                break;
            case Av1PartitionType.Vertical4:
                for (int i = 0; i < 4; i++)
                {
                    int currentBlockColumn = columnIndex + (i * quarterBlock4x4Size);
                    if (i > 0 && currentBlockColumn >= this.FrameHeader.ModeInfoColumnCount)
                    {
                        break;
                    }

                    Point currentLocation = new(currentBlockColumn, modeInfoLocation.Y);
                    this.ParseBlock(ref reader, currentLocation, subSize, superblockInfo, tileInfo, Av1PartitionType.Vertical4);
                }

                break;
            default:
                throw new NotImplementedException($"Partition type: {partitionType} is not supported.");
        }

        this.UpdatePartitionContext(new Point(columnIndex, rowIndex), tileInfo, superblockInfo, subSize, blockSize, partitionType);
    }

    /// <summary>
    /// Parses all syntax associated with one final coding block and stores its frame mode information.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="modeInfoLocation">The block origin in 4x4 mode-information units.</param>
    /// <param name="blockSize">The final block size.</param>
    /// <param name="superblockInfo">The containing superblock.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="partitionType">The partition type that produced the block.</param>
    private void ParseBlock(ref Av1SymbolDecoder reader, Point modeInfoLocation, Av1BlockSize blockSize, Av1SuperblockInfo superblockInfo, Av1TileInfo tileInfo, Av1PartitionType partitionType)
    {
        int rowIndex = modeInfoLocation.Y;
        int columnIndex = modeInfoLocation.X;
        int block4x4Width = blockSize.Get4x4WideCount();
        int block4x4Height = blockSize.Get4x4HighCount();
        int planesCount = this.SequenceHeader.ColorConfig.PlaneCount;
        Point superblockLocation = superblockInfo.Position * this.SequenceHeader.SuperblockModeInfoSize;
        Point locationInSuperblock = new Point(modeInfoLocation.X - superblockLocation.X, modeInfoLocation.Y - superblockLocation.Y);
        Av1BlockModeInfo blockModeInfo = new(planesCount, blockSize, locationInSuperblock);
        blockModeInfo.PartitionType = partitionType;
        blockModeInfo.FirstTransformLocation[0] = this.firstTransformOffset[0];
        blockModeInfo.FirstTransformLocation[1] = this.firstTransformOffset[1];
        bool hasChroma = HasChroma(this.SequenceHeader, modeInfoLocation, blockSize);
        Av1PartitionInfo partitionInfo = new(blockModeInfo, superblockInfo, hasChroma, partitionType);
        partitionInfo.ColumnIndex = columnIndex;
        partitionInfo.RowIndex = rowIndex;
        superblockInfo.BlockCount++;
        partitionInfo.ComputeBoundaryOffsets(this.SequenceHeader, this.FrameHeader, tileInfo);
        if (hasChroma)
        {
            if (this.SequenceHeader.ColorConfig.SubSamplingY && block4x4Height == 1)
            {
                partitionInfo.AvailableAboveForChroma = this.IsInside(rowIndex - 2, columnIndex);
            }

            if (this.SequenceHeader.ColorConfig.SubSamplingX && block4x4Width == 1)
            {
                partitionInfo.AvailableLeftForChroma = this.IsInside(rowIndex, columnIndex - 2);
            }
        }

        partitionInfo.PopulateModeInfoNeighbors(this.SequenceHeader.ColorConfig);

        this.ReadModeInfo(ref reader, partitionInfo);
        ReadPaletteTokens(ref reader, partitionInfo);
        this.ReadBlockTransformSize(ref reader, modeInfoLocation, partitionInfo, superblockInfo, tileInfo);
        if (partitionInfo.ModeInfo.Skip)
        {
            this.ResetSkipContext(partitionInfo, tileInfo);
        }

        this.Residual(ref reader, partitionInfo, superblockInfo, tileInfo, blockSize);

        // Store the record only after all syntax has populated it, then map every covered 4x4 position.
        this.FrameInfo.UpdateModeInfo(blockModeInfo, superblockInfo);
    }

    /// <summary>
    /// Clears coefficient neighbor contexts across every plane of a skipped block.
    /// </summary>
    /// <param name="partitionInfo">The skipped block and its frame position.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <remarks>Corresponds to <c>reset_skip_context</c> in SVT-AV1.</remarks>
    private void ResetSkipContext(Av1PartitionInfo partitionInfo, Av1TileInfo tileInfo)
    {
        int planesCount = this.SequenceHeader.ColorConfig.PlaneCount;
        for (int i = 0; i < planesCount; i++)
        {
            int subX = (i > 0 && this.SequenceHeader.ColorConfig.SubSamplingX) ? 1 : 0;
            int subY = (i > 0 && this.SequenceHeader.ColorConfig.SubSamplingY) ? 1 : 0;
            Av1BlockSize planeBlockSize = partitionInfo.ModeInfo.BlockSize.GetSubsampled(subX, subY);
            DebugGuard.IsTrue(planeBlockSize != Av1BlockSize.Invalid, nameof(planeBlockSize));
            int txsWide = planeBlockSize.GetWidth() >> 2;
            int txsHigh = planeBlockSize.GetHeight() >> 2;
            int aboveOffset = (partitionInfo.ColumnIndex - tileInfo.ModeInfoColumnStart) >> subX;
            int leftOffset = (partitionInfo.RowIndex - partitionInfo.SuperblockInfo.ModeInfoPosition.Y) >> subY;
            this.aboveNeighborContext.ClearContext(i, aboveOffset, txsWide);
            this.leftNeighborContext.ClearContext(i, leftOffset, txsHigh);
        }
    }

    /// <summary>
    /// Parses every luma and chroma transform block and its coefficients for a coding block.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <param name="superblockInfo">The containing superblock and coefficient storage.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="blockSize">The coding block size.</param>
    /// <remarks>Implements AV1 section 5.11.34 and corresponds to <c>parse_residual</c> in SVT-AV1.</remarks>
    private void Residual(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo, Av1SuperblockInfo superblockInfo, Av1TileInfo tileInfo, Av1BlockSize blockSize)
    {
        int maxBlocksWide = partitionInfo.GetMaxBlockWide(blockSize, false);
        int maxBlocksHigh = partitionInfo.GetMaxBlockHigh(blockSize, false);
        Av1BlockSize maxUnitSize = Av1BlockSize.Block64x64;
        int modeUnitBlocksWide = maxUnitSize.GetWidth() >> 2;
        int modeUnitBlocksHigh = maxUnitSize.GetHeight() >> 2;
        modeUnitBlocksWide = Math.Min(maxBlocksWide, modeUnitBlocksWide);
        modeUnitBlocksHigh = Math.Min(maxBlocksHigh, modeUnitBlocksHigh);
        int planeCount = this.SequenceHeader.ColorConfig.PlaneCount;
        bool isLossless = this.FrameHeader.LosslessArray[partitionInfo.ModeInfo.SegmentId];
        bool isLosslessBlock = isLossless && (blockSize >= Av1BlockSize.Block64x64) && (blockSize <= Av1BlockSize.Block128x128);
        int subSampling = (this.SequenceHeader.ColorConfig.SubSamplingX ? 1 : 0) + (this.SequenceHeader.ColorConfig.SubSamplingY ? 1 : 0);
        int chromaTransformUnitCount = isLosslessBlock ? ((maxBlocksWide * maxBlocksHigh) >> subSampling) : partitionInfo.ModeInfo.TransformUnitsCount[(int)Av1PlaneType.Uv];

        int[] transformInfoIndices = new int[3];
        transformInfoIndices[0] = superblockInfo.TransformInfoIndexY + partitionInfo.ModeInfo.FirstTransformLocation[(int)Av1PlaneType.Y];
        transformInfoIndices[1] = superblockInfo.TransformInfoIndexUv + partitionInfo.ModeInfo.FirstTransformLocation[(int)Av1PlaneType.Uv];
        transformInfoIndices[2] = transformInfoIndices[1] + chromaTransformUnitCount;
        int forceSplitCount = 0;

        // AV1 forces residual traversal into at most 64x64 regions even when the coding block is larger.
        // transformUnitCount preserves the transform geometry generated for each such region and plane.
        for (int row = 0; row < maxBlocksHigh; row += modeUnitBlocksHigh)
        {
            for (int column = 0; column < maxBlocksWide; column += modeUnitBlocksWide)
            {
                for (int plane = 0; plane < planeCount; ++plane)
                {
                    int totalTransformUnitCount;
                    int transformUnitCount;
                    int subX = (plane > 0 && this.SequenceHeader.ColorConfig.SubSamplingX) ? 1 : 0;
                    int subY = (plane > 0 && this.SequenceHeader.ColorConfig.SubSamplingY) ? 1 : 0;

                    if (plane != 0 && !partitionInfo.IsChroma)
                    {
                        continue;
                    }

                    Span<Av1TransformInfo> transformInfoSpan = (plane == 0) ? superblockInfo.GetTransformInfoY() : superblockInfo.GetTransformInfoUv();
                    if (isLosslessBlock)
                    {
                        // Lossless coding fixes transforms at 4x4, so count each clipped 4x4 unit
                        // directly after applying the plane's chroma subsampling.
                        int unitHeight = Av1Math.RoundPowerOf2(Math.Min(modeUnitBlocksHigh + row, maxBlocksHigh), 0);
                        int unitWidth = Av1Math.RoundPowerOf2(Math.Min(modeUnitBlocksWide + column, maxBlocksWide), 0);
                        DebugGuard.IsTrue(transformInfoSpan[transformInfoIndices[plane]].Size == Av1TransformSize.Size4x4, "Lossless frame shall have transform units of size 4x4.");
                        transformUnitCount = ((unitWidth - column) * (unitHeight - row)) >> (subX + subY);
                    }
                    else
                    {
                        totalTransformUnitCount = partitionInfo.ModeInfo.TransformUnitsCount[Math.Min(1, plane)];
                        transformUnitCount = this.transformUnitCount[plane][forceSplitCount];

                        DebugGuard.IsFalse(totalTransformUnitCount == 0, nameof(totalTransformUnitCount), string.Empty);
                        DebugGuard.IsTrue(
                            totalTransformUnitCount ==
                                this.transformUnitCount[plane][0] + this.transformUnitCount[plane][1] +
                                this.transformUnitCount[plane][2] + this.transformUnitCount[plane][3],
                            nameof(totalTransformUnitCount),
                            string.Empty);
                    }

                    DebugGuard.IsFalse(transformUnitCount == 0, nameof(transformUnitCount), string.Empty);
                    for (int tu = 0; tu < transformUnitCount; tu++)
                    {
                        Av1TransformInfo transformInfo = transformInfoSpan[transformInfoIndices[plane]];
                        DebugGuard.MustBeLessThanOrEqualTo(transformInfo.OffsetX, maxBlocksWide, nameof(transformInfo));
                        DebugGuard.MustBeLessThanOrEqualTo(transformInfo.OffsetY, maxBlocksHigh, nameof(transformInfo));

                        int coefficientIndex = this.coefficientIndex[plane];
                        int endOfBlock = 0;
                        int blockColumn = transformInfo.OffsetX;
                        int blockRow = transformInfo.OffsetY;
                        int startX = (partitionInfo.ColumnIndex >> subX) + blockColumn;
                        int startY = (partitionInfo.RowIndex >> subY) + blockRow;

                        if (startX >= (this.FrameHeader.ModeInfoColumnCount >> subX) ||
                            startY >= (this.FrameHeader.ModeInfoRowCount >> subY))
                        {
                            return;
                        }

                        if (!partitionInfo.ModeInfo.Skip)
                        {
                            Span<int> coefficientBuffer = superblockInfo.GetCoefficients((Av1Plane)plane)[coefficientIndex..];
                            endOfBlock = this.ParseTransformBlock(ref reader, partitionInfo, coefficientBuffer, transformInfo, plane, blockColumn, blockRow, startX, startY, transformInfo.Size, subX != 0, subY != 0);
                        }

                        if (endOfBlock != 0)
                        {
                            // Coefficients are stored as an end index followed by scan-order values, so the
                            // next transform begins after both the prefix and its decoded coefficient range.
                            this.coefficientIndex[plane] += endOfBlock + 1;
                            transformInfo.CodeBlockFlag = true;
                        }
                        else
                        {
                            transformInfo.CodeBlockFlag = false;
                        }

                        transformInfoIndices[plane]++;
                    }
                }

                forceSplitCount++;
            }
        }
    }

    /// <summary>
    /// Determines whether a luma coding block owns chroma mode and residual syntax at its frame position.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header describing chroma subsampling.</param>
    /// <param name="modeInfoLocation">The block origin in 4x4 luma mode-information units.</param>
    /// <param name="blockSize">The luma block size.</param>
    /// <returns><see langword="true"/> when the block is a chroma reference position; otherwise, <see langword="false"/>.</returns>
    public static bool HasChroma(ObuSequenceHeader sequenceHeader, Point modeInfoLocation, Av1BlockSize blockSize)
    {
        int blockWide = blockSize.Get4x4WideCount();
        int blockHigh = blockSize.Get4x4HighCount();
        bool subX = sequenceHeader.ColorConfig.SubSamplingX;
        bool subY = sequenceHeader.ColorConfig.SubSamplingY;
        bool hasChroma = ((modeInfoLocation.Y & 0x01) != 0 || (blockHigh & 0x01) == 0 || !subY) &&
            ((modeInfoLocation.X & 0x01) != 0 || (blockWide & 0x01) == 0 || !subX);
        return hasChroma;
    }

    /// <summary>
    /// Derives a transform block's entropy context and decodes its coefficient syntax.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The containing coding block.</param>
    /// <param name="coefficientBuffer">The destination beginning at this transform's coefficient slot.</param>
    /// <param name="transformInfo">The transform geometry and syntax state to populate.</param>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <param name="blockColumn">The transform's horizontal offset within the coding block in 4x4 units.</param>
    /// <param name="blockRow">The transform's vertical offset within the coding block in 4x4 units.</param>
    /// <param name="startX">The frame-relative transform column in 4x4 units of the target plane.</param>
    /// <param name="startY">The frame-relative transform row in 4x4 units of the target plane.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="subX">A value indicating whether the target plane is horizontally subsampled.</param>
    /// <param name="subY">A value indicating whether the target plane is vertically subsampled.</param>
    /// <returns>The decoded end-of-block coefficient position, or zero for an all-zero transform.</returns>
    /// <remarks>
    /// Implements AV1 section 5.11.35 using the traversal shape of the corresponding SVT-AV1 implementation.
    /// </remarks>
    private int ParseTransformBlock(
        ref Av1SymbolDecoder reader,
        Av1PartitionInfo partitionInfo,
        Span<int> coefficientBuffer,
        Av1TransformInfo transformInfo,
        int plane,
        int blockColumn,
        int blockRow,
        int startX,
        int startY,
        Av1TransformSize transformSize,
        bool subX,
        bool subY)
    {
        int endOfBlock = 0;
        Av1BlockSize planeBlockSize = partitionInfo.ModeInfo.BlockSize.GetSubsampled(subX, subY);
        int transformBlockUnitWideCount = transformSize.Get4x4WideCount();
        int transformBlockUnitHighCount = transformSize.Get4x4HighCount();

        if (partitionInfo.ModeBlockToRightEdge < 0)
        {
            int blocksWide = partitionInfo.GetMaxBlockWide(planeBlockSize, subX);
            transformBlockUnitWideCount = Math.Min(transformBlockUnitWideCount, blocksWide - blockColumn);
        }

        if (partitionInfo.ModeBlockToBottomEdge < 0)
        {
            int blocksHigh = partitionInfo.GetMaxBlockHigh(planeBlockSize, subY);
            transformBlockUnitHighCount = Math.Min(transformBlockUnitHighCount, blocksHigh - blockRow);
        }

        Av1TransformBlockContext transformBlockContext = this.GetTransformBlockContext(transformSize, plane, planeBlockSize, transformBlockUnitHighCount, transformBlockUnitWideCount, startY, startX);
        endOfBlock = this.ParseCoefficients(ref reader, partitionInfo, startY, startX, blockRow, blockColumn, plane, transformBlockContext, transformSize, transformInfo, coefficientBuffer);

        return endOfBlock;
    }

    /// <summary>
    /// Decodes transform coefficients and updates the coefficient neighbor contexts for one color plane.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The containing coding block.</param>
    /// <param name="blockRow">The frame-relative transform row in 4x4 units of the target plane.</param>
    /// <param name="blockColumn">The frame-relative transform column in 4x4 units of the target plane.</param>
    /// <param name="aboveOffset">The horizontal transform offset within the coding block in 4x4 units.</param>
    /// <param name="leftOffset">The vertical transform offset within the coding block in 4x4 units.</param>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <param name="transformBlockContext">The coefficient skip and DC-sign entropy contexts.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="transformInfo">The transform syntax state to populate.</param>
    /// <param name="coefficientBuffer">The destination beginning at this transform's coefficient slot.</param>
    /// <returns>The decoded end-of-block coefficient position, or zero for an all-zero transform.</returns>
    /// <remarks>
    /// Implements AV1 section 5.11.39 using the traversal shape of the corresponding SVT-AV1 implementation.
    /// </remarks>
    private int ParseCoefficients(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo, int blockRow, int blockColumn, int aboveOffset, int leftOffset, int plane, Av1TransformBlockContext transformBlockContext, Av1TransformSize transformSize, Av1TransformInfo transformInfo, Span<int> coefficientBuffer)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        Av1TransformSize transformSizeContext = Av1SymbolContextHelper.GetTransformSizeContext(transformSize);
        Av1PlaneType planeType = (Av1PlaneType)Math.Min(plane, 1);
        Point blockPosition = new(blockColumn, blockRow);
        bool isLossless = this.FrameHeader.LosslessArray[partitionInfo.ModeInfo.SegmentId];
        bool subX = this.SequenceHeader.ColorConfig.SubSamplingX;
        bool subY = this.SequenceHeader.ColorConfig.SubSamplingY;
        Av1BlockSize planeBlockSize = partitionInfo.ModeInfo.BlockSize.GetSubsampled(subX, subY);
        int blocksWide = partitionInfo.GetMaxBlockWide(planeBlockSize, subX);
        int blocksHigh = partitionInfo.GetMaxBlockHigh(planeBlockSize, subY);

        return reader.ReadCoefficients(partitionInfo.ModeInfo, blockPosition, this.aboveNeighborContext.GetContext(plane), this.leftNeighborContext.GetContext(plane), aboveOffset, leftOffset, plane, blocksWide, blocksHigh, transformBlockContext, transformSize, isLossless, this.FrameHeader.UseReducedTransformSet, transformInfo, partitionInfo.ModeBlockToRightEdge, partitionInfo.ModeBlockToBottomEdge, coefficientBuffer);
    }

    /// <summary>
    /// Derives coefficient skip and DC-sign contexts from the transform block's above and left neighbors.
    /// </summary>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <param name="planeBlockSize">The containing block size on the target plane.</param>
    /// <param name="transformBlockUnitHighCount">The transform height clipped to the frame in 4x4 units.</param>
    /// <param name="transformBlockUnitWideCount">The transform width clipped to the frame in 4x4 units.</param>
    /// <param name="startY">The frame-relative transform row in 4x4 units of the target plane.</param>
    /// <param name="startX">The frame-relative transform column in 4x4 units of the target plane.</param>
    /// <returns>The derived transform-block entropy contexts.</returns>
    private Av1TransformBlockContext GetTransformBlockContext(Av1TransformSize transformSize, int plane, Av1BlockSize planeBlockSize, int transformBlockUnitHighCount, int transformBlockUnitWideCount, int startY, int startX)
    {
        Av1TransformBlockContext transformBlockContext = new();
        int[] aboveContext = this.aboveNeighborContext.GetContext(plane);
        int[] leftContext = this.leftNeighborContext.GetContext(plane);
        int dcSign = 0;
        int k = 0;
        int mask = (1 << Av1Constants.CoefficientContextBitCount) - 1;

        // The high bits of each neighbor value encode its DC sign class. Summing both edges maps
        // negative, balanced, and positive neighborhoods to the AV1 DC-sign context.
        do
        {
            uint sign = (uint)aboveContext[k] >> Av1Constants.CoefficientContextBitCount;
            DebugGuard.MustBeLessThanOrEqualTo(sign, 2U, nameof(sign));
            dcSign += Signs[sign];
        }
        while (++k < transformBlockUnitWideCount);

        k = 0;
        do
        {
            uint sign = (uint)leftContext[k] >> Av1Constants.CoefficientContextBitCount;
            DebugGuard.MustBeLessThanOrEqualTo(sign, 2U, nameof(sign));
            dcSign += Signs[sign];
        }
        while (++k < transformBlockUnitHighCount);

        transformBlockContext.DcSignContext = DcSignContexts[dcSign + (Av1Constants.MaxTransformSizeUnit << 1)];

        if (plane == 0)
        {
            if (planeBlockSize == transformSize.ToBlockSize())
            {
                transformBlockContext.SkipContext = 0;
            }
            else
            {
                // Luma skip contexts preserve both the weakest neighboring level and whether either edge is stronger.
                int top = 0;
                int left = 0;

                k = 0;
                do
                {
                    top |= aboveContext[k];
                }
                while (++k < transformBlockUnitWideCount);
                top &= mask;

                k = 0;
                do
                {
                    left |= leftContext[k];
                }
                while (++k < transformBlockUnitHighCount);
                left &= mask;

                int max = Math.Min(top | left, 4);
                int min = Math.Min(Math.Min(top, left), 4);

                transformBlockContext.SkipContext = SkipContexts[min][max];
            }
        }
        else
        {
            // Chroma needs only the presence of nonzero levels on each edge, plus an offset that
            // distinguishes a transform smaller than its containing plane block.
            int contextBase = GetEntropyContext(transformSize, aboveContext, leftContext);
            int contextOffset = planeBlockSize.GetPelsLog2Count() > transformSize.ToBlockSize().GetPelsLog2Count() ? 10 : 7;
            transformBlockContext.SkipContext = contextBase + contextOffset;
        }

        return transformBlockContext;
    }

    /// <summary>
    /// Determines whether the above and left edges contain nonzero chroma coefficient contexts.
    /// </summary>
    /// <param name="transformSize">The transform size that selects how many edge entries to inspect.</param>
    /// <param name="above">The above coefficient contexts.</param>
    /// <param name="left">The left coefficient contexts.</param>
    /// <returns>The sum of the nonzero-above and nonzero-left flags.</returns>
    private static int GetEntropyContext(Av1TransformSize transformSize, int[] above, int[] left)
    {
        bool aboveEntropyContext = false;
        bool leftEntropyContext = false;

        // The reference implementation tests packed 16, 32, 64, or 128-bit edge groups. Enumerating
        // each transform shape keeps those exact edge widths without unaligned native memory reads.
        switch (transformSize)
        {
            case Av1TransformSize.Size4x4:
                aboveEntropyContext = above[0] != 0;
                leftEntropyContext = left[0] != 0;
                break;
            case Av1TransformSize.Size4x8:
                aboveEntropyContext = above[0] != 0;
                leftEntropyContext = (left[0] & (left[1] << 8)) != 0; // !!*(const uint16_t*)left;
                break;
            case Av1TransformSize.Size8x4:
                aboveEntropyContext = (above[0] & (above[1] << 8)) != 0; // !!*(const uint16_t*)above;
                leftEntropyContext = left[0] != 0;
                break;
            case Av1TransformSize.Size8x16:
                aboveEntropyContext = (above[0] & (above[1] << 8)) != 0; // !!*(const uint16_t*)above;
                leftEntropyContext = (left[0] & (left[1] << 8) & (left[2] << 16) & (left[3] << 24)) != 0; //  !!*(const uint32_t*)left;
                break;
            case Av1TransformSize.Size16x8:
                aboveEntropyContext = (above[0] & (above[1] << 8) & (above[2] << 16) & (above[3] << 24)) != 0; // !!*(const uint32_t*)above;
                leftEntropyContext = (left[0] & (left[1] << 8)) != 0; // !!*(const uint16_t*)left;
                break;
            case Av1TransformSize.Size16x32:
                aboveEntropyContext = (above[0] & (above[1] << 8) & (above[2] << 16) & (above[3] << 24)) != 0; // !!*(const uint32_t*)above;
                leftEntropyContext =
                    (left[0] & (left[1] << 8) & (left[2] << 16) & (left[3] << 24)) != 0 ||
                    (left[4] & (left[5] << 8) & (left[6] << 16) & (left[7] << 24)) != 0; // !!*(const uint64_t*)left;
                break;
            case Av1TransformSize.Size32x16:
                aboveEntropyContext =
                    (above[0] & (above[1] << 8) & (above[2] << 16) & (above[3] << 24)) != 0 ||
                    (above[4] & (above[5] << 8) & (above[6] << 16) & (above[7] << 24)) != 0; // !!*(const uint64_t*)above;
                leftEntropyContext = (left[0] & (left[1] << 8) & (left[2] << 16) & (left[3] << 24)) != 0; // !!*(const uint32_t*)left;
                break;
            case Av1TransformSize.Size8x8:
                aboveEntropyContext = (above[0] & (above[1] << 8)) != 0; // !!*(const uint16_t*)above;
                leftEntropyContext = (left[0] & (left[1] << 8)) != 0; // !!*(const uint16_t*)left;
                break;
            case Av1TransformSize.Size16x16:
                aboveEntropyContext = (above[0] & (above[1] << 8) & (above[2] << 16) & (above[3] << 24)) != 0; // !!*(const uint32_t*)above;
                leftEntropyContext = (left[0] & (left[1] << 8) & (left[2] << 16) & (left[3] << 24)) != 0; // !!*(const uint32_t*)left;
                break;
            case Av1TransformSize.Size32x32:
                aboveEntropyContext =
                    (above[0] & (above[1] << 8) & (above[2] << 16) & (above[3] << 24)) != 0 ||
                    (above[4] & (above[5] << 8) & (above[6] << 16) & (above[7] << 24)) != 0; // !!*(const uint64_t*)above;
                leftEntropyContext =
                    (left[0] & (left[1] << 8) & (left[2] << 16) & (left[3] << 24)) != 0 ||
                    (left[4] & (left[5] << 8) & (left[6] << 16) & (left[7] << 24)) != 0; // !!*(const uint64_t*)left;
                break;
            case Av1TransformSize.Size64x64:
                aboveEntropyContext =
                    (above[0] & (above[1] << 8) & (above[2] << 16) & (above[3] << 24)) != 0 ||
                    (above[4] & (above[5] << 8) & (above[6] << 16) & (above[7] << 24)) != 0 ||
                    (above[8] & (above[9] << 8) & (above[10] << 16) & (above[11] << 24)) != 0 ||
                    (above[12] & (above[13] << 8) & (above[14] << 16) & (above[15] << 24)) != 0; // !!(*(const uint64_t*)above | *(const uint64_t*)(above + 8));
                leftEntropyContext =
                    (left[0] & (left[1] << 8) & (left[2] << 16) & (left[3] << 24)) != 0 ||
                    (left[4] & (left[5] << 8) & (left[6] << 16) & (left[7] << 24)) != 0 ||
                    (left[8] & (left[9] << 8) & (left[10] << 16) & (left[11] << 24)) != 0 ||
                    (left[12] & (left[13] << 8) & (left[14] << 16) & (left[15] << 24)) != 0; // !!(*(const uint64_t*)left | *(const uint64_t*)(left + 8));
                break;
            case Av1TransformSize.Size32x64:
                aboveEntropyContext =
                    (above[0] & (above[1] << 8) & (above[2] << 16) & (above[3] << 24)) != 0 ||
                    (above[4] & (above[5] << 8) & (above[6] << 16) & (above[7] << 24)) != 0; // !!*(const uint64_t*)above;
                leftEntropyContext =
                    (left[0] & (left[1] << 8) & (left[2] << 16) & (left[3] << 24)) != 0 ||
                    (left[4] & (left[5] << 8) & (left[6] << 16) & (left[7] << 24)) != 0 ||
                    (left[8] & (left[9] << 8) & (left[10] << 16) & (left[11] << 24)) != 0 ||
                    (left[12] & (left[13] << 8) & (left[14] << 16) & (left[15] << 24)) != 0; // !!(*(const uint64_t*)left | *(const uint64_t*)(left + 8));
                break;
            case Av1TransformSize.Size64x32:
                aboveEntropyContext =
                    (above[0] & (above[1] << 8) & (above[2] << 16) & (above[3] << 24)) != 0 ||
                    (above[4] & (above[5] << 8) & (above[6] << 16) & (above[7] << 24)) != 0 ||
                    (above[8] & (above[9] << 8) & (above[10] << 16) & (above[11] << 24)) != 0 ||
                    (above[12] & (above[13] << 8) & (above[14] << 16) & (above[15] << 24)) != 0; // !!(*(const uint64_t*)above | *(const uint64_t*)(above + 8));
                leftEntropyContext =
                    (left[0] & (left[1] << 8) & (left[2] << 16) & (left[3] << 24)) != 0 ||
                    (left[4] & (left[5] << 8) & (left[6] << 16) & (left[7] << 24)) != 0; // !!*(const uint64_t*)left;
                break;
            case Av1TransformSize.Size4x16:
                aboveEntropyContext = above[0] != 0;
                leftEntropyContext = (left[0] & (left[1] << 8) & (left[2] << 16) & (left[3] << 24)) != 0; // !!*(const uint32_t*)left;
                break;
            case Av1TransformSize.Size16x4:
                aboveEntropyContext = (above[0] & (above[1] << 8) & (above[2] << 16) & (above[3] << 24)) != 0; // !!*(const uint32_t*)above;
                leftEntropyContext = left[0] != 0;
                break;
            case Av1TransformSize.Size8x32:
                aboveEntropyContext = (above[0] & (above[1] << 8)) != 0; // !!*(const uint16_t*)above;
                leftEntropyContext =
                    (left[0] & (left[1] << 8) & (left[2] << 16) & (left[3] << 24)) != 0 ||
                    (left[4] & (left[5] << 8) & (left[6] << 16) & (left[7] << 24)) != 0; // !!*(const uint64_t*)left;
                break;
            case Av1TransformSize.Size32x8:
                aboveEntropyContext =
                    (above[0] & (above[1] << 8) & (above[2] << 16) & (above[3] << 24)) != 0 ||
                    (above[4] & (above[5] << 8) & (above[6] << 16) & (above[7] << 24)) != 0; // !!*(const uint64_t*)above;
                leftEntropyContext = (left[0] & (left[1] << 8)) != 0; // !!*(const uint16_t*)left;
                break;
            case Av1TransformSize.Size16x64:
                aboveEntropyContext = (above[0] & (above[1] << 8) & (above[2] << 16) & (above[3] << 24)) != 0; // !!*(const uint32_t*)above;
                leftEntropyContext =
                    (left[0] & (left[1] << 8) & (left[2] << 16) & (left[3] << 24)) != 0 ||
                    (left[4] & (left[5] << 8) & (left[6] << 16) & (left[7] << 24)) != 0 ||
                    (left[8] & (left[9] << 8) & (left[10] << 16) & (left[11] << 24)) != 0 ||
                    (left[12] & (left[13] << 8) & (left[14] << 16) & (left[15] << 24)) != 0; // !!(*(const uint64_t*)left | *(const uint64_t*)(left + 8));
                break;
            case Av1TransformSize.Size64x16:
                aboveEntropyContext =
                    (above[0] & (above[1] << 8) & (above[2] << 16) & (above[3] << 24)) != 0 ||
                    (above[4] & (above[5] << 8) & (above[6] << 16) & (above[7] << 24)) != 0 ||
                    (above[8] & (above[9] << 8) & (above[10] << 16) & (above[11] << 24)) != 0 ||
                    (above[12] & (above[13] << 8) & (above[14] << 16) & (above[15] << 24)) != 0; // !!(*(const uint64_t*)above | *(const uint64_t*)(above + 8));
                leftEntropyContext = (left[0] & (left[1] << 8) & (left[2] << 16) & (left[3] << 24)) != 0; // !!*(const uint32_t*)left;
                break;
            default:
                Guard.IsTrue(false, nameof(transformSize), "Invalid transform size.");
                break;
        }

        return (aboveEntropyContext ? 1 : 0) + (leftEntropyContext ? 1 : 0);
    }

    /// <summary>
    /// Selects the transform size for a coding block from lossless, explicit-selection, or maximum-size rules.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <param name="superblockInfo">The containing superblock.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="allowSelect">A value indicating whether transform-size selection syntax is allowed at this node.</param>
    /// <returns>The selected transform size.</returns>
    /// <remarks>Implements AV1 section 5.11.15.</remarks>
    private Av1TransformSize ReadTransformSize(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo, Av1SuperblockInfo superblockInfo, Av1TileInfo tileInfo, bool allowSelect)
    {
        Av1BlockModeInfo modeInfo = partitionInfo.ModeInfo;
        if (this.FrameHeader.LosslessArray[modeInfo.SegmentId])
        {
            return Av1TransformSize.Size4x4;
        }

        if (modeInfo.BlockSize > Av1BlockSize.Block4x4 && allowSelect && this.FrameHeader.TransformMode == Av1TransformMode.Select)
        {
            return this.ReadSelectedTransformSize(ref reader, partitionInfo, superblockInfo, tileInfo);
        }

        return modeInfo.BlockSize.GetMaximumTransformSize();
    }

    /// <summary>
    /// Reads a transform size using the available above and left transform-size contexts.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <param name="superblockInfo">The containing superblock.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <returns>The decoded transform size.</returns>
    private Av1TransformSize ReadSelectedTransformSize(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo, Av1SuperblockInfo superblockInfo, Av1TileInfo tileInfo)
    {
        int context = 0;
        Av1TransformSize maxTransformSize = partitionInfo.ModeInfo.BlockSize.GetMaximumTransformSize();
        int aboveWidth = this.aboveNeighborContext.AboveTransformWidth[partitionInfo.ColumnIndex - tileInfo.ModeInfoColumnStart];
        int above = (aboveWidth >= maxTransformSize.GetWidth()) ? 1 : 0;
        int leftHeight = this.leftNeighborContext.LeftTransformHeight[partitionInfo.RowIndex - superblockInfo.ModeInfoPosition.Y];
        int left = (leftHeight >= maxTransformSize.GetHeight()) ? 1 : 0;
        bool hasAbove = partitionInfo.AvailableAbove;
        bool hasLeft = partitionInfo.AvailableLeft;

        if (hasAbove && hasLeft)
        {
            context = above + left;
        }
        else if (hasAbove)
        {
            context = above;
        }
        else if (hasLeft)
        {
            context = left;
        }
        else
        {
            context = 0;
        }

        return reader.ReadTransformSize(partitionInfo.ModeInfo.BlockSize, context);
    }

    /// <summary>
    /// Reads a coding block's transform size, updates neighbor contexts, and creates its transform geometry records.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="modeInfoLocation">The block origin in 4x4 mode-information units.</param>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <param name="superblockInfo">The containing superblock.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <remarks>Implements AV1 section 5.11.16 and corresponds to <c>read_block_tx_size</c> in SVT-AV1.</remarks>
    private void ReadBlockTransformSize(ref Av1SymbolDecoder reader, Point modeInfoLocation, Av1PartitionInfo partitionInfo, Av1SuperblockInfo superblockInfo, Av1TileInfo tileInfo)
    {
        Av1BlockSize blockSize = partitionInfo.ModeInfo.BlockSize;
        int block4x4Width = blockSize.Get4x4WideCount();
        int block4x4Height = blockSize.Get4x4HighCount();

        // HEIF still-image decoding follows the independently decodable intra-frame transform-size branch.
        Av1TransformSize transformSize = this.ReadTransformSize(ref reader, partitionInfo, superblockInfo, tileInfo, true);
        this.aboveNeighborContext.UpdateTransformation(modeInfoLocation, tileInfo, transformSize, blockSize, false);
        this.leftNeighborContext.UpdateTransformation(modeInfoLocation, superblockInfo, transformSize, blockSize, false);
        this.UpdateTransformInfo(partitionInfo, superblockInfo, blockSize, transformSize);
    }

    /// <summary>
    /// Populates luma and chroma transform-information records in residual traversal order.
    /// </summary>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <param name="superblockInfo">The containing superblock and transform storage.</param>
    /// <param name="blockSize">The coding block size.</param>
    /// <param name="transformSize">The selected luma transform size.</param>
    private unsafe void UpdateTransformInfo(Av1PartitionInfo partitionInfo, Av1SuperblockInfo superblockInfo, Av1BlockSize blockSize, Av1TransformSize transformSize)
    {
        int transformInfoYIndex = partitionInfo.ModeInfo.FirstTransformLocation[(int)Av1PlaneType.Y];
        int transformInfoUvIndex = partitionInfo.ModeInfo.FirstTransformLocation[(int)Av1PlaneType.Uv];
        Span<Av1TransformInfo> lumaTransformInfo = superblockInfo.GetTransformInfoY();
        Span<Av1TransformInfo> chromaTransformInfo = superblockInfo.GetTransformInfoUv();
        int totalLumaTransformUnitCount = 0;
        int totalChromaTransformUnitCount = 0;
        int forceSplitCount = 0;
        bool subX = this.SequenceHeader.ColorConfig.SubSamplingX;
        bool subY = this.SequenceHeader.ColorConfig.SubSamplingY;
        int maxBlockWide = partitionInfo.GetMaxBlockWide(blockSize, false);
        int maxBlockHigh = partitionInfo.GetMaxBlockHigh(blockSize, false);
        int width = 64 >> 2;
        int height = 64 >> 2;
        width = Math.Min(width, maxBlockWide);
        height = Math.Min(height, maxBlockHigh);

        bool isLossLess = this.FrameHeader.LosslessArray[partitionInfo.ModeInfo.SegmentId];
        Av1TransformSize transformSizeUv = isLossLess ? Av1TransformSize.Size4x4 : blockSize.GetMaxUvTransformSize(subX, subY);

        // Residual syntax visits at most 64x64 luma regions. Record transform geometry in the same
        // nested region/row/column order so coefficient parsing and reconstruction consume matching spans.
        for (int idy = 0; idy < maxBlockHigh; idy += height)
        {
            for (int idx = 0; idx < maxBlockWide; idx += width, forceSplitCount++)
            {
                int lumaTransformUnitCount = 0;
                int chromaTransformUnitCount = 0;

                // Luma transform offsets remain relative to the coding block in 4x4 luma units.
                int stepColumn = transformSize.Get4x4WideCount();
                int stepRow = transformSize.Get4x4HighCount();

                int unitHeight = Av1Math.RoundPowerOf2(Math.Min(height + idy, maxBlockHigh), 0);
                int unitWidth = Av1Math.RoundPowerOf2(Math.Min(width + idx, maxBlockWide), 0);
                for (int blockRow = idy; blockRow < unitHeight; blockRow += stepRow)
                {
                    for (int blockColumn = idx; blockColumn < unitWidth; blockColumn += stepColumn)
                    {
                        lumaTransformInfo[transformInfoYIndex] = new Av1TransformInfo(
                            transformSize, blockColumn, blockRow);
                        transformInfoYIndex++;
                        lumaTransformUnitCount++;
                        totalLumaTransformUnitCount++;
                    }
                }

                this.transformUnitCount[(int)Av1Plane.Y][forceSplitCount] = lumaTransformUnitCount;

                if (this.SequenceHeader.ColorConfig.IsMonochrome || !partitionInfo.IsChroma)
                {
                    continue;
                }

                // Chroma geometry is rounded to the subsampling grid before stepping its transform size.
                stepColumn = transformSizeUv.Get4x4WideCount();
                stepRow = transformSizeUv.Get4x4HighCount();

                unitHeight = Av1Math.RoundPowerOf2(Math.Min(height + idx, maxBlockHigh), subY ? 1 : 0);
                unitWidth = Av1Math.RoundPowerOf2(Math.Min(width + idx, maxBlockWide), subX ? 1 : 0);
                for (int blockRow = idy; blockRow < unitHeight; blockRow += stepRow)
                {
                    for (int blockColumn = idx; blockColumn < unitWidth; blockColumn += stepColumn)
                    {
                        chromaTransformInfo[transformInfoUvIndex] = new Av1TransformInfo(
                            transformSizeUv, blockColumn, blockRow);
                        transformInfoUvIndex++;
                        chromaTransformUnitCount++;
                        totalChromaTransformUnitCount++;
                    }
                }

                this.transformUnitCount[(int)Av1Plane.U][forceSplitCount] = chromaTransformUnitCount;
                this.transformUnitCount[(int)Av1Plane.V][forceSplitCount] = chromaTransformUnitCount;
            }
        }

        // U and V share transform geometry, so append a second copy for V after the complete U sequence.
        if (totalChromaTransformUnitCount != 0)
        {
            DebugGuard.IsTrue(
                (transformInfoUvIndex - totalChromaTransformUnitCount) ==
                partitionInfo.ModeInfo.FirstTransformLocation[(int)Av1PlaneType.Uv],
                nameof(totalChromaTransformUnitCount));
            int originalIndex = transformInfoUvIndex - totalChromaTransformUnitCount;
            ref Av1TransformInfo originalInfo = ref chromaTransformInfo[originalIndex];
            ref Av1TransformInfo infoV = ref chromaTransformInfo[transformInfoUvIndex];
            for (int i = 0; i < totalChromaTransformUnitCount; i++)
            {
                // U and V share transform geometry, but their entropy state and coefficients remain independent.
                infoV = new Av1TransformInfo(originalInfo);
                originalInfo = ref Unsafe.Add(ref originalInfo, 1);
                infoV = ref Unsafe.Add(ref infoV, 1);
            }
        }

        partitionInfo.ModeInfo.TransformUnitsCount[(int)Av1PlaneType.Y] = totalLumaTransformUnitCount;
        partitionInfo.ModeInfo.TransformUnitsCount[(int)Av1PlaneType.Uv] = totalChromaTransformUnitCount;

        this.firstTransformOffset[(int)Av1PlaneType.Y] += totalLumaTransformUnitCount;
        this.firstTransformOffset[(int)Av1PlaneType.Uv] += totalChromaTransformUnitCount << 1;
    }

    /// <summary>
    /// Reads luma and chroma palette-map tokens when a block selects palette prediction.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <exception cref="NotImplementedException">The block selects a nonempty luma or chroma palette.</exception>
    /// <remarks>Implements AV1 section 5.11.49.</remarks>
    private static void ReadPaletteTokens(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
    {
        if (partitionInfo.ModeInfo.GetPaletteSize(Av1PlaneType.Y) != 0)
        {
            throw new NotImplementedException();
        }

        if (partitionInfo.ModeInfo.GetPaletteSize(Av1PlaneType.Uv) != 0)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Reads the prediction, segmentation, skip, quantizer, and filter mode information for a still-image block.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <remarks>Implements the intra-frame branch of AV1 section 5.11.6.</remarks>
    private void ReadModeInfo(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
    {
        DebugGuard.IsTrue(this.FrameHeader.FrameType is ObuFrameType.KeyFrame or ObuFrameType.IntraOnlyFrame, "Only INTRA frames supported.");
        this.ReadIntraFrameModeInfo(ref reader, partitionInfo);
    }

    /// <summary>
    /// Reads all intra-frame mode syntax for a coding block in bitstream order.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block and its neighbors.</param>
    /// <remarks>Implements AV1 section 5.11.7.</remarks>
    private void ReadIntraFrameModeInfo(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
    {
        if (this.FrameHeader.SegmentationParameters.SegmentIdPrecedesSkip)
        {
            this.IntraSegmentId(ref reader, partitionInfo);
        }

        partitionInfo.ModeInfo.Skip = this.ReadSkip(ref reader, partitionInfo);
        if (!this.FrameHeader.SegmentationParameters.SegmentIdPrecedesSkip)
        {
            this.IntraSegmentId(ref reader, partitionInfo);
        }

        this.ReadCdef(ref reader, partitionInfo);

        if (this.FrameHeader.DeltaQParameters.IsPresent)
        {
            this.ReadDeltaQuantizerIndex(ref reader, partitionInfo);
            this.ReadDeltaLoopFilter(ref reader, partitionInfo);
        }

        // Independently decodable still-image blocks reference only the current intra frame.
        partitionInfo.ReferenceFrame[0] = 0;
        partitionInfo.ReferenceFrame[1] = -1;
        partitionInfo.ModeInfo.SetPaletteSizes(0, 0);
        bool useIntraBlockCopy = false;
        if (this.AllowIntraBlockCopy())
        {
            useIntraBlockCopy = reader.ReadUseIntraBlockCopy();
        }

        if (useIntraBlockCopy)
        {
            partitionInfo.ModeInfo.YMode = Av1PredictionMode.DC;
            partitionInfo.ModeInfo.UvMode = Av1PredictionMode.DC;
        }
        else
        {
            partitionInfo.ModeInfo.YMode = reader.ReadYMode(partitionInfo.AboveModeInfo, partitionInfo.LeftModeInfo);

            partitionInfo.ModeInfo.AngleDelta[(int)Av1PlaneType.Y] = IntraAngleInfo(ref reader, partitionInfo.ModeInfo.YMode, partitionInfo.ModeInfo.BlockSize);
            if (partitionInfo.IsChroma && !this.SequenceHeader.ColorConfig.IsMonochrome)
            {
                partitionInfo.ModeInfo.UvMode = reader.ReadIntraModeUv(partitionInfo.ModeInfo.YMode, this.IsChromaForLumaAllowed(partitionInfo));
                if (partitionInfo.ModeInfo.UvMode == Av1PredictionMode.UvChromaFromLuma)
                {
                    ReadChromaFromLumaAlphas(ref reader, partitionInfo.ModeInfo);
                }

                partitionInfo.ModeInfo.AngleDelta[(int)Av1PlaneType.Uv] = IntraAngleInfo(ref reader, partitionInfo.ModeInfo.UvMode, partitionInfo.ModeInfo.BlockSize);
            }
            else
            {
                partitionInfo.ModeInfo.UvMode = Av1PredictionMode.DC;
            }

            if (partitionInfo.ModeInfo.BlockSize >= Av1BlockSize.Block8x8 &&
                partitionInfo.ModeInfo.BlockSize.GetWidth() <= 64 &&
                partitionInfo.ModeInfo.BlockSize.GetHeight() <= 64 &&
                this.FrameHeader.AllowScreenContentTools)
            {
                this.PaletteModeInfo(ref reader, partitionInfo);
            }

            this.FilterIntraModeInfo(ref reader, partitionInfo);
        }
    }

    /// <summary>
    /// Determines whether the frame header permits intra block copy for an intra still image.
    /// </summary>
    /// <returns><see langword="true"/> when the frame and sequence enable intra block copy; otherwise, <see langword="false"/>.</returns>
    private bool AllowIntraBlockCopy()
        => (this.FrameHeader.FrameType is ObuFrameType.KeyFrame or ObuFrameType.IntraOnlyFrame) &&
            (this.SequenceHeader.ForceScreenContentTools > 0) &&
            this.FrameHeader.AllowIntraBlockCopy;

    /// <summary>
    /// Determines whether chroma-from-luma prediction is available for a coding block.
    /// </summary>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <returns><see langword="true"/> when the lossless transform or block dimensions permit chroma-from-luma prediction; otherwise, <see langword="false"/>.</returns>
    private bool IsChromaForLumaAllowed(Av1PartitionInfo partitionInfo)
    {
        if (this.FrameHeader.LosslessArray[partitionInfo.ModeInfo.SegmentId])
        {
            // Lossless mode fixes transforms at 4x4, so CfL is available only when the subsampled
            // plane block is itself 4x4 and therefore has no smaller transform partition.
            bool subX = this.SequenceHeader.ColorConfig.SubSamplingX;
            bool subY = this.SequenceHeader.ColorConfig.SubSamplingY;
            Av1BlockSize planeBlockSize = partitionInfo.ModeInfo.BlockSize.GetSubsampled(subX, subY);
            return planeBlockSize == Av1BlockSize.Block4x4;
        }

        // Outside lossless mode, AV1 limits CfL to luma blocks no larger than 32x32.
        return partitionInfo.ModeInfo.BlockSize.GetWidth() <= 32 && partitionInfo.ModeInfo.BlockSize.GetHeight() <= 32;
    }

    /// <summary>
    /// Reads filter-intra selection for an eligible DC-predicted luma block.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block.</param>
    private void FilterIntraModeInfo(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
    {
        partitionInfo.ModeInfo.FilterIntraModeInfo.UseFilterIntra = false;
        if (this.SequenceHeader.EnableFilterIntra &&
            partitionInfo.ModeInfo.YMode == Av1PredictionMode.DC &&
            partitionInfo.ModeInfo.GetPaletteSize(Av1PlaneType.Y) == 0 &&
            Math.Max(partitionInfo.ModeInfo.BlockSize.GetWidth(), partitionInfo.ModeInfo.BlockSize.GetHeight()) <= 32)
        {
            Av1FilterIntraMode filterIntraMode = reader.ReadFilterUltraMode(partitionInfo.ModeInfo.BlockSize);
            if (filterIntraMode != Av1FilterIntraMode.AllFilterIntraModes)
            {
                partitionInfo.ModeInfo.FilterIntraModeInfo.UseFilterIntra = true;
                partitionInfo.ModeInfo.FilterIntraModeInfo.Mode = filterIntraMode;
            }
        }
    }

    /// <summary>
    /// Reads palette size and color syntax for an eligible screen-content block.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <exception cref="NotImplementedException">Palette-mode syntax is not implemented.</exception>
    /// <remarks>Implements AV1 section 5.11.46.</remarks>
    private void PaletteModeInfo(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
        => throw new NotImplementedException();

    /// <summary>
    /// Reads the joint signs and nonzero alpha magnitudes for chroma-from-luma prediction.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="modeInfo">The block mode information to populate.</param>
    /// <remarks>Implements AV1 section 5.11.45.</remarks>
    private static void ReadChromaFromLumaAlphas(ref Av1SymbolDecoder reader, Av1BlockModeInfo modeInfo)
    {
        int jointSignPlus1 = reader.ReadChromFromLumaSign() + 1;
        int index = 0;
        if (jointSignPlus1 >= 3)
        {
            index = reader.ReadChromaFromLumaAlphaU(jointSignPlus1) << Av1Constants.ChromaFromLumaAlphabetSizeLog2;
        }

        if (jointSignPlus1 % 3 != 0)
        {
            index += reader.ReadChromaFromLumaAlphaV(jointSignPlus1);
        }

        modeInfo.ChromaFromLumaAlphaSign = jointSignPlus1 - 1;
        modeInfo.ChromaFromLumaAlphaIndex = index;
    }

    /// <summary>
    /// Reads a directional intra-prediction angle adjustment when the block and mode permit one.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="mode">The selected luma or chroma prediction mode.</param>
    /// <param name="blockSize">The block size.</param>
    /// <returns>The signed angle adjustment.</returns>
    /// <remarks>Implements AV1 sections 5.11.42 and 5.11.43.</remarks>
    private static int IntraAngleInfo(ref Av1SymbolDecoder reader, Av1PredictionMode mode, Av1BlockSize blockSize)
    {
        int angleDelta = 0;
        if (blockSize >= Av1BlockSize.Block8x8 && IsDirectionalMode(mode))
        {
            int symbol = reader.ReadAngleDelta(mode);
            angleDelta = symbol - Av1Constants.MaxAngleDelta;
        }

        return angleDelta;
    }

    /// <summary>
    /// Determines whether a prediction mode belongs to the AV1 directional-mode range.
    /// </summary>
    /// <param name="mode">The prediction mode.</param>
    /// <returns><see langword="true"/> for a directional mode; otherwise, <see langword="false"/>.</returns>
    private static bool IsDirectionalMode(Av1PredictionMode mode)
        => mode is >= Av1PredictionMode.Vertical and <= Av1PredictionMode.Directional67Degrees;

    /// <summary>
    /// Reads or inherits a segment identifier and writes it over every 4x4 position covered by the block.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <remarks>Implements AV1 section 5.11.8.</remarks>
    private void IntraSegmentId(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
    {
        if (this.FrameHeader.SegmentationParameters.Enabled)
        {
            this.ReadSegmentId(ref reader, partitionInfo);
        }

        int blockWidth4x4 = partitionInfo.ModeInfo.BlockSize.Get4x4WideCount();
        int blockHeight4x4 = partitionInfo.ModeInfo.BlockSize.Get4x4HighCount();
        int modeInfoCountX = Math.Min(this.FrameHeader.ModeInfoColumnCount - partitionInfo.ColumnIndex, blockWidth4x4);
        int modeInfoCountY = Math.Min(this.FrameHeader.ModeInfoRowCount - partitionInfo.RowIndex, blockHeight4x4);
        int segmentId = partitionInfo.ModeInfo.SegmentId;

        // Later blocks predict from 4x4 positions, so replicate one block ID over its clipped frame coverage.
        for (int y = 0; y < modeInfoCountY; y++)
        {
            int[] segmentRow = this.segmentIds[partitionInfo.RowIndex + y];
            for (int x = 0; x < modeInfoCountX; x++)
            {
                segmentRow[partitionInfo.ColumnIndex + x] = segmentId;
            }
        }
    }

    /// <summary>
    /// Predicts and, when required, decodes the segment identifier for an intra block.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block and its available neighbors.</param>
    /// <remarks>Implements AV1 section 5.11.9.</remarks>
    private void ReadSegmentId(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
    {
        int predictor;
        int prevUL = -1;
        int prevU = -1;
        int prevL = -1;
        int columnIndex = partitionInfo.ColumnIndex;
        int rowIndex = partitionInfo.RowIndex;
        if (partitionInfo.AvailableAbove && partitionInfo.AvailableLeft)
        {
            prevUL = Av1SymbolContextHelper.GetSegmentId(this.segmentIds, rowIndex - 1, columnIndex - 1);
        }

        if (partitionInfo.AvailableAbove)
        {
            prevU = Av1SymbolContextHelper.GetSegmentId(this.segmentIds, rowIndex - 1, columnIndex);
        }

        if (partitionInfo.AvailableLeft)
        {
            prevL = Av1SymbolContextHelper.GetSegmentId(this.segmentIds, rowIndex, columnIndex - 1);
        }

        if (prevU == -1)
        {
            predictor = prevL == -1 ? 0 : prevL;
        }
        else if (prevL == -1)
        {
            predictor = prevU;
        }
        else
        {
            predictor = prevU == prevUL ? prevU : prevL;
        }

        if (partitionInfo.ModeInfo.Skip)
        {
            partitionInfo.ModeInfo.SegmentId = predictor;
        }
        else
        {
            // Any unavailable neighbor selects the edge context; otherwise, agreement among two
            // or three neighbors increases the specificity of the segment-ID distribution.
            int ctx = prevUL < 0 ? 0
                : prevUL == prevU && prevUL == prevL ? 2
                : prevUL == prevU || prevUL == prevL || prevU == prevL ? 1 : 0;
            int lastActiveSegmentId = this.FrameHeader.SegmentationParameters.LastActiveSegmentId;
            partitionInfo.ModeInfo.SegmentId = Av1SymbolContextHelper.NegativeDeinterleave(reader.ReadSegmentId(ctx), predictor, lastActiveSegmentId + 1);
        }
    }

    /// <summary>
    /// Reads the constrained directional enhancement filter strength for the block's 64x64 filter unit.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block.</param>
    /// <remarks>Implements AV1 section 5.11.56 and corresponds to <c>read_cdef</c> in SVT-AV1.</remarks>
    private void ReadCdef(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
    {
        if (partitionInfo.ModeInfo.Skip || this.FrameHeader.CodedLossless || !this.SequenceHeader.EnableCdef || this.FrameHeader.AllowIntraBlockCopy)
        {
            return;
        }

        int cdefSize4 = Av1BlockSize.Block64x64.Get4x4WideCount();
        int row = partitionInfo.RowIndex & cdefSize4;
        int col = partitionInfo.ColumnIndex & cdefSize4;
        int index = this.SequenceHeader.SuperblockSize == Av1BlockSize.Block128x128 ? Math.Max(1, col) + (Math.Max(1, row) << 1) : 0;
        if (partitionInfo.CdefStrength[index] == -1)
        {
            int cdfStrength = reader.ReadCdfStrength(this.FrameHeader.CdefParameters.BitCount);
            partitionInfo.CdefStrength[index] = cdfStrength;

            // A block in a 128x128 superblock can cover multiple 64x64 CDEF units. Replicate the
            // first decoded strength so subsequent blocks in every covered unit observe it as assigned.
            if (this.SequenceHeader.SuperblockSize == Av1BlockSize.Block128x128)
            {
                int w4 = partitionInfo.ModeInfo.BlockSize.Get4x4WideCount();
                int h4 = partitionInfo.ModeInfo.BlockSize.Get4x4HighCount();
                for (int i = row; i < row + h4; i += cdefSize4)
                {
                    for (int j = col; j < col + w4; j += cdefSize4)
                    {
                        partitionInfo.CdefStrength[Math.Max(1, j & cdefSize4) + (Math.Max(1, i & cdefSize4) << 1)] = cdfStrength;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Reads and accumulates the loop-filter delta values carried by a coding block.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block and superblock delta storage.</param>
    private void ReadDeltaLoopFilter(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
    {
        Av1BlockSize superBlockSize = this.SequenceHeader.Use128x128Superblock ? Av1BlockSize.Block128x128 : Av1BlockSize.Block64x64;
        if (this.FrameHeader.DeltaLoopFilterParameters.IsPresent ||
            (partitionInfo.ModeInfo.BlockSize == superBlockSize && partitionInfo.ModeInfo.Skip))
        {
            return;
        }

        if (this.FrameHeader.DeltaLoopFilterParameters.IsPresent)
        {
            int frameLoopFilterCount = 1;
            if (this.FrameHeader.DeltaLoopFilterParameters.IsMulti)
            {
                frameLoopFilterCount = this.SequenceHeader.ColorConfig.PlaneCount > 1 ? Av1Constants.FrameLoopFilterCount : Av1Constants.FrameLoopFilterCount - 2;
            }

            Span<int> currentDeltaLoopFilter = partitionInfo.SuperblockInfo.SuperblockDeltaLoopFilter;
            for (int i = 0; i < frameLoopFilterCount; i++)
            {
                int reducedDeltaLoopFilterLevel = reader.ReadDeltaLoopFilter();
                int deltaLoopFilterResolution = this.FrameHeader.DeltaLoopFilterParameters.Resolution;
                currentDeltaLoopFilter[i] = Av1Math.Clip3(-Av1Constants.MaxLoopFilter, Av1Constants.MaxLoopFilter, currentDeltaLoopFilter[i] + (reducedDeltaLoopFilterLevel << deltaLoopFilterResolution));
            }
        }
    }

    /// <summary>
    /// Reads or infers the residual-skip flag for a coding block.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block and its available neighbors.</param>
    /// <returns><see langword="true"/> when the block omits residual coefficients; otherwise, <see langword="false"/>.</returns>
    private bool ReadSkip(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
    {
        int segmentId = partitionInfo.ModeInfo.SegmentId;
        if (this.FrameHeader.SegmentationParameters.SegmentIdPrecedesSkip &&
            this.FrameHeader.SegmentationParameters.IsFeatureActive(segmentId, ObuSegmentationLevelFeature.Skip))
        {
            return true;
        }
        else
        {
            int aboveSkip = partitionInfo.AboveModeInfo != null && partitionInfo.AboveModeInfo.Skip ? 1 : 0;
            int leftSkip = partitionInfo.LeftModeInfo != null && partitionInfo.LeftModeInfo.Skip ? 1 : 0;
            return reader.ReadSkip(aboveSkip + leftSkip);
        }
    }

    /// <summary>
    /// Reads and accumulates a superblock quantizer-index delta when the block carries one.
    /// </summary>
    /// <param name="reader">The tile symbol decoder.</param>
    /// <param name="partitionInfo">The current coding block and superblock quantizer storage.</param>
    /// <remarks>Corresponds to <c>read_delta_qindex</c> in SVT-AV1.</remarks>
    private void ReadDeltaQuantizerIndex(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
    {
        Av1BlockSize superBlockSize = this.SequenceHeader.Use128x128Superblock ? Av1BlockSize.Block128x128 : Av1BlockSize.Block64x64;
        if (!this.FrameHeader.DeltaQParameters.IsPresent ||
            (partitionInfo.ModeInfo.BlockSize == superBlockSize && partitionInfo.ModeInfo.Skip))
        {
            return;
        }

        if (partitionInfo.ModeInfo.BlockSize != this.SequenceHeader.SuperblockSize || !partitionInfo.ModeInfo.Skip)
        {
            int reducedDeltaQuantizerIndex = reader.ReadDeltaQuantizerIndex();
            int deltaQuantizerResolution = this.FrameHeader.DeltaQParameters.Resolution;
            this.currentQuantizerIndex = Av1Math.Clip3(1, 255, this.currentQuantizerIndex + (reducedDeltaQuantizerIndex << deltaQuantizerResolution));
            partitionInfo.SuperblockInfo.SuperblockDeltaQ = this.currentQuantizerIndex;
        }
    }

    /// <summary>
    /// Determines whether a frame-relative mode-information position lies inside the active tile.
    /// </summary>
    /// <param name="rowIndex">The frame-relative mode-information row.</param>
    /// <param name="columnIndex">The frame-relative mode-information column.</param>
    /// <returns><see langword="true"/> when the position lies within the active tile; otherwise, <see langword="false"/>.</returns>
    private bool IsInside(int rowIndex, int columnIndex) =>
        columnIndex >= this.FrameHeader.TilesInfo.TileColumnCount &&
        columnIndex < this.FrameHeader.TilesInfo.TileColumnCount &&
        rowIndex >= this.FrameHeader.TilesInfo.TileRowCount &&
        rowIndex < this.FrameHeader.TilesInfo.TileRowCount;

    /// <summary>
    /// Derives the partition entropy context from the current split bit of the above and left neighbors.
    /// </summary>
    /// <param name="location">The partition origin in 4x4 mode-information units.</param>
    /// <param name="blockSize">The square parent block size.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="superblockInfo">The containing superblock.</param>
    /// <returns>The partition entropy context.</returns>
    /// <remarks>Corresponds to <c>partition_plane_context</c> in SVT-AV1.</remarks>
    private int GetPartitionPlaneContext(Point location, Av1BlockSize blockSize, Av1TileInfo tileInfo, Av1SuperblockInfo superblockInfo)
    {
        // The five stored split bits begin at the 8x8 partition point, so normalize the block-size log to that bit index.
        int aboveCtx = this.aboveNeighborContext.AbovePartitionWidth[location.X - tileInfo.ModeInfoColumnStart];
        int leftCtx = this.leftNeighborContext.LeftPartitionHeight[(location.Y - superblockInfo.ModeInfoPosition.Y) & Av1PartitionContext.Mask];
        int blockSizeLog = blockSize.Get4x4WidthLog2() - Av1BlockSize.Block8x8.Get4x4WidthLog2();
        int above = (aboveCtx >> blockSizeLog) & 0x1;
        int left = (leftCtx >> blockSizeLog) & 0x1;
        DebugGuard.IsTrue(blockSize.Get4x4WidthLog2() == blockSize.Get4x4HeightLog2(), "Blocks should be square.");
        DebugGuard.MustBeGreaterThanOrEqualTo(blockSizeLog, 0, nameof(blockSizeLog));
        return ((left << 1) + above) + (blockSizeLog * Av1Constants.PartitionProbabilitySet);
    }

    /// <summary>
    /// Publishes the decoded partition sizes to the above and left neighbor contexts.
    /// </summary>
    /// <param name="modeInfoLocation">The parent block origin in 4x4 mode-information units.</param>
    /// <param name="tileLoc">The active tile boundaries.</param>
    /// <param name="superblockInfo">The containing superblock.</param>
    /// <param name="subSize">The primary size produced by the partition.</param>
    /// <param name="blockSize">The parent block size.</param>
    /// <param name="partition">The decoded partition type.</param>
    private void UpdatePartitionContext(Point modeInfoLocation, Av1TileInfo tileLoc, Av1SuperblockInfo superblockInfo, Av1BlockSize subSize, Av1BlockSize blockSize, Av1PartitionType partition)
    {
        if (blockSize >= Av1BlockSize.Block8x8)
        {
            int hbs = blockSize.Get4x4WideCount() / 2;
            Av1BlockSize blockSize2 = Av1PartitionType.Split.GetBlockSubSize(blockSize);
            switch (partition)
            {
                case Av1PartitionType.Split:
                    if (blockSize != Av1BlockSize.Block8x8)
                    {
                        break;
                    }

                    goto PARTITIONS;
                case Av1PartitionType.None:
                case Av1PartitionType.Horizontal:
                case Av1PartitionType.Vertical:
                case Av1PartitionType.Horizontal4:
                case Av1PartitionType.Vertical4:
                    PARTITIONS:
                    this.aboveNeighborContext.UpdatePartition(modeInfoLocation, tileLoc, subSize, blockSize);
                    this.leftNeighborContext.UpdatePartition(modeInfoLocation, superblockInfo, subSize, blockSize);
                    break;
                case Av1PartitionType.HorizontalA:
                    this.aboveNeighborContext.UpdatePartition(modeInfoLocation, tileLoc, blockSize2, subSize);
                    this.leftNeighborContext.UpdatePartition(modeInfoLocation, superblockInfo, blockSize2, subSize);
                    Point locHorizontalA = new(modeInfoLocation.X, modeInfoLocation.Y + hbs);
                    this.aboveNeighborContext.UpdatePartition(locHorizontalA, tileLoc, subSize, subSize);
                    this.leftNeighborContext.UpdatePartition(locHorizontalA, superblockInfo, subSize, subSize);
                    break;
                case Av1PartitionType.HorizontalB:
                    this.aboveNeighborContext.UpdatePartition(modeInfoLocation, tileLoc, subSize, subSize);
                    this.leftNeighborContext.UpdatePartition(modeInfoLocation, superblockInfo, subSize, subSize);
                    Point locHorizontalB = new(modeInfoLocation.X, modeInfoLocation.Y + hbs);
                    this.aboveNeighborContext.UpdatePartition(locHorizontalB, tileLoc, blockSize2, subSize);
                    this.leftNeighborContext.UpdatePartition(locHorizontalB, superblockInfo, blockSize2, subSize);
                    break;
                case Av1PartitionType.VerticalA:
                    this.aboveNeighborContext.UpdatePartition(modeInfoLocation, tileLoc, blockSize2, subSize);
                    this.leftNeighborContext.UpdatePartition(modeInfoLocation, superblockInfo, blockSize2, subSize);
                    Point locVerticalA = new(modeInfoLocation.X + hbs, modeInfoLocation.Y);
                    this.aboveNeighborContext.UpdatePartition(locVerticalA, tileLoc, subSize, subSize);
                    this.leftNeighborContext.UpdatePartition(locVerticalA, superblockInfo, subSize, subSize);
                    break;
                case Av1PartitionType.VerticalB:
                    this.aboveNeighborContext.UpdatePartition(modeInfoLocation, tileLoc, subSize, subSize);
                    this.leftNeighborContext.UpdatePartition(modeInfoLocation, superblockInfo, subSize, subSize);
                    Point locVerticalB = new(modeInfoLocation.X, modeInfoLocation.Y + hbs);
                    this.aboveNeighborContext.UpdatePartition(locVerticalB, tileLoc, blockSize2, subSize);
                    this.leftNeighborContext.UpdatePartition(locVerticalB, superblockInfo, blockSize2, subSize);
                    break;
                default:
                    throw new InvalidImageContentException($"Unknown partition type: {partition}");
            }
        }
    }
}
