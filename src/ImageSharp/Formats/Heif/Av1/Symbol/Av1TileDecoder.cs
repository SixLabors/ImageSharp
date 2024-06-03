// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Quantization;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

internal class Av1TileDecoder : IAv1TileDecoder
{
    private static readonly int[] SgrprojXqdMid = [-32, 31];
    private static readonly int[] WienerTapsMid = [3, -7, 15];
    private const int PartitionProbabilitySet = 4;

    // Number of Coefficients in a single ModeInfo 4x4 block of pixels (1 DC + 16 AC).
    private const int NumberofCoefficients = 1 + 16;

    private bool[][][] blockDecoded = [];
    private int[][] referenceSgrXqd = [];
    private int[][][] referenceLrWiener = [];
    private Av1ParseAboveContext aboveContext = new();
    private Av1ParseLeftContext leftContext = new();
    private int currentQuantizerIndex;
    private int[][] aboveLevelContext = [];
    private int[][] aboveDcContext = [];
    private int[][] leftLevelContext = [];
    private int[][] leftDcContext = [];
    private int[][] segmentIds = [];
    private int maxLumaWidth;
    private int maxLumaHeight;
    private int deltaLoopFilterResolution = -1;
    private int deltaQuantizerResolution = -1;
    private int[] coefficientsY = [];
    private int[] coefficientsU = [];
    private int[] coefficientsV = [];
    private int numModeInfosInSuperblock;
    private int superblockColumnCount;
    private int superblockRowCount;
    private Av1SuperblockInfo[] superblockInfos;
    private Av1BlockModeInfo[] modeInfos;
    private Av1TransformInfo[] transformInfosY;
    private Av1TransformInfo[] transformInfosUv;
    private int[] deltaQ;
    private int[] cdefStrength;
    private int[] deltaLoopFilter;

    public Av1TileDecoder(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, ObuTileInfo tileInfo)
    {
        this.FrameInfo = frameInfo;
        this.SequenceHeader = sequenceHeader;
        this.TileInfo = tileInfo;

        // init_main_frame_ctxt
        int superblockSizeLog2 = this.SequenceHeader.SuperBlockSizeLog2;
        int superblockAlignedWidth = Av1Math.AlignPowerOf2(this.SequenceHeader.MaxFrameWidth, superblockSizeLog2);
        int superblockAlignedHeight = Av1Math.AlignPowerOf2(this.SequenceHeader.MaxFrameHeight, superblockSizeLog2);
        this.superblockColumnCount = superblockAlignedWidth >> superblockSizeLog2;
        this.superblockRowCount = superblockAlignedHeight >> superblockSizeLog2;
        int superblockCount = this.superblockColumnCount * this.superblockRowCount;
        this.numModeInfosInSuperblock = (1 << (superblockSizeLog2 - ObuConstants.ModeInfoSizeLog2)) * (1 << (superblockSizeLog2 - ObuConstants.ModeInfoSizeLog2));

        this.superblockInfos = new Av1SuperblockInfo[superblockCount];
        this.modeInfos = new Av1BlockModeInfo[superblockCount * this.numModeInfosInSuperblock];
        this.transformInfosY = new Av1TransformInfo[superblockCount * this.numModeInfosInSuperblock];
        this.transformInfosUv = new Av1TransformInfo[2 * superblockCount * this.numModeInfosInSuperblock];
        this.coefficientsY = new int[superblockCount * this.numModeInfosInSuperblock * NumberofCoefficients];
        int subsamplingFactor = (this.SequenceHeader.ColorConfig.SubSamplingX && this.SequenceHeader.ColorConfig.SubSamplingY) ? 2 :
                (this.SequenceHeader.ColorConfig.SubSamplingX && !this.SequenceHeader.ColorConfig.SubSamplingY) ? 1 :
                (!this.SequenceHeader.ColorConfig.SubSamplingX && !this.SequenceHeader.ColorConfig.SubSamplingY) ? 0 : -1;
        Guard.IsFalse(subsamplingFactor == -1, nameof(subsamplingFactor), "Invalid combination of subsampling.");
        this.coefficientsU = new int[(superblockCount * this.numModeInfosInSuperblock * NumberofCoefficients) >> subsamplingFactor];
        this.coefficientsV = new int[(superblockCount * this.numModeInfosInSuperblock * NumberofCoefficients) >> subsamplingFactor];
        this.deltaQ = new int[superblockCount];
        this.cdefStrength = new int[superblockCount * (this.SequenceHeader.Use128x128SuperBlock ? 4 : 1)];
        Array.Fill(this.cdefStrength, -1);
        this.deltaLoopFilter = new int[superblockCount * ObuConstants.FrameLoopFilterCount];
    }

    public bool SequenceHeaderDone { get; set; }

    public bool ShowExistingFrame { get; set; }

    public bool SeenFrameHeader { get; set; }

    public ObuFrameHeader FrameInfo { get; }

    public ObuSequenceHeader SequenceHeader { get; }

    public ObuTileInfo TileInfo { get; }

    public void DecodeTile(Span<byte> tileData, int tileNum)
    {
        Av1SymbolDecoder reader = new(tileData);
        int tileColumnIndex = tileNum % this.TileInfo.TileColumnCount;
        int tileRowIndex = tileNum / this.TileInfo.TileColumnCount;

        this.aboveContext.Clear(this.TileInfo.TileColumnStartModeInfo[tileColumnIndex], this.TileInfo.TileColumnStartModeInfo[tileColumnIndex - 1]);
        this.ClearLoopFilterDelta();
        int planesCount = this.SequenceHeader.ColorConfig.IsMonochrome ? 1 : 3;
        this.referenceSgrXqd = new int[planesCount][];
        this.referenceLrWiener = new int[planesCount][][];
        for (int plane = 0; plane < planesCount; plane++)
        {
            this.referenceSgrXqd[plane] = new int[2];
            Array.Copy(SgrprojXqdMid, this.referenceSgrXqd[plane], SgrprojXqdMid.Length);
            this.referenceLrWiener[plane] = new int[2][];
            for (int pass = 0; pass < 2; pass++)
            {
                this.referenceLrWiener[plane][pass] = new int[ObuConstants.WienerCoefficientCount];
                Array.Copy(WienerTapsMid, this.referenceLrWiener[plane][pass], WienerTapsMid.Length);
            }
        }

        Av1BlockSize superBlockSize = this.SequenceHeader.Use128x128SuperBlock ? Av1BlockSize.Block128x128 : Av1BlockSize.Block64x64;
        int superBlock4x4Size = superBlockSize.Get4x4WideCount();
        for (int row = this.TileInfo.TileRowStartModeInfo[tileRowIndex]; row < this.TileInfo.TileRowStartModeInfo[tileRowIndex + 1]; row += this.SequenceHeader.ModeInfoSize)
        {
            int superBlockRow = row << ObuConstants.ModeInfoSizeLog2 >> this.SequenceHeader.SuperBlockSizeLog2;
            this.leftContext.Clear();
            for (int column = this.TileInfo.TileColumnStartModeInfo[tileColumnIndex]; column < this.TileInfo.TileColumnStartModeInfo[tileColumnIndex + 1]; column += this.SequenceHeader.ModeInfoSize)
            {
                int superBlockColumn = column << ObuConstants.ModeInfoSizeLog2 >> this.SequenceHeader.SuperBlockSizeLog2;
                bool subSamplingX = this.SequenceHeader.ColorConfig.SubSamplingX;
                bool subSamplingY = this.SequenceHeader.ColorConfig.SubSamplingY;

                bool readDeltas = this.FrameInfo.DeltaQParameters.IsPresent;

                this.ClearBlockDecodedFlags(row, column, superBlock4x4Size);

                int superblockIndex = (superBlockRow * this.superblockColumnCount) + superBlockColumn;
                int cdefFactor = this.SequenceHeader.Use128x128SuperBlock ? 4 : 1;
                Av1SuperblockInfo superblockInfo = new(this.modeInfos[superblockIndex], this.transformInfosY[superblockIndex])
                {
                    CoefficientsY = this.coefficientsY,
                    CoefficientsU = this.coefficientsU,
                    CoefficientsV = this.coefficientsV,
                    CdefStrength = this.cdefStrength[superblockIndex * cdefFactor],
                    SuperblockDeltaLoopFilter = this.deltaLoopFilter[ObuConstants.FrameLoopFilterCount * superblockIndex],
                    SuperblockDeltaQ = this.deltaQ[superblockIndex]
                };

                // Nothing to do for CDEF
                // this.ClearCdef(row, column);
                // this.ReadLoopRestoration(row, column, superBlockSize);
                this.ParsePartition(ref reader, row, column, superBlockSize, superblockInfo);
            }
        }
    }

    private void ClearLoopFilterDelta()
        => this.deltaLoopFilter = new int[4];

    private void ClearBlockDecodedFlags(int row, int column, int superBlock4x4Size)
    {
        int planesCount = this.SequenceHeader.ColorConfig.ChannelCount;
        for (int plane = 0; plane < planesCount; plane++)
        {
            int subX = plane > 0 && this.SequenceHeader.ColorConfig.SubSamplingX ? 1 : 0;
            int subY = plane > 0 && this.SequenceHeader.ColorConfig.SubSamplingY ? 1 : 0;
            int superBlock4x4Width = (this.FrameInfo.ModeInfoColumnCount - column) >> subX;
            int superBlock4x4Height = (this.FrameInfo.ModeInfoRowCount - row) >> subY;
            for (int y = -1; y <= superBlock4x4Size >> subY; y++)
            {
                for (int x = -1; x <= superBlock4x4Size >> subX; x++)
                {
                    if (y < 0 && x < superBlock4x4Width)
                    {
                        this.blockDecoded[plane][y][x] = true;
                    }
                    else if (x < 0 && y < superBlock4x4Height)
                    {
                        this.blockDecoded[plane][y][x] = true;
                    }
                    else
                    {
                        this.blockDecoded[plane][y][x] = false;
                    }
                }
            }

            int lastIndex = this.blockDecoded[plane][(superBlock4x4Size >> subY) - 1].Length - 1;
            this.blockDecoded[plane][(superBlock4x4Size >> subY) - 1][lastIndex] = false;
        }
    }

    private void ReadLoopRestoration(int row, int column, Av1BlockSize superBlockSize)
    {
        int planesCount = this.SequenceHeader.ColorConfig.ChannelCount;
        for (int plane = 0; plane < planesCount; plane++)
        {
            if (this.FrameInfo.LoopRestorationParameters[plane].Type != ObuRestorationType.None)
            {
                // TODO: Implement.
                throw new NotImplementedException("No loop restoration filter support.");
            }
        }
    }

    public void FinishDecodeTiles(bool doCdef, bool doLoopRestoration)
    {
        // TODO: Implement
    }

    private void ParsePartition(ref Av1SymbolDecoder reader, int rowIndex, int columnIndex, Av1BlockSize blockSize, Av1SuperblockInfo superblockInfo)
    {
        if (rowIndex >= this.TileInfo.TileRowStartModeInfo[rowIndex] ||
            columnIndex >= this.TileInfo.TileColumnStartModeInfo[columnIndex])
        {
            return;
        }

        bool availableUp = this.IsInside(rowIndex - 1, columnIndex);
        bool availableLeft = this.IsInside(rowIndex, columnIndex - 1);
        int block4x4Size = blockSize.Get4x4WideCount();
        int halfBlock4x4Size = block4x4Size >> 1;
        int quarterBlock4x4Size = halfBlock4x4Size >> 2;
        bool hasRows = (rowIndex + halfBlock4x4Size) < this.FrameInfo.ModeInfoRowCount;
        bool hasColumns = (columnIndex + halfBlock4x4Size) < this.FrameInfo.ModeInfoColumnCount;
        Av1PartitionType partitionType = Av1PartitionType.Split;
        if (blockSize < Av1BlockSize.Block8x8)
        {
            partitionType = Av1PartitionType.None;
        }
        else if (hasRows && hasColumns)
        {
            int ctx = this.GetPartitionContext(rowIndex, columnIndex, blockSize);
            partitionType = reader.ReadPartitionType(ctx);
        }
        else if (hasColumns)
        {
            int ctx = this.GetPartitionContext(rowIndex, columnIndex, blockSize);
            bool splitOrHorizontal = reader.ReadSplitOrHorizontal(blockSize, ctx);
            partitionType = splitOrHorizontal ? Av1PartitionType.Split : Av1PartitionType.Horizontal;
        }
        else if (hasRows)
        {
            int ctx = this.GetPartitionContext(rowIndex, columnIndex, blockSize);
            bool splitOrVertical = reader.ReadSplitOrVertical(blockSize, ctx);
            partitionType = splitOrVertical ? Av1PartitionType.Split : Av1PartitionType.Vertical;
        }

        Av1BlockSize subSize = partitionType.GetBlockSubSize(blockSize);
        Av1BlockSize splitSize = Av1PartitionType.Split.GetBlockSubSize(blockSize);
        switch (partitionType)
        {
            case Av1PartitionType.Split:
                this.ParsePartition(ref reader, rowIndex, columnIndex, subSize, superblockInfo);
                this.ParsePartition(ref reader, rowIndex, columnIndex + halfBlock4x4Size, subSize, superblockInfo);
                this.ParsePartition(ref reader, rowIndex + halfBlock4x4Size, columnIndex, subSize, superblockInfo);
                this.ParsePartition(ref reader, rowIndex + halfBlock4x4Size, columnIndex + halfBlock4x4Size, subSize, superblockInfo);
                break;
            case Av1PartitionType.None:
                this.ParseBlock(ref reader, rowIndex, columnIndex, subSize, superblockInfo, Av1PartitionType.None);
                break;
            case Av1PartitionType.Horizontal:
                this.ParseBlock(ref reader, rowIndex, columnIndex, subSize, superblockInfo, Av1PartitionType.Horizontal);
                if (hasRows)
                {
                    this.ParseBlock(ref reader, rowIndex + halfBlock4x4Size, columnIndex, subSize, superblockInfo, Av1PartitionType.Horizontal);
                }

                break;
            case Av1PartitionType.Vertical:
                this.ParseBlock(ref reader, rowIndex, columnIndex, subSize, superblockInfo, Av1PartitionType.Vertical);
                if (hasRows)
                {
                    this.ParseBlock(ref reader, rowIndex, columnIndex + halfBlock4x4Size, subSize, superblockInfo, Av1PartitionType.Vertical);
                }

                break;
            case Av1PartitionType.HorizontalA:
                this.ParseBlock(ref reader, rowIndex, columnIndex, splitSize, superblockInfo, Av1PartitionType.HorizontalA);
                this.ParseBlock(ref reader, rowIndex, columnIndex + halfBlock4x4Size, splitSize, superblockInfo, Av1PartitionType.HorizontalA);
                this.ParseBlock(ref reader, rowIndex + halfBlock4x4Size, columnIndex + halfBlock4x4Size, subSize, superblockInfo, Av1PartitionType.HorizontalA);
                break;
            case Av1PartitionType.HorizontalB:
                this.ParseBlock(ref reader, rowIndex, columnIndex, subSize, superblockInfo, Av1PartitionType.HorizontalB);
                this.ParseBlock(ref reader, rowIndex + halfBlock4x4Size, columnIndex, splitSize, superblockInfo, Av1PartitionType.HorizontalB);
                this.ParseBlock(ref reader, rowIndex + halfBlock4x4Size, columnIndex + halfBlock4x4Size, splitSize, superblockInfo, Av1PartitionType.HorizontalB);
                break;
            case Av1PartitionType.VerticalA:
                this.ParseBlock(ref reader, rowIndex, columnIndex, splitSize, superblockInfo, Av1PartitionType.VerticalA);
                this.ParseBlock(ref reader, rowIndex + halfBlock4x4Size, columnIndex, splitSize, superblockInfo, Av1PartitionType.VerticalA);
                this.ParseBlock(ref reader, rowIndex + halfBlock4x4Size, columnIndex + halfBlock4x4Size, subSize, superblockInfo, Av1PartitionType.VerticalA);
                break;
            case Av1PartitionType.VerticalB:
                this.ParseBlock(ref reader, rowIndex, columnIndex, subSize, superblockInfo, Av1PartitionType.VerticalB);
                this.ParseBlock(ref reader, rowIndex, columnIndex + halfBlock4x4Size, splitSize, superblockInfo, Av1PartitionType.VerticalB);
                this.ParseBlock(ref reader, rowIndex + halfBlock4x4Size, columnIndex + halfBlock4x4Size, splitSize, superblockInfo, Av1PartitionType.VerticalB);
                break;
            case Av1PartitionType.Horizontal4:
                for (int i = 0; i < 4; i++)
                {
                    int currentBlockRow = rowIndex + (i * quarterBlock4x4Size);
                    if (i > 0 && currentBlockRow > this.FrameInfo.ModeInfoRowCount)
                    {
                        break;
                    }

                    this.ParseBlock(ref reader, currentBlockRow, columnIndex, subSize, superblockInfo, Av1PartitionType.Horizontal4);
                }

                break;
            case Av1PartitionType.Vertical4:
                for (int i = 0; i < 4; i++)
                {
                    int currentBlockColumn = columnIndex + (i * quarterBlock4x4Size);
                    if (i > 0 && currentBlockColumn > this.FrameInfo.ModeInfoColumnCount)
                    {
                        break;
                    }

                    this.ParseBlock(ref reader, rowIndex, currentBlockColumn, subSize, superblockInfo, Av1PartitionType.Vertical4);
                }

                break;
            default:
                throw new NotImplementedException($"Partition type: {partitionType} is not supported.");
        }
    }

    private void ParseBlock(ref Av1SymbolDecoder reader, int rowIndex, int columnIndex, Av1BlockSize blockSize, Av1SuperblockInfo superblockInfo, Av1PartitionType partitionType)
    {
        int block4x4Width = blockSize.Get4x4WideCount();
        int block4x4Height = blockSize.Get4x4HighCount();
        int planesCount = this.SequenceHeader.ColorConfig.ChannelCount;
        Av1BlockModeInfo blockModeInfo = new(planesCount, blockSize);
        bool hasChroma = this.HasChroma(rowIndex, columnIndex, blockSize);
        Av1PartitionInfo partitionInfo = new(blockModeInfo, superblockInfo, hasChroma, partitionType);
        partitionInfo.ColumnIndex = columnIndex;
        partitionInfo.RowIndex = rowIndex;
        partitionInfo.AvailableUp = this.IsInside(rowIndex - 1, columnIndex);
        partitionInfo.AvailableLeft = this.IsInside(rowIndex, columnIndex - 1);
        partitionInfo.AvailableUpForChroma = partitionInfo.AvailableUp;
        partitionInfo.AvailableLeftForChroma = partitionInfo.AvailableLeft;
        if (hasChroma)
        {
            if (this.SequenceHeader.ColorConfig.SubSamplingY && block4x4Height == 1)
            {
                partitionInfo.AvailableUpForChroma = this.IsInside(rowIndex - 2, columnIndex);
            }

            if (this.SequenceHeader.ColorConfig.SubSamplingX && block4x4Width == 1)
            {
                partitionInfo.AvailableLeftForChroma = this.IsInside(rowIndex, columnIndex - 2);
            }
        }

        if (partitionInfo.AvailableUp)
        {
            partitionInfo.AboveModeInfo = superblockInfo.GetModeInfo(rowIndex - 1, columnIndex);
        }

        if (partitionInfo.AvailableLeft)
        {
            partitionInfo.LeftModeInfo = superblockInfo.GetModeInfo(rowIndex, columnIndex - 1);
        }

        this.ReadModeInfo(ref reader, partitionInfo);
        ReadPaletteTokens(ref reader, partitionInfo);
        ReadBlockTransformSize(ref reader, rowIndex, columnIndex, blockSize);
        if (partitionInfo.ModeInfo.Skip)
        {
            this.ResetSkipContext(partitionInfo);
        }

        this.Residual(rowIndex, columnIndex, blockSize);
    }

    private void ResetSkipContext(Av1PartitionInfo partitionInfo)
    {
        int planesCount = this.SequenceHeader.ColorConfig.IsMonochrome ? 1 : 3;
        for (int i = 0; i < planesCount; i++)
        {
            bool subX = i > 0 && this.SequenceHeader.ColorConfig.SubSamplingX;
            bool subY = i > 0 && this.SequenceHeader.ColorConfig.SubSamplingY;
            Av1BlockSize planeBlockSize = partitionInfo.ModeInfo.BlockSize.GetSubsampled(subX, subY);
            int txsWide = planeBlockSize.GetWidth() >> 2;
            int txsHigh = planeBlockSize.GetHeight() >> 2;
            int aboveOffset = (partitionInfo.ColumnIndex - this.TileInfo.TileColumnStartModeInfo[partitionInfo.ColumnIndex]) >> (subX ? 1 : 0);
            int leftOffset = (partitionInfo.RowIndex - this.TileInfo.TileRowStartModeInfo[partitionInfo.RowIndex]) >> (subY ? 1 : 0);
            int[] aboveContext = this.aboveContext.AboveContext[i + aboveOffset];
            int[] leftContext = this.leftContext.LeftContext[i + leftOffset];
            Array.Fill(aboveContext, 0);
            Array.Fill(leftContext, 0);
        }
    }

    private void Residual(int rowIndex, int columnIndex, Av1BlockSize blockSize)
    {
        bool subsamplingX = this.SequenceHeader.ColorConfig.SubSamplingX;
        bool subsamplingY = this.SequenceHeader.ColorConfig.SubSamplingY;
        int superBlockMask = this.SequenceHeader.Use128x128SuperBlock ? 31 : 15;
        int widthChunks = Math.Max(1, blockSize.Get4x4WideCount() >> 6);
        int heightChunks = Math.Max(1, blockSize.Get4x4HighCount() >> 6);
        Av1BlockSize sizeChunk = widthChunks > 1 || heightChunks > 1 ? Av1BlockSize.Block64x64 : blockSize;

        for (int chunkY = 0; chunkY < heightChunks; chunkY++)
        {
            for (int chunkX = 0; chunkX < widthChunks; chunkX++)
            {
                int rowChunk = rowIndex + (chunkY << 4);
                int columnChunk = columnIndex + (chunkX << 4);
                int subBlockRow = rowChunk & superBlockMask;
                int subBlockColumn = columnChunk & superBlockMask;
                for (int plane = 0; plane < 1 + (this.HasChroma(rowIndex, columnIndex, blockSize) ? 2 : 0); plane++)
                {
                    Av1TransformSize transformSize = this.FrameInfo.CodedLossless ? Av1TransformSize.Size4x4 : this.GetSize(plane, -1);
                    int stepX = transformSize.GetWidth() >> 2;
                    int stepY = transformSize.GetHeight() >> 2;
                    Av1BlockSize planeSize = this.GetPlaneResidualSize(sizeChunk, plane);
                    int num4x4Width = planeSize.Get4x4WideCount();
                    int num4x4Height = planeSize.Get4x4HighCount();
                    int subX = plane > 0 && subsamplingX ? 1 : 0;
                    int subY = plane > 0 && subsamplingY ? 1 : 0;
                    int baseX = (columnChunk >> subX) * (1 << ObuConstants.ModeInfoSizeLog2);
                    int baseY = (rowChunk >> subY) * (1 << ObuConstants.ModeInfoSizeLog2);
                    int baseXBlock = (columnIndex >> subX) * (1 << ObuConstants.ModeInfoSizeLog2);
                    int baseYBlock = (rowIndex >> subY) * (1 << ObuConstants.ModeInfoSizeLog2);
                    for (int y = 0; y < num4x4Height; y += stepY)
                    {
                        for (int x = 0; x < num4x4Width; x += stepX)
                        {
                            this.TransformBlock(plane, baseXBlock, baseYBlock, transformSize, x + (chunkX << 4 >> subX), y + (chunkY << 4 >> subY));
                        }
                    }
                }
            }
        }
    }

    private bool HasChroma(int rowIndex, int columnIndex, Av1BlockSize blockSize)
    {
        int bw = blockSize.Get4x4WideCount();
        int bh = blockSize.Get4x4HighCount();
        bool subX = this.SequenceHeader.ColorConfig.SubSamplingX;
        bool subY = this.SequenceHeader.ColorConfig.SubSamplingY;
        bool hasChroma = ((rowIndex & 0x01) != 0 || (bh & 0x01) == 0 || !subY) &&
            ((columnIndex & 0x01) != 0 || (bw & 0x01) == 0 || !subX);
        return hasChroma;
    }

    private Av1TransformSize GetSize(int plane, object transformSize) => throw new NotImplementedException();

    private Av1BlockSize GetPlaneResidualSize(Av1BlockSize sizeChunk, int plane) => throw new NotImplementedException();

    private void TransformBlock(int plane, int baseX, int baseY, Av1TransformSize transformSize, int x, int y)
    {
        Av1PartitionInfo partitionInfo = new(new(1, Av1BlockSize.Invalid), new(new(1, Av1BlockSize.Invalid), new()), false, Av1PartitionType.None);
        int startX = (baseX + 4) * x;
        int startY = (baseY + 4) * y;
        bool subsamplingX = this.SequenceHeader.ColorConfig.SubSamplingX;
        bool subsamplingY = this.SequenceHeader.ColorConfig.SubSamplingY;
        int subX = plane > 0 && subsamplingX ? 1 : 0;
        int subY = plane > 0 && subsamplingY ? 1 : 0;
        int columnIndex = startX << subX >> ObuConstants.ModeInfoSizeLog2;
        int rowIndex = startY << subY >> ObuConstants.ModeInfoSizeLog2;
        int superBlockMask = this.SequenceHeader.Use128x128SuperBlock ? 31 : 15;
        int subBlockColumn = columnIndex & superBlockMask;
        int subBlockRow = rowIndex & superBlockMask;
        int stepX = transformSize.GetWidth() >> ObuConstants.ModeInfoSizeLog2;
        int stepY = transformSize.GetHeight() >> ObuConstants.ModeInfoSizeLog2;
        int maxX = (this.SequenceHeader.ModeInfoSize * (1 << ObuConstants.ModeInfoSizeLog2)) >> subX;
        int maxY = (this.SequenceHeader.ModeInfoSize * (1 << ObuConstants.ModeInfoSizeLog2)) >> subY;
        if (startX >= maxX || startY >= maxY)
        {
            return;
        }

        if ((plane == 0 && partitionInfo.ModeInfo.GetPaletteSize(Av1PlaneType.Y) > 0) ||
            (plane != 0 && partitionInfo.ModeInfo.GetPaletteSize(Av1PlaneType.Uv) > 0))
        {
            this.PredictPalette(plane, startX, startY, x, y, transformSize);
        }
        else
        {
            bool isChromaFromLuma = plane > 0 && partitionInfo.ModeInfo.UvMode == Av1PredictionMode.UvChromaFromLuma;
            Av1PredictionMode mode;
            if (plane == 0)
            {
                mode = partitionInfo.ModeInfo.YMode;
            }
            else
            {
                mode = isChromaFromLuma ? Av1PredictionMode.DC : partitionInfo.ModeInfo.UvMode;
            }

            int log2Width = transformSize.GetWidthLog2();
            int log2Height = transformSize.GetHeightLog2();
            bool leftAvailable = x > 0 || plane == 0 ? partitionInfo.AvailableLeft : partitionInfo.AvailableLeftForChroma;
            bool upAvailable = y > 0 || plane == 0 ? partitionInfo.AvailableUp : partitionInfo.AvailableUpForChroma;
            bool haveAboveRight = this.blockDecoded[plane][(subBlockRow >> subY) - 1][(subBlockColumn >> subX) + stepX];
            bool haveBelowLeft = this.blockDecoded[plane][(subBlockRow >> subY) + stepY][(subBlockColumn >> subX) - 1];
            this.PredictIntra(plane, startX, startY, leftAvailable, upAvailable, haveAboveRight, haveBelowLeft, mode, log2Width, log2Height);
            if (isChromaFromLuma)
            {
                this.PredictChromaFromLuma(plane, startX, startY, transformSize);
            }
        }

        if (plane == 0)
        {
            this.maxLumaWidth = startX + (stepX * 4);
            this.maxLumaHeight = startY + (stepY * 4);
        }

        if (!partitionInfo.ModeInfo.Skip)
        {
            int eob = this.Coefficients(plane, startX, startY, transformSize);
            if (eob > 0)
            {
                this.Reconstruct(plane, startX, startY, transformSize);
            }
        }

        for (int i = 0; i < stepY; i++)
        {
            for (int j = 0; j < stepX; j++)
            {
                // Ignore loop filter.
                this.blockDecoded[plane][(subBlockRow >> subY) + i][(subBlockColumn >> subX) + j] = true;
            }
        }
    }

    private void Reconstruct(int plane, int startX, int startY, Av1TransformSize transformSize) => throw new NotImplementedException();

    private int Coefficients(int plane, int startX, int startY, Av1TransformSize transformSize) => throw new NotImplementedException();

    private void PredictChromaFromLuma(int plane, int startX, int startY, Av1TransformSize transformSize) => throw new NotImplementedException();

    private void PredictIntra(int plane, int startX, int startY, bool leftAvailable, bool upAvailable, bool haveAboveRight, bool haveBelowLeft, Av1PredictionMode mode, int log2Width, int log2Height) => throw new NotImplementedException();

    private void PredictPalette(int plane, int startX, int startY, int x, int y, Av1TransformSize transformSize) => throw new NotImplementedException();

    private void ResetBlockContext(int rowIndex, int columnIndex, Av1BlockSize blockSize)
    {
        int block4x4Width = blockSize.Get4x4WideCount();
        int block4x4Height = blockSize.Get4x4HighCount();
        bool subsamplingX = this.SequenceHeader.ColorConfig.SubSamplingX;
        bool subsamplingY = this.SequenceHeader.ColorConfig.SubSamplingY;
        int endPlane = this.HasChroma(rowIndex, columnIndex, blockSize) ? 3 : 1;
        this.aboveLevelContext = new int[3][];
        this.aboveDcContext = new int[3][];
        this.leftLevelContext = new int[3][];
        this.leftDcContext = new int[3][];
        for (int plane = 0; plane < endPlane; plane++)
        {
            int subX = plane > 0 && subsamplingX ? 1 : 0;
            int subY = plane > 0 && subsamplingY ? 1 : 0;
            this.aboveLevelContext[plane] = new int[(columnIndex + block4x4Width) >> subX];
            this.aboveDcContext[plane] = new int[(columnIndex + block4x4Width) >> subX];
            this.leftLevelContext[plane] = new int[(rowIndex + block4x4Height) >> subY];
            this.leftDcContext[plane] = new int[(rowIndex + block4x4Height) >> subY];
            Array.Fill(this.aboveLevelContext[plane], 0);
            Array.Fill(this.aboveDcContext[plane], 0);
            Array.Fill(this.leftLevelContext[plane], 0);
            Array.Fill(this.leftDcContext[plane], 0);
        }
    }

    private static void ReadBlockTransformSize(ref Av1SymbolDecoder reader, int rowIndex, int columnIndex, Av1BlockSize blockSize)
    {
        int block4x4Width = blockSize.Get4x4WideCount();
        int block4x4Height = blockSize.Get4x4HighCount();

        // First condition in spec is for INTER frames, implemented only the INTRA condition.
        ReadBlockTransformSize(ref reader, rowIndex, columnIndex, blockSize);
        /*for (int row = rowIndex; row < rowIndex + block4x4Height; row++)
        {
            for (int column = columnIndex; column < columnIndex + block4x4Width; column++)
            {
                this.InterTransformSizes[row][column] = this.TransformSize;
            }
        }*/
    }

    private static void ReadPaletteTokens(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
    {
        reader.ReadLiteral(-1);
        if (partitionInfo.ModeInfo.GetPaletteSize(Av1PlaneType.Y) != 0)
        {
            // Todo: Implement.
            throw new NotImplementedException();
        }

        if (partitionInfo.ModeInfo.GetPaletteSize(Av1PlaneType.Uv) != 0)
        {
            // Todo: Implement.
            throw new NotImplementedException();
        }
    }

    private void ReadModeInfo(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
    {
        DebugGuard.IsTrue(this.FrameInfo.FrameType is ObuFrameType.KeyFrame or ObuFrameType.IntraOnlyFrame, "Only INTRA frames supported.");
        this.ReadIntraFrameModeInfo(ref reader, partitionInfo);
    }

    private void ReadIntraFrameModeInfo(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
    {
        if (this.FrameInfo.SegmentationParameters.SegmentIdPrecedesSkip)
        {
            this.IntraSegmentId(ref reader, partitionInfo);
        }

        // this.skipMode = false;
        partitionInfo.ModeInfo.Skip = this.ReadSkip(ref reader, partitionInfo);
        if (!this.FrameInfo.SegmentationParameters.SegmentIdPrecedesSkip)
        {
            this.IntraSegmentId(ref reader, partitionInfo);
        }

        this.ReadCdef(ref reader, partitionInfo);

        bool readDeltas = false;
        if (readDeltas)
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
            partitionInfo.ModeInfo.AngleDelta[(int)Av1PlaneType.Y] = IntraAngleInfo(ref reader, partitionInfo.ModeInfo.YMode, partitionInfo.ModeInfo.BlockSize);
            if (partitionInfo.IsChroma && !this.SequenceHeader.ColorConfig.IsMonochrome)
            {
                partitionInfo.ModeInfo.UvMode = reader.ReadIntraModeUv(partitionInfo.ModeInfo.YMode, this.IsChromaForLumaAllowed(partitionInfo));
                if (partitionInfo.ModeInfo.UvMode == Av1PredictionMode.UvChromaFromLuma)
                {
                    this.ReadChromaFromLumaAlphas(ref reader, partitionInfo);
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
                this.FrameInfo.AllowScreenContentTools)
            {
                this.PaletteModeInfo(ref reader, partitionInfo);
            }

            this.FilterIntraModeInfo(ref reader, partitionInfo);
        }
    }

    private bool AllowIntraBlockCopy()
        => (this.FrameInfo.FrameType is ObuFrameType.KeyFrame or ObuFrameType.IntraOnlyFrame) &&
            (this.SequenceHeader.ForceScreenContentTools > 0) &&
            this.FrameInfo.AllowIntraBlockCopy;

    private bool IsChromaForLumaAllowed(Av1PartitionInfo partitionInfo)
    {
        if (this.FrameInfo.LosslessArray[partitionInfo.ModeInfo.SegmentId])
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

    private void PaletteModeInfo(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo) =>

        // TODO: Implement.
        throw new NotImplementedException();

    private void ReadChromaFromLumaAlphas(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo) =>

        // TODO: Implement.
        throw new NotImplementedException();

    private static int IntraAngleInfo(ref Av1SymbolDecoder reader, Av1PredictionMode mode, Av1BlockSize blockSize)
    {
        int angleDelta = 0;
        if (blockSize >= Av1BlockSize.Block8x8 && IsDirectionalMode(mode))
        {
            int symbol = reader.ReadAngleDelta(mode);
            angleDelta = symbol - ObuConstants.MaxAngleDelta;
        }

        return angleDelta;
    }

    private static bool IsDirectionalMode(Av1PredictionMode mode)
        => mode is >= Av1PredictionMode.Vertical and <= Av1PredictionMode.Directional67Degrees;

    private void IntraSegmentId(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
    {
        if (this.FrameInfo.SegmentationParameters.Enabled)
        {
            this.ReadSegmentId(ref reader, partitionInfo);
        }

        int bw4 = partitionInfo.ModeInfo.BlockSize.Get4x4WideCount();
        int bh4 = partitionInfo.ModeInfo.BlockSize.Get4x4HighCount();
        int x_mis = Math.Min(this.FrameInfo.ModeInfoColumnCount - partitionInfo.ColumnIndex, bw4);
        int y_mis = Math.Min(this.FrameInfo.ModeInfoRowCount - partitionInfo.RowIndex, bh4);

        for (int y = 0; y < y_mis; y++)
        {
            for (int x = 0; x < x_mis; x++)
            {
                this.segmentIds[partitionInfo.RowIndex + y][partitionInfo.ColumnIndex + x] = partitionInfo.ModeInfo.SegmentId;
            }
        }
    }

    private void ReadSegmentId(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
    {
        int predictor;
        int prevUL = -1;
        int prevU = -1;
        int prevL = -1;
        int columnIndex = partitionInfo.ColumnIndex;
        int rowIndex = partitionInfo.RowIndex;
        if (partitionInfo.AvailableUp && partitionInfo.AvailableLeft)
        {
            prevUL = this.GetSegmentId(partitionInfo, rowIndex - 1, columnIndex - 1);
        }

        if (partitionInfo.AvailableUp)
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
            int lastActiveSegmentId = this.FrameInfo.SegmentationParameters.LastActiveSegmentId;
            partitionInfo.ModeInfo.SegmentId = NegativeDeinterleave(reader.ReadSegmentId(ctx), predictor, lastActiveSegmentId + 1);
        }
    }

    private int GetSegmentId(Av1PartitionInfo partitionInfo, int rowIndex, int columnIndex)
    {
        int modeInfoOffset = (rowIndex * this.FrameInfo.ModeInfoColumnCount) + columnIndex;
        int bw4 = partitionInfo.ModeInfo.BlockSize.Get4x4WideCount();
        int bh4 = partitionInfo.ModeInfo.BlockSize.Get4x4HighCount();
        int xMin = Math.Min(this.FrameInfo.ModeInfoColumnCount - columnIndex, bw4);
        int yMin = Math.Min(this.FrameInfo.ModeInfoRowCount - rowIndex, bh4);
        int segmentId = ObuConstants.MaxSegments - 1;
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

    private void ReadCdef(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
    {
        if (partitionInfo.ModeInfo.Skip || this.FrameInfo.CodedLossless || !this.SequenceHeader.EnableCdef || this.FrameInfo.AllowIntraBlockCopy)
        {
            return;
        }

        int cdefSize4 = Av1BlockSize.Block64x64.Get4x4WideCount();
        int cdefMask4 = ~(cdefSize4 - 1);
        int r = partitionInfo.RowIndex & cdefMask4;
        int c = partitionInfo.ColumnIndex & cdefMask4;
        if (partitionInfo.CdefStrength[r][c] == -1)
        {
            partitionInfo.CdefStrength[r][c] = reader.ReadLiteral(this.FrameInfo.CdefParameters.BitCount);
            if (this.SequenceHeader.SuperBlockSize == Av1BlockSize.Block128x128)
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
        Av1BlockSize superBlockSize = this.SequenceHeader.Use128x128SuperBlock ? Av1BlockSize.Block128x128 : Av1BlockSize.Block64x64;
        if (this.FrameInfo.DeltaLoopFilterParameters.IsPresent ||
            (partitionInfo.ModeInfo.BlockSize == superBlockSize && partitionInfo.ModeInfo.Skip))
        {
            return;
        }

        if (this.FrameInfo.DeltaLoopFilterParameters.IsPresent)
        {
            int frameLoopFilterCount = 1;
            if (this.FrameInfo.DeltaLoopFilterParameters.Multi)
            {
                frameLoopFilterCount = this.SequenceHeader.ColorConfig.ChannelCount > 1 ? ObuConstants.FrameLoopFilterCount : ObuConstants.FrameLoopFilterCount - 2;
            }

            for (int i = 0; i < frameLoopFilterCount; i++)
            {
                int deltaLoopFilterAbsolute = reader.ReadDeltaLoopFilterAbsolute();
                if (deltaLoopFilterAbsolute == ObuConstants.DeltaLoopFilterSmall)
                {
                    int deltaLoopFilterRemainingBits = reader.ReadLiteral(3) + 1;
                    int deltaLoopFilterAbsoluteBitCount = reader.ReadLiteral(deltaLoopFilterRemainingBits);
                    deltaLoopFilterAbsolute = deltaLoopFilterAbsoluteBitCount + (1 << deltaLoopFilterRemainingBits) + 1;
                }

                if (deltaLoopFilterAbsolute != 0)
                {
                    bool deltaLoopFilterSign = reader.ReadLiteral(1) > 0;
                    int reducedDeltaLoopFilterLevel = deltaLoopFilterSign ? -deltaLoopFilterAbsolute : deltaLoopFilterAbsolute;
                    this.deltaLoopFilter[i] = Av1Math.Clip3(-ObuConstants.MaxLoopFilter, ObuConstants.MaxLoopFilter, this.deltaLoopFilter[i] + (reducedDeltaLoopFilterLevel << this.deltaLoopFilterResolution));
                }
            }
        }
    }

    private bool ReadSkip(ref Av1SymbolDecoder reader, Av1PartitionInfo partitionInfo)
    {
        int segmentId = partitionInfo.ModeInfo.SegmentId;
        if (this.FrameInfo.SegmentationParameters.SegmentIdPrecedesSkip &&
            this.FrameInfo.SegmentationParameters.IsFeatureActive(segmentId, ObuSegmentationLevelFeature.Skip))
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
        Av1BlockSize superBlockSize = this.SequenceHeader.Use128x128SuperBlock ? Av1BlockSize.Block128x128 : Av1BlockSize.Block64x64;
        if (!this.FrameInfo.DeltaQParameters.IsPresent ||
            (partitionInfo.ModeInfo.BlockSize == superBlockSize && partitionInfo.ModeInfo.Skip))
        {
            return;
        }

        if (partitionInfo.ModeInfo.BlockSize != this.SequenceHeader.SuperBlockSize || !partitionInfo.ModeInfo.Skip)
        {
            int deltaQuantizerAbsolute = reader.ReadDeltaQuantizerAbsolute();
            if (deltaQuantizerAbsolute == ObuConstants.DeltaQuantizerSmall)
            {
                int deltaQuantizerRemainingBits = reader.ReadLiteral(3) + 1;
                int deltaQuantizerAbsoluteBitCount = reader.ReadLiteral(deltaQuantizerRemainingBits);
                deltaQuantizerAbsolute = deltaQuantizerRemainingBits + (1 << deltaQuantizerRemainingBits) + 1;
            }

            if (deltaQuantizerAbsolute != 0)
            {
                bool deltaQuantizerSignBit = reader.ReadLiteral(1) > 0;
                int reducedDeltaQuantizerIndex = deltaQuantizerSignBit ? -deltaQuantizerAbsolute : deltaQuantizerAbsolute;
                this.currentQuantizerIndex = Av1Math.Clip3(1, 255, this.currentQuantizerIndex + (reducedDeltaQuantizerIndex << this.deltaQuantizerResolution));
                partitionInfo.SuperblockInfo.SuperblockDeltaQ = this.currentQuantizerIndex;
            }
        }
    }

    private bool IsInside(int rowIndex, int columnIndex) =>
        columnIndex >= this.TileInfo.TileColumnCount &&
        columnIndex < this.TileInfo.TileColumnCount &&
        rowIndex >= this.TileInfo.TileRowCount &&
        rowIndex < this.TileInfo.TileRowCount;

    /*
    private static bool IsChroma(int rowIndex, int columnIndex, Av1BlockModeInfo blockMode, bool subSamplingX, bool subSamplingY)
    {
        int block4x4Width = blockMode.BlockSize.Get4x4WideCount();
        int block4x4Height = blockMode.BlockSize.Get4x4HighCount();
        bool xPos = (columnIndex & 0x1) > 0 || (block4x4Width & 0x1) > 0 || !subSamplingX;
        bool yPos = (rowIndex & 0x1) > 0 || (block4x4Height & 0x1) > 0 || !subSamplingY;
        return xPos && yPos;
    }*/

    private int GetPartitionContext(int rowIndex, int columnIndex, Av1BlockSize blockSize)
    {
        // Maximum partition point is 8x8. Offset the log value occordingly.
        int blockSizeLog = blockSize.Get4x4WidthLog2() - Av1BlockSize.Block8x8.Get4x4WidthLog2();
        int aboveCtx = this.aboveContext.PartitionWidth + columnIndex - this.TileInfo.TileColumnStartModeInfo[columnIndex];
        int leftCtx = this.leftContext.PartitionHeight + rowIndex - this.TileInfo.TileRowStartModeInfo[rowIndex];
        int above = (aboveCtx >> blockSizeLog) & 0x1;
        int left = (leftCtx >> blockSizeLog) & 0x1;
        return (left * 2) + above + (blockSizeLog * PartitionProbabilitySet);
    }
}
