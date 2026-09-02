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
                PreviousQIndex = []
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
    public void EncoderBlocksKeepInlineModeStateWithoutPerBlockAllocations()
    {
        Av1EncoderBlockStruct[] blocks = new Av1EncoderBlockStruct[2];

        long before = GC.GetAllocatedBytesForCurrentThread();
        ref Av1EncoderBlockStruct block = ref blocks[1];
        block.PaletteSize[0] = 3;
        block.PaletteSize[1] = 5;
        block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = -2;
        block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv] = 3;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(3, blocks[1].PaletteSize[0]);
        Assert.Equal(5, blocks[1].PaletteSize[1]);
        Assert.Equal(-2, blocks[1].PredictionUnit.AngleDelta[(int)Av1PlaneType.Y]);
        Assert.Equal(3, blocks[1].PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv]);
        Assert.Equal(0, allocated);
    }

    [Theory]
    [InlineData(false, 6, 4096, 1024, 6144, 256, 64, 384, 36864L)]
    [InlineData(true, 2, 16384, 4096, 24576, 1024, 256, 1536, 49152L)]
    public void EncoderCoefficientBufferMatchesLibaom420SuperblockLayout(
        bool use128x128Superblock,
        int expectedSuperblockCount,
        int expectedLumaCount,
        int expectedChromaCount,
        int expectedCoefficientsPerSuperblock,
        int expectedLumaTransformBlockCount,
        int expectedChromaTransformBlockCount,
        int expectedTransformBlocksPerSuperblock,
        long expectedTotalCoefficientCount)
    {
        ObuSequenceHeader sequenceHeader = new() { Use128x128Superblock = use128x128Superblock };
        sequenceHeader.ColorConfig.IsMonochrome = false;
        sequenceHeader.ColorConfig.SubSamplingX = true;
        sequenceHeader.ColorConfig.SubSamplingY = true;

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            sequenceHeader,
            width: 129,
            height: 65);

        Assert.Equal(expectedSuperblockCount, coefficients.SuperblockCount);
        Assert.Equal(expectedLumaCount, coefficients.LumaCoefficientCount);
        Assert.Equal(expectedChromaCount, coefficients.ChromaCoefficientCount);
        Assert.Equal(expectedCoefficientsPerSuperblock, coefficients.CoefficientsPerSuperblock);
        Assert.Equal(expectedLumaTransformBlockCount, coefficients.LumaTransformBlockCount);
        Assert.Equal(expectedChromaTransformBlockCount, coefficients.ChromaTransformBlockCount);
        Assert.Equal(expectedTransformBlocksPerSuperblock, coefficients.TransformBlocksPerSuperblock);
        Assert.Equal(expectedTotalCoefficientCount, coefficients.TotalCoefficientCount);
        Assert.Equal(expectedLumaCount, coefficients.GetPlaneSpan(0, Av1Plane.Y).Length);
        Assert.Equal(expectedChromaCount, coefficients.GetPlaneSpan(0, Av1Plane.U).Length);
        Assert.Equal(expectedChromaCount, coefficients.GetPlaneSpan(0, Av1Plane.V).Length);
        Assert.Equal(expectedLumaTransformBlockCount, coefficients.GetTransformBlockSpan(0, Av1Plane.Y).Length);
        Assert.Equal(expectedChromaTransformBlockCount, coefficients.GetTransformBlockSpan(0, Av1Plane.U).Length);
        Assert.Equal(expectedChromaTransformBlockCount, coefficients.GetTransformBlockSpan(0, Av1Plane.V).Length);
    }

    [Fact]
    public void EncoderCoefficientBufferKeepsEveryPlaneAndSuperblockDisjoint()
    {
        ObuSequenceHeader sequenceHeader = new() { Use128x128Superblock = true };
        sequenceHeader.ColorConfig.IsMonochrome = false;
        sequenceHeader.ColorConfig.SubSamplingX = true;
        sequenceHeader.ColorConfig.SubSamplingY = true;

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            sequenceHeader,
            width: 129,
            height: 65);

        coefficients.GetPlaneSpan(0, Av1Plane.Y)[0] = 11;
        coefficients.GetPlaneSpan(0, Av1Plane.U)[0] = 22;
        coefficients.GetPlaneSpan(0, Av1Plane.V)[0] = 33;
        coefficients.GetPlaneSpan(1, Av1Plane.Y)[0] = 44;
        coefficients.GetTransformBlockSpan(0, Av1Plane.Y)[0].EndOfBlock = 55;
        coefficients.GetTransformBlockSpan(0, Av1Plane.U)[0].EndOfBlock = 66;
        coefficients.GetTransformBlockSpan(0, Av1Plane.V)[0].EndOfBlock = 77;
        coefficients.GetTransformBlockSpan(1, Av1Plane.Y)[0].EndOfBlock = 88;

        Assert.Equal(11, coefficients.GetPlaneSpan(0, Av1Plane.Y)[0]);
        Assert.Equal(22, coefficients.GetPlaneSpan(0, Av1Plane.U)[0]);
        Assert.Equal(33, coefficients.GetPlaneSpan(0, Av1Plane.V)[0]);
        Assert.Equal(44, coefficients.GetPlaneSpan(1, Av1Plane.Y)[0]);
        Assert.Equal(55, coefficients.GetTransformBlockSpan(0, Av1Plane.Y)[0].EndOfBlock);
        Assert.Equal(66, coefficients.GetTransformBlockSpan(0, Av1Plane.U)[0].EndOfBlock);
        Assert.Equal(77, coefficients.GetTransformBlockSpan(0, Av1Plane.V)[0].EndOfBlock);
        Assert.Equal(88, coefficients.GetTransformBlockSpan(1, Av1Plane.Y)[0].EndOfBlock);
    }

    [Fact]
    public void EncoderLumaTraversalRepresentsAllTransformsIn128x128Block()
    {
        Av1PictureControlSet picture = CreateEncoderPicture(32, 32, use128x128Superblock: true);
        Av1MacroBlockModeInfo modeInfo = picture.ModeInfoAllocation[0].MacroBlockModeInfo;
        modeInfo.Block.BlockSize = Av1BlockSize.Block128x128;
        modeInfo.Block.TransformSize = Av1TransformSize.Size16x16;
        modeInfo.Block.SegmentId = 0;
        Av1TileInfo tile = new(0, 0, picture.Parent.FrameHeader);
        Av1TileWriter.Av1EntropyCodingContext context = new()
        {
            MacroBlock = new Av1MacroBlockD { Tile = tile },
            MacroBlockModeInfo = modeInfo,
            SuperblockOrigin = Point.Empty
        };

        using Av1NeighborArrayUnit<byte> luma = new(
            Configuration.Default,
            leftSize: 128,
            topSize: 128,
            topLeftSize: 256)
        {
            GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2,
            GranularityTopLeftLog2 = Av1Constants.ModeInfoSizeLog2
        };

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            picture.Sequence.SequenceHeader,
            width: 128,
            height: 128);

        Span<Av1EncoderTransformBlockState> transformBlocks =
            coefficients.GetTransformBlockSpan(0, Av1Plane.Y);
        transformBlocks.Fill(new Av1EncoderTransformBlockState { TransformType = Av1TransformType.Identity });

        Av1EncoderBlockStruct block = default;
        using Av1SymbolEncoder writer = new(Configuration.Default, 4096, BaseQIndex);
        Av1TileWriter.EncodeTransformCoefficientsY(
            picture,
            context,
            writer,
            ref block,
            Point.Empty,
            Av1PredictionMode.DC,
            Av1BlockSize.Block128x128,
            coefficients,
            superblockIndex: 0,
            luma);

        writer.Dispose();

        int visitedTransformCount = 0;
        for (int index = 0; index < transformBlocks.Length; index++)
        {
            if ((index % 16) == 0)
            {
                Assert.Equal(Av1TransformType.DctDct, transformBlocks[index].TransformType);
                visitedTransformCount++;
            }
            else
            {
                Assert.Equal(Av1TransformType.Identity, transformBlocks[index].TransformType);
            }
        }

        Assert.Equal(64, visitedTransformCount);
        Assert.Equal(16384, context.CodedAreaSuperblock);
    }

    [Fact]
    public void SegmentationUpdateUsesModeInfoUnits()
    {
        Av1PictureControlSet picture = CreateEncoderPicture(8, 8);
        picture.SegmentationNeighborMap = new byte[64];

        picture.UpdateSegmentation(Av1BlockSize.Block16x8, new Point(8, 12), segmentId: 5);

        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 8; column++)
            {
                byte expected = row is 3 or 4 && column >= 2 && column < 6 ? (byte)5 : (byte)0;
                Assert.Equal(expected, picture.SegmentationNeighborMap[(row * 8) + column]);
            }
        }
    }

    [Fact]
    public void SelectedTransformSizeRoundTripsAndPublishesRectangularEdgeContexts()
    {
        Av1PictureControlSet picture = CreateEncoderPicture(16, 16);
        picture.Parent.FrameHeader.TransformMode = Av1TransformMode.Select;
        Av1MacroBlockModeInfo modeInfo = picture.ModeInfoAllocation[0].MacroBlockModeInfo;
        modeInfo.Block.BlockSize = Av1BlockSize.Block16x32;
        modeInfo.Block.TransformSize = Av1TransformSize.Size8x8;
        modeInfo.Block.SegmentId = 0;
        Point blockOrigin = new(16, 16);
        Av1MacroBlockD macroBlock = new()
        {
            Tile = new Av1TileInfo(0, 0, picture.Parent.FrameHeader),
            IsUpAvailable = true,
            IsLeftAvailable = true
        };

        using Av1NeighborArrayUnit<byte> transforms = new(
            Configuration.Default,
            leftSize: 64,
            topSize: 64,
            topLeftSize: 128)
        {
            GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2,
            GranularityTopLeftLog2 = Av1Constants.ModeInfoSizeLog2
        };

        int topIndex = transforms.GetTopIndex(blockOrigin);
        int leftIndex = transforms.GetLeftIndex(blockOrigin);
        transforms.Top[topIndex] = 16;
        transforms.Left[leftIndex] = 16;
        picture.TransformFunctionContexts = [transforms];

        using Av1SymbolEncoder writer = new(Configuration.Default, 64, BaseQIndex);
        Av1TileWriter.WriteTransformSize(
            picture,
            writer,
            modeInfo,
            macroBlock,
            modeInfo.Block.BlockSize,
            blockOrigin,
            tileIndex: 0);

        using IMemoryOwner<byte> encoded = writer.Exit();
        writer.Dispose();

        Av1SymbolDecoder reader = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        Assert.Equal(
            Av1TransformSize.Size8x8,
            reader.ReadTransformSize(Av1BlockSize.Block16x32, context: 1));

        for (int index = 0; index < transforms.Top.Length; index++)
        {
            byte expected = index >= topIndex && index < topIndex + 4 ? (byte)8 : (byte)0;
            Assert.Equal(expected, transforms.Top[index]);
        }

        for (int index = 0; index < transforms.Left.Length; index++)
        {
            byte expected = index >= leftIndex && index < leftIndex + 8 ? (byte)8 : (byte)0;
            Assert.Equal(expected, transforms.Left[index]);
        }
    }

    [Theory]
    [InlineData((int)Av1PartitionType.None, 24, 24)]
    [InlineData((int)Av1PartitionType.Horizontal, 24, 28)]
    [InlineData((int)Av1PartitionType.Vertical, 28, 24)]
    [InlineData((int)Av1PartitionType.Split, 0, 0)]
    [InlineData((int)Av1PartitionType.HorizontalA, 24, 28)]
    [InlineData((int)Av1PartitionType.HorizontalB, 28, 28)]
    [InlineData((int)Av1PartitionType.VerticalA, 28, 24)]
    [InlineData((int)Av1PartitionType.VerticalB, 28, 28)]
    [InlineData((int)Av1PartitionType.Horizontal4, 24, 30)]
    [InlineData((int)Av1PartitionType.Vertical4, 30, 24)]
    public void PartitionContextUpdatesMatchLibaomExtendedPartitionRules(
        int partitionValue,
        byte expectedAbove,
        byte expectedLeft)
    {
        using Av1NeighborArrayUnit<Av1PartitionContext> neighbors = new(
            Configuration.Default,
            leftSize: 16,
            topSize: 16,
            topLeftSize: 32)
        {
            GranularityNormalLog2 = 2,
            GranularityTopLeftLog2 = 2
        };

        Av1PartitionType partition = (Av1PartitionType)partitionValue;
        Av1BlockSize blockSize = Av1BlockSize.Block32x32;
        Av1BlockSize subSize = partition.GetBlockSubSize(blockSize);

        Av1TileWriter.UpdatePartitionContexts(
            neighbors,
            new Point(8, 12),
            subSize,
            blockSize,
            partition);

        for (int index = 0; index < 16; index++)
        {
            byte above = index is >= 2 and < 10 ? expectedAbove : (byte)0;
            byte left = index is >= 3 and < 11 ? expectedLeft : (byte)0;
            Assert.Equal(above, neighbors.Top[index].Above);
            Assert.Equal(left, neighbors.Left[index].Left);
        }
    }

    [Fact]
    public void EightByEightSplitPublishesFourByFourPartitionContexts()
    {
        using Av1NeighborArrayUnit<Av1PartitionContext> neighbors = new(
            Configuration.Default,
            leftSize: 4,
            topSize: 4,
            topLeftSize: 8)
        {
            GranularityNormalLog2 = 2,
            GranularityTopLeftLog2 = 2
        };

        Av1TileWriter.UpdatePartitionContexts(
            neighbors,
            new Point(4, 4),
            Av1BlockSize.Block4x4,
            Av1BlockSize.Block8x8,
            Av1PartitionType.Split);

        Assert.Equal(31, neighbors.Top[1].Above);
        Assert.Equal(31, neighbors.Top[2].Above);
        Assert.Equal(31, neighbors.Left[1].Left);
        Assert.Equal(31, neighbors.Left[2].Left);
    }

    [Fact]
    public void EncoderModeInfoEdgesUseFourByFourUnits()
    {
        Av1PictureControlSet picture = CreateEncoderPicture(6, 5);
        Av1TileInfo tile = new(0, 0, picture.Parent.FrameHeader);
        Av1MacroBlockD macroBlock = new() { Tile = tile };
        Point position = new(2, 3);

        Av1TileWriter.SetModeInfoRowAndColumn(
            picture,
            macroBlock,
            tile,
            position,
            Av1BlockSize.Block16x8,
            picture.ModeInfoStride,
            picture.Parent.Common.ModeInfoRowCount,
            picture.Parent.Common.ModeInfoColumnCount);

        Assert.Equal(-96, macroBlock.ToTopEdge);
        Assert.Equal(0, macroBlock.ToBottomEdge);
        Assert.Equal(-64, macroBlock.ToLeftEdge);
        Assert.Equal(0, macroBlock.ToRightEdge);
        Assert.Same(picture.ModeInfoGrid[14].MacroBlockModeInfo, macroBlock.AboveMacroBlock);
        Assert.Same(picture.ModeInfoGrid[19].MacroBlockModeInfo, macroBlock.LeftMacroBlock);
    }

    [Fact]
    public void CdefUsesLibaomUnitIndexAndFirstBlockStrength()
    {
        Av1PictureControlSet picture = CreateEncoderPicture(32, 32, use128x128Superblock: true);
        picture.Parent.FrameHeader.CdefParameters.BitCount = 2;
        picture.ModeInfoGrid[16].MacroBlockModeInfo.CdefStrength = 3;
        picture.ModeInfoGrid[20].MacroBlockModeInfo.CdefStrength = 1;
        using Av1SymbolEncoder writer = new(Configuration.Default, 16, BaseQIndex);

        Av1TileWriter.WriteCdef(
            picture.Sequence,
            picture,
            writer,
            tileIndex: 0,
            skip: false,
            modeInfoPosition: new Point(20, 4));

        Assert.Equal(new[] { -1, 3, -1, -1 }, picture.CdefPreset[0]);
    }

    [Fact]
    public void SuperblockWriterTraversesSplitTreeFromAbsoluteOrigin()
    {
        Av1PictureControlSet picture = CreateEncoderPicture(32, 16);
        picture.Sequence.SequenceHeader.ColorConfig.IsMonochrome = true;
        picture.Parent.FrameHeader.CodedLossless = true;
        using Av1NeighborArrayUnit<Av1PartitionContext> partitions = new(
            Configuration.Default,
            leftSize: 16,
            topSize: 32,
            topLeftSize: 48)
        {
            GranularityNormalLog2 = 2,
            GranularityTopLeftLog2 = 2
        };

        using Av1NeighborArrayUnit<byte> luma = new(
            Configuration.Default,
            leftSize: 16,
            topSize: 32,
            topLeftSize: 48)
        {
            GranularityNormalLog2 = 2,
            GranularityTopLeftLog2 = 2
        };

        using Av1NeighborArrayUnit<byte> red = new(
            Configuration.Default,
            leftSize: 16,
            topSize: 32,
            topLeftSize: 48)
        {
            GranularityNormalLog2 = 2,
            GranularityTopLeftLog2 = 2
        };

        using Av1NeighborArrayUnit<byte> blue = new(
            Configuration.Default,
            leftSize: 16,
            topSize: 32,
            topLeftSize: 48)
        {
            GranularityNormalLog2 = 2,
            GranularityTopLeftLog2 = 2
        };

        using Av1NeighborArrayUnit<byte> transforms = new(
            Configuration.Default,
            leftSize: 16,
            topSize: 32,
            topLeftSize: 48)
        {
            GranularityNormalLog2 = 2,
            GranularityTopLeftLog2 = 2
        };

        picture.PartitionContexts = [partitions];
        picture.LuminanceDcSignLevelCoefficientNeighbors = [luma];
        picture.CrDcSignLevelCoefficientNeighbors = [red];
        picture.CbDcSignLevelCoefficientNeighbors = [blue];
        picture.TransformFunctionContexts = [transforms];
        Av1TileInfo tile = new(0, 0, picture.Parent.FrameHeader);
        Point[] blockPositions = [new(16, 0), new(24, 0), new(16, 8), new(24, 8)];
        Av1EncoderBlockStruct[] blocks = new Av1EncoderBlockStruct[blockPositions.Length];
        for (int index = 0; index < blockPositions.Length; index++)
        {
            Point position = blockPositions[index];
            Av1EncoderBlockModeInfo blockMode = picture.ModeInfoAllocation[
                (position.Y * picture.ModeInfoStride) + position.X].MacroBlockModeInfo.Block;

            blockMode.BlockSize = Av1BlockSize.Block32x32;
            blockMode.Skip = true;
            blockMode.Mode = Av1PredictionMode.DC;
            blockMode.UvMode = Av1ChromaPredictionMode.DC;
            blocks[index] = new Av1EncoderBlockStruct { HasChroma = false };
        }

        Av1Superblock superblock = new()
        {
            FinalBlocks = blocks,
            TileInfo = tile,
            CodingUnitPartitionTypes =
            [
                Av1PartitionType.Split,
                Av1PartitionType.None,
                Av1PartitionType.None,
                Av1PartitionType.None,
                Av1PartitionType.None
            ],
            Index = 1
        };
        Av1TileWriter.Av1EntropyCodingContext context = new()
        {
            MacroBlock = new Av1MacroBlockD { Tile = tile },
            MacroBlockModeInfo = picture.ModeInfoAllocation[16].MacroBlockModeInfo,
            SuperblockOrigin = new Point(64, 0)
        };
        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            picture.Sequence.SequenceHeader,
            width: 128,
            height: 64);

        using Av1SymbolEncoder writer = new(Configuration.Default, 512, BaseQIndex);

        Av1TileWriter.WriteSuperblock(
            picture,
            context,
            writer,
            superblock,
            coefficients,
            tileIndex: 0);

        writer.Dispose();

        Assert.Equal(4096, context.CodedAreaSuperblock);
        Assert.Equal(0, context.CodedAreaSuperblockUv);
        for (int index = 0; index < partitions.Top.Length; index++)
        {
            Assert.Equal(index < 16 ? 0 : 24, partitions.Top[index].Above);
        }

        for (int index = 0; index < partitions.Left.Length; index++)
        {
            Assert.Equal(24, partitions.Left[index].Left);
        }

        for (int index = 0; index < transforms.Top.Length; index++)
        {
            Assert.Equal(index < 16 ? 0 : 32, transforms.Top[index]);
        }

        for (int index = 0; index < transforms.Left.Length; index++)
        {
            Assert.Equal(32, transforms.Left[index]);
        }
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
        Av1TransformBlockContext transformBlockContext = default;
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
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
        Av1TransformBlockContext transformBlockContext = default;
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
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
        Av1TransformBlockContext transformBlockContext = default;
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
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

    private static Av1PictureControlSet CreateEncoderPicture(
        int modeInfoColumnCount,
        int modeInfoRowCount,
        bool use128x128Superblock = false)
    {
        ObuTileGroupHeader tiles = new()
        {
            TileColumnCount = 1,
            TileRowCount = 1
        };

        tiles.TileColumnStartModeInfo[1] = modeInfoColumnCount;
        tiles.TileRowStartModeInfo[1] = modeInfoRowCount;
        ObuSequenceHeader sequenceHeader = new() { Use128x128Superblock = use128x128Superblock };
        ObuFrameHeader frameHeader = new()
        {
            ModeInfoColumnCount = modeInfoColumnCount,
            ModeInfoRowCount = modeInfoRowCount,
            TilesInfo = tiles
        };

        Av1ModeInfo[] modeInfoGrid = new Av1ModeInfo[modeInfoColumnCount * modeInfoRowCount];
        for (int index = 0; index < modeInfoGrid.Length; index++)
        {
            modeInfoGrid[index] = CreateModeInfo(Av1PredictionMode.DC);
        }

        return new Av1PictureControlSet
        {
            PartitionContexts = [],
            LuminanceDcSignLevelCoefficientNeighbors = [],
            CrDcSignLevelCoefficientNeighbors = [],
            CbDcSignLevelCoefficientNeighbors = [],
            TransformFunctionContexts = [],
            Sequence = new Av1SequenceControlSet { SequenceHeader = sequenceHeader },
            Parent = new Av1PictureParentControlSet
            {
                Common = new Av1EncoderCommon
                {
                    ModeInfoColumnCount = modeInfoColumnCount,
                    ModeInfoRowCount = modeInfoRowCount,
                    ModeInfoStride = modeInfoColumnCount,
                    FrameSize = new ObuFrameSize(),
                    TilesInfo = tiles
                },
                FrameHeader = frameHeader,
                PreviousQIndex = []
            },
            SegmentationNeighborMap = [],
            ModeInfoGrid = modeInfoGrid,
            ModeInfoAllocation = modeInfoGrid,
            ModeInfoStride = modeInfoColumnCount,
            CdefPreset = [[-1, -1, -1, -1]]
        };
    }

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
