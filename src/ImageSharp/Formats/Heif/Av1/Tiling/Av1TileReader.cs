// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1TileReader : IAv1TileReader
{
    private static readonly int[] SgrprojXqdMid = [-32, 31];
    private static readonly int[] WienerTapsMid = [3, -7, 15];
    private static readonly int[] Signs = [0, -1, 1];
    private static readonly int[] DcSignContexts = [
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2];

    private static readonly int[][] SkipContexts = [
        [1, 2, 2, 2, 3], [1, 4, 4, 4, 5], [1, 4, 4, 4, 5], [1, 4, 4, 4, 5], [1, 4, 4, 4, 6]];

    private static readonly int[] EndOfBlockGroupStart = [0, 1, 2, 3, 5, 9, 17, 33, 65, 129, 257, 513];
    private static readonly int[] EndOfBlockOffsetBits = [0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

    private int[][] referenceSgrXqd = [];
    private int[][][] referenceLrWiener = [];
    private readonly Av1ParseAboveNeighbor4x4Context aboveNeighborContext;
    private readonly Av1ParseLeftNeighbor4x4Context leftNeighborContext;
    private int currentQuantizerIndex;
    private readonly int[][] segmentIds = [];
    private readonly int[][] transformUnitCount;
    private readonly int[] firstTransformOffset = new int[2];
    private readonly int[] coefficientIndex = [];
    private readonly Configuration configuration;
    private readonly IAv1FrameDecoder frameDecoder;

    public Av1TileReader(Configuration configuration, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader, IAv1FrameDecoder frameDecoder)
    {
        this.FrameHeader = frameHeader;
        this.configuration = configuration;
        this.SequenceHeader = sequenceHeader;
        this.frameDecoder = frameDecoder;

        // init_main_frame_ctxt
        this.FrameInfo = new(this.SequenceHeader);
        this.segmentIds = new int[this.FrameHeader.ModeInfoRowCount][];
        for (int y = 0; y < this.FrameHeader.ModeInfoRowCount; y++)
        {
            this.segmentIds[y] = new int[this.FrameHeader.ModeInfoColumnCount];
        }

        // reallocate_parse_context_memory
        // Hard code number of threads to 1 for now.
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

    public ObuFrameHeader FrameHeader { get; }

    public ObuSequenceHeader SequenceHeader { get; }

    public Av1FrameInfo FrameInfo { get; }

    /// <summary>
    /// SVT: parse_tile
    /// </summary>
    public void ReadTile(Span<byte> tileData, int tileNum)
    {
        Av1SymbolDecoder reader = new(tileData, this.FrameHeader.QuantizationParameters.BaseQIndex);
        int tileColumnIndex = tileNum % this.FrameHeader.TilesInfo.TileColumnCount;
        int tileRowIndex = tileNum / this.FrameHeader.TilesInfo.TileColumnCount;

        int modeInfoColumnStart = this.FrameHeader.TilesInfo.TileColumnStartModeInfo[tileColumnIndex];
        int modeInfoColumnEnd = this.FrameHeader.TilesInfo.TileColumnStartModeInfo[tileColumnIndex + 1];
        int modeInfoRowStart = this.FrameHeader.TilesInfo.TileRowStartModeInfo[tileRowIndex];
        int modeInfoRowEnd = this.FrameHeader.TilesInfo.TileRowStartModeInfo[tileRowIndex + 1];
        this.aboveNeighborContext.Clear(this.SequenceHeader, modeInfoColumnStart, modeInfoColumnEnd);
        this.ClearLoopFilterDelta();
        int planesCount = this.SequenceHeader.ColorConfig.PlaneCount;

        // Default initialization of Wiener and SGR Filter.
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
                this.ReadLoopRestoration(modeInfoPosition, superBlockSize);
                this.ParsePartition(ref reader, modeInfoPosition, superBlockSize, superblockInfo, tileInfo);

                // decoding of the superblock
                this.frameDecoder.DecodeSuperblock(modeInfoPosition, superblockInfo, tileInfo);
            }
        }
    }

    private void ClearLoopFilterDelta()
        => this.FrameInfo.ClearDeltaLoopFilter();

    private void ReadLoopRestoration(Point modeInfoLocation, Av1BlockSize superBlockSize)
    {
        int planesCount = this.SequenceHeader.ColorConfig.PlaneCount;
        for (int plane = 0; plane < planesCount; plane++)
        {
            if (this.FrameHeader.LoopRestorationParameters.Items[plane].Type != ObuRestorationType.None)
            {
                // TODO: Implement.
                throw new NotImplementedException("No loop restoration filter support.");
            }
        }
    }

    /// <summary>
    /// 5.11.4. Decode partition syntax.
    /// </summary>
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
            if (blockSize < Av1BlockSize.Block8x8)
            {
                partitionType = Av1PartitionType.None;
            }
            else if (hasRows && hasColumns)
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

    private void ParseBlock(ref Av1SymbolDecoder reader, Point modeInfoLocation, Av1BlockSize blockSize, Av1SuperblockInfo superblockInfo, Av1TileInfo tileInfo, Av1PartitionType partitionType)
    {
        int rowIndex = modeInfoLocation.Y;
        int columnIndex = modeInfoLocation.X;
        int block4x4Width = blockSize.Get4x4WideCount();
        int block4x4Height = blockSize.Get4x4HighCount();
        int planesCount = this.SequenceHeader.ColorConfig.PlaneCount;
        int subX = this.SequenceHeader.ColorConfig.SubSamplingX ? 1 : 0;
        int subY = this.SequenceHeader.ColorConfig.SubSamplingY ? 1 : 0;
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
        partitionInfo.ComputeBoundaryOffsets(this.configuration, this.SequenceHeader, this.FrameHeader, tileInfo);
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

        if (partitionInfo.AvailableAbove)
        {
            partitionInfo.AboveModeInfo = superblockInfo.GetModeInfo(new Point(rowIndex - 1, columnIndex));
        }

        if (partitionInfo.AvailableLeft)
        {
            partitionInfo.LeftModeInfo = superblockInfo.GetModeInfo(new Point(rowIndex, columnIndex - 1));
        }

        if (partitionInfo.AvailableAboveForChroma)
        {
            partitionInfo.AboveModeInfoForChroma = superblockInfo.GetModeInfo(new Point(rowIndex & ~subY, columnIndex | subX));
        }

        if (partitionInfo.AvailableLeftForChroma)
        {
            partitionInfo.LeftModeInfoForChroma = superblockInfo.GetModeInfo(new Point(rowIndex | subY, columnIndex & ~subX));
        }

        this.ReadModeInfo(ref reader, partitionInfo);
        ReadPaletteTokens(ref reader, partitionInfo);
        this.ReadBlockTransformSize(ref reader, modeInfoLocation, partitionInfo, superblockInfo, tileInfo);
        if (partitionInfo.ModeInfo.Skip)
        {
            this.ResetSkipContext(partitionInfo, tileInfo);
        }

        this.Residual(ref reader, partitionInfo, superblockInfo, tileInfo, blockSize);

        // Update the Frame buffer for this ModeInfo.
        this.FrameInfo.UpdateModeInfo(blockModeInfo, superblockInfo);
    }

    /// <summary>
    /// SVT: reset_skip_context
    /// </summary>
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
    /// 5.11.34. Residual syntax.
    /// </summary>
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

                    ref Av1TransformInfo transformInfoRef = ref (plane == 0) ? ref superblockInfo.GetTransformInfoY() : ref superblockInfo.GetTransformInfoUv();
                    if (isLosslessBlock)
                    {
                        // TODO: Implement.
                        int unitHeight = Av1Math.RoundPowerOf2(Math.Min(modeUnitBlocksHigh + row, maxBlocksHigh), 0);
                        int unitWidth = Av1Math.RoundPowerOf2(Math.Min(modeUnitBlocksWide + column, maxBlocksWide), 0);
                        DebugGuard.IsTrue(Unsafe.Add(ref transformInfoRef, transformInfoIndices[plane]).Size == Av1TransformSize.Size4x4, "Lossless frame shall have transform units of size 4x4.");
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
                        Av1TransformInfo transformInfo = Unsafe.Add(ref transformInfoRef, transformInfoIndices[plane]);
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
                            endOfBlock = this.ParseTransformBlock(ref reader, partitionInfo, coefficientIndex, transformInfo, plane, blockColumn, blockRow, startX, startY, transformInfo.Size, subX != 0, subY != 0);
                        }

                        if (endOfBlock != 0)
                        {
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

    private Av1TransformSize GetSize(int plane, object transformSize) => throw new NotImplementedException();

    /// <summary>
    /// 5.11.38. Get plane residual size function.
    /// The GetPlaneResidualSize returns the size of a residual block for the specified plane. (The residual block will always
    /// have width and height at least equal to 4.)
    /// </summary>
    private Av1BlockSize GetPlaneResidualSize(Av1BlockSize sizeChunk, int plane)
    {
        bool subsamplingX = this.SequenceHeader.ColorConfig.SubSamplingX;
        bool subsamplingY = this.SequenceHeader.ColorConfig.SubSamplingY;
        bool subX = plane > 0 && subsamplingX;
        bool subY = plane > 0 && subsamplingY;
        return sizeChunk.GetSubsampled(subX, subY);
    }

    /// <summary>
    /// 5.11.35. Transform block syntax.
    /// </summary>
    /// <remarks>
    /// The implementation is taken from SVT-AV1 library, which deviates from the code flow in the specification.
    /// </remarks>
    private int ParseTransformBlock(
        ref Av1SymbolDecoder reader,
        Av1PartitionInfo partitionInfo,
        int coefficientIndex,
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
        endOfBlock = this.ParseCoefficients(ref reader, partitionInfo, startY, startX, blockRow, blockColumn, plane, transformBlockContext, transformSize, coefficientIndex, transformInfo);

        return endOfBlock;
    }

    /// <summary>
    /// 5.11.39. Coefficients syntax.
    /// </summary>
    /// <remarks>
    /// The implementation is taken from SVT-AV1 library, which deviates from the code flow in the specification.
    /// </remarks>
    private int ParseCoefficients(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo, int blockRow, int blockColumn, int aboveOffset, int leftOffset, int plane, Av1TransformBlockContext transformBlockContext, Av1TransformSize transformSize, int coefficientIndex, Av1TransformInfo transformInfo)
    {
        Span<int> coefficientBuffer = this.FrameInfo.GetCoefficients(plane);
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        Av1TransformSize transformSizeContext = (Av1TransformSize)(((int)transformSize.GetSquareSize() + ((int)transformSize.GetSquareUpSize() + 1)) >> 1);
        Av1PlaneType planeType = (Av1PlaneType)Math.Min(plane, 1);
        int culLevel = 0;

        int[] levelsBuffer = new int[Av1Constants.TransformPad2d];
        Span<int> levels = levelsBuffer.AsSpan()[(Av1Constants.TransformPadTop * (width + Av1Constants.TransformPadHorizontal))..];

        bool allZero = reader.ReadTransformBlockSkip(transformSizeContext, transformBlockContext.SkipContext);
        int bwl = transformSize.GetBlockWidthLog2();
        int endOfBlock;
        if (allZero)
        {
            if (plane == 0)
            {
                transformInfo.Type = Av1TransformType.DctDct;
                transformInfo.CodeBlockFlag = false;
            }

            this.UpdateCoefficientContext(plane, partitionInfo, transformSize, blockRow, blockColumn, aboveOffset, leftOffset, culLevel);
            return 0;
        }

        int endOfBlockExtra = 0;
        int endOfBlockPoint = 0;

        transformInfo.Type = this.ComputeTransformType(planeType, partitionInfo, transformSize, transformInfo);
        Av1TransformClass transformClass = transformInfo.Type.ToClass();
        Av1ScanOrder scanOrder = Av1ScanOrderConstants.GetScanOrder(transformSize, transformInfo.Type);
        Span<short> scan = scanOrder.Scan;

        endOfBlockPoint = reader.ReadEndOfBlockFlag(planeType, transformClass, transformSize);
        int endOfBlockShift = EndOfBlockOffsetBits[endOfBlockPoint];
        if (endOfBlockShift > 0)
        {
            int endOfBlockContext = endOfBlockPoint;
            bool bit = reader.ReadEndOfBlockExtra(transformSizeContext, planeType, endOfBlockContext);
            if (bit)
            {
                endOfBlockExtra += 1 << (endOfBlockShift - 1);
            }
            else
            {
                for (int j = 1; j < endOfBlockShift; j++)
                {
                    if (reader.ReadLiteral(1) != 0)
                    {
                        endOfBlockExtra += 1 << (endOfBlockShift - 1 - j);
                    }
                }
            }
        }

        endOfBlock = RecordEndOfBlockPosition(endOfBlockPoint, endOfBlockExtra);
        if (endOfBlock > 1)
        {
            Array.Fill(levelsBuffer, 0, 0, ((width + Av1Constants.TransformPadHorizontal) * (height + Av1Constants.TransformPadVertical)) + Av1Constants.TransformPadEnd);
        }

        reader.ReadCoefficientsEndOfBlock(transformClass, endOfBlock, height, scan, bwl, levels, transformSizeContext, planeType);
        if (endOfBlock > 1)
        {
            if (transformClass == Av1TransformClass.Class2D)
            {
                reader.ReadCoefficientsReverse2d(transformSize, 1, endOfBlock - 1 - 1, scan, bwl, levels, transformSizeContext, planeType);
                reader.ReadCoefficientsReverse(transformSize, transformClass, 0, 0, scan, bwl, levels, transformSizeContext, planeType);
            }
            else
            {
                reader.ReadCoefficientsReverse(transformSize, transformClass, 0, endOfBlock - 1 - 1, scan, bwl, levels, transformSizeContext, planeType);
            }
        }

        DebugGuard.MustBeGreaterThan(scan.Length, 0, nameof(scan));
        culLevel = reader.ReadCoefficientsDc(coefficientBuffer, endOfBlock, scan, bwl, levels, transformBlockContext.DcSignContext, planeType);
        this.UpdateCoefficientContext(plane, partitionInfo, transformSize, blockRow, blockColumn, aboveOffset, leftOffset, culLevel);

        transformInfo.CodeBlockFlag = true;
        return endOfBlock;
    }

    private void UpdateCoefficientContext(int plane, Av1PartitionInfo partitionInfo, Av1TransformSize transformSize, int blockRow, int blockColumn, int aboveOffset, int leftOffset, int culLevel)
    {
        bool subX = this.SequenceHeader.ColorConfig.SubSamplingX;
        bool subY = this.SequenceHeader.ColorConfig.SubSamplingY;
        int[] aboveContexts = this.aboveNeighborContext.GetContext(plane);
        int[] leftContexts = this.leftNeighborContext.GetContext(plane);
        int transformSizeWide = transformSize.Get4x4WideCount();
        int transformSizeHigh = transformSize.Get4x4HighCount();

        if (partitionInfo.ModeBlockToRightEdge < 0)
        {
            Av1BlockSize planeBlockSize = partitionInfo.ModeInfo.BlockSize.GetSubsampled(subX, subY);
            int blocksWide = partitionInfo.GetMaxBlockWide(planeBlockSize, subX);
            int aboveContextCount = Math.Min(transformSizeWide, blocksWide - aboveOffset);
            Array.Fill(aboveContexts, culLevel, 0, aboveContextCount);
            Array.Fill(aboveContexts, 0, aboveContextCount, transformSizeWide - aboveContextCount);
        }
        else
        {
            Array.Fill(aboveContexts, culLevel, 0, transformSizeWide);
        }

        if (partitionInfo.ModeBlockToBottomEdge < 0)
        {
            Av1BlockSize planeBlockSize = partitionInfo.ModeInfo.BlockSize.GetSubsampled(subX, subY);
            int blocksHigh = partitionInfo.GetMaxBlockHigh(planeBlockSize, subY);
            int leftContextCount = Math.Min(transformSizeHigh, blocksHigh - leftOffset);
            Array.Fill(leftContexts, culLevel, 0, leftContextCount);
            Array.Fill(leftContexts, 0, leftContextCount, transformSizeWide - leftContextCount);
        }
        else
        {
            Array.Fill(leftContexts, culLevel, 0, transformSizeHigh);
        }
    }

    private static int RecordEndOfBlockPosition(int endOfBlockPoint, int endOfBlockExtra)
    {
        int endOfBlock = EndOfBlockGroupStart[endOfBlockPoint];
        if (endOfBlock > 2)
        {
            endOfBlock += endOfBlockExtra;
        }

        return endOfBlock;
    }

    private Av1TransformType ComputeTransformType(Av1PlaneType planeType, Av1PartitionInfo partitionInfo, Av1TransformSize transformSize, Av1TransformInfo transformInfo)
    {
        Av1TransformType transformType = Av1TransformType.DctDct;
        if (this.FrameHeader.LosslessArray[partitionInfo.ModeInfo.SegmentId] || transformSize.GetSquareUpSize() > Av1TransformSize.Size32x32)
        {
            transformType = Av1TransformType.DctDct;
        }
        else
        {
            if (planeType == Av1PlaneType.Y)
            {
                transformType = transformInfo.Type;
            }
            else
            {
                // In intra mode, uv planes don't share the same prediction mode as y
                // plane, so the tx_type should not be shared
                transformType = ConvertIntraModeToTransformType(partitionInfo.ModeInfo, Av1PlaneType.Uv);
            }
        }

        Av1TransformSetType transformSetType = GetExtendedTransformSetType(transformSize, this.FrameHeader.UseReducedTransformSet);
        if (!transformType.IsExtendedSetUsed(transformSetType))
        {
            transformType = Av1TransformType.DctDct;
        }

        return transformType;
    }

    private static Av1TransformSetType GetExtendedTransformSetType(Av1TransformSize transformSize, bool useReducedSet)
    {
        Av1TransformSize squareUpSize = transformSize.GetSquareUpSize();

        if (squareUpSize >= Av1TransformSize.Size32x32)
        {
            return Av1TransformSetType.DctOnly;
        }

        if (useReducedSet)
        {
            return Av1TransformSetType.Dtt4Identity;
        }

        Av1TransformSize squareSize = transformSize.GetSquareSize();
        return squareSize == Av1TransformSize.Size16x16 ? Av1TransformSetType.Dtt4Identity : Av1TransformSetType.Dtt4Identity1dDct;
    }

    private static Av1TransformType ConvertIntraModeToTransformType(Av1BlockModeInfo modeInfo, Av1PlaneType planeType)
    {
        Av1PredictionMode mode = (planeType == Av1PlaneType.Y) ? modeInfo.YMode : modeInfo.UvMode;
        if (mode == Av1PredictionMode.UvChromaFromLuma)
        {
            mode = Av1PredictionMode.DC;
        }

        return mode.ToTransformType();
    }

    private Av1TransformBlockContext GetTransformBlockContext(Av1TransformSize transformSize, int plane, Av1BlockSize planeBlockSize, int transformBlockUnitHighCount, int transformBlockUnitWideCount, int startY, int startX)
    {
        Av1TransformBlockContext transformBlockContext = new();
        int[] aboveContext = this.aboveNeighborContext.GetContext(plane);
        int[] leftContext = this.leftNeighborContext.GetContext(plane);
        int dcSign = 0;
        int k = 0;
        int mask = (1 << Av1Constants.CoefficientContextBitCount) - 1;

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
            int contextBase = GetEntropyContext(transformSize, aboveContext, leftContext);
            int contextOffset = planeBlockSize.GetPelsLog2Count() > transformSize.ToBlockSize().GetPelsLog2Count() ? 10 : 7;
            transformBlockContext.SkipContext = contextBase + contextOffset;
        }

        return transformBlockContext;
    }

    private static int GetEntropyContext(Av1TransformSize transformSize, int[] above, int[] left)
    {
        bool aboveEntropyContext = false;
        bool leftEntropyContext = false;

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
    /// 5.11.15. TX size syntax.
    /// </summary>
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
    /// Section 5.11.16. Block TX size syntax.
    /// </summary>
    private void ReadBlockTransformSize(ref Av1SymbolDecoder reader, Point modeInfoLocation, Av1PartitionInfo partitionInfo, Av1SuperblockInfo superblockInfo, Av1TileInfo tileInfo)
    {
        Av1BlockSize blockSize = partitionInfo.ModeInfo.BlockSize;
        int block4x4Width = blockSize.Get4x4WideCount();
        int block4x4Height = blockSize.Get4x4HighCount();

        // First condition in spec is for INTER frames, implemented only the INTRA condition.
        Av1TransformSize transformSize = this.ReadTransformSize(ref reader, partitionInfo, superblockInfo, tileInfo, true);
        this.aboveNeighborContext.UpdateTransformation(modeInfoLocation, tileInfo, transformSize, blockSize, false);
        this.leftNeighborContext.UpdateTransformation(modeInfoLocation, superblockInfo, transformSize, blockSize, false);
        this.UpdateTransformInfo(partitionInfo, superblockInfo, blockSize, transformSize);
    }

    private unsafe void UpdateTransformInfo(Av1PartitionInfo partitionInfo, Av1SuperblockInfo superblockInfo, Av1BlockSize blockSize, Av1TransformSize transformSize)
    {
        int transformInfoYIndex = partitionInfo.ModeInfo.FirstTransformLocation[(int)Av1PlaneType.Y];
        int transformInfoUvIndex = partitionInfo.ModeInfo.FirstTransformLocation[(int)Av1PlaneType.Uv];
        ref Av1TransformInfo lumaTransformInfo = ref superblockInfo.GetTransformInfoY();
        ref Av1TransformInfo chromaTransformInfo = ref superblockInfo.GetTransformInfoUv();
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

        for (int idy = 0; idy < maxBlockHigh; idy += height)
        {
            for (int idx = 0; idx < maxBlockWide; idx += width, forceSplitCount++)
            {
                int lumaTransformUnitCount = 0;
                int chromaTransformUnitCount = 0;

                // Update Luminance Transform Info.
                int stepColumn = transformSize.Get4x4WideCount();
                int stepRow = transformSize.Get4x4HighCount();

                int unitHeight = Av1Math.RoundPowerOf2(Math.Min(height + idy, maxBlockHigh), 0);
                int unitWidth = Av1Math.RoundPowerOf2(Math.Min(width + idx, maxBlockWide), 0);
                for (int blockRow = idy; blockRow < unitHeight; blockRow += stepRow)
                {
                    for (int blockColumn = idx; blockColumn < unitWidth; blockColumn += stepColumn)
                    {
                        Unsafe.Add(ref lumaTransformInfo, transformInfoYIndex) = new Av1TransformInfo(
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

                // Update Chroma Transform Info.
                stepColumn = transformSizeUv.Get4x4WideCount();
                stepRow = transformSizeUv.Get4x4HighCount();

                unitHeight = Av1Math.RoundPowerOf2(Math.Min(height + idx, maxBlockHigh), subY ? 1 : 0);
                unitWidth = Av1Math.RoundPowerOf2(Math.Min(width + idx, maxBlockWide), subX ? 1 : 0);
                for (int blockRow = idy; blockRow < unitHeight; blockRow += stepRow)
                {
                    for (int blockColumn = idx; blockColumn < unitWidth; blockColumn += stepColumn)
                    {
                        Unsafe.Add(ref chromaTransformInfo, transformInfoUvIndex) = new Av1TransformInfo(
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

        // Cr Transform Info Update from Cb.
        if (totalChromaTransformUnitCount != 0)
        {
            DebugGuard.IsTrue(
                (transformInfoUvIndex - totalChromaTransformUnitCount) ==
                partitionInfo.ModeInfo.FirstTransformLocation[(int)Av1PlaneType.Uv],
                nameof(totalChromaTransformUnitCount));
            int originalIndex = transformInfoUvIndex - totalChromaTransformUnitCount;
            ref Av1TransformInfo originalInfo = ref Unsafe.Add(ref chromaTransformInfo, originalIndex);
            ref Av1TransformInfo infoV = ref Unsafe.Add(ref chromaTransformInfo, transformInfoUvIndex);
            for (int i = 0; i < totalChromaTransformUnitCount; i++)
            {
                infoV = originalInfo;
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
    /// 5.11.49. Palette tokens syntax.
    /// </summary>
    private static void ReadPaletteTokens(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
    {
        if (partitionInfo.ModeInfo.GetPaletteSize(Av1PlaneType.Y) != 0)
        {
            // TODO: Implement.
            throw new NotImplementedException();
        }

        if (partitionInfo.ModeInfo.GetPaletteSize(Av1PlaneType.Uv) != 0)
        {
            // TODO: Implement.
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 5.11.6. Mode info syntax.
    /// </summary>
    private void ReadModeInfo(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
    {
        DebugGuard.IsTrue(this.FrameHeader.FrameType is ObuFrameType.KeyFrame or ObuFrameType.IntraOnlyFrame, "Only INTRA frames supported.");
        this.ReadIntraFrameModeInfo(ref reader, partitionInfo);
    }

    /// <summary>
    /// 5.11.7. Intra frame mode info syntax.
    /// </summary>
    private void ReadIntraFrameModeInfo(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
    {
        if (this.FrameHeader.SegmentationParameters.SegmentIdPrecedesSkip)
        {
            this.IntraSegmentId(ref reader, partitionInfo);
        }

        // this.skipMode = false;
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

        partitionInfo.ReferenceFrame[0] = 0; // IntraFrame;
        partitionInfo.ReferenceFrame[1] = -1; // None;
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
            // this.IsInter = false;
            partitionInfo.ModeInfo.YMode = reader.ReadYMode(partitionInfo.AboveModeInfo, partitionInfo.LeftModeInfo);

            // 5.11.42.Intra angle info luma syntax.
            partitionInfo.ModeInfo.AngleDelta[(int)Av1PlaneType.Y] = IntraAngleInfo(ref reader, partitionInfo.ModeInfo.YMode, partitionInfo.ModeInfo.BlockSize);
            if (partitionInfo.IsChroma && !this.SequenceHeader.ColorConfig.IsMonochrome)
            {
                partitionInfo.ModeInfo.UvMode = reader.ReadIntraModeUv(partitionInfo.ModeInfo.YMode, this.IsChromaForLumaAllowed(partitionInfo));
                if (partitionInfo.ModeInfo.UvMode == Av1PredictionMode.UvChromaFromLuma)
                {
                    ReadChromaFromLumaAlphas(ref reader, partitionInfo.ModeInfo);
                }

                // 5.11.43.Intra angle info chroma syntax.
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

    private bool AllowIntraBlockCopy()
        => (this.FrameHeader.FrameType is ObuFrameType.KeyFrame or ObuFrameType.IntraOnlyFrame) &&
            (this.SequenceHeader.ForceScreenContentTools > 0) &&
            this.FrameHeader.AllowIntraBlockCopy;

    private bool IsChromaForLumaAllowed(Av1PartitionInfo partitionInfo)
    {
        if (this.FrameHeader.LosslessArray[partitionInfo.ModeInfo.SegmentId])
        {
            // In lossless, CfL is available when the partition size is equal to the
            // transform size.
            bool subX = this.SequenceHeader.ColorConfig.SubSamplingX;
            bool subY = this.SequenceHeader.ColorConfig.SubSamplingY;
            Av1BlockSize planeBlockSize = partitionInfo.ModeInfo.BlockSize.GetSubsampled(subX, subY);
            return planeBlockSize == Av1BlockSize.Block4x4;
        }

        // Spec: CfL is available to luma partitions lesser than or equal to 32x32
        return partitionInfo.ModeInfo.BlockSize.GetWidth() <= 32 && partitionInfo.ModeInfo.BlockSize.GetHeight() <= 32;
    }

    private void FilterIntraModeInfo(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
    {
        if (this.SequenceHeader.EnableFilterIntra &&
            partitionInfo.ModeInfo.YMode == Av1PredictionMode.DC &&
            partitionInfo.ModeInfo.GetPaletteSize(Av1PlaneType.Y) == 0 &&
            Math.Max(partitionInfo.ModeInfo.BlockSize.GetWidth(), partitionInfo.ModeInfo.BlockSize.GetHeight()) <= 32)
        {
            partitionInfo.ModeInfo.FilterIntraModeInfo.UseFilterIntra = reader.ReadUseFilterUltra(partitionInfo.ModeInfo.BlockSize);
            if (partitionInfo.ModeInfo.FilterIntraModeInfo.UseFilterIntra)
            {
                partitionInfo.ModeInfo.FilterIntraModeInfo.Mode = reader.ReadFilterUltraMode();
            }
        }
        else
        {
            partitionInfo.ModeInfo.FilterIntraModeInfo.UseFilterIntra = false;
        }
    }

    /// <summary>
    /// 5.11.46. Palette mode info syntax.
    /// </summary>
    private void PaletteModeInfo(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo) =>

        // TODO: Implement.
        throw new NotImplementedException();

    /// <summary>
    /// 5.11.45. Read CFL alphas syntax.
    /// </summary>
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
    /// 5.11.42. and 5.11.43.
    /// </summary>
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

    private static bool IsDirectionalMode(Av1PredictionMode mode)
        => mode is >= Av1PredictionMode.Vertical and <= Av1PredictionMode.Directional67Degrees;

    /// <summary>
    /// 5.11.8. Intra segment ID syntax.
    /// </summary>
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
    /// 5.11.9. Read segment ID syntax.
    /// </summary>
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
            prevUL = this.GetSegmentId(partitionInfo, rowIndex - 1, columnIndex - 1);
        }

        if (partitionInfo.AvailableAbove)
        {
            prevU = this.GetSegmentId(partitionInfo, rowIndex - 1, columnIndex);
        }

        if (partitionInfo.AvailableLeft)
        {
            prevU = this.GetSegmentId(partitionInfo, rowIndex, columnIndex - 1);
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
            int ctx = prevUL < 0 ? 0 /* Edge cases */
                : prevUL == prevU && prevUL == prevL ? 2
                : prevUL == prevU || prevUL == prevL || prevU == prevL ? 1 : 0;
            int lastActiveSegmentId = this.FrameHeader.SegmentationParameters.LastActiveSegmentId;
            partitionInfo.ModeInfo.SegmentId = NegativeDeinterleave(reader.ReadSegmentId(ctx), predictor, lastActiveSegmentId + 1);
        }
    }

    private int GetSegmentId(Av1PartitionInfo partitionInfo, int rowIndex, int columnIndex)
    {
        int modeInfoOffset = (rowIndex * this.FrameHeader.ModeInfoColumnCount) + columnIndex;
        int bw4 = partitionInfo.ModeInfo.BlockSize.Get4x4WideCount();
        int bh4 = partitionInfo.ModeInfo.BlockSize.Get4x4HighCount();
        int xMin = Math.Min(this.FrameHeader.ModeInfoColumnCount - columnIndex, bw4);
        int yMin = Math.Min(this.FrameHeader.ModeInfoRowCount - rowIndex, bh4);
        int segmentId = Av1Constants.MaxSegmentCount - 1;
        for (int y = 0; y < yMin; y++)
        {
            for (int x = 0; x < xMin; x++)
            {
                segmentId = Math.Min(segmentId, this.segmentIds[y][x]);
            }
        }

        return segmentId;
    }

    private static int NegativeDeinterleave(int diff, int reference, int max)
    {
        if (reference == 0)
        {
            return diff;
        }

        if (reference >= max - 1)
        {
            return max - diff - 1;
        }

        if (2 * reference < max)
        {
            if (diff <= 2 * reference)
            {
                if ((diff & 1) > 0)
                {
                    return reference + ((diff + 1) >> 1);
                }
                else
                {
                    return reference - (diff >> 1);
                }
            }

            return diff;
        }
        else
        {
            if (diff <= 2 * (max - reference - 1))
            {
                if ((diff & 1) > 0)
                {
                    return reference + ((diff + 1) >> 1);
                }
                else
                {
                    return reference - (diff >> 1);
                }
            }

            return max - (diff + 1);
        }
    }

    /// <summary>
    /// 5.11.56. Read CDEF syntax.
    /// </summary>
    private void ReadCdef(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
    {
        if (partitionInfo.ModeInfo.Skip || this.FrameHeader.CodedLossless || !this.SequenceHeader.EnableCdef || this.FrameHeader.AllowIntraBlockCopy)
        {
            return;
        }

        int cdefSize4 = Av1BlockSize.Block64x64.Get4x4WideCount();
        int cdefMask4 = ~(cdefSize4 - 1);
        int r = partitionInfo.RowIndex & cdefMask4;
        int c = partitionInfo.ColumnIndex & cdefMask4;
        if (partitionInfo.CdefStrength[r][c] == -1)
        {
            partitionInfo.CdefStrength[r][c] = reader.ReadLiteral(this.FrameHeader.CdefParameters.BitCount);
            if (this.SequenceHeader.SuperblockSize == Av1BlockSize.Block128x128)
            {
                int w4 = partitionInfo.ModeInfo.BlockSize.Get4x4WideCount();
                int h4 = partitionInfo.ModeInfo.BlockSize.Get4x4HighCount();
                for (int i = r; i < r + h4; i += cdefSize4)
                {
                    for (int j = c; j < c + w4; j += cdefSize4)
                    {
                        partitionInfo.CdefStrength[i & cdefMask4][j & cdefMask4] = partitionInfo.CdefStrength[r][c];
                    }
                }
            }
        }
    }

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
                int deltaLoopFilterAbsolute = reader.ReadDeltaLoopFilterAbsolute();
                if (deltaLoopFilterAbsolute == Av1Constants.DeltaLoopFilterSmall)
                {
                    int deltaLoopFilterRemainingBits = reader.ReadLiteral(3) + 1;
                    int deltaLoopFilterAbsoluteBitCount = reader.ReadLiteral(deltaLoopFilterRemainingBits);
                    deltaLoopFilterAbsolute = deltaLoopFilterAbsoluteBitCount + (1 << deltaLoopFilterRemainingBits) + 1;
                }

                if (deltaLoopFilterAbsolute != 0)
                {
                    bool deltaLoopFilterSign = reader.ReadLiteral(1) > 0;
                    int reducedDeltaLoopFilterLevel = deltaLoopFilterSign ? -deltaLoopFilterAbsolute : deltaLoopFilterAbsolute;
                    int deltaLoopFilterResolution = this.FrameHeader.DeltaLoopFilterParameters.Resolution;
                    currentDeltaLoopFilter[i] = Av1Math.Clip3(-Av1Constants.MaxLoopFilter, Av1Constants.MaxLoopFilter, currentDeltaLoopFilter[i] + (reducedDeltaLoopFilterLevel << deltaLoopFilterResolution));
                }
            }
        }
    }

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
            int deltaQuantizerAbsolute = reader.ReadDeltaQuantizerAbsolute();
            if (deltaQuantizerAbsolute == Av1Constants.DeltaQuantizerSmall)
            {
                int deltaQuantizerRemainingBits = reader.ReadLiteral(3) + 1;
                int deltaQuantizerAbsoluteBitCount = reader.ReadLiteral(deltaQuantizerRemainingBits);
                deltaQuantizerAbsolute = deltaQuantizerRemainingBits + (1 << deltaQuantizerRemainingBits) + 1;
            }

            if (deltaQuantizerAbsolute != 0)
            {
                bool deltaQuantizerSignBit = reader.ReadLiteral(1) > 0;
                int reducedDeltaQuantizerIndex = deltaQuantizerSignBit ? -deltaQuantizerAbsolute : deltaQuantizerAbsolute;
                int deltaQuantizerResolution = this.FrameHeader.DeltaQParameters.Resolution;
                this.currentQuantizerIndex = Av1Math.Clip3(1, 255, this.currentQuantizerIndex + (reducedDeltaQuantizerIndex << deltaQuantizerResolution));
                partitionInfo.SuperblockInfo.SuperblockDeltaQ = this.currentQuantizerIndex;
            }
        }
    }

    private bool IsInside(int rowIndex, int columnIndex) =>
        columnIndex >= this.FrameHeader.TilesInfo.TileColumnCount &&
        columnIndex < this.FrameHeader.TilesInfo.TileColumnCount &&
        rowIndex >= this.FrameHeader.TilesInfo.TileRowCount &&
        rowIndex < this.FrameHeader.TilesInfo.TileRowCount;

    /*
    private static bool IsChroma(int rowIndex, int columnIndex, Av1BlockModeInfo blockMode, bool subSamplingX, bool subSamplingY)
    {
        int block4x4Width = blockMode.BlockSize.Get4x4WideCount();
        int block4x4Height = blockMode.BlockSize.Get4x4HighCount();
        bool xPos = (columnIndex & 0x1) > 0 || (block4x4Width & 0x1) > 0 || !subSamplingX;
        bool yPos = (rowIndex & 0x1) > 0 || (block4x4Height & 0x1) > 0 || !subSamplingY;
        return xPos && yPos;
    }*/

    /// <summary>
    /// SVT: partition_plane_context
    /// </summary>
    private int GetPartitionPlaneContext(Point location, Av1BlockSize blockSize, Av1TileInfo tileInfo, Av1SuperblockInfo superblockInfo)
    {
        // Maximum partition point is 8x8. Offset the log value occordingly.
        int aboveCtx = this.aboveNeighborContext.AbovePartitionWidth[location.X - tileInfo.ModeInfoColumnStart];
        int leftCtx = this.leftNeighborContext.LeftPartitionHeight[(location.Y - superblockInfo.ModeInfoPosition.Y) & Av1PartitionContext.Mask];
        int blockSizeLog = blockSize.Get4x4WidthLog2() - Av1BlockSize.Block8x8.Get4x4WidthLog2();
        int above = (aboveCtx >> blockSizeLog) & 0x1;
        int left = (leftCtx >> blockSizeLog) & 0x1;
        DebugGuard.IsTrue(blockSize.Get4x4WidthLog2() == blockSize.Get4x4HeightLog2(), "Blocks should be square");
        DebugGuard.MustBeGreaterThanOrEqualTo(blockSizeLog, 0, nameof(blockSizeLog));
        return ((left << 1) + above) + (blockSizeLog * Av1Constants.PartitionProbabilitySet);
    }

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
