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

    private int[] deltaLoopFilter = [];
    private bool[][][] blockDecoded = [];
    private int[][] referenceSgrXqd = [];
    private int[][][] referenceLrWiener = [];
    private bool availableUp;
    private bool availableLeft;
    private bool availableUpForChroma;
    private bool availableLeftForChroma;
    private Av1ParseAboveContext aboveContext = new();
    private Av1ParseLeftContext leftContext = new();
    private bool skip;
    private bool readDeltas;
    private int currentQuantizerIndex;
    private Av1PredictionMode[][] yModes = [];
    private Av1PredictionMode yMode = Av1PredictionMode.DC;
    private Av1PredictionMode uvMode = Av1PredictionMode.DC;
    private Av1PredictionMode[][] uvModes = [];
    private object[] referenceFrame = [];
    private object[][][] referenceFrames = [];
    private int paletteSizeY;
    private int paletteSizeUv;
    private object[][] aboveLevelContext = [];
    private object[][] aboveDcContext = [];
    private object[][] leftLevelContext = [];
    private object[][] leftDcContext = [];
    private Av1TransformSize transformSize = Av1TransformSize.Size4x4;
    private object filterUltraMode = -1;
    private int angleDeltaY;
    private int angleDeltaUv;
    private bool lossless;
    private int[][] segmentIds = [];
    private int maxLumaWidth;
    private int maxLumaHeight;
    private int segmentId;
    private int[][] cdefIndex = [];
    private int deltaLoopFilterResolution = -1;
    private int deltaQuantizerResolution = -1;

    public Av1TileDecoder(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, ObuTileInfo tileInfo)
    {
        this.FrameInfo = frameInfo;
        this.SequenceHeader = sequenceHeader;
        this.TileInfo = tileInfo;
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
        int tileRowIndex = tileNum / this.TileInfo.TileColumnCount;
        int tileColumnIndex = tileNum % this.TileInfo.TileColumnCount;
        this.aboveContext.Clear();
        this.ClearLoopFilterDelta();
        int planesCount = this.SequenceHeader.ColorConfig.ChannelCount;
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

                // Nothing to do for CDEF
                // this.ClearCdef(row, column);
                this.ClearBlockDecodedFlags(row, column, superBlock4x4Size);
                this.ReadLoopRestoration(row, column, superBlockSize);
                this.DecodePartition(ref reader, row, column, superBlockSize);
            }
        }
    }

    private void ClearLoopFilterDelta() => this.deltaLoopFilter = new int[4];

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

    private void DecodePartition(ref Av1SymbolDecoder reader, int rowIndex, int columnIndex, Av1BlockSize blockSize)
    {
        if (rowIndex >= this.TileInfo.TileRowStartModeInfo[rowIndex] ||
            columnIndex >= this.TileInfo.TileColumnStartModeInfo[columnIndex])
        {
            return;
        }

        this.availableUp = this.IsInside(rowIndex - 1, columnIndex);
        this.availableLeft = this.IsInside(rowIndex, columnIndex - 1);
        int block4x4Size = blockSize.Get4x4WideCount();
        int halfBlock4x4Size = block4x4Size >> 1;
        int quarterBlock4x4Size = halfBlock4x4Size >> 2;
        bool hasRows = rowIndex + halfBlock4x4Size < this.TileInfo.TileRowCount;
        bool hasColumns = columnIndex + halfBlock4x4Size < this.TileInfo.TileColumnCount;
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
                this.DecodePartition(ref reader, rowIndex, columnIndex, subSize);
                this.DecodePartition(ref reader, rowIndex, columnIndex + halfBlock4x4Size, subSize);
                this.DecodePartition(ref reader, rowIndex + halfBlock4x4Size, columnIndex, subSize);
                this.DecodePartition(ref reader, rowIndex + halfBlock4x4Size, columnIndex + halfBlock4x4Size, subSize);
                break;
            case Av1PartitionType.None:
                this.DecodeBlock(ref reader, rowIndex, columnIndex, subSize);
                break;
            default:
                throw new NotImplementedException($"Partition type: {partitionType} is not supported.");
        }
    }

    private void DecodeBlock(ref Av1SymbolDecoder reader, int rowIndex, int columnIndex, Av1BlockSize blockSize)
    {
        int block4x4Width = blockSize.Get4x4WideCount();
        int block4x4Height = blockSize.Get4x4HighCount();
        int planesCount = this.SequenceHeader.ColorConfig.ChannelCount;
        bool hasChroma = planesCount > 1;
        if (block4x4Height == 1 && this.SequenceHeader.ColorConfig.SubSamplingY && (rowIndex & 0x1) == 0)
        {
            hasChroma = false;
        }

        if (block4x4Width == 1 && this.SequenceHeader.ColorConfig.SubSamplingX && (columnIndex & 0x1) == 0)
        {
            hasChroma = false;
        }

        this.availableUp = this.IsInside(rowIndex - 1, columnIndex);
        this.availableLeft = this.IsInside(rowIndex, columnIndex - 1);
        this.availableUpForChroma = this.availableUp;
        this.availableLeftForChroma = this.availableLeft;
        if (hasChroma)
        {
            if (this.SequenceHeader.ColorConfig.SubSamplingY && block4x4Height == 1)
            {
                this.availableUpForChroma = this.IsInside(rowIndex - 2, columnIndex);
            }

            if (this.SequenceHeader.ColorConfig.SubSamplingX && block4x4Width == 1)
            {
                this.availableLeftForChroma = this.IsInside(rowIndex, columnIndex - 2);
            }
        }

        this.ReadModeInfo(ref reader, rowIndex, columnIndex, blockSize);
        this.ReadPaletteTokens(ref reader);
        ReadBlockTransformSize(ref reader, rowIndex, columnIndex, blockSize);
        if (this.skip)
        {
            this.ResetBlockContext(rowIndex, columnIndex, blockSize);
        }

        // bool isCompound = false;
        for (int y = 0; y < block4x4Height; y++)
        {
            for (int x = 0; x < block4x4Width; x++)
            {
                this.yModes[rowIndex + y][columnIndex + x] = this.yMode;
                if (this.referenceFrame[0] == (object)ObuFrameType.IntraOnlyFrame && hasChroma)
                {
                    this.uvModes[rowIndex + y][columnIndex + x] = this.uvMode;
                }

                for (int refList = 0; refList < 2; refList++)
                {
                    this.referenceFrames[rowIndex + y][columnIndex + x][refList] = this.referenceFrame[refList];
                }
            }
        }

        ComputePrediction();
        this.Residual(rowIndex, columnIndex, blockSize);
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
                    Av1TransformSize transformSize = this.FrameInfo.CodedLossless ? Av1TransformSize.Size4x4 : this.GetSize(plane, this.transformSize);
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

        if ((plane == 0 && this.paletteSizeY > 0) ||
            (plane != 0 && this.paletteSizeUv > 0))
        {
            this.PredictPalette(plane, startX, startY, x, y, transformSize);
        }
        else
        {
            bool isChromaFromLuma = plane > 0 && this.uvMode == Av1PredictionMode.UvChromaFromLuma;
            Av1PredictionMode mode;
            if (plane == 0)
            {
                mode = this.yMode;
            }
            else
            {
                mode = isChromaFromLuma ? Av1PredictionMode.DC : this.uvMode;
            }

            int log2Width = transformSize.GetWidthLog2();
            int log2Height = transformSize.GetHeightLog2();
            bool leftAvailable = x > 0 || plane == 0 ? this.availableLeft : this.availableLeftForChroma;
            bool upAvailable = y > 0 || plane == 0 ? this.availableUp : this.availableUpForChroma;
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

        if (!this.skip)
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

    private static void ComputePrediction()
    {
        // Not applicable for INTRA frames.
    }

    private void ResetBlockContext(int rowIndex, int columnIndex, Av1BlockSize blockSize)
    {
        int block4x4Width = blockSize.Get4x4WideCount();
        int block4x4Height = blockSize.Get4x4HighCount();
        bool subsamplingX = this.SequenceHeader.ColorConfig.SubSamplingX;
        bool subsamplingY = this.SequenceHeader.ColorConfig.SubSamplingY;
        int endPlane = this.HasChroma(rowIndex, columnIndex, blockSize) ? 3 : 1;
        for (int plane = 0; plane < endPlane; plane++)
        {
            int subX = plane > 0 && subsamplingX ? 1 : 0;
            int subY = plane > 0 && subsamplingY ? 1 : 0;
            for (int i = columnIndex >> subX; i < (columnIndex + block4x4Width) >> subX; i++)
            {
                this.aboveLevelContext[plane][i] = 0;
                this.aboveDcContext[plane][i] = 0;
            }

            for (int i = rowIndex >> subY; i < (rowIndex + block4x4Height) >> subY; i++)
            {
                this.leftLevelContext[plane][i] = 0;
                this.leftDcContext[plane][i] = 0;
            }
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

    private void ReadPaletteTokens(ref Av1SymbolDecoder reader)
    {
        reader.ReadLiteral(-1);
        if (this.paletteSizeY != 0)
        {
            // Todo: Implement.
            throw new NotImplementedException();
        }

        if (this.paletteSizeUv != 0)
        {
            // Todo: Implement.
            throw new NotImplementedException();
        }
    }

    private void ReadModeInfo(ref Av1SymbolDecoder reader, int rowIndex, int columnIndex, Av1BlockSize blockSize)
        => this.ReadIntraFrameModeInfo(ref reader, rowIndex, columnIndex, blockSize);

    private void ReadIntraFrameModeInfo(ref Av1SymbolDecoder reader, int rowIndex, int columnIndex, Av1BlockSize blockSize)
    {
        this.skip = false;
        if (this.FrameInfo.SegmentationParameters.SegmentIdPrecedesSkip)
        {
            this.ReadIntraSegmentId(ref reader);
        }

        // this.skipMode = false;
        this.ReadSkip(ref reader);
        if (!this.FrameInfo.SegmentationParameters.SegmentIdPrecedesSkip)
        {
            this.IntraSegmentId(ref reader, rowIndex, columnIndex);
        }

        this.ReadCdef(ref reader, rowIndex, columnIndex, blockSize);
        this.ReadDeltaQuantizerIndex(ref reader, blockSize);
        this.ReadDeltaLoopFilter(ref reader, blockSize);
        this.readDeltas = false;
        this.referenceFrame[0] = -1; // IntraFrame;
        this.referenceFrame[1] = -1; // None;
        bool useIntraBlockCopy = false;
        if (this.FrameInfo.AllowIntraBlockCopy)
        {
            useIntraBlockCopy = reader.ReadUseIntraBlockCopy();
        }

        if (useIntraBlockCopy)
        {
            // TODO: Implement
        }
        else
        {
            // this.IsInter = false;
            this.yMode = reader.ReadIntraFrameYMode(blockSize);
            this.IntraAngleInfoY(ref reader, blockSize);
            if (this.HasChroma(rowIndex, columnIndex, blockSize))
            {
                this.uvMode = reader.ReadUvMode(blockSize, this.IsChromaForLumaAllowed(blockSize));
                if (this.uvMode == Av1PredictionMode.UvChromaFromLuma)
                {
                    this.ReadChromaFromLumaAlphas(ref reader);
                }

                this.IntraAngleInfoUv(ref reader, blockSize);
            }

            this.paletteSizeY = 0;
            this.paletteSizeUv = 0;
            if (this.SequenceHeader.ModeInfoSize >= (int)Av1BlockSize.Block8x8 &&
                ((Av1BlockSize)this.SequenceHeader.ModeInfoSize).Get4x4WideCount() <= 64 &&
                ((Av1BlockSize)this.SequenceHeader.ModeInfoSize).Get4x4HighCount() <= 64 &&
                this.FrameInfo.AllowScreenContentTools)
            {
                this.PaletteModeInfo(ref reader);
            }

            this.FilterIntraModeInfo(ref reader, blockSize);
        }
    }

    private bool IsChromaForLumaAllowed(Av1BlockSize blockSize)
    {
        if (this.lossless)
        {
            // In lossless, CfL is available when the partition size is equal to the
            // transform size.
            bool subX = this.SequenceHeader.ColorConfig.SubSamplingX;
            bool subY = this.SequenceHeader.ColorConfig.SubSamplingY;
            Av1BlockSize planeBlockSize = blockSize.GetSubsampled(subX, subY);
            return planeBlockSize == Av1BlockSize.Block4x4;
        }

        // Spec: CfL is available to luma partitions lesser than or equal to 32x32
        return blockSize.Get4x4WideCount() <= 32 && blockSize.Get4x4HighCount() <= 32;
    }

    private void ReadIntraSegmentId(ref Av1SymbolDecoder reader) => throw new NotImplementedException();

    private void FilterIntraModeInfo(ref Av1SymbolDecoder reader, Av1BlockSize blockSize)
    {
        bool useFilterIntra = false;
        if (this.SequenceHeader.EnableFilterIntra &&
            this.yMode == Av1PredictionMode.DC && this.paletteSizeY == 0 &&
            Math.Max(blockSize.GetWidth(), blockSize.GetHeight()) <= 32)
        {
            useFilterIntra = reader.ReadUseFilterUltra();
            if (useFilterIntra)
            {
                this.filterUltraMode = reader.ReadFilterUltraMode();
            }
        }
    }

    private void PaletteModeInfo(ref Av1SymbolDecoder reader) =>

        // TODO: Implement.
        throw new NotImplementedException();

    private void ReadChromaFromLumaAlphas(ref Av1SymbolDecoder reader) =>

        // TODO: Implement.
        throw new NotImplementedException();

    private void IntraAngleInfoY(ref Av1SymbolDecoder reader, Av1BlockSize blockSize)
    {
        this.angleDeltaY = 0;
        if (blockSize >= Av1BlockSize.Block8x8 && IsDirectionalMode(this.yMode))
        {
            int angleDeltaY = reader.ReadAngleDelta(this.yMode);
            this.angleDeltaY = angleDeltaY - ObuConstants.MaxAngleDelta;
        }
    }

    private void IntraAngleInfoUv(ref Av1SymbolDecoder reader, Av1BlockSize blockSize)
    {
        this.angleDeltaUv = 0;
        if (blockSize >= Av1BlockSize.Block8x8 && IsDirectionalMode(this.uvMode))
        {
            int angleDeltaUv = reader.ReadAngleDelta(this.uvMode);
            this.angleDeltaUv = angleDeltaUv - ObuConstants.MaxAngleDelta;
        }
    }

    private static bool IsDirectionalMode(Av1PredictionMode mode)
        => mode is >= Av1PredictionMode.Vertical and <= Av1PredictionMode.Directional67Degrees;

    private void IntraSegmentId(ref Av1SymbolDecoder reader, int rowIndex, int columnIndex)
    {
        if (this.FrameInfo.SegmentationParameters.Enabled)
        {
            this.ReadSegmentId(ref reader, rowIndex, columnIndex);
        }
        else
        {
            this.segmentId = 0;
        }

        this.lossless = this.FrameInfo.LosslessArray[this.segmentId];
    }

    private void ReadSegmentId(ref Av1SymbolDecoder reader, int rowIndex, int columnIndex)
    {
        int pred;
        int prevUL = -1;
        int prevU = -1;
        int prevL = -1;
        if (this.availableUp && this.availableLeft)
        {
            prevUL = this.segmentIds[rowIndex - 1][columnIndex - 1];
        }

        if (this.availableUp)
        {
            prevU = this.segmentIds[rowIndex - 1][columnIndex];
        }

        if (this.availableLeft)
        {
            prevU = this.segmentIds[rowIndex][columnIndex - 1];
        }

        if (prevU == -1)
        {
            pred = prevL == -1 ? 0 : prevL;
        }
        else if (prevL == -1)
        {
            pred = prevU;
        }
        else
        {
            pred = prevU == prevUL ? prevU : prevL;
        }

        if (this.skip)
        {
            this.segmentId = 0;
        }
        else
        {
            int lastActiveSegmentId = this.FrameInfo.SegmentationParameters.LastActiveSegmentId;
            this.segmentId = NegativeDeinterleave(reader.ReadSegmentId(-1), pred, lastActiveSegmentId + 1);
        }
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

    private void ReadCdef(ref Av1SymbolDecoder reader, int rowIndex, int columnIndex, Av1BlockSize blockSize)
    {
        if (this.skip || this.FrameInfo.CodedLossless || !this.SequenceHeader.EnableCdef || this.FrameInfo.AllowIntraBlockCopy)
        {
            return;
        }

        int cdefSize4 = Av1BlockSize.Block64x64.Get4x4WideCount();
        int cdefMask4 = ~(cdefSize4 - 1);
        int r = rowIndex & cdefMask4;
        int c = columnIndex & cdefMask4;
        if (this.cdefIndex[r][c] == -1)
        {
            this.cdefIndex[r][c] = reader.ReadLiteral(this.FrameInfo.CdefParameters.BitCount);
            int w4 = blockSize.Get4x4WideCount();
            int h4 = blockSize.Get4x4HighCount();
            for (int i = r; i < r + h4; i += cdefSize4)
            {
                for (int j = c; j < c + w4; j += cdefSize4)
                {
                    this.cdefIndex[i][j] = this.cdefIndex[r][c];
                }
            }
        }
    }

    private void ReadDeltaLoopFilter(ref Av1SymbolDecoder reader, Av1BlockSize blockSize)
    {
        Av1BlockSize superBlockSize = this.SequenceHeader.Use128x128SuperBlock ? Av1BlockSize.Block128x128 : Av1BlockSize.Block64x64;
        if (blockSize == superBlockSize && this.skip)
        {
            return;
        }

        if (this.readDeltas && this.FrameInfo.DeltaLoopFilterParameters.IsPresent)
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

    private void ReadSkip(ref Av1SymbolDecoder reader)
    {
        if (this.FrameInfo.SegmentationParameters.SegmentIdPrecedesSkip &&
            this.FrameInfo.SegmentationParameters.IsFeatureActive(-1, ObuSegmentationFeature.LevelSkip))
        {
            this.skip = true;
        }
        else
        {
            this.skip = reader.ReadSkip(-1);
        }
    }

    private void ReadDeltaQuantizerIndex(ref Av1SymbolDecoder reader, Av1BlockSize blockSize)
    {
        Av1BlockSize superBlockSize = this.SequenceHeader.Use128x128SuperBlock ? Av1BlockSize.Block128x128 : Av1BlockSize.Block64x64;
        if (blockSize == superBlockSize && this.skip)
        {
            return;
        }

        if (this.readDeltas)
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
