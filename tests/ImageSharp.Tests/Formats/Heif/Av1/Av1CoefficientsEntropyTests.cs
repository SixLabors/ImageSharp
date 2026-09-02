// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1CoefficientsEntropyTests
{
    private const int BaseQIndex = 23;

    [Fact]
    public void NeighborArrayWritesEveryCoveredFourByFourEdgeUnit()
    {
        using Av1NeighborArrayUnit<byte> neighbors = new(
            Configuration.Default,
            leftSize: 8,
            topSize: 8,
            topLeftSize: 16)
        {
            GranularityNormalLog2 = 2,
            GranularityTopLeftLog2 = 2
        };

        neighbors.UnitModeWrite(
            37,
            new Point(8, 4),
            new Size(16, 8),
            Av1NeighborArrayUnit<byte>.UnitMask.Top | Av1NeighborArrayUnit<byte>.UnitMask.Left);

        Assert.Equal(new byte[] { 0, 0, 37, 37, 37, 37, 0, 0 }, neighbors.Top.ToArray());
        Assert.Equal(new byte[] { 0, 37, 37, 0, 0, 0, 0, 0 }, neighbors.Left.ToArray());
    }

    [Theory]
    [InlineData((int)Av1ComponentType.Luminance, 5)]
    [InlineData((int)Av1ComponentType.Chroma, 12)]
    public void WriterDerivesTransformContextFromCompleteFourByFourEdges(
        int componentType,
        int expectedSkipContext)
    {
        using Av1NeighborArrayUnit<byte> neighbors = new(
            Configuration.Default,
            leftSize: 8,
            topSize: 8,
            topLeftSize: 16)
        {
            GranularityNormalLog2 = 2,
            GranularityTopLeftLog2 = 2
        };

        // The high bits carry positive, positive, and negative DC signs. The low bits select
        // the high-above and low-left coefficient classes used by the luma skip-context table.
        neighbors.Top[2] = (2 << Av1Constants.CoefficientContextBitCount) | 4;
        neighbors.Top[3] = 2 << Av1Constants.CoefficientContextBitCount;
        neighbors.Left[1] = (1 << Av1Constants.CoefficientContextBitCount) | 1;
        Av1TransformBlockContext context = Av1TileWriter.GetTransformBlockContexts(
            (Av1ComponentType)componentType,
            neighbors,
            new Point(8, 4),
            Av1BlockSize.Block16x16,
            Av1TransformSize.Size8x8);

        Assert.Equal(2, context.DcSignContext);
        Assert.Equal(expectedSkipContext, context.SkipContext);
    }

    [Theory]
    [InlineData(false, 2, 1, 6, 6)]
    [InlineData(true, 2, 2, 10, 3)]
    public void PictureControlSetMapsModeInfoAllocationByReference(
        bool disallow4x4,
        int column,
        int row,
        int gridOffset,
        int allocationOffset)
    {
        Av1ModeInfo expected = CreateModeInfo(Av1PredictionMode.Paeth);
        Av1ModeInfo[] allocation = new Av1ModeInfo[8];
        allocation[allocationOffset] = expected;
        Av1PictureControlSet picture = new()
        {
            PartitionContexts = [],
            LuminanceDcSignLevelCoefficientNeighbors = [],
            CrDcSignLevelCoefficientNeighbors = [],
            CbDcSignLevelCoefficientNeighbors = [],
            TransformFunctionContexts = [],
            Sequence = new Av1SequenceControlSet { SequenceHeader = new ObuSequenceHeader() },
            Parent = new Av1PictureParentControlSet
            {
                Common = new Av1EncoderCommon
                {
                    FrameSize = new ObuFrameSize(),
                    TilesInfo = new ObuTileGroupHeader()
                },
                FrameHeader = new ObuFrameHeader(),
                PreviousQIndex = [],
                SuperblockGeometry = []
            },
            SegmentationNeighborMap = [],
            ModeInfoGrid = new Av1ModeInfo[16],
            ModeInfoAllocation = allocation,
            ModeInfoStride = 4,
            Disallow4x4AllFrames = disallow4x4,
            CdefPreset = []
        };

        Av1MacroBlockModeInfo result = picture.GetMacroBlockModeInfo(new Point(column, row));

        Assert.Same(expected.MacroBlockModeInfo, result);
        Assert.Same(expected, picture.ModeInfoGrid[gridOffset]);
    }

    [Fact]
    public void MacroBlockReadsNeighborsRelativeToCurrentGridEntry()
    {
        Av1ModeInfo above = CreateModeInfo(Av1PredictionMode.Vertical);
        Av1ModeInfo left = CreateModeInfo(Av1PredictionMode.Horizontal);
        Av1ModeInfo current = CreateModeInfo(Av1PredictionMode.DC);
        Av1ModeInfo[] grid = new Av1ModeInfo[9];
        grid[1] = above;
        grid[3] = left;
        grid[4] = current;
        Av1MacroBlockD macroBlock = CreateMacroBlock();
        macroBlock.SetModeInfoGrid(grid, 4);

        Assert.Same(left, macroBlock.GetRelativeModeInfo(-1));
        Assert.Same(above, macroBlock.GetRelativeModeInfo(-3));
        Assert.Same(current, macroBlock.GetRelativeModeInfo(0));
    }

    [Fact]
    public void EncoderBlockModeInfoStoresSelectedSyntax()
    {
        Av1EncoderBlockModeInfo modeInfo = new();

        Assert.False(modeInfo.Skip);
        Assert.False(modeInfo.SkipMode);
        Assert.False(modeInfo.UseIntraBlockCopy);

        modeInfo.Skip = true;
        modeInfo.SkipMode = true;
        modeInfo.UseIntraBlockCopy = true;
        modeInfo.BlockSize = Av1BlockSize.Block16x16;
        modeInfo.PartitionType = Av1PartitionType.Split;
        modeInfo.SegmentId = 3;
        modeInfo.Mode = Av1PredictionMode.Smooth;
        modeInfo.UvMode = Av1ChromaPredictionMode.Smooth;

        Assert.True(modeInfo.Skip);
        Assert.True(modeInfo.SkipMode);
        Assert.True(modeInfo.UseIntraBlockCopy);
        Assert.Equal(Av1BlockSize.Block16x16, modeInfo.BlockSize);
        Assert.Equal(Av1PartitionType.Split, modeInfo.PartitionType);
        Assert.Equal(3, modeInfo.SegmentId);
        Assert.Equal(Av1PredictionMode.Smooth, modeInfo.Mode);
        Assert.Equal(Av1ChromaPredictionMode.Smooth, modeInfo.UvMode);
    }

    [Fact]
    public void EncoderBlockInlineStateSupportsEveryTransformWithoutTraversalAllocations()
    {
        Av1EncoderBlockStruct block = new() { MacroBlock = CreateMacroBlock() };

        long before = GC.GetAllocatedBytesForCurrentThread();
        Span<Av1TransformUnit> transforms = block.TransformBlocks;
        transforms[^1].NzCoefficientCount[2] = 17;
        transforms[^1].TransformType[(int)Av1PlaneType.Uv] = Av1TransformType.VerticalAdst;
        block.PaletteSize[0] = 3;
        block.PaletteSize[1] = 5;
        block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = -2;
        block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv] = 3;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(Av1Constants.MaxTransformUnitCount, transforms.Length);
        Assert.Equal(17, block.TransformBlocks[^1].NzCoefficientCount[2]);
        Assert.Equal(Av1TransformType.VerticalAdst, block.TransformBlocks[^1].TransformType[(int)Av1PlaneType.Uv]);
        Assert.Equal(3, block.PaletteSize[0]);
        Assert.Equal(5, block.PaletteSize[1]);
        Assert.Equal(-2, block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y]);
        Assert.Equal(3, block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv]);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void RoundTripZeroEndOfBlock()
    {
        // Assign
        Av1BlockSize blockSize = Av1BlockSize.Block4x4;
        Av1TransformSize transformSize = Av1TransformSize.Size4x4;
        Av1TransformType transformType = Av1TransformType.Identity;
        Av1PredictionMode intraDirection = Av1PredictionMode.DC;
        Av1ComponentType componentType = Av1ComponentType.Luminance;
        Av1FilterIntraMode filterIntraMode = Av1FilterIntraMode.DC;
        ushort endOfBlock = 0;
        Av1BlockModeInfo modeInfo = new(blockSize, new Point(0, 0));
        Av1TransformInfo transformInfo = new(transformSize, 0, 0);
        int[] aboveContexts = new int[1];
        int[] leftContexts = new int[1];
        Av1TransformBlockContext transformBlockContext = new();
        Configuration configuration = Configuration.Default;
        Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        Span<int> coefficientsBuffer = [1, 2, 3, 4, 5];
        Span<int> expected = new int[16];
        Span<int> actuals = new int[16];

        // Act
        encoder.WriteCoefficients(transformSize, transformType, intraDirection, coefficientsBuffer, componentType, transformBlockContext, endOfBlock, true, filterIntraMode);

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        using Av1LevelBuffer levels = new(Configuration.Default);
        decoder.ReadCoefficients(
            modeInfo,
            new Point(0, 0),
            aboveContexts,
            leftContexts,
            0,
            0,
            0,
            1,
            1,
            transformBlockContext,
            transformSize,
            false,
            true,
            transformType,
            ref transformInfo,
            0,
            0,
            levels,
            actuals);

        // Assert
        Assert.Equal(endOfBlock, actuals[0]);
        Assert.Equal(expected, actuals);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    public void RoundTripFullBlock(ushort endOfBlock)
    {
        // Assign
        const Av1BlockSize blockSize = Av1BlockSize.Block4x4;
        const Av1TransformSize transformSize = Av1TransformSize.Size4x4;
        const Av1TransformType transformType = Av1TransformType.Identity;
        const Av1PredictionMode intraDirection = Av1PredictionMode.DC;
        const Av1ComponentType componentType = Av1ComponentType.Luminance;
        const Av1FilterIntraMode filterIntraMode = Av1FilterIntraMode.DC;
        Av1BlockModeInfo modeInfo = new(blockSize, new Point(0, 0));
        Av1TransformInfo transformInfo = new(transformSize, 0, 0);
        int[] aboveContexts = new int[1];
        int[] leftContexts = new int[1];
        Av1TransformBlockContext transformBlockContext = new();
        Configuration configuration = Configuration.Default;
        Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        Span<int> coefficientsBuffer = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        Span<int> actuals = new int[16 + 1];

        // Act
        encoder.WriteCoefficients(transformSize, transformType, intraDirection, coefficientsBuffer, componentType, transformBlockContext, endOfBlock, true, filterIntraMode);

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        using Av1LevelBuffer levels = new(Configuration.Default);
        int plane = Math.Min((int)componentType, 1);
        decoder.ReadCoefficients(
            modeInfo,
            new Point(0, 0),
            aboveContexts,
            leftContexts,
            0,
            0,
            plane,
            1,
            1,
            transformBlockContext,
            transformSize,
            false,
            true,
            transformType,
            ref transformInfo,
            0,
            0,
            levels,
            actuals);

        // Assert
        Assert.Equal(endOfBlock, actuals[0]);
    }

    [Theory]
    [MemberData(nameof(GetTransformTypes))]
    public void RoundTripFullCoefficientsYSize4x4(int txType)
    {
        // Assign
        const ushort endOfBlock = 16;
        const Av1ComponentType componentType = Av1ComponentType.Luminance;
        Av1BlockSize blockSize = Av1BlockSize.Block4x4;
        Av1TransformSize transformSize = blockSize.GetMaximumTransformSize();
        Av1TransformType transformType = (Av1TransformType)txType;
        Av1PredictionMode intraDirection = Av1PredictionMode.DC;
        Av1FilterIntraMode filterIntraMode = Av1FilterIntraMode.DC;
        RoundTripCoefficientsCore(endOfBlock, componentType, blockSize, transformSize, transformType, intraDirection, filterIntraMode);
    }

    [Theory]
    [MemberData(nameof(GetTransformTypes))]
    public void RoundTripFullCoefficientsUvSize4x4(int txType)
    {
        // Assign
        const ushort endOfBlock = 16;
        const Av1ComponentType componentType = Av1ComponentType.Chroma;
        Av1BlockSize blockSize = Av1BlockSize.Block4x4;
        Av1TransformSize transformSize = blockSize.GetMaxUvTransformSize(true, true);
        Av1TransformType transformType = (Av1TransformType)txType;
        Av1PredictionMode intraDirection = Av1PredictionMode.DC;
        Av1FilterIntraMode filterIntraMode = Av1FilterIntraMode.DC;
        RoundTripCoefficientsCore(endOfBlock, componentType, blockSize, transformSize, transformType, intraDirection, filterIntraMode);
    }

    private static void RoundTripCoefficientsCore(ushort endOfBlock, Av1ComponentType componentType, Av1BlockSize blockSize, Av1TransformSize transformSize, Av1TransformType transformType, Av1PredictionMode intraDirection, Av1FilterIntraMode filterIntraMode)
    {
        Av1BlockModeInfo modeInfo = new(blockSize, new Point(0, 0));
        Av1TransformInfo transformInfo = new(transformSize, 0, 0);
        int[] aboveContexts = new int[transformSize.Get4x4WideCount()];
        int[] leftContexts = new int[transformSize.Get4x4HighCount()];
        Av1TransformBlockContext transformBlockContext = new();
        Configuration configuration = Configuration.Default;
        Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        Span<int> coefficientsBuffer = Enumerable.Range(0, blockSize.GetHeight() * blockSize.GetWidth()).ToArray();
        Span<int> actuals = new int[16 + 1];

        // Act
        encoder.WriteCoefficients(transformSize, transformType, intraDirection, coefficientsBuffer, componentType, transformBlockContext, endOfBlock, true, filterIntraMode);
        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        using Av1LevelBuffer levels = new(Configuration.Default);
        int plane = Math.Min((int)componentType, 1);
        decoder.ReadCoefficients(
            modeInfo,
            new Point(0, 0),
            aboveContexts,
            leftContexts,
            0,
            0,
            plane,
            1,
            1,
            transformBlockContext,
            transformSize,
            false,
            true,
            transformType,
            ref transformInfo,
            0,
            0,
            levels,
            actuals);

        // Assert
        Assert.Equal(endOfBlock, actuals[0]);
        ReadOnlySpan<short> scan = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType).Scan;

        // The parser retains quantized levels in entropy scan order; inverse quantization maps them back to raster positions.
        for (int coefficientIndex = 0; coefficientIndex < endOfBlock; coefficientIndex++)
        {
            Assert.Equal(coefficientsBuffer[scan[coefficientIndex]], actuals[coefficientIndex + 1]);
        }
    }

    private static Av1ModeInfo CreateModeInfo(Av1PredictionMode mode)
        => new()
        {
            MacroBlockModeInfo = new Av1MacroBlockModeInfo
            {
                Block = new Av1EncoderBlockModeInfo { Mode = mode }
            }
        };

    private static Av1MacroBlockD CreateMacroBlock()
    {
        ObuTileGroupHeader tiles = new()
        {
            TileColumnCount = 1,
            TileRowCount = 1
        };

        tiles.TileColumnStartModeInfo[1] = 3;
        tiles.TileRowStartModeInfo[1] = 3;
        ObuFrameHeader frameHeader = new()
        {
            ModeInfoColumnCount = 3,
            ModeInfoRowCount = 3,
            TilesInfo = tiles
        };

        return new Av1MacroBlockD { Tile = new Av1TileInfo(0, 0, frameHeader) };
    }

    public static TheoryData<int> GetTransformTypes()
    {
        TheoryData<int> result = [];
        for (Av1TransformType transformType = Av1TransformType.DctDct; transformType < Av1TransformType.VerticalDct; transformType++)
        {
            result.Add((int)transformType);
        }

        return result;
    }
}
